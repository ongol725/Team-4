using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagSurvivor.Monster;
using BagSurvivor.UI;

/// <summary>
/// 활성 시너지 목록(BattleLoadout.ActiveSynergies)을 받아
/// 트리거 타입별로 스킬형/소환형 시너지를 실행하는 메인 관리자.
///
/// ▶ 트리거 타입
///   AutoTimer  — 쿨타임마다 자동 발동 (암살단·일렉트로·처형자·소드마스터·티탄·마왕)
///   OnHitTaken — 플레이어 피격 시 발동 (난공불락)
///   OnMove     — 플레이어 이동 거리 누적 시 발동 (대부호)
///   Passive    — 소환수/오브젝트를 전투 내내 유지 (핀볼·페어리·정령술사·성기사단)
///   Penalty    — 브~골 패널티, 프리즘 초강력 발동 (과부하)
///
/// ▶ 스케일링 스탯
///   WPN_ATK_SUM — 무기 공격력 총합
///   WPN_ATK_AVG — 무기 공격력 평균
///   ARM_HP_SUM  — 방어구 체력 총합 (난공불락)
/// </summary>
public class SynergyManager : MonoBehaviour
{
    // ── Inspector 바인딩 ───────────────────────────────────────────

    [System.Serializable]
    public class SynergySkillBinding
    {
        public SynergyType  synergyType;
        public SynergyGrade grade;
        public SO_SkillData skill;
    }

    [System.Serializable]
    public class SynergySummonBinding
    {
        public SynergyType   synergyType;
        public SynergyGrade  grade;
        public SO_SummonData summon;
        public int           count = 1;
    }

    [Header("스킬형 시너지 바인딩")]
    [SerializeField] private SynergySkillBinding[]  _skillBindings;

    [Header("소환형 시너지 바인딩")]
    [SerializeField] private SynergySummonBinding[] _summonBindings;

    [Header("적 레이어")]
    [SerializeField] private LayerMask _enemyLayer;

    // ── 런타임 상태 ───────────────────────────────────────────────

    private GameManager                  _gm;
    private Transform                    _player;
    private PlayerHealth                 _playerHealth;
    private PlayerMovement               _playerMovement;
    private PlayerStats                  _playerStats;
    private PlayerAttack                 _playerAttack;
    private BattleLoadout                _loadout;

    // 과부하 패널티: 진입 직전 피해량 배율을 저장해 두고 해제 시 원래 값으로 복구
    // (캐릭터 고유 배율이 1이 아닐 수 있으므로 1f로 강제하지 않는다)
    private float _savedAtkMul = 1f;

    private readonly List<Coroutine>         _skillRoutines = new();
    private readonly List<Coroutine>         _summonRoutines = new(); // duration>0 소환수 재시전 루프
    private readonly List<SummonController>  _summons       = new();
    private Transform                        _summonRoot;

    // 지속시간 소환수(성역 등)가 소멸한 뒤 다시 시전될 때까지의 빈 시간(초).
    // 실제 재시전 주기 = data.duration + 이 값. (등급↑ = duration↑ → 가동률↑)
    private const float SummonRecastGap = 2f;

    // 플레이어를 감싸며 따라다니는 영구 이펙트(마왕 소용돌이 등). Refresh/파괴 시 정리.
    private readonly List<GameObject>        _auraVFX       = new();

    // OnHitTaken 트리거 등록 목록 (난공불락)
    private readonly List<(SO_SkillData skill, int dmg)> _onHitSkills = new();

    // OnMove 트리거 누적 (대부호)
    private readonly List<(SO_SkillData skill, int dmg)> _onMoveSkills = new();
    private float _moveDistAccum = 0f;
    private const float MoveDropInterval = 1f; // 1유닛 이동마다 골드 드랍

    // 테스트 패널이 적용 중이면 정식 로드아웃(방 진입 등) 발행을 무시해 덮어쓰기 방지
    private bool _testLockActive = false;

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        FindPlayerRefs();

        _gm = GameManager.Instance;
        if (_gm != null)
        {
            _gm.onLoadoutReady += OnLoadoutReady;
            if (_gm.CurrentLoadout != null) OnLoadoutReady(_gm.CurrentLoadout);
        }

        _summonRoot = new GameObject("SummonRoot").transform;
        _summonRoot.SetParent(transform);
    }

    private void OnDestroy()
    {
        if (_gm != null) _gm.onLoadoutReady -= OnLoadoutReady;
        UnsubscribePlayerEvents();
        if (_playerHealth != null) _playerHealth.DamageReductionPct = 0f;
        // 플레이어에 부착된 영구 오라는 매니저 파괴 시 함께 정리
        foreach (var v in _auraVFX) if (v != null) Destroy(v);
        _auraVFX.Clear();
    }

    // ─────────────────────────────────────────────────────────────

    private void FindPlayerRefs()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null) return;

        _player         = playerGO.transform;
        _playerHealth   = playerGO.GetComponent<PlayerHealth>();
        _playerMovement = playerGO.GetComponent<PlayerMovement>();
        _playerStats    = playerGO.GetComponent<PlayerStats>();
        _playerAttack   = playerGO.GetComponent<PlayerAttack>();
    }

    private void SubscribePlayerEvents()
    {
        if (_playerHealth   != null) _playerHealth.onDamageTaken   += OnPlayerHit;
        if (_playerMovement != null) _playerMovement.onDistanceMoved += OnPlayerMoved;
    }

    private void UnsubscribePlayerEvents()
    {
        if (_playerHealth   != null) _playerHealth.onDamageTaken   -= OnPlayerHit;
        if (_playerMovement != null) _playerMovement.onDistanceMoved -= OnPlayerMoved;
    }

    // ─────────────────────────────────────────────────────────────

    private void OnLoadoutReady(BattleLoadout loadout)
    {
        // 테스트 패널이 잠금 중이면 정식 로드아웃(방 진입 시 무기 0개 등)으로 덮어쓰지 않는다
        if (_testLockActive)
        {
            Debug.Log("[SynergyManager] 테스트 잠금 중 — 정식 로드아웃 무시");
            return;
        }
        _loadout = loadout;
        Refresh();
    }

    /// <summary>로드아웃이 갱신될 때마다 기존 러너 제거 후 재구성</summary>
    private void Refresh()
    {
        // 기존 루프/소환 전부 정리
        foreach (var co in _skillRoutines) if (co != null) StopCoroutine(co);
        _skillRoutines.Clear();
        foreach (var co in _summonRoutines) if (co != null) StopCoroutine(co);
        _summonRoutines.Clear();
        foreach (var s in _summons) if (s != null) Destroy(s.gameObject);
        _summons.Clear();
        foreach (var v in _auraVFX) if (v != null) Destroy(v);
        _auraVFX.Clear();
        // 코루틴 정리 직후 패널티 해제 — Refresh 도중 PenaltyLoop가 중단돼도 이동속도가 복구됨
        RemoveOverloadPenalty();

        UnsubscribePlayerEvents();
        _onHitSkills.Clear();
        _onMoveSkills.Clear();
        _moveDistAccum = 0f;

        // 패시브 피해 감소 초기화
        if (_playerHealth != null) _playerHealth.DamageReductionPct = 0f;

        if (_loadout == null) return;
        if (_player == null) FindPlayerRefs();

        bool needHitEvent  = false;
        bool needMoveEvent = false;

        foreach (var entry in _loadout.ActiveSynergies)
        {
            int dmgBase = 0;

            // ── 스킬형 ─────────────────────────────────────────
            // 같은 (시너지·등급)에 여러 스킬을 바인딩할 수 있다(마왕 누적 발동: 투사체+소용돌이 등).
            foreach (var skillBinding in FindSkillBindings(entry.type, entry.grade))
            {
                var skill = skillBinding.skill;
                dmgBase = Mathf.RoundToInt(_loadout.GetScaledBase(skill.scalingStat) * skill.dmgMultiplier);

                // 지속 빔(과부하 레이저): 트리거별 발사 루프 대신 추종 빔 1개 생성 → 지속 데미지
                if (skill.skillType == SkillType.Beam)
                {
                    SpawnBeam(skill, dmgBase);
                    Debug.Log($"[SynergyManager] Beam: {entry.type} {entry.grade} — {skill.skillName}");
                    continue;
                }

                switch (skill.triggerType)
                {
                    case SynergyTriggerType.AutoTimer:
                        _skillRoutines.Add(StartCoroutine(SkillLoop(skill, dmgBase)));
                        // 플레이어를 감싸는 효과는 영구 오라로 1회 생성(깜빡임 방지). 데미지는 SkillLoop가 담당.
                        if (skill.vfxFollowPlayer) SpawnPersistentAura(skill);
                        Debug.Log($"[SynergyManager] AutoTimer: {entry.type} {entry.grade} — {skill.skillName}");
                        break;

                    case SynergyTriggerType.OnHitTaken:
                        _onHitSkills.Add((skill, dmgBase));
                        needHitEvent = true;
                        Debug.Log($"[SynergyManager] OnHitTaken: {entry.type} {entry.grade} — {skill.skillName}");
                        break;

                    case SynergyTriggerType.OnMove:
                        _onMoveSkills.Add((skill, dmgBase));
                        needMoveEvent = true;
                        Debug.Log($"[SynergyManager] OnMove: {entry.type} {entry.grade} — {skill.skillName}");
                        break;

                    case SynergyTriggerType.Penalty:
                        _skillRoutines.Add(StartCoroutine(PenaltyLoop(skill, entry.grade, dmgBase)));
                        Debug.Log($"[SynergyManager] Penalty: {entry.type} {entry.grade} — {skill.skillName}");
                        break;

                    case SynergyTriggerType.Passive:
                        // Passive 스킬은 소환형과 함께 처리 (아래)
                        break;
                }
            }

            // ── 소환형 ─────────────────────────────────────────
            // 같은 (시너지·등급)에 여러 소환수를 바인딩할 수 있다(정령술사 원소별 누적 소환 등).
            foreach (var summonBinding in FindSummonBindings(entry.type, entry.grade))
            {
                var summon    = summonBinding.summon;
                int summonAtk = Mathf.RoundToInt(_loadout.GetScaledBase(summon.scalingStat) * summon.atkMultiplier);
                if (summon.duration > 0f)
                    // 지속시간형(성역): 소멸 후 주기적으로 재시전
                    _summonRoutines.Add(StartCoroutine(SummonRespawnLoop(summon, summonBinding.count, summonAtk)));
                else
                    SpawnMinions(summon, summonBinding.count, summonAtk);
                Debug.Log($"[SynergyManager] Summon: {entry.type} {entry.grade} — {summon.summonName} x{summonBinding.count}");
            }
        }

        // 이벤트 구독
        if (needHitEvent || needMoveEvent)
            SubscribePlayerEvents();

        // DamageReduction 패시브 집계 후 PlayerHealth에 적용 (난공불락)
        if (_playerHealth != null)
        {
            float maxReduction = 0f;
            foreach (var entry in _loadout.ActiveSynergies)
            {
                foreach (var sb in FindSkillBindings(entry.type, entry.grade))
                    if (sb.skill.fixedEffect == FixedEffectType.DamageReduction)
                        maxReduction = Mathf.Max(maxReduction, sb.skill.fixedEffectValue / 100f);
            }
            _playerHealth.DamageReductionPct = Mathf.Clamp01(maxReduction);
            if (maxReduction > 0f)
                Debug.Log($"[SynergyManager] 피해 감소 패시브 적용: {maxReduction * 100f:F0}%");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 플레이어 이벤트 핸들러

    private void OnPlayerHit(int dmgAmount)
    {
        foreach (var (skill, dmg) in _onHitSkills)
            ExecuteSkill(skill, dmg);
    }

    private void OnPlayerMoved(float dist)
    {
        if (_onMoveSkills.Count == 0) return;
        _moveDistAccum += dist;
        while (_moveDistAccum >= MoveDropInterval)
        {
            _moveDistAccum -= MoveDropInterval;
            foreach (var (skill, dmg) in _onMoveSkills)
                DropGoldCoins(skill, dmg);
        }
    }

    // ── 대부호: 이동 시 골드 코인을 떨구고 일정 시간 후 폭발 ──────────
    private void DropGoldCoins(SO_SkillData skill, int damage)
    {
        if (_player == null) return;
        int   count = skill.extraCount > 0 ? skill.extraCount : 3;
        float delay = skill.duration   > 0f ? skill.duration   : 2f;

        // 콘셉트(슬라이드 23): 캐릭터 기준 "원의 경로"를 따라 골드를 균등 살포한다.
        // 시작 각을 매번 무작위로 돌려 같은 자리에 겹치지 않게 한다.
        const float ringRadius = 1.8f;
        float startAngle = Random.value * 360f;
        for (int i = 0; i < count; i++)
        {
            float ang  = (startAngle + 360f / count * i) * Mathf.Deg2Rad;
            float r    = ringRadius + Random.Range(-0.25f, 0.25f); // 살짝 흩뿌려 자연스럽게
            Vector2 offset = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            StartCoroutine(GoldCoinRoutine(skill, (Vector2)_player.position + offset, damage, delay));
        }
    }

    private IEnumerator GoldCoinRoutine(SO_SkillData skill, Vector3 pos, int damage, float delay)
    {
        // 골드 코인 비주얼 (Gold_Coin 등급별)
        GameObject coin = null;
        if (skill.dropFrames != null && skill.dropFrames.Length > 0)
        {
            coin = new GameObject("GoldCoin");
            coin.transform.SetParent(transform);
            coin.transform.position = pos;
            var sr = coin.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SynergyLayers.Ground; // 골드 코인은 바닥에 깔림
            sr.sprite = skill.dropFrames[0];
            NormalizeScale(coin, sr.sprite, 0.4f);
            coin.AddComponent<SpriteSheetAnimator>().Play(skill.dropFrames, skill.dropFps);
        }

        yield return new WaitForSeconds(delay);
        if (coin != null) Destroy(coin);

        // 폭발: 범위 데미지 + 폭발 VFX(animFrames = RichCoin_BOMB)
        float radius = skill.rangeRadius > 0f ? skill.rangeRadius : 3f;
        foreach (var mc in GetEnemiesInRange(pos, radius)) HitEnemy(skill, mc, damage);
        SpawnVFX(skill, pos);
    }

    // ─────────────────────────────────────────────────────────────
    // AutoTimer 스킬 루프

    private IEnumerator SkillLoop(SO_SkillData skill, int damage)
    {
        var wait     = new WaitForSeconds(skill.cooldown);
        int hitCount = Mathf.Max(1, skill.hitCount);
        var hitWait  = hitCount > 1 ? new WaitForSeconds(skill.hitInterval) : null;
        while (true)
        {
            yield return wait;
            for (int h = 0; h < hitCount; h++)
            {
                ExecuteSkill(skill, damage);
                if (h < hitCount - 1) yield return hitWait;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Penalty 루프 (과부하)

    private IEnumerator PenaltyLoop(SO_SkillData skill, SynergyGrade grade, int damage)
    {
        if (grade == SynergyGrade.Prism)
        {
            // 프리즘: 초강력 스킬 무한 발동
            var wait = new WaitForSeconds(skill.cooldown > 0f ? skill.cooldown : 0.33f);
            while (true)
            {
                ExecuteSkill(skill, damage);
                yield return wait;
            }
        }
        else
        {
            // 브론즈~골드: 10초마다 1초간 이동속도/피해량 50% 감소
            var waitCycle   = new WaitForSeconds(10f);
            var waitPenalty = new WaitForSeconds(1f);
            float penaltyRate = skill.fixedEffectValue > 0f ? 1f - skill.fixedEffectValue / 100f : 0.5f;

            while (true)
            {
                yield return waitCycle;
                ApplyOverloadPenalty(penaltyRate);
                yield return waitPenalty;
                RemoveOverloadPenalty();
            }
        }
    }

    private void ApplyOverloadPenalty(float rate)
    {
        if (_playerMovement != null)
            _playerMovement.speedMultiplier = rate;
        if (_playerStats != null)
        {
            _savedAtkMul = _playerStats.attackMultiplier;
            _playerStats.attackMultiplier *= rate;
        }
        Debug.Log($"[과부하] 패널티 발동 (이동속도·피해량 ×{rate:F2})");
    }

    private void RemoveOverloadPenalty()
    {
        if (_playerMovement != null)
            _playerMovement.speedMultiplier = 1f;
        if (_playerStats != null)
            _playerStats.attackMultiplier = _savedAtkMul;
        Debug.Log("[과부하] 패널티 해제");
    }

    // ─────────────────────────────────────────────────────────────
    // 스킬 실행

    private void ExecuteSkill(SO_SkillData skill, int damage)
    {
        if (_player == null) return;

        Vector3 vfxPos = _player.position;

        switch (skill.targetType)
        {
            case SkillTargetType.RandomEnemy:
            {
                var enemies = GetEnemiesInRange(_player.position, skill.rangeRadius <= 0f ? 50f : skill.rangeRadius);
                int  count  = skill.extraCount > 0 ? skill.extraCount : 1;
                bool isProj = skill.skillType == SkillType.Projectile || skill.skillType == SkillType.Slash;
                for (int i = 0; i < count && enemies.Count > 0; i++)
                {
                    var target = enemies[Random.Range(0, enemies.Count)];
                    // 다수 대상(티탄 돌·과부하 비눗방울 등)은 명중 지점마다 이펙트를 띄운다.
                    if (!isProj) SpawnVFX(skill, target.transform.position);
                    // damageDelay > 0 이면 VFX(연출) 진행 후 데미지 적용 (티탄: 돌이 떨어진 뒤 타격)
                    if (skill.damageDelay > 0f)
                        StartCoroutine(DelayedHit(skill, target, damage, skill.damageDelay));
                    else
                        ApplyHit(skill, target, damage);
                    enemies.Remove(target);
                }
                return; // VFX를 대상별로 처리했으므로 하단 공용 VFX 생략
            }

            case SkillTargetType.RandomAroundSelf:
            {
                // 과부하 비눗방울: 적을 조준하지 않고 플레이어 주변 랜덤 위치에 흩뿌린 뒤 그 자리 적을 타격.
                // rangeRadius = 흩뿌리는 반경, visualSize = 비눗방울 지름(타격 반경 = 그 절반).
                int   count   = skill.extraCount > 0 ? skill.extraCount : 1;
                float scatter = skill.rangeRadius  > 0f ? skill.rangeRadius  : 4f;
                float hitR    = skill.visualSize   > 0f ? skill.visualSize * 0.5f : 1f;
                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = (Vector2)_player.position + Random.insideUnitCircle * scatter;
                    SpawnVFX(skill, pos);
                    foreach (var mc in GetEnemiesInRange(pos, hitR)) HitEnemy(skill, mc, damage);
                }
                return; // 비눗방울별 VFX 처리 완료
            }

            case SkillTargetType.AreaCenter:
            case SkillTargetType.Self:
            {
                var enemies = GetEnemiesInRange(_player.position, skill.rangeRadius);
                foreach (var mc in enemies) ApplyHit(skill, mc, damage);
                break;
            }

            case SkillTargetType.Forward:
            {
                var enemies = GetEnemiesInRange(_player.position, skill.rangeRadius <= 0f ? 20f : skill.rangeRadius);
                MonsterController nearest = FindNearest(enemies, _player.position);
                if (nearest != null) { vfxPos = nearest.transform.position; ApplyHit(skill, nearest, damage); }
                break;
            }

            case SkillTargetType.ForwardDual:
            {
                // 처형자: 적을 조준하지 않고 플레이어 기준 좌·우 고정 방향으로 낫을 발사한다.
                // 각 낫은 경로상의 적을 관통하며, 즉사 등 고정효과는 명중 시 적용된다.
                int pierceHits = skill.pierceCount > 0 ? skill.pierceCount : 999; // 기본: 경로상 모든 적 관통
                FireProjectileInDirection(skill, Vector2.left,  damage, pierceHits);
                FireProjectileInDirection(skill, Vector2.right, damage, pierceHits);
                break;
            }

            case SkillTargetType.ForwardTriple:
            {
                var enemies = GetEnemiesInRange(_player.position, skill.rangeRadius <= 0f ? 20f : skill.rangeRadius);
                for (int i = 0; i < 3 && enemies.Count > 0; i++)
                {
                    MonsterController t = FindNearest(enemies, _player.position);
                    if (t == null) break;
                    if (i == 0) vfxPos = t.transform.position;
                    ApplyHit(skill, t, damage);
                    enemies.Remove(t);
                }
                break;
            }

            case SkillTargetType.SidePillars:
            {
                // 일렉트로: 플레이어 좌·우(필요 시 더 바깥쪽)에 번개 기둥을 세운다.
                // extraCount = 기둥 수. 좌→우→더 먼 좌→더 먼 우 순으로 대칭 배치.
                int   pillars = skill.extraCount > 0 ? skill.extraCount : 2;
                const float gap = 2.8f;       // 플레이어~기둥 간격
                const float pillarRadius = 1.8f; // 기둥당 타격 반경
                for (int i = 0; i < pillars; i++)
                {
                    int sign = (i % 2 == 0) ? -1 : 1; // 좌, 우, 좌, 우…
                    int rank = i / 2 + 1;
                    Vector3 pos = (Vector2)_player.position + new Vector2(sign * gap * rank, 0f);
                    foreach (var mc in GetEnemiesInRange(pos, pillarRadius)) HitEnemy(skill, mc, damage);
                    SpawnVFX(skill, pos);
                }
                return; // 기둥별 VFX 처리 완료
            }

            case SkillTargetType.FacingForward:
            {
                // 과부하 레이저: 적을 조준하지 않고 플레이어가 바라보는 방향으로 발사.
                Vector2 dir = _playerAttack != null ? _playerAttack.FacingDirection : Vector2.right;
                if (dir == Vector2.zero) dir = Vector2.right;
                int pierce = skill.pierceCount > 0 ? skill.pierceCount : 1;
                FireProjectileInDirection(skill, dir, damage, pierce);
                return; // 투사체 스프라이트 자체가 비주얼
            }

            case SkillTargetType.ChainLightning:
            {
                // 과부하: 최근접 적부터 시작해 직전 적 기준 가장 가까운 적으로 연쇄 타격.
                // 플레이어 → 적 → 적 … 을 번개 선으로 잇고, 명중 지점마다 임팩트 이펙트를 띄운다.
                int hops = skill.extraCount > 0 ? skill.extraCount : 4;
                var pool = GetEnemiesInRange(_player.position, skill.rangeRadius <= 0f ? 50f : skill.rangeRadius);
                var chainPts = new List<Vector3> { _player.position };
                MonsterController cur = FindNearest(pool, _player.position);
                while (hops-- > 0 && cur != null)
                {
                    Vector3 hitPos = cur.transform.position;
                    HitEnemy(skill, cur, damage);
                    // 노드 임팩트: 타격 지점마다 nodeFrames(과부하=OVERLOAD2.1_Pr) 1회 재생. 비어 있으면 연결선만.
                    if (skill.nodeFrames != null && skill.nodeFrames.Length > 0)
                        SpawnFramesVFX(skill.nodeFrames, hitPos,
                                       skill.visualSize > 0f ? skill.visualSize : 1.5f,
                                       skill.animFps * 2f, SynergyLayers.Link); // 2배속 재생 → 재생시간 절반
                    chainPts.Add(hitPos);
                    pool.Remove(cur);
                    cur = FindNearest(pool, hitPos); // 다음 홉은 직전 적 기준 최근접
                }
                // 적과 적을 잇는 번개 — 스킬 시트(OVERLOAD2_Pr)의 프레임을 거리만큼 반복(타일)
                Sprite chainSeg = (skill.animFrames != null && skill.animFrames.Length > 0) ? skill.animFrames[0] : null;
                if (chainPts.Count >= 2) SpawnChainLine(chainPts, chainSeg);
                return; // 홉마다 VFX 처리 완료
            }
        }

        // Projectile/Slash는 투사체 스프라이트 자체가 비주얼 → 발동 위치 VFX 생략.
        // 플레이어 추종 오라(vfxFollowPlayer)는 영구 오라가 이미 표시 중이므로 매 시전 VFX 생략(깜빡임 방지).
        if (skill.skillType != SkillType.Projectile && skill.skillType != SkillType.Slash && !skill.vfxFollowPlayer)
            SpawnVFX(skill, vfxPos);
    }

    /// <summary>
    /// 스킬 형태(skillType)에 따라 타격을 적용한다.
    /// Projectile = 타겟 방향으로 투사체 발사, 그 외 = 즉시 데미지(기존 동작).
    /// </summary>
    private void ApplyHit(SO_SkillData skill, MonsterController mc, int damage)
    {
        if (mc == null) return;
        // Projectile(수리검)·Slash(검기/낫)는 타겟 방향으로 날아가는 투사체로 처리
        if (skill.skillType == SkillType.Projectile || skill.skillType == SkillType.Slash)
            FireProjectileAt(skill, mc, damage);
        else
            HitEnemy(skill, mc, damage);
    }

    /// <summary>VFX 연출이 진행된 뒤(delay초 후) 데미지를 적용한다(티탄 돌 떨구기 등).
    /// 지연 중 대상이 죽거나 사라지면 안전하게 무시한다.</summary>
    private IEnumerator DelayedHit(SO_SkillData skill, MonsterController mc, int damage, float delay)
    {
        yield return new WaitForSeconds(delay);
        // 티탄(메테오): 돌이 떨어져 착탄하는 순간 — 대상이 이미 죽었어도 돌은 떨어지므로 소리는 재생
        if (skill != null && skill.skillID != null && skill.skillID.StartsWith("SK_METEOR"))
            AudioUtil.PlaySfx("99.External/Titan_RockImpact", 1f, warnIfMissing: false); // 외부 에셋 — 없으면 무음
        if (mc == null || mc.IsDead || !mc.gameObject.activeInHierarchy) yield break;
        ApplyHit(skill, mc, damage);
    }

    /// <summary>플레이어 위치에서 타겟 방향으로 투사체를 발사한다(ProjectileBase 재사용).</summary>
    private void FireProjectileAt(SO_SkillData skill, MonsterController target, int damage)
    {
        if (_player == null || target == null) return;

        Vector2 dir = ((Vector2)target.transform.position - (Vector2)_player.position).normalized;
        int maxHits = skill.pierceCount > 0 ? skill.pierceCount : 1; // 관통 횟수(암살단 수리검 등)
        FireProjectileInDirection(skill, dir, damage, maxHits);
    }

    /// <summary>플레이어 위치에서 지정한 방향으로 투사체를 발사한다(ProjectileBase 재사용).
    /// 적을 조준하지 않는 고정 방향 발사(예: 처형자 좌·우)에 사용한다.</summary>
    private void FireProjectileInDirection(SO_SkillData skill, Vector2 dir, int damage, int maxHits = 1)
    {
        if (_player == null) return;

        dir = dir.normalized;
        if (dir == Vector2.zero) dir = Vector2.right;

        GameObject go = skill.projectilePrefab != null
            ? Instantiate(skill.projectilePrefab, _player.position, Quaternion.identity)
            : BuildTempProjectile(skill);
        go.transform.position = _player.position;

        float speed   = skill.projectileSpeed > 0f ? skill.projectileSpeed : 15f;
        float kbForce = skill.fixedEffect == FixedEffectType.Knockback ? skill.fixedEffectValue : 0f;

        var proj = go.GetComponent<ProjectileBase>() ?? go.AddComponent<ProjectileBase>();

        // 명중 시 시너지 고정효과 적용
        if (skill.fixedEffect == FixedEffectType.Burn)
            proj.SetOnHit(mc => StartCoroutine(ApplyBurn(mc, Mathf.Max(1, damage / 5), 3f, 1f)));
        else if (skill.fixedEffect == FixedEffectType.InstantDeath && skill.fixedEffectValue > 0f)
            proj.SetOnHit(mc =>
            {
                if (mc != null && !mc.IsDead && mc.HpRatio <= skill.fixedEffectValue / 100f)
                    mc.TakeDamage(999999, 0f, Vector2.zero);
            });

        proj.Init(dir, damage, speed, lifetime: 3f, maxHits: maxHits, knockbackForce: kbForce);
    }

    /// <summary>프리팹이 없는 시너지 투사체용 임시 GameObject를 생성한다.
    /// animFrames가 있으면 시트 애니메이션, 없으면 노란 원 fallback.</summary>
    private GameObject BuildTempProjectile(SO_SkillData skill)
    {
        var go = new GameObject($"SynergyProj_{skill.skillID}");

        var sr          = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = SynergyLayers.Effect; // 투사체는 캐릭터 위
        float size      = skill.visualSize > 0f ? skill.visualSize : 0.8f;

        if (skill.animFrames != null && skill.animFrames.Length > 0)
        {
            sr.sprite = skill.animFrames[0];
            sr.color  = Color.white;
            NormalizeScale(go, sr.sprite, size);
            go.AddComponent<SpriteSheetAnimator>().Play(skill.animFrames, skill.animFps);
        }
        else
        {
            sr.sprite = GetCircleSprite();
            sr.color  = new Color(1f, 0.9f, 0.1f); // 노란 원 fallback
            go.transform.localScale = Vector3.one * (size * 0.5f);
        }

        // 세로(로컬 Y) 비균등 확대 — 투사체는 진행방향으로 회전하므로 진행방향 수직 두께가 늘어난다(소드마스터 검기)
        if (skill.visualStretchY > 0f && skill.visualStretchY != 1f)
        {
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(s.x, s.y * skill.visualStretchY, s.z);
        }

        var rb          = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        var col         = go.AddComponent<CircleCollider2D>();
        col.isTrigger   = true;
        col.radius      = 0.3f;

        return go;
    }

    /// <summary>스프라이트의 실제 월드 크기를 targetSize(지름)에 맞춰 스케일 보정.</summary>
    private static void NormalizeScale(GameObject go, Sprite sprite, float targetSize)
    {
        if (sprite == null) { go.transform.localScale = Vector3.one * targetSize; return; }
        float maxExtent = Mathf.Max(sprite.bounds.extents.x, sprite.bounds.extents.y);
        go.transform.localScale = maxExtent > 0.001f
            ? Vector3.one * (targetSize * 0.5f / maxExtent)
            : Vector3.one * targetSize;
    }

    // 임시 투사체용 흰 원 스프라이트 (최초 1회 생성 후 캐싱, color로 틴트)
    private static Sprite _circleSprite;
    private static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "SynergyProjCircle" };
        float c = (S - 1) * 0.5f, r = c;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            tex.SetPixel(x, y, d <= r ? Color.white : Color.clear);
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return _circleSprite;
    }

    private void HitEnemy(SO_SkillData skill, MonsterController mc, int damage)
    {
        // 처형자 즉사 판정
        if (skill.fixedEffect == FixedEffectType.InstantDeath && skill.fixedEffectValue > 0f)
        {
            float hpRatio = mc.HpRatio; // 0~1
            if (hpRatio <= skill.fixedEffectValue / 100f)
            {
                mc.TakeDamage(999999, 0f, Vector2.zero);
                return;
            }
        }

        // Burn DoT — 초당 피해량의 20% × 3초
        if (skill.fixedEffect == FixedEffectType.Burn)
            StartCoroutine(ApplyBurn(mc, Mathf.Max(1, damage / 5), 3f, 1f));

        Vector2 kb      = ((Vector2)mc.transform.position - (Vector2)_player.position).normalized;
        float kbForce   = skill.fixedEffect == FixedEffectType.Knockback ? skill.fixedEffectValue : 0f;
        mc.TakeDamage(damage, kbForce, kb);
    }

    private IEnumerator ApplyBurn(MonsterController mc, int dmgPerTick, float duration, float interval)
    {
        float elapsed = 0f;
        var wait = new WaitForSeconds(interval);
        while (elapsed < duration)
        {
            yield return wait;
            // 대기 후 null/사망 체크를 TakeDamage 직전에 수행 (레이스 컨디션 방지)
            if (mc == null || mc.IsDead || !mc.gameObject.activeInHierarchy) yield break;
            elapsed += interval;
            mc.TakeDamage(dmgPerTick, 0f, Vector2.zero);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // VFX

    private void SpawnVFX(SO_SkillData skill, Vector3 pos)
    {
        if (skill.vfxPrefab != null)
        {
            var vfx = Instantiate(skill.vfxPrefab, pos, Quaternion.identity);
            Destroy(vfx, skill.duration > 0f ? skill.duration : 2f);
            return;
        }
        // 시트 애니메이션 이펙트 (돌·낙뢰·충격파 등)
        if (skill.animFrames != null && skill.animFrames.Length > 0)
        {
            StartCoroutine(SheetVFX(skill, pos));
            return;
        }
        StartCoroutine(PlaceholderVFX(pos, skill.rangeRadius));
    }

    private IEnumerator SheetVFX(SO_SkillData skill, Vector3 pos)
    {
        var go = new GameObject($"SynergyVFX_{skill.skillID}");
        go.transform.SetParent(transform);
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = skill.vfxSortingOrder; // 바닥 효과(난공불락)는 낮은 값
        sr.sprite = skill.animFrames[0];
        float size = skill.visualSize > 0f ? skill.visualSize : Mathf.Max(1f, skill.rangeRadius * 0.3f);
        NormalizeScale(go, sr.sprite, size);

        // 세로(Y) 비균등 확대 — 일렉트로 번개 기둥처럼 위로 길게 늘릴 때 사용
        if (skill.visualStretchY > 0f && skill.visualStretchY != 1f)
        {
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(s.x, s.y * skill.visualStretchY, s.z);
        }

        // 하단 앵커: 중심이 아니라 스프라이트 하단이 대상에 닿도록 위로 절반만큼 올린다(낙뢰 등).
        if (skill.vfxAnchorBottom)
        {
            float h = sr.sprite.bounds.size.y * go.transform.localScale.y;
            go.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
        }

        go.AddComponent<SpriteSheetAnimator>().Play(skill.animFrames, skill.animFps, loop: false);

        // 애니 재생 시간 + 마지막 프레임 유지 시간(vfxLingerTime) 후 소멸
        float linger = skill.vfxLingerTime > 0f ? skill.vfxLingerTime : 0.1f;
        float life = skill.animFrames.Length / Mathf.Max(1f, skill.animFps) + linger;
        yield return new WaitForSeconds(life);
        if (go != null) Destroy(go);
    }

    /// <summary>지정한 프레임 배열을 pos에서 1회 재생하는 일회성 VFX(체인 노드 임팩트 등).</summary>
    private void SpawnFramesVFX(Sprite[] frames, Vector3 pos, float size, float fps, int sortingOrder)
    {
        if (frames == null || frames.Length == 0) return;
        StartCoroutine(FramesVFXRoutine(frames, pos, size, fps, sortingOrder));
    }

    private IEnumerator FramesVFXRoutine(Sprite[] frames, Vector3 pos, float size, float fps, int sortingOrder)
    {
        var go = new GameObject("SynergyNodeVFX");
        go.transform.SetParent(transform);
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;
        sr.sprite = frames[0];
        NormalizeScale(go, sr.sprite, size > 0f ? size : 1f);

        float useFps = fps > 0f ? fps : 12f;
        go.AddComponent<SpriteSheetAnimator>().Play(frames, useFps, loop: false);

        float life = frames.Length / Mathf.Max(1f, useFps) + 0.1f;
        yield return new WaitForSeconds(life);
        if (go != null) Destroy(go);
    }

    /// <summary>
    /// 플레이어에 부착되어 끊김 없이 루프 재생되는 영구 오라를 1회 생성한다(마왕 소용돌이).
    /// 데미지는 별도 SkillLoop가 처리하며, 이 오라는 순수 비주얼이다. Refresh/파괴 시 정리된다.
    /// </summary>
    private void SpawnPersistentAura(SO_SkillData skill)
    {
        if (_player == null) FindPlayerRefs();
        if (_player == null || skill.animFrames == null || skill.animFrames.Length == 0) return;

        var go = new GameObject($"SynergyAura_{skill.skillID}");
        go.transform.SetParent(_player);
        go.transform.localPosition = Vector3.zero;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = SynergyLayers.BelowChar; // 마왕 소용돌이: 캐릭터 아래
        sr.sprite = skill.animFrames[0];
        float size = skill.visualSize > 0f ? skill.visualSize : Mathf.Max(1f, skill.rangeRadius);
        NormalizeScale(go, sr.sprite, size);

        go.AddComponent<SpriteSheetAnimator>().Play(skill.animFrames, skill.animFps, loop: true);
        _auraVFX.Add(go);
    }

    /// <summary>
    /// 지속 빔(과부하 레이저)을 1개 생성한다. 플레이어가 바라보는 방향을 추적하며 주기적으로
    /// 빔 경로의 적에게 데미지를 준다. Refresh/파괴 시 _auraVFX 정리 루틴이 함께 제거한다.
    /// rangeRadius=빔 길이, visualSize=빔 두께, cooldown=데미지 틱 간격으로 해석한다.
    /// </summary>
    private void SpawnBeam(SO_SkillData skill, int damage)
    {
        if (_player == null) FindPlayerRefs();
        if (_player == null) return;

        var go   = new GameObject($"SynergyBeam_{skill.skillID}");
        go.transform.SetParent(transform);
        var beam = go.AddComponent<SynergyBeam>();
        beam.Init(_player, _playerAttack,
                  length: skill.rangeRadius, width: skill.visualSize,
                  damage: damage, tickInterval: skill.cooldown,
                  frames: skill.animFrames, fps: skill.animFps,
                  sortingOrder: skill.vfxSortingOrder);
        _auraVFX.Add(go);
    }

    /// <summary>
    /// 적과 적을 잇는 번개를 그린다(체인라이트닝).
    /// seg 스프라이트가 있으면 구간 길이만큼 이미지를 반복(타일)해 깔고, 없으면 단색 LineRenderer로 대체한다.
    /// </summary>
    private void SpawnChainLine(List<Vector3> points, Sprite seg)
    {
        if (points == null || points.Count < 2) return;

        // seg 없거나 크기 이상 → 단색 선 fallback
        if (seg == null || seg.bounds.size.y < 0.001f) { SpawnChainLineColored(points); return; }

        const float thickness = 1.0f;                 // 번개 두께(월드 유닛)
        float nativeH = seg.bounds.size.y;            // 스프라이트 1장 높이(스케일1 기준)
        float scale   = thickness / nativeH;          // 두께에 맞춘 스케일
        // 이음새 빈틈 보정: 각 구간을 번개 이미지 1장 길이의 5/10 만큼 늘려 이웃 구간과 겹치게 한다.
        float overlap = seg.bounds.size.x * scale * 0.5f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 a = points[i], b = points[i + 1];
            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.01f) continue;

            var go = new GameObject("ChainSeg");
            go.transform.SetParent(transform);
            go.transform.position   = (a + b) * 0.5f;
            go.transform.rotation   = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = seg;
            sr.sortingOrder = SynergyLayers.Link;
            sr.drawMode     = SpriteDrawMode.Tiled;     // 길이만큼 가로로 반복
            sr.tileMode     = SpriteTileMode.Continuous;
            // 로컬 size: 가로=구간 길이(+겹침 보정)를 스케일로 환산, 세로=원본 높이(→ 월드 두께)
            sr.size = new Vector2((len + overlap) / scale, nativeH);

            StartCoroutine(FadeSprite(sr, go, 0.25f));
        }
    }

    /// <summary>seg 스프라이트가 없을 때의 단색 LineRenderer 번개(대체).</summary>
    private void SpawnChainLineColored(List<Vector3> points)
    {
        var go = new GameObject("ChainLightningLine");
        go.transform.SetParent(transform);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace   = true;
        lr.positionCount   = points.Count;
        for (int i = 0; i < points.Count; i++) lr.SetPosition(i, points[i]);
        lr.widthMultiplier = 0.36f;
        lr.numCapVertices  = 2;
        lr.numCornerVertices = 2;
        lr.material        = new Material(Shader.Find("Sprites/Default"));
        var col            = new Color(0.6f, 0.9f, 1f, 1f);
        lr.startColor = col; lr.endColor = col;
        lr.sortingOrder = SynergyLayers.Link;

        StartCoroutine(FadeChainLine(lr, go, 0.25f));
    }

    /// <summary>SpriteRenderer 알파 페이드 후 파괴(체인 세그먼트용).</summary>
    private IEnumerator FadeSprite(SpriteRenderer sr, GameObject go, float dur)
    {
        Color baseCol = sr != null ? sr.color : Color.white;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            if (sr != null) sr.color = new Color(baseCol.r, baseCol.g, baseCol.b, Mathf.Lerp(1f, 0f, t / dur));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private IEnumerator FadeChainLine(LineRenderer lr, GameObject go, float dur)
    {
        Material mat = lr != null ? lr.material : null;
        Color baseCol = lr != null ? lr.startColor : Color.white;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t / dur);
            if (lr != null)
            {
                var c = new Color(baseCol.r, baseCol.g, baseCol.b, a);
                lr.startColor = c; lr.endColor = c;
            }
            yield return null;
        }
        if (go  != null) Destroy(go);
        if (mat != null) Destroy(mat); // 런타임 생성 머티리얼 누수 방지
    }

    private IEnumerator PlaceholderVFX(Vector3 pos, float radius)
    {
        var go = new GameObject("VFX_Placeholder");
        go.transform.SetParent(transform); // SynergyManager 파괴 시 자동 소멸
        go.transform.position = pos;

        var sr  = go.AddComponent<SpriteRenderer>();
        var tex = new Texture2D(64, 64);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float dx = x - 32f, dy = y - 32f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = dist < 28f ? 1f : dist < 32f ? (32f - dist) / 4f : 0f;
            tex.SetPixel(x, y, new Color(1f, 0.9f, 0.1f, alpha));
        }
        tex.Apply();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
        sr.sortingOrder = SynergyLayers.Effect;
        float scale = Mathf.Max(0.5f, radius * 0.08f);
        go.transform.localScale = Vector3.one * scale;

        float elapsed = 0f;
        while (elapsed < 0.4f)
        {
            elapsed += Time.deltaTime;
            sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.8f, 0f, elapsed / 0.4f));
            yield return null;
        }
        Destroy(go);
    }

    // ─────────────────────────────────────────────────────────────
    // 소환형

    /// <summary>지속시간 소환수(성역)를 소멸 후 SummonRecastGap 만큼 쉬었다가 다시 시전하는 루프.</summary>
    private IEnumerator SummonRespawnLoop(SO_SummonData data, int count, int atkPower)
    {
        while (true)
        {
            SpawnMinions(data, count, atkPower);
            // 소환수는 data.duration 후 스스로 소멸 → 빈 시간(Gap) 뒤 재시전
            yield return new WaitForSeconds(data.duration + SummonRecastGap);
        }
    }

    private void SpawnMinions(SO_SummonData data, int count, int atkPower)
    {
        if (_player == null) FindPlayerRefs();
        if (_player == null) return;

        // 자가 소멸한(파괴된) 소환수의 null 항목 제거 — 재시전 누적으로 리스트가 무한히 커지는 것 방지
        _summons.RemoveAll(s => s == null);

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * 1.5f;
            var go = new GameObject($"Summon_{data.summonID}_{i}");
            go.transform.SetParent(_summonRoot);
            go.transform.position = _player.position + (Vector3)offset;

            var sc = go.AddComponent<SummonController>();
            sc.Init(data, _player, atkPower, _enemyLayer, i, count);
            _summons.Add(sc);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 유틸

    private SynergySkillBinding FindSkillBinding(SynergyType type, SynergyGrade grade)
    {
        if (_skillBindings == null) return null;
        foreach (var b in _skillBindings)
            if (b.synergyType == type && b.grade == grade && b.skill != null) return b;
        return null;
    }

    /// <summary>같은 (시너지·등급)에 바인딩된 모든 스킬 항목을 반환(마왕 누적 발동 지원).</summary>
    private IEnumerable<SynergySkillBinding> FindSkillBindings(SynergyType type, SynergyGrade grade)
    {
        if (_skillBindings == null) yield break;
        foreach (var b in _skillBindings)
            if (b.synergyType == type && b.grade == grade && b.skill != null) yield return b;
    }

    private SynergySummonBinding FindSummonBinding(SynergyType type, SynergyGrade grade)
    {
        if (_summonBindings == null) return null;
        foreach (var b in _summonBindings)
            if (b.synergyType == type && b.grade == grade && b.summon != null) return b;
        return null;
    }

    /// <summary>같은 (시너지·등급)에 바인딩된 모든 소환수 항목을 반환(원소별 누적 소환 지원).</summary>
    private IEnumerable<SynergySummonBinding> FindSummonBindings(SynergyType type, SynergyGrade grade)
    {
        if (_summonBindings == null) yield break;
        foreach (var b in _summonBindings)
            if (b.synergyType == type && b.grade == grade && b.summon != null) yield return b;
    }

    private List<MonsterController> GetEnemiesInRange(Vector3 center, float range)
    {
        var result = new List<MonsterController>();

        // 트리거 콜라이더 포함(useTriggers) + 레이어 필터 없음(useLayerMask=false)으로 검색.
        // 적 Collider가 트리거이거나 _enemyLayer 설정이 어긋나도 잡히도록 한다.
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = false, useDepth = false };
        var cols   = new List<Collider2D>();
        Physics2D.OverlapCircle((Vector2)center, range, filter, cols);
        foreach (var h in cols)
        {
            var mc = h.GetComponent<MonsterController>()
                  ?? h.GetComponentInParent<MonsterController>();
            if (mc != null && !mc.IsDead && mc.gameObject.activeInHierarchy && !result.Contains(mc))
                result.Add(mc);
        }
        return result;
    }

    private MonsterController FindNearest(List<MonsterController> list, Vector3 from)
    {
        MonsterController best = null;
        float minD = float.MaxValue;
        foreach (var mc in list)
        {
            float d = Vector2.Distance(from, mc.transform.position);
            if (d < minD) { minD = d; best = mc; }
        }
        return best;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // ─────────────────────────────────────────────────────────────
    // 테스트 전용 진입점 (SynergyTestPanel 에서 호출)

    /// <summary>임의로 구성한 로드아웃을 즉시 적용하고, 정식 로드아웃 덮어쓰기를 잠근다.</summary>
    public void ApplyLoadoutForTest(BattleLoadout loadout)
    {
        _testLockActive = true;
        _loadout = loadout;
        if (_player == null) FindPlayerRefs();
        Refresh();
    }

    /// <summary>테스트 잠금을 풀고 현재 정식 로드아웃으로 복귀한다.</summary>
    public void ReleaseTestLock()
    {
        _testLockActive = false;
        if (_gm != null && _gm.CurrentLoadout != null)
        {
            _loadout = _gm.CurrentLoadout;
            Refresh();
        }
    }

    /// <summary>해당 (시너지·등급)에 실제 바인딩(스킬/소환수)이 존재하는지 — 테스트 패널 버튼 표시용.</summary>
    public bool HasBindingFor(SynergyType type, SynergyGrade grade)
    {
        if (_skillBindings != null)
            foreach (var b in _skillBindings)
                if (b.synergyType == type && b.grade == grade && b.skill != null) return true;
        if (_summonBindings != null)
            foreach (var b in _summonBindings)
                if (b.synergyType == type && b.grade == grade && b.summon != null) return true;
        return false;
    }

    /// <summary>OnHitTaken 트리거(난공불락 등)를 강제 1회 발동.</summary>
    public void TestSimulateHit() => OnPlayerHit(10);

    /// <summary>OnMove 트리거(대부호)를 강제 1회 발동.</summary>
    public void TestSimulateMove() => OnPlayerMoved(MoveDropInterval);
#endif
}
