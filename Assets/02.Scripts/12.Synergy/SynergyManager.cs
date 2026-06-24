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
///   Penalty    — 브~골 패널티, 프리즘 초강력 발동 (과부화)
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
    private BattleLoadout                _loadout;

    private readonly List<Coroutine>         _skillRoutines = new();
    private readonly List<SummonController>  _summons       = new();
    private Transform                        _summonRoot;

    // OnHitTaken 트리거 등록 목록 (난공불락)
    private readonly List<(SO_SkillData skill, int dmg)> _onHitSkills = new();

    // OnMove 트리거 누적 (대부호)
    private readonly List<(SO_SkillData skill, int dmg)> _onMoveSkills = new();
    private float _moveDistAccum = 0f;
    private const float MoveDropInterval = 1f; // 1유닛 이동마다 골드 드랍

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
    }

    // ─────────────────────────────────────────────────────────────

    private void FindPlayerRefs()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null) return;

        _player         = playerGO.transform;
        _playerHealth   = playerGO.GetComponent<PlayerHealth>();
        _playerMovement = playerGO.GetComponent<PlayerMovement>();
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
        _loadout = loadout;
        Refresh();
    }

    /// <summary>로드아웃이 갱신될 때마다 기존 러너 제거 후 재구성</summary>
    private void Refresh()
    {
        // 기존 루프/소환 전부 정리
        foreach (var co in _skillRoutines) if (co != null) StopCoroutine(co);
        _skillRoutines.Clear();
        foreach (var s in _summons) if (s != null) Destroy(s.gameObject);
        _summons.Clear();
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
            var skillBinding = FindSkillBinding(entry.type, entry.grade);
            if (skillBinding?.skill != null)
            {
                var skill = skillBinding.skill;
                dmgBase = Mathf.RoundToInt(_loadout.GetScaledBase(skill.scalingStat) * skill.dmgMultiplier);

                switch (skill.triggerType)
                {
                    case SynergyTriggerType.AutoTimer:
                        _skillRoutines.Add(StartCoroutine(SkillLoop(skill, dmgBase)));
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
            var summonBinding = FindSummonBinding(entry.type, entry.grade);
            if (summonBinding?.summon != null)
            {
                var summon    = summonBinding.summon;
                int summonAtk = Mathf.RoundToInt(_loadout.GetScaledBase(summon.scalingStat) * summon.atkMultiplier);
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
                var sb = FindSkillBinding(entry.type, entry.grade);
                if (sb?.skill != null && sb.skill.fixedEffect == FixedEffectType.DamageReduction)
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
                ExecuteSkill(skill, dmg);
        }
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
    // Penalty 루프 (과부화)

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

    private void ApplyOverloadPenalty(float speedRate)
    {
        if (_playerMovement != null)
            _playerMovement.speedMultiplier = speedRate;
        Debug.Log($"[과부화] 패널티 발동 (이동속도 ×{speedRate:F2})");
    }

    private void RemoveOverloadPenalty()
    {
        if (_playerMovement != null)
            _playerMovement.speedMultiplier = 1f;
        Debug.Log("[과부화] 패널티 해제");
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
                int count   = skill.extraCount > 0 ? skill.extraCount : 1;
                for (int i = 0; i < count && enemies.Count > 0; i++)
                {
                    var target = enemies[Random.Range(0, enemies.Count)];
                    vfxPos = target.transform.position;
                    HitEnemy(skill, target, damage);
                    enemies.Remove(target);
                }
                break;
            }

            case SkillTargetType.AreaCenter:
            case SkillTargetType.Self:
            {
                var enemies = GetEnemiesInRange(_player.position, skill.rangeRadius);
                foreach (var mc in enemies) HitEnemy(skill, mc, damage);
                break;
            }

            case SkillTargetType.Forward:
            {
                var enemies = GetEnemiesInRange(_player.position, skill.rangeRadius <= 0f ? 20f : skill.rangeRadius);
                MonsterController nearest = FindNearest(enemies, _player.position);
                if (nearest != null) { vfxPos = nearest.transform.position; HitEnemy(skill, nearest, damage); }
                break;
            }

            case SkillTargetType.ForwardDual:
            {
                // 좌·우 방향으로 각각 가장 가까운 적 타격 (처형자)
                var enemies = GetEnemiesInRange(_player.position, skill.rangeRadius <= 0f ? 20f : skill.rangeRadius);
                MonsterController nearest = FindNearest(enemies, _player.position);
                if (nearest != null)
                {
                    vfxPos = nearest.transform.position;
                    HitEnemy(skill, nearest, damage);
                    // 두 번째 타격: 첫 번째와 다른 적
                    enemies.Remove(nearest);
                    MonsterController second = FindNearest(enemies, _player.position);
                    if (second != null) HitEnemy(skill, second, damage);
                }
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
                    HitEnemy(skill, t, damage);
                    enemies.Remove(t);
                }
                break;
            }
        }

        SpawnVFX(skill, vfxPos);
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
        StartCoroutine(PlaceholderVFX(pos, skill.rangeRadius));
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
        sr.sortingOrder = 10;
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

    private void SpawnMinions(SO_SummonData data, int count, int atkPower)
    {
        if (_player == null) FindPlayerRefs();
        if (_player == null) return;

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

    private SynergySummonBinding FindSummonBinding(SynergyType type, SynergyGrade grade)
    {
        if (_summonBindings == null) return null;
        foreach (var b in _summonBindings)
            if (b.synergyType == type && b.grade == grade && b.summon != null) return b;
        return null;
    }

    private List<MonsterController> GetEnemiesInRange(Vector3 center, float range)
    {
        var result = new List<MonsterController>();
        var filter = new ContactFilter2D { useTriggers = true };
        if (_enemyLayer != 0) filter.SetLayerMask(_enemyLayer);
        else filter = ContactFilter2D.noFilter;

        var cols = new List<Collider2D>();
        Physics2D.OverlapCircle((Vector2)center, range, filter, cols);
        foreach (var h in cols)
        {
            var mc = h.GetComponent<MonsterController>()
                  ?? h.GetComponentInParent<MonsterController>();
            if (mc != null && !mc.IsDead && mc.gameObject.activeInHierarchy)
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
}
