using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagSurvivor.Monster;

/// <summary>
/// BattleLoadout의 무기 목록을 읽어 자동 공격을 처리한다.
/// effectiveGrade >= 4(5단계)일 때 무기별 강화 로직이 적용된다.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("레이어 설정")]
    [SerializeField] private LayerMask _enemyLayer;

    [Header("근접 기본값")]
    [SerializeField] private float _meleeRange    = 1.5f;
    [SerializeField] private float _meleeKnockback = 2f;

    private PlayerStats              _stats;
    private Rigidbody2D              _rb;
    private GameManager              _gm;
    private Vector2                  _lastMoveDir = Vector2.right;
    /// <summary>플레이어가 마지막으로 이동(=바라보는)한 방향. 시너지 등 외부에서 발사 방향으로 사용.</summary>
    public Vector2 FacingDirection => _lastMoveDir;
    private readonly List<Coroutine> _loops = new();
    private BattleLoadout            _currentLoadout;
    private bool                     _paused;
    private bool                     _inCombatZone;
    private bool                     _boomerangGoLeft; // WPN_010 좌우 방향 토글
    private GameObject               _flameVisual;     // WPN_026 지속 불꽃 비주얼(하나만 유지)
    private bool                     _flameActive;     // WPN_026 채널 중복 시작 방지

    // ─────────────────────────────────────────────────────────────

    // PlayerStats가 없을 때 사용할 캐릭터 배율 폴백 (전사 등)
    private float _charAtkMul    = 1f;
    private float _charAtkSpdMul = 1f;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _rb    = GetComponent<Rigidbody2D>();

        // PlayerStats가 없으면 캐릭터 데이터(미선택 시 전사 폴백)에서 공격/공속 배율을 직접 읽는다.
        if (_stats == null)
        {
            var cd = CharacterManager.GetSelectedOrDefault();
            if (cd != null)
            {
                _charAtkMul    = cd.attackMultiplier;
                _charAtkSpdMul = cd.attackSpeedMultiplier;
            }
        }
    }

    private void Start()
    {
        _gm = GameManager.Instance;
        if (_gm != null)
        {
            _gm.onLoadoutReady += OnLoadoutReady;
            if (_gm.CurrentLoadout != null)
                OnLoadoutReady(_gm.CurrentLoadout);
        }
        else
            Debug.LogWarning("[PlayerAttack] GameManager.Instance가 null — GameManager 프리팹이 씬에 있는지 확인하세요.");

        InventoryPopupToggle.onPopupToggled += OnInventoryToggled;
        CombatZone.onCombatStateChanged     += OnCombatStateChanged;
    }

    private void OnDestroy()
    {
        if (_gm != null) _gm.onLoadoutReady -= OnLoadoutReady;
        InventoryPopupToggle.onPopupToggled -= OnInventoryToggled;
        CombatZone.onCombatStateChanged     -= OnCombatStateChanged;
    }

    private void Update()
    {
        if (_rb != null && _rb.linearVelocity.sqrMagnitude > 0.01f)
            _lastMoveDir = _rb.linearVelocity.normalized;
    }

    // ─────────────────────────────────────────────────────────────

    private void OnLoadoutReady(BattleLoadout loadout)
    {
        _currentLoadout = loadout;
        RestartLoops();
    }

    private void OnInventoryToggled(bool isOpen)
    {
        if (_paused == isOpen) return;
        _paused = isOpen;
        RestartLoops();
    }

    private void OnCombatStateChanged(bool inCombat)
    {
        if (_inCombatZone == inCombat) return;
        _inCombatZone = inCombat;
        RestartLoops();
        Debug.Log($"[PlayerAttack] {(inCombat ? "전투 구역 진입 — 공격 시작" : "비전투 구역 이탈 — 공격 정지")}");
    }

    private void RestartLoops()
    {
        // StopAllCoroutines: AttackMeleeBurst, AttackBurst, DelayedFanDir 등
        // _loops 밖에서 기동된 서브 코루틴도 함께 중단
        StopAllCoroutines();
        _loops.Clear();
        if (_flameVisual != null) Destroy(_flameVisual); // 화염방사기 지속 불꽃 정리
        _flameVisual = null;
        _flameActive = false;

        Debug.Log($"[PlayerAttack] RestartLoops — inCombat={_inCombatZone}, paused={_paused}, loadout={_currentLoadout != null}, weapons={_currentLoadout?.Weapons.Count ?? 0}");
        if (!_inCombatZone || _paused || _currentLoadout == null || _currentLoadout.Weapons.Count == 0) return;

        Debug.Log($"[PlayerAttack] 공격 루프 시작 — 무기 {_currentLoadout.Weapons.Count}개");
        foreach (var w in _currentLoadout.Weapons)
            _loops.Add(StartCoroutine(AttackLoop(w)));
    }

    private IEnumerator AttackLoop(WeaponLoadoutEntry entry)
    {
        bool  g5           = entry.effectiveGrade >= 4;
        string id          = entry.data.itemID;
        float  baseAps     = _stats != null ? _stats.attackSpeed : 1f;
        float  weaponAps   = entry.attackSpeed > 0f ? entry.attackSpeed : 1f;
        float  charSpeedMul = _stats != null ? _stats.attackSpeedMultiplier : _charAtkSpdMul;

        // 5단계 공격 속도/쿨타임 보정 (interval = 1/(...×spdBoost) → 클수록 빠름)
        float spdBoost = 1f;
        if (g5) spdBoost = id switch
        {
            "WPN_001" => 4f,   // 단검: 쿨타임 1/4
            "WPN_002" => 4f,   // 장검: 쿨타임 1/4
            "WPN_009" => 4f,   // 활: 쿨타임 1/4
            "WPN_014" => 4f,   // 수류탄: 쿨타임 1/4
            "WPN_030" => 5f,   // 사이드: 쿨타임 1/5
            "WPN_020" => 4f,   // 카타나 5단계: 공속 +300%(×4)
            "WPN_010" => 3f,   // 부메랑: 공속 +200%
            "WPN_016" => 2f,   // 수리검 5단계: 공속 2배
            "WPN_021" => 2f,   // 스피어: 찌르기 애니속도 2배
            "WPN_022" => 2f,   // 플레일: 공속 +100%
            "WPN_003" => 2f,   // 철퇴: 찌르기 속도 2배
            _ => 1f,
        };

        // attackSpeed = "이 초마다 1발"(초/발, 클수록 느림). 최종 쿨타임 = 무기공속 / (기본공속 × 5단계보정 × 캐릭터공속배율)
        // → weaponAps가 분자라 클수록 간격이 길어짐(느려짐), spdBoost·charSpeedMul이 클수록 빨라짐.
        float spdDenom = baseAps * spdBoost * charSpeedMul;
        if (spdDenom <= 0f) spdDenom = 1f; // 0 나눗셈 방지
        float interval = weaponAps / spdDenom;
        if (g5 && id == "WPN_025") interval = 1f; // 너클 5단계: 쿨타임 1초 고정
        interval = Mathf.Max(interval, 0.05f); // 최소 쿨타임 하한(금반지+5단계 공속 폭주·투사체 폭증 방지)
        Debug.Log($"[PlayerAttack] {entry.data.itemName} 루프 시작 — {interval:F2}s / {entry.data.attackStyleType}{(g5 ? " [5단계]" : "")}");

        var wait = new WaitForSeconds(interval);
        while (true)
        {
            yield return wait;
            TryAttack(entry);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 공격 디스패치 — 무기 ID / 등급 기준으로 파라미터 결정

    private void TryAttack(WeaponLoadoutEntry entry)
    {
        bool   g5    = entry.effectiveGrade >= 4;
        string id    = entry.data.itemID;
        float  range = entry.data.range > 0 ? entry.data.range : _meleeRange;

        PlayFireSfx(id, entry.data.attackStyleType); // 무기별 발사/휘두름 효과음

        // 근접무기 전체 사거리 ×3 (표시·피격 동반, 비율 유지). 개별 크기 배율은 각 case에서 추가 적용.
        bool isMelee = entry.data.attackStyleType == WeaponAttackStyleType.MeleeFan ||
                       entry.data.attackStyleType == WeaponAttackStyleType.MeleeSingle;
        if (isMelee) range *= 3f;
        // 상점 아이콘(A그룹) 근접: 아이콘이 커서 크기 1/3로 축소(표시·피격 동반)
        if (isMelee && UsesShopIcon(id)) range *= 1f / 3f;

        switch (entry.data.attackStyleType)
        {
            // ── SingleTarget ───────────────────────────────────────
            case WeaponAttackStyleType.SingleTarget:
            {
                int   shots      = 1;
                float dmgMult    = 1f;
                float spdMult    = 1f;
                float scaleMult  = id switch
                {
                    "WPN_016"              => 3f, // 수리검: 투사체 크기 ×3(가로세로 각각)
                    "WPN_001"              => 2f, // 단검: 투사체 크기 ×2 (4→2, 1/2 축소)
                    "WPN_007"              => 2.8f, // 권총: ×2.8 (기존 ×4의 70%)
                    "WPN_009"              => 2f, // 활: ×2(가로세로 각각)
                    "WPN_006"              => 2f, // 쇠뇌: 투사체 크기 ×2
                    "WPN_011"              => 1.3f, // 지팡이: 투사체 크기 ×1.3
                    _                      => 1f,
                };
                int   pierce     = 0;
                bool  homing     = id == "WPN_011"; // 지팡이: 유도 투사체

                // 쇠뇌: 4단계(effectiveGrade≥3)부터 무제한 관통
                if (id == "WPN_006" && entry.effectiveGrade >= 3)
                    pierce = 99;

                if (g5)
                {
                    switch (id)
                    {
                        case "WPN_001": shots     = 4;          break; // 단검: 투사체 +3 (쿨 1/4는 AttackLoop)
                        case "WPN_006": pierce    = 10; scaleMult *= 2f; break; // 쇠뇌 5단계: 관통 +10 + 투사체 크기 ×2
                        case "WPN_007": dmgMult   = 6f;         break; // 권총: 데미지 +500%
                        case "WPN_009": scaleMult *= 2f; pierce = 5; break; // 활 5단계: 투사체 크기 ×2 + 관통 +5
                        case "WPN_011": shots     = 3;          break; // 지팡이: 3발 (유도)
                        case "WPN_016": scaleMult *= 3f; pierce = 5; range *= 2f; break; // 수리검 5단계: 크기 ×3(기본3→9) + 관통 +5 + 탐지범위 ×2
                    }
                }

                AttackSingleTarget(entry, range, shots, dmgMult, spdMult, scaleMult, pierce, homing);
                break;
            }

            // ── MeleeFan ───────────────────────────────────────────
            case WeaponAttackStyleType.MeleeFan:
            {
                if (id == "WPN_004") range *= 0.5f; // 채찍: 크기 0.5배(표시·피격 함께)
                if (id == "WPN_022") range *= 0.375f; // 플레일: 0.5→0.375 (3/4 축소)
                if (id == "WPN_021") range *= 0.375f; // 스피어: 0.5→0.375 (3/4 축소)
                if (id == "WPN_019") range *= 0.9f;   // 대검: 1.2→0.9 (3/4 축소)
                if (id == "WPN_024") range *= 0.525f; // 몽둥이: 0.7→0.525 (3/4 축소)
                if (id == "WPN_023") range *= 0.3f;  // 메이스: 0.4→0.3 (3/4 축소)
                if (id == "WPN_029") range *= 0.75f; // 워해머: 3/4 축소
                if (id == "WPN_030") range *= 0.75f; // 사이드: 3/4 축소
                // 무기별 기본 각도
                float angle = id switch
                {
                    "WPN_002" => 90f,
                    "WPN_004" => 120f, // 채찍: 우측 120도
                    "WPN_005" => 120f, // 도끼: 전방 120도 회전
                    "WPN_019" => 180f, // 대검: 180도
                    "WPN_020" => 120f,
                    "WPN_021" => 30f,  // 스피어: 좁은 직선 찌르기(전방 관통)
                    "WPN_022" => 120f, // 플레일: 전방 범위(전용 애니, 크기는 아래 range 배율)
                    "WPN_023" => 90f,  // 메이스: 전방 90도
                    "WPN_024" => 180f, // 몽둥이: 전방 180도
                    "WPN_027" => 180f, // 할버드: 180도 회전(이후 찌르기 콤보)
                    "WPN_029" => 360f, // 워해머: 전방위
                    "WPN_030" => 180f, // 사이드: 반원
                    _         => 90f,
                };
                float kb         = _meleeKnockback;
                float flashScale = 1f;
                float dmgMult    = 1f;

                if (g5)
                {
                    switch (id)
                    {
                        // 사이드030의 5단계는 쿨타임 감소(AttackLoop)로 처리 — 각도/애니 변경 없음
                        case "WPN_002": range     *= 1.5f; break; // 장검 5단계: 판정 범위·이미지 크기 ×1.5(표시·피격 동반)
                        case "WPN_005": range     *= 1.5f; break; // 도끼 5단계: 공격 범위 +50%
                        case "WPN_019": kb        = 10f;   break; // 대검: 넉백 +10 (이속저하는 후처리)
                        case "WPN_020": flashScale = 2f;   break; // 카타나: 이펙트 크기 +100%
                        case "WPN_021": range     *= 2f;   break; // 스피어 5단계: 사거리 2배(관통은 부채꼴 기본, 애니속도는 spdBoost)
                        case "WPN_022": range     *= 1.5f; break; // 플레일: 범위 +50% (후면 타격은 후처리)
                        case "WPN_024": kb        *= 8f;   break; // 몽둥이 5단계: 넉백 8배
                        case "WPN_029": dmgMult    = 4f;   break; // 워해머 5단계: 데미지 +300%(×4)
                    }
                }

                // 화염방사기: 전투 중 계속 켜지는 지속 분사(도트). 이미 켜져 있으면 중복 시작 안 함.
                if (id == "WPN_026")
                {
                    if (!_flameActive) StartCoroutine(FlamethrowerChannel(entry, 120f, 4f));
                    break;
                }

                // 채찍: 오른쪽 → 왼쪽 순차 공격 (5단계: 상하좌우 4방향)
                if (id == "WPN_004")
                {
                    AttackMeleeFanDir(entry, range, angle, kb, flashScale, Vector2.right);
                    if (g5)
                    {
                        StartCoroutine(DelayedFanDir(entry, range, angle, kb, flashScale, Vector2.left, 0.15f));
                        StartCoroutine(DelayedFanDir(entry, range, angle, kb, flashScale, Vector2.up,   0.30f));
                        StartCoroutine(DelayedFanDir(entry, range, angle, kb, flashScale, Vector2.down, 0.45f));
                    }
                    break;
                }

                // 할버드: 180° 회전 → 찌르기 콤보(근접). 근접 크기 0.2배
                if (id == "WPN_027")
                {
                    range *= 0.15f; // 할버드: 0.2→0.15 (3/4 축소)
                    StartCoroutine(HalberdCombo(entry, range, angle, kb, flashScale));
                    break;
                }

                // 판정 반경엔 ApplyMeleeFan이 전방 오프셋(reach)을 더해 보이는 무기와 일치시킴
                AttackMeleeFan(entry, range, angle, kb, flashScale, dmgMult);

                // 플레일 5단계: 정면뿐 아니라 후면에도 부채꼴 타격(두 부채꼴)
                if (g5 && id == "WPN_022")
                    AttackMeleeFanDir(entry, range, angle, kb, flashScale, -FacingDir(range));

                // 도끼 5단계: 피격 적을 중심으로 주변 적에게 스플래시(범위) 데미지
                if (g5 && id == "WPN_005")
                {
                    int   splashDmg = ScaleDamage(entry.attackPower, 0.5f);
                    float splashR   = 1.5f;
                    foreach (var hit in FindInFan(FacingDir(range), range, angle))
                    {
                        var cols = _enemyLayer == 0
                            ? Physics2D.OverlapCircleAll(hit.transform.position, splashR)
                            : Physics2D.OverlapCircleAll(hit.transform.position, splashR, _enemyLayer);
                        foreach (var col in cols)
                        {
                            var mc = col.GetComponent<MonsterController>() ?? col.GetComponentInParent<MonsterController>();
                            if (mc == null || mc.IsDead || mc == hit) continue;
                            Vector2 kbDir = ((Vector2)mc.transform.position - (Vector2)hit.transform.position).normalized;
                            mc.TakeDamage(splashDmg, 0f, kbDir);
                            TryRingOnHit(entry, mc);
                        }
                    }
                }

                // 메이스 5단계: 실제 타격한 적(부채꼴 판정 내)에게만 스턴 1초
                if (g5 && id == "WPN_023")
                {
                    float maceReach = entry.data.meleeReach > 0f ? entry.data.meleeReach : 1.6f;
                    foreach (var mc in FindInFan(FacingDir(range), range + maceReach, angle))
                        mc.ApplyStun(1f);
                }
                // 워해머 5단계: 전방위 적 이동속도 1/4 (2초)
                if (g5 && id == "WPN_029")
                {
                    foreach (var mc in GetEnemiesInRange(range))
                        mc.ApplySlow(0.25f, 2f);
                }
                // 대검 5단계: 피격 적 이동속도 1/2 (2초)
                if (g5 && id == "WPN_019")
                {
                    foreach (var mc in FindInFan(FacingDir(range), range, angle))
                        mc.ApplySlow(0.5f, 2f);
                }
                // 몽둥이 5단계: 넉백 방향에 벽이 가까우면(벽 꿍) 추가 데미지 +200%
                if (g5 && id == "WPN_024")
                {
                    Vector2 face = FacingDir(range);
                    foreach (var mc in FindInFan(face, range, angle))
                    {
                        Vector2 kbDir = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
                        if (HasWallBehind(mc.transform.position, kbDir, 2.5f))
                            mc.TakeDamage(ScaleDamage(entry.attackPower, 2f), 0f, Vector2.zero); // +200%
                    }
                }

                break;
            }

            // ── MeleeSingle ────────────────────────────────────────
            case WeaponAttackStyleType.MeleeSingle:
            {
                bool  splash    = false;
                int   burst     = id == "WPN_025" ? 2 : 1;        // 너클: 기본 2연타
                float scaleMult = 1f;
                // 크기 조정은 range로 → 표시(2×range)와 피격(반경 range)이 함께 변함
                if (id == "WPN_025") range *= 0.1575f; // 너클: 0.225→0.1575 (크기 70%로 축소)
                if (id == "WPN_003") range *= 0.7f;   // 철퇴: 크기 70%로 축소

                if (g5)
                {
                    if (id == "WPN_025") burst = 5;      // 너클: 5연타
                    if (id == "WPN_003") { range *= 2f; splash = true; } // 철퇴 5단계: 찌르기 거리 2배 + 광역 판정(주변 50%)
                }

                if (burst > 1)
                    StartCoroutine(AttackMeleeBurst(entry, range, burst, scaleMult));
                else
                    AttackMeleeSingle(entry, range, splash, scaleMult);
                break;
            }

            // ── SpreadShot ─────────────────────────────────────────
            case WeaponAttackStyleType.SpreadShot:
            {
                float spreadAngle = 60f;
                int   bullets     = 3;
                float pelletScale = id == "WPN_008" ? 2f : 1f; // 산탄총: 투사체 크기 ×2(콜라이더 동반 → 피격범위도)
                if (g5 && id == "WPN_008") { spreadAngle = 120f; bullets = 6; }
                AttackSpread(entry, range, spreadAngle, bullets, pelletScale);
                if (g5 && id == "WPN_008") // 산탄총 5단계: X2 연사(약간의 딜레이 후 2번째 발사)
                    StartCoroutine(DelayedSpread(entry, range, spreadAngle, bullets, pelletScale, 0.12f));
                break;
            }

            // ── BurstFire ──────────────────────────────────────────
            case WeaponAttackStyleType.BurstFire:
            {
                int   count     = id == "WPN_018" ? 4 : 3; // 레일건 4발, 라이플 3발
                int   volleys   = id == "WPN_015" ? 2 : 1; // 라이플: 3발×2회
                float kbPerShot = id == "WPN_015" ? 0.3f : 0f; // 라이플: 넉백
                float bulletScale = id == "WPN_015" ? 2f : 1f; // 라이플: 투사체 크기 ×2(콜라이더 동반 → 피격범위도)
                if (g5)
                {
                    if (id == "WPN_015") { count = 5; volleys = 3; kbPerShot = 0.6f; } // 5발×3회, 넉백 2배
                    if (id == "WPN_018") count = 8;
                }
                StartCoroutine(AttackBurst(entry, range, count, kbPerShot, volleys, bulletScale));
                break;
            }

            // ── AreaDrop ───────────────────────────────────────────
            case WeaponAttackStyleType.AreaDrop:
            {
                if (id == "WPN_012")
                {
                    // 마도서: 대상 위치에 마법진을 띄워 애니 1회 재생 → 형성 시점에 범위 폭발
                    //         (빠른 투사체로 날리면 거의 안 보여서 고정 연출로 변경)
                    float dropRange = range > 0f ? range : 5f;
                    float explodeR  = dropRange * 0.5f;
                    if (g5) explodeR *= 2f; // 5단계: 메테오 크기 2배

                    var     nearest = FindNearest(dropRange);
                    Vector3 pos     = nearest != null
                        ? nearest.transform.position
                        : (Vector3)((Vector2)transform.position + _lastMoveDir * dropRange);
                    SpawnTargetedExplosion(entry, pos, explodeR);
                }
                else if (id == "WPN_013")
                {
                    // 번개구슬: 가장 가까운 적 머리 위에 낙뢰 이펙트 생성 + 범위 피해(마도서 방식)
                    int   targetCnt = g5 ? 3 : 1; // 5단계 낙뢰 +2 (총 3)
                    float explodeR  = 1.5f;
                    var enemies = GetEnemiesInRange(8f);
                    enemies.Sort((a, b) =>
                        Vector2.Distance(transform.position, a.transform.position)
                            .CompareTo(Vector2.Distance(transform.position, b.transform.position)));
                    int hit = 0;
                    foreach (var mc in enemies)
                    {
                        if (hit >= targetCnt) break;
                        SpawnTargetedExplosion(entry, mc.transform.position, explodeR);
                        hit++;
                    }
                }
                else
                {
                    AttackSingleTarget(entry, range);
                }
                break;
            }

            // ── ThrownExplosive ────────────────────────────────────
            case WeaponAttackStyleType.ThrownExplosive:
            {
                float explodeR = range * 0.5f;
                if (id == "WPN_017") explodeR = 1.0f; // 바주카: 폭발 반경 고정(기본 너프 1.5→1.0)
                int   count    = 1;

                if (g5)
                {
                    // 수류탄014의 "투척 속도 2배"는 AttackLoop spdBoost에서 처리(여기선 발수 그대로)
                    if (id == "WPN_017") explodeR *= 1.5f; // 바주카 5단계: 폭발 반경 +50%(×1.5)
                }

                // 바주카는 미사일 비행 거리(range)와 탐지 범위를 일치시켜 미사일이 적까지 실제로 날아가 맞게 함
                float   detectRange = id == "WPN_017" ? range : range * 2f;
                var     nearest  = FindNearest(detectRange);
                if (id == "WPN_014" && nearest == null) break; // 수류탄: 적이 사정거리 내에 있을 때만 투척
                if (id == "WPN_017" && nearest == null) break; // 바주카: 사정거리 내 적 있을 때만 발사
                // 대상까지의 벡터(없으면 전방 range 거리). 폭발 애니가 있으면 그 지점에 고정 폭발한다.
                Vector2 toTarget  = nearest != null
                    ? ((Vector2)nearest.transform.position - (Vector2)transform.position)
                    : _lastMoveDir * range;
                bool    animated  = ValidFrames(entry.data.attackFrames).Length > 0;

                for (int i = 0; i < count; i++)
                {
                    Vector2 v = count > 1 ? Rotate(toTarget, (i - count / 2) * 20f) : toTarget;
                    if (id == "WPN_017")
                        // 바주카: 애니 투사체가 적을 유도 추적해 명중 시 소규모 폭발(전방 생성)
                        SpawnProjectile(entry, v.normalized, explosionRadius: explodeR, homing: true);
                    else if (animated)
                        // 수류탄 등: 대상 지점에 폭발 애니 1회 재생 + 범위 피해(날아가며 사라지지 않게)
                        SpawnTargetedExplosion(entry, (Vector3)((Vector2)transform.position + v), explodeR);
                    else
                        // 시트 없는 투척물: 기존 비행 투사체 폭발
                        SpawnExplosive(entry, v.normalized, range, explodeR);
                }
                break;
            }

            // ── PierceLine ─────────────────────────────────────────
            case WeaponAttackStyleType.PierceLine:
            {
                float lineRange = g5 && id == "WPN_021" ? range * 2f : range; // 5단계 사거리 2배
                int   pierce    = g5 && id == "WPN_021" ? 13 : 3;             // 5단계 관통 +10
                AttackPierceLine(entry, lineRange, pierce);
                break;
            }

            // ── Boomerang ──────────────────────────────────────────
            case WeaponAttackStyleType.Boomerang:
            {
                float   spdM = g5 ? 3f : 1f; // 5단계: 투사체 속도 +200%(×3)
                Vector2 dir  = Rotate(_lastMoveDir, _boomerangGoLeft ? 90f : -90f);
                _boomerangGoLeft = !_boomerangGoLeft;
                // 던졌다가 플레이어에게 회전하며 복귀
                SpawnProjectile(entry, dir, spdMult: spdM, scaleMult: 4f, boomerang: true); // 부메랑: 투사체 크기 ×4(기존 ×2에서 2배, 콜라이더 동반)
                break;
            }

            // ── Sniper ─────────────────────────────────────────────
            case WeaponAttackStyleType.Sniper:
            {
                int pierce = g5 && id == "WPN_028" ? 5 : 0; // 장궁 5단계: 관통 +5
                AttackSniper(entry, range, pierce);
                break;
            }

            default:
                AttackSingleTarget(entry, range);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 공격 스타일 구현

    /// <summary>무기별 발사/휘두름 효과음. 전용 클립(채찍·바주카·레일건·총기) 우선,
    /// 근접 무기는 무게에 따라 Swing_Heavy/Light. 대응 클립 없는 무기(활·마법 등)는 무음 유지.
    /// 99.External은 에셋스토어 클립(gitignore) — 미임포트 팀원은 자동 폴백/무음.</summary>
    private static void PlayFireSfx(string id, WeaponAttackStyleType style)
    {
        const string Dir = "01.SFX/01.Players/01.Combat/";
        const string Ext = "99.External/";
        string path = id switch
        {
            "WPN_004" => Dir + "Whip",     // 채찍
            "WPN_017" => Dir + "Bazooka",  // 바주카
            "WPN_018" => Dir + "Railgun",  // 레일건
            "WPN_007" or "WPN_008" or "WPN_015" => Dir + "Gunshot", // 권총/샷건/라이플
            // 철퇴: 전용 해머음(외부 에셋), 없으면 Swing_Heavy 폴백
            "WPN_023" => AudioUtil.Has(Ext + "Mace_Swing") ? Ext + "Mace_Swing" : Dir + "Swing_Heavy",
            // 번개구슬: 번개 임팩트(외부 에셋), 없으면 무음
            "WPN_013" => AudioUtil.Has(Ext + "LightningOrb_Attack") ? Ext + "LightningOrb_Attack" : null,
            _ => null,
        };
        if (path == null &&
            (style == WeaponAttackStyleType.MeleeFan || style == WeaponAttackStyleType.MeleeSingle))
        {
            bool heavy = id is "WPN_003" or "WPN_005" or "WPN_019" or "WPN_022"
                            or "WPN_024" or "WPN_027" or "WPN_029" or "WPN_030";
            path = heavy ? Dir + "Swing_Heavy" : Dir + "Swing_Light";
        }
        if (path != null) AudioUtil.PlaySfx(path);
    }

    private void AttackSingleTarget(WeaponLoadoutEntry entry, float range,
        int shots = 1, float dmgMult = 1f, float spdMult = 1f,
        float scaleMult = 1f, int pierce = 0, bool homing = false)
    {
        var    nearest = FindNearest(range);
        Vector2 center = nearest != null
            ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
            : _lastMoveDir;

        if (shots <= 1)
        {
            SpawnProjectile(entry, center, dmgMult, spdMult, scaleMult, pierce, homing: homing);
        }
        else
        {
            float spread = 15f;
            float step   = shots > 1 ? spread * 2f / (shots - 1) : 0f;
            for (int i = 0; i < shots; i++)
                SpawnProjectile(entry, Rotate(center, -spread + step * i), dmgMult, spdMult, scaleMult, pierce, homing: homing);
        }
    }

    private void AttackMultiTarget(WeaponLoadoutEntry entry, float range, int count,
        float dmgMult = 1f, float spdMult = 1f, float scaleMult = 1f, int pierce = 0)
    {
        var enemies = GetEnemiesInRange(range);
        enemies.Sort((a, b) =>
            Vector2.Distance(transform.position, a.transform.position)
                .CompareTo(Vector2.Distance(transform.position, b.transform.position)));

        int fired = 0;
        foreach (var mc in enemies)
        {
            if (fired >= count) break;
            Vector2 dir = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
            SpawnProjectile(entry, dir, dmgMult, spdMult, scaleMult, pierce);
            fired++;
        }
        for (int i = fired; i < count; i++)
            SpawnProjectile(entry, Rotate(_lastMoveDir, (i - fired) * 20f), dmgMult, spdMult, scaleMult, pierce);
    }

    private void AttackMeleeFan(WeaponLoadoutEntry entry, float range,
        float angleDeg, float knockback, float flashScale = 1f, float dmgMult = 1f)
    {
        ApplyMeleeFan(entry, FacingDir(range), range, angleDeg, knockback, flashScale, dmgMult: dmgMult);
    }

    private void AttackMeleeFanDir(WeaponLoadoutEntry entry, float range,
        float angleDeg, float knockback, float flashScale, Vector2 fixedDir)
    {
        ApplyMeleeFan(entry, fixedDir.normalized, range, angleDeg, knockback, flashScale);
    }

    // 반지 인접 기믹(근접 피격 시): 뼈=확률 스턴, 나무=슬로우. 등급 면역·쿨다운은 MonsterController가 처리.
    private void TryRingOnHit(WeaponLoadoutEntry entry, MonsterController mc)
    {
        if (mc == null || mc.IsDead) return;
        if (entry.ringStunChance > 0f && UnityEngine.Random.value < entry.ringStunChance) mc.ApplyStun(1f);
        if (entry.ringSlowSec > 0f) mc.ApplySlow(0.25f, entry.ringSlowSec);
    }

    private void ApplyMeleeFan(WeaponLoadoutEntry entry, Vector2 facing,
        float range, float angleDeg, float knockback, float flashScale, float visualRange = -1f, float dmgMult = 1f)
    {
        // 표시는 앞쪽으로 reach만큼 띄워지므로(대검·몽둥이·메이스 등) 판정 반경에 reach를 더해 보이는 무기와 일치시킴
        float reach    = entry.data.meleeReach > 0f ? entry.data.meleeReach : 1.6f;
        float hitRange = range + reach;
        var enemies = angleDeg >= 360f
            ? GetEnemiesInRange(hitRange)
            : FindInFan(facing, hitRange, angleDeg);

        int meleeDmg = ScaleDamage(entry.attackPower, dmgMult);
        int hitCount = 0;
        foreach (var mc in enemies)
        {
            Vector2 kbDir = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
            mc.TakeDamage(meleeDmg, knockback, kbDir);
            TryRingOnHit(entry, mc);
            hitCount++;
        }
        if (hitCount > 0) AudioUtil.PlaySfx("01.SFX/01.Players/01.Combat/Hit_Blade", 0.8f); // 근접 적중음(스윙당 1회)
        // 표시 반경을 판정 반경과 분리 가능(visualRange). 스윙 호 각도는 데미지 부채꼴 각도(angleDeg)와 일치.
        StartCoroutine(ShowMeleeFlash(entry.data, facing, visualRange > 0f ? visualRange : range, flashScale, swingArc: angleDeg));
    }

    private IEnumerator DelayedFanDir(WeaponLoadoutEntry entry, float range,
        float angleDeg, float knockback, float flashScale, Vector2 dir, float delay)
    {
        yield return new WaitForSeconds(delay);
        AttackMeleeFanDir(entry, range, angleDeg, knockback, flashScale, dir);
    }

    // 할버드: 전방 angleDeg(180)° 부채꼴 회전 → 잠시 후 앞으로 찌르기(전방 좁은 판정 + 찌르기 모션).
    private IEnumerator HalberdCombo(WeaponLoadoutEntry entry, float range,
        float angleDeg, float kb, float flashScale)
    {
        // 1타: 180° 회전 (무기 시트 재생 = 무기 기본 모션)
        AttackMeleeFan(entry, range, angleDeg, kb, flashScale);
        yield return new WaitForSeconds(0.25f);

        // 2타: 앞으로 찌르기 — 전방 좁은 판정 + Thrust 모션 비주얼
        Vector2 dir   = FacingDir(range);
        float   reach = entry.data.meleeReach > 0f ? entry.data.meleeReach : 1.6f;
        int     dmg   = ScaleDamage(entry.attackPower);
        foreach (var mc in FindInFan(dir, range + reach, 40f))
        {
            Vector2 kbDir = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
            mc.TakeDamage(dmg, kb, kbDir);
            TryRingOnHit(entry, mc);
        }
        StartCoroutine(ShowMeleeFlash(entry.data, dir, range, flashScale, MeleeMotionType.Thrust));

        // 5단계: 찌르기와 함께 검기(SwordWave 투사체)를 상·하·좌·우 4방향으로 방출(관통)
        if (entry.effectiveGrade >= 4)
        {
            var wave = entry.data.auxProjectileFrames;
            SpawnProjectile(entry, Vector2.up,    pierce: 99, scaleMult: 3f, overrideFrames: wave); // 검기 크기 ×3
            SpawnProjectile(entry, Vector2.down,  pierce: 99, scaleMult: 3f, overrideFrames: wave);
            SpawnProjectile(entry, Vector2.left,  pierce: 99, scaleMult: 3f, overrideFrames: wave);
            SpawnProjectile(entry, Vector2.right, pierce: 99, scaleMult: 3f, overrideFrames: wave);
        }
    }

    private void AttackMeleeSingle(WeaponLoadoutEntry entry, float range, bool splash = false, float scaleMult = 1f)
    {
        // 표시가 앞쪽으로 reach만큼 띄워지므로 판정 반경에 reach를 더해 일치시킴
        float reach    = entry.data.meleeReach > 0f ? entry.data.meleeReach : 1.6f;
        float hitRange = range + reach;
        var    target = FindNearest(hitRange);
        Vector2 dir   = target != null
            ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized
            : _lastMoveDir;

        if (target != null)
        {
            target.TakeDamage(ScaleDamage(entry.attackPower), _meleeKnockback, dir);
            TryRingOnHit(entry, target);

            if (splash)
            {
                foreach (var mc in GetEnemiesInRange(hitRange))
                {
                    if (mc == target) continue;
                    Vector2 kbDir = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
                    mc.TakeDamage(ScaleDamage(entry.attackPower, 0.5f), _meleeKnockback * 0.5f, kbDir);
                    TryRingOnHit(entry, mc);
                }
            }
        }
        StartCoroutine(ShowMeleeFlash(entry.data, dir, range, scaleMult));
    }

    private static readonly WaitForSeconds _waitMeleeBurst = new(0.12f);
    private IEnumerator AttackMeleeBurst(WeaponLoadoutEntry entry, float range, int count, float scaleMult = 1f)
    {
        for (int i = 0; i < count; i++)
        {
            AttackMeleeSingle(entry, range, false, scaleMult);
            yield return _waitMeleeBurst;
        }
    }

    // 산탄총 5단계 X2 연사: delay초 후 2번째 스프레드 발사
    private IEnumerator DelayedSpread(WeaponLoadoutEntry entry, float range,
        float totalAngle, int bulletCount, float scaleMult, float delay)
    {
        yield return new WaitForSeconds(delay);
        AttackSpread(entry, range, totalAngle, bulletCount, scaleMult);
    }

    private void AttackSpread(WeaponLoadoutEntry entry, float range,
        float totalAngle, int bulletCount, float scaleMult = 1f)
    {
        var nearest = FindNearest(range);
        Vector2 center = nearest != null
            ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
            : _lastMoveDir;

        float start = -totalAngle * 0.5f;
        float step  = bulletCount > 1 ? totalAngle / (bulletCount - 1) : 0f;
        for (int i = 0; i < bulletCount; i++)
            SpawnProjectile(entry, Rotate(center, start + step * i), scaleMult: scaleMult);
    }

    private static readonly WaitForSeconds _waitBurst  = new(0.1f);
    private static readonly WaitForSeconds _waitVolley = new(0.25f);
    private IEnumerator AttackBurst(WeaponLoadoutEntry entry, float range,
        int count, float kbPerShot = 0f, int volleys = 1, float scaleMult = 1f)
    {
        for (int v = 0; v < volleys; v++)
        {
            var nearest = FindNearest(range);
            Vector2 dir = nearest != null
                ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
                : _lastMoveDir;

            for (int i = 0; i < count; i++)
            {
                SpawnProjectile(entry, dir, knockbackForce: kbPerShot, scaleMult: scaleMult);
                yield return _waitBurst;
            }
            if (v < volleys - 1) yield return _waitVolley; // 회차 간 간격
        }
    }

    private void AttackPierceLine(WeaponLoadoutEntry entry, float range, int pierce)
    {
        SpawnProjectile(entry, FacingDir(range), pierce: pierce);
    }

    private void AttackSniper(WeaponLoadoutEntry entry, float range, int pierce = 0)
    {
        var    farthest = FindFarthest(range);
        Vector2 dir     = farthest != null
            ? ((Vector2)farthest.transform.position - (Vector2)transform.position).normalized
            : _lastMoveDir;
        SpawnProjectile(entry, dir, pierce: pierce);
    }

    // ─────────────────────────────────────────────────────────────
    // 투사체 생성

    // A그룹(회전/찌르기/투척): 상점 정적 아이콘을 그대로 사용한다(전용 애니 시트 대신).
    private static readonly HashSet<string> _shopIconWeapons = new()
    {
        "WPN_001", "WPN_002", "WPN_003", "WPN_005", "WPN_010", "WPN_016",
        "WPN_019", "WPN_020", "WPN_021", "WPN_024", "WPN_025", "WPN_027", "WPN_029", "WPN_030",
    };
    private static bool UsesShopIcon(string id) => _shopIconWeapons.Contains(id);

    private void SpawnProjectile(WeaponLoadoutEntry entry, Vector2 dir,
        float dmgMult = 1f, float spdMult = 1f, float scaleMult = 1f,
        int pierce = 0, float knockbackForce = 0f, bool homing = false, bool boomerang = false,
        float explosionRadius = 0f, Sprite[] overrideFrames = null)
    {
        var   wd       = entry.data;
        // 강철 반지: 인접 무기 투사체 크기·속도 배율(크기는 피격범위 동반)
        scaleMult *= entry.ringProjScale;
        spdMult   *= entry.ringProjSpeed;
        float rawSpeed = wd.projectileSpeed > 0f ? wd.projectileSpeed : 10f;
        float speed    = rawSpeed * spdMult;
        // 강철 반지 속도감소(ringProjSpeed)는 사거리에 영향 없이 '느려지기만' 하도록 lifetime을 보정
        float lifetime = wd.range > 0 ? (float)wd.range / rawSpeed / entry.ringProjSpeed : 3f;
        int   damage   = ScaleDamage(entry.attackPower, dmgMult);
        int   maxHits  = wd.maxTargets > 0 ? wd.maxTargets : 1; // 0 = 기본 1타
        if (wd.projectileData != null) maxHits += wd.projectileData.pierceCount;
        maxHits += pierce;

        // overrideFrames(예: 할버드 검기 SwordWave)가 있으면 그 스프라이트로, 아니면 무기 기본 프레임/아이콘.
        bool useOverride = overrideFrames != null && ValidFrames(overrideFrames).Length > 0;
        var projFrames = useOverride ? ValidFrames(overrideFrames) : ValidFrames(wd.attackFrames);
        // A그룹 투척(단검·수리검·부메랑): 상점 정적 아이콘이 곧 투사체가 된다. (오버라이드 시 제외)
        if (!useOverride && UsesShopIcon(wd.itemID) && wd.itemImage != null)
            projFrames = new[] { wd.itemImage };
        GameObject go = wd.projectile != null && !useOverride
            ? Instantiate(wd.projectile, transform.position, Quaternion.identity)
            : (projFrames.Length > 0
                ? BuildAnimatedGO(projFrames, wd.attackFps, $"Proj_{wd.itemName}", 0.8f, loop: true, withBody: true)
                : BuildTempGO(wd.itemImage, wd.itemName));

        // 캐릭터 몸 밖(전방)에서 생성 — 캐릭터와 겹쳐 발사되는 것 방지
        go.transform.position = transform.position + (Vector3)(dir.normalized * 0.7f);

        if (scaleMult != 1f)
            go.transform.localScale *= scaleMult;
        // 상점 아이콘(A그룹) 투척: 아이콘이 커서 크기 1/3로 축소. (검기 등 오버라이드 투사체는 제외)
        if (!useOverride && UsesShopIcon(wd.itemID))
            go.transform.localScale *= 1f / 3f;

        // 부메랑: 비행 중 자전(초당 2바퀴). 5단계는 회전속도 ×3.
        float spin = wd.itemID == "WPN_010" ? (entry.effectiveGrade >= 4 ? 2160f : 720f) : 0f;
        float rotOff = wd.itemID switch
        {
            "WPN_001" => -90f, // 단검: 스프라이트 -90° 회전해서 등장
            "WPN_008" => 0f,   // 산탄총: 총알이 진행 방향 정면을 보게(0°)
            _         => 0f,
        };
        float armTime = explosionRadius > 0f ? 0.25f : 0f; // 폭발형: 무장 지연(발사 직후 오폭 방지)
        var proj = go.GetComponent<ProjectileBase>() ?? go.AddComponent<ProjectileBase>();
        proj.Init(dir, damage, speed, lifetime, maxHits, knockbackForce,
                  explosionRadius: explosionRadius,
                  homing: homing, boomerang: boomerang, owner: boomerang ? transform : null,
                  spinSpeed: spin, rotationOffset: rotOff, armTime: armTime,
                  explosionFrames: wd.explosionFrames,
                  ringStunChance: entry.ringStunChance, ringSlowSec: entry.ringSlowSec);
    }

    private void SpawnExplosive(WeaponLoadoutEntry entry, Vector2 dir,
        float travelRange, float explodeRadius)
    {
        var   wd       = entry.data;
        float rawSpeed = wd.projectileSpeed > 0f ? wd.projectileSpeed : 10f;
        float lifetime = travelRange / rawSpeed;
        int   damage   = ScaleDamage(entry.attackPower);

        var projFrames = ValidFrames(wd.attackFrames);
        GameObject go = wd.projectile != null
            ? Instantiate(wd.projectile, transform.position, Quaternion.identity)
            : (projFrames.Length > 0
                ? BuildAnimatedGO(projFrames, wd.attackFps, $"Proj_{wd.itemName}", 0.8f, loop: true, withBody: true)
                : BuildTempGO(wd.itemImage, wd.itemName));
        go.transform.position = transform.position;

        var proj = go.GetComponent<ProjectileBase>() ?? go.AddComponent<ProjectileBase>();
        proj.Init(dir, damage, rawSpeed, lifetime, 1, explosionRadius: explodeRadius,
                  ringStunChance: entry.ringStunChance, ringSlowSec: entry.ringSlowSec);
    }

    /// <summary>
    /// 대상 위치에 시트 애니(마법진/폭발 등)를 고정 생성해 1회 재생하고, 형성 시점에 범위 피해를 준다.
    /// 마도서·그레네이드처럼 "날아가지 않고 그 자리에서 터지는" 연출에 사용한다.
    /// 표시 지름은 피해 반경(explodeRadius)의 2배로 맞춰 시각과 판정이 일치한다.
    /// </summary>
    private void SpawnTargetedExplosion(WeaponLoadoutEntry entry, Vector3 pos, float explodeRadius)
    {
        var   wd       = entry.data;
        int   damage   = ScaleDamage(entry.attackPower);
        float diameter = Mathf.Max(1f, explodeRadius * 2f);
        var   af       = ValidFrames(wd.attackFrames); // 삭제된 프레임 방어
        bool  animated = af.Length > 0;

        const float dur = 0.6f; // 애니 재생 시간
        GameObject go;
        if (animated)
        {
            float fps = af.Length / dur; // 시트 전체를 dur 안에 1회 재생
            go = BuildAnimatedGO(af, fps, $"Burst_{wd.itemName}",
                                 diameter, loop: false, withBody: false);
        }
        else
        {
            go = new GameObject($"Burst_{wd.itemName}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = wd.itemImage; sr.sortingOrder = 10;
            float ext = wd.itemImage != null
                ? Mathf.Max(wd.itemImage.bounds.extents.x, wd.itemImage.bounds.extents.y) : 0.5f;
            go.transform.localScale = Vector3.one * (ext > 0.001f ? diameter * 0.5f / ext : diameter);
        }
        go.transform.position = pos;
        if (wd.itemID == "WPN_012") go.transform.localScale *= 0.5f; // 마도서: 표시 크기 ×0.5 (피해 반경은 유지)
        Destroy(go, dur + 0.1f);

        // 원이 형성되는 시점(애니 60%)에 1회 범위 피해
        StartCoroutine(DelayedExplodeAt(entry, pos, explodeRadius, damage, dur * 0.6f));
    }

    /// <summary>delay초 후 pos 중심 radius 내 모든 적에게 1회 범위 피해를 적용한다.</summary>
    private IEnumerator DelayedExplodeAt(WeaponLoadoutEntry entry, Vector3 pos, float radius, int damage, float delay)
    {
        yield return new WaitForSeconds(delay);
        var hits = _enemyLayer == 0
            ? Physics2D.OverlapCircleAll(pos, radius)
            : Physics2D.OverlapCircleAll(pos, radius, _enemyLayer);
        foreach (var h in hits)
        {
            var mc = h.GetComponent<MonsterController>() ?? h.GetComponentInParent<MonsterController>();
            if (mc == null || mc.IsDead) continue;
            Vector2 kb = ((Vector2)mc.transform.position - (Vector2)pos).normalized;
            mc.TakeDamage(damage, 0f, kb);
            TryRingOnHit(entry, mc);
        }
    }

    /// <summary>
    /// 시트 프레임을 SpriteSheetAnimator로 재생하는 GameObject를 만든다.
    /// 스프라이트는 자식에 두고 중심을 루트에 맞춰(피벗 무관) targetSize 지름으로 정규화한다.
    /// withBody=true면 투사체용 Rigidbody2D+트리거 콜라이더를 루트에 부착한다.
    /// </summary>
    /// <summary>null/삭제된 스프라이트를 제외한 유효 프레임만 반환(없으면 빈 배열). 에셋 삭제로 참조가 깨진 경우 방어.</summary>
    private static Sprite[] ValidFrames(Sprite[] arr)
    {
        if (arr == null) return System.Array.Empty<Sprite>();
        var list = new List<Sprite>(arr.Length);
        foreach (var s in arr) if (s != null) list.Add(s); // Unity의 == null은 삭제(missing)된 객체도 true
        return list.ToArray();
    }

    private static GameObject BuildAnimatedGO(Sprite[] frames, float fps, string name,
        float targetSize, bool loop, bool withBody)
    {
        var go = new GameObject(name);

        if (withBody)
        {
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = targetSize * 0.5f; // 피격 반경을 표시 반경(targetSize/2)에 일치 → 투사체 크기=피격범위
        }

        // 삭제/누락 프레임 방어 — 유효한 게 없으면 빈 오브젝트만 반환(크래시 방지)
        if (frames == null || frames.Length == 0 || frames[0] == null) return go;

        var child = new GameObject("Sprite");
        child.transform.SetParent(go.transform, false);
        var sr = child.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        sr.sprite = frames[0];

        // 목표 지름으로 정규화 + 스프라이트 중심을 루트 원점에 정렬(피벗이 모서리여도 회전 중심 일치)
        var   b         = frames[0].bounds;
        float maxExtent = Mathf.Max(b.extents.x, b.extents.y);
        float scale     = maxExtent > 0.001f ? targetSize * 0.5f / maxExtent : targetSize;
        child.transform.localScale    = Vector3.one * scale;
        child.transform.localPosition = -(Vector3)b.center * scale;

        child.AddComponent<SpriteSheetAnimator>().Play(frames, fps > 0f ? fps : 12f, loop);
        return go;
    }

    private static GameObject BuildTempGO(Sprite icon, string weaponName)
    {
        var go = new GameObject($"Proj_{weaponName}");

        var sr          = go.AddComponent<SpriteRenderer>();
        sr.sprite       = icon;
        sr.color        = icon != null ? Color.white : new Color(1f, 0.8f, 0.2f);
        sr.sortingOrder = 10;

        // 아이콘 스프라이트의 실제 월드 크기에 관계없이 투사체를 0.4 유닛으로 고정
        // (UI용 아이콘은 PPU가 낮아 스케일 0.4f 고정 시 과도하게 커질 수 있음)
        if (icon != null)
        {
            float maxExtent = Mathf.Max(icon.bounds.extents.x, icon.bounds.extents.y);
            go.transform.localScale = maxExtent > 0.001f
                ? Vector3.one * (0.2f / maxExtent)
                : Vector3.one * 0.4f;
        }
        else
        {
            go.transform.localScale = Vector3.one * 0.4f;
        }

        var rb          = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        var col         = go.AddComponent<CircleCollider2D>();
        col.isTrigger   = true;
        col.radius      = 0.3f;

        return go;
    }

    // 화염방사기: dur초 동안 tick마다 전방 부채꼴 도트 피해 + 분사 비주얼.
    // 화염방사기: 전투 중 지속 분사. 불꽃 비주얼 1개를 유지(루프)하며 플레이어를 따라 방향을 향하고,
    // tick마다 부채꼴 도트 데미지. 전투 종료/일시정지 시 RestartLoops가 코루틴 중단 + 불꽃 정리한다.
    private IEnumerator FlamethrowerChannel(WeaponLoadoutEntry entry, float fanAngle, float fanRange)
    {
        _flameActive = true;
        var   wd   = entry.data;
        bool  g5   = entry.effectiveGrade >= 4;
        float tick = g5 ? 0.1f : 0.5f; // 도트 주기(5단계 0.1초)

        // 지속 불꽃 비주얼(루프). 플레이어 중심 피벗 자식으로 앞쪽에 배치.
        var af = ValidFrames(wd.attackFrames);
        Sprite[] frames = af.Length > 0 ? af : (wd.itemImage != null ? new[] { wd.itemImage } : null);
        var pivot = new GameObject($"FlamePivot_{wd.itemName}");
        _flameVisual = pivot;
        if (frames != null)
        {
            float fps  = af.Length > 1 ? af.Length / 0.5f : 12f; // 0.5초에 1회전 루프
            float size = 2f * fanRange / 3f; // 불꽃 표시 크기 1/3
            var   vis  = BuildAnimatedGO(frames, fps, $"Flame_{wd.itemName}", size, loop: true, withBody: false);
            vis.transform.SetParent(pivot.transform, false);
            vis.transform.localPosition = new Vector3(0f, size * 0.5f + 0.6f, 0f); // 캐릭터 앞쪽으로
            vis.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // 불꽃 스프라이트 90° 회전
        }

        float tickTimer = 0f;
        while (true) // 전투 종료/일시정지 시 RestartLoops의 StopAllCoroutines로 중단됨
        {
            Vector2 faceDir = FacingDir(fanRange);
            pivot.transform.position = transform.position; // 매 프레임 플레이어 추적
            pivot.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(faceDir.y, faceDir.x) * Mathf.Rad2Deg - 90f);

            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                int dmg = ScaleDamage(entry.attackPower, g5 ? 2f : 1f); // 5단계: 도트 데미지 ×2
                foreach (var mc in FindInFan(faceDir, fanRange, fanAngle))
                {
                    mc.TakeDamage(dmg, 0f, Vector2.zero);
                    TryRingOnHit(entry, mc);
                }
                tickTimer = tick;
            }
            yield return null;
        }
    }

    // 근접 비주얼: 플레이어 중심 외부 피벗으로 무기를 띄우고, 모션 타입에 따라 스윙(호 회전)/찌르기(전진·복귀).
    // 무기 스프라이트는 12시 기준 → 피벗을 (공격방향 − 90°)로 돌려 날이 공격 방향을 향하게 한다.
    // attackFrames 있으면 시트 1회 재생, 없으면 itemImage 정적 표시. (데미지는 ApplyMeleeFan이 별도 처리)
    private IEnumerator ShowMeleeFlash(SO_WeaponData wd, Vector2 dir,
        float range, float scaleMult = 1f, MeleeMotionType? motionOverride = null, float swingArc = -1f)
    {
        var       af       = ValidFrames(wd.attackFrames);   // 삭제된 프레임 방어
        bool      useIcon  = UsesShopIcon(wd.itemID);         // A그룹: 상점 정적 아이콘 사용(시트 무시)
        bool      animated = !useIcon && af.Length > 0;
        Sprite[]  frames   = animated ? af
                                      : (wd.itemImage != null ? new[] { wd.itemImage } : null);
        if (frames == null) yield break;

        float dur   = wd.meleeMotionDuration > 0f ? wd.meleeMotionDuration : 0.2f;
        float reach = wd.meleeReach          > 0f ? wd.meleeReach          : 1.6f;
        float arc   = swingArc > 0f ? swingArc : (wd.meleeSwingAngle > 0f ? wd.meleeSwingAngle : 90f);
        float fps   = animated ? af.Length / dur : 1f;

        // 무기 비주얼(스프라이트 중심이 root 원점) — 플레이어 중심 피벗에 매달아 앞쪽으로 띄운다.
        MeleeMotionType motion = motionOverride ?? wd.meleeMotion; // 콤보 등에서 모션 강제 가능
        bool baked = motion == MeleeMotionType.Baked; // 모션이 프레임에 포함 → 스윕 없이 방향만 정렬
        // 이미지 크기 = 타격범위에 일치: 표시 지름 = 2 × 타격 반경(range). (range≤0이면 기본 근접범위)
        float hitDiameter = 2f * (range > 0f ? range : _meleeRange);
        var  wpn   = BuildAnimatedGO(frames, fps, $"Melee_{wd.itemName}", hitDiameter * scaleMult, loop: false, withBody: false);

        // 채찍: 위로 떠 보여 캐릭터 우측 높이로 내림(값 조절 가능)
        Vector3 pivotOffset = wd.itemID == "WPN_004" ? new Vector3(0f, -1.5f, 0f) : Vector3.zero;
        var pivot = new GameObject($"MeleePivot_{wd.itemName}");
        pivot.transform.position = transform.position + pivotOffset;
        wpn.transform.SetParent(pivot.transform, false);
        // 무기 안쪽 끝이 캐릭터를 벗어나도록: 표시 반경(=지름/2) + 여유. 큰 무기일수록 더 멀리 띄움.
        float standoff = hitDiameter * scaleMult * 0.5f + 0.6f;
        if (wd.itemID == "WPN_004") standoff *= 0.5f; // 채찍: 캐릭터에 더 붙임
        wpn.transform.localPosition = new Vector3(0f, standoff, 0f);
        // 무기별 스프라이트 회전 보정
        float spriteRot = wd.itemID == "WPN_020" ? -90f : 0f; // 카타나: -90° 회전
        if (spriteRot != 0f) wpn.transform.localRotation = Quaternion.Euler(0f, 0f, spriteRot);
        // 도끼: 세로축 대칭(좌우 반전) — 회전 방향과 도끼날 일치
        if (wd.itemID == "WPN_005" || wd.itemID == "WPN_027") wpn.transform.localScale = new Vector3(-1f, 1f, 1f); // 도끼·할버드: 세로축 대칭
        Destroy(pivot, dur + 0.05f);

        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f; // +Y(12시)를 dir로 정렬

        float t = 0f;
        while (t < dur)
        {
            // 피벗·무기 GO가 파괴됐으면(전투 종료·씬 전환·재컴파일 등) 즉시 종료
            if (pivot == null || wpn == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            pivot.transform.position = transform.position + pivotOffset; // 플레이어 이동에 즉시 따라오게(매 프레임 동기화)
            if (motion == MeleeMotionType.Thrust)
            {
                pivot.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle);
                float lunge = Mathf.Sin(k * Mathf.PI);                 // 0→1→0 전진·복귀
                wpn.transform.localPosition = new Vector3(0f, standoff + lunge * reach, 0f);
            }
            else if (baked)
            {
                // C-1: 공격 방향으로만 고정 정렬(스윕 없음) — 프레임에 담긴 모션을 그대로 재생
                pivot.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle);
            }
            else // Swing
            {
                float off = Mathf.Lerp(-arc * 0.5f, arc * 0.5f, k);    // 호로 휘두름
                pivot.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle + off);
            }
            yield return null;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 적 탐색 유틸

    private MonsterController FindNearest(float range)
    {
        MonsterController best    = null;
        float             minDist = float.MaxValue;
        foreach (var mc in GetEnemiesInRange(range))
        {
            float d = Vector2.Distance(transform.position, mc.transform.position);
            if (d < minDist) { minDist = d; best = mc; }
        }
        return best;
    }

    private MonsterController FindFarthest(float range)
    {
        MonsterController best    = null;
        float             maxDist = -1f;
        foreach (var mc in GetEnemiesInRange(range))
        {
            float d = Vector2.Distance(transform.position, mc.transform.position);
            if (d > maxDist) { maxDist = d; best = mc; }
        }
        return best;
    }

    private List<MonsterController> FindInFan(Vector2 facing, float range, float angleDeg)
    {
        var result = new List<MonsterController>();
        foreach (var mc in GetEnemiesInRange(range))
        {
            Vector2 toEnemy = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
            if (Vector2.Angle(facing, toEnemy) <= angleDeg * 0.5f)
                result.Add(mc);
        }
        return result;
    }

    private List<MonsterController> GetEnemiesInRange(float range)
    {
        var result = new List<MonsterController>();
        var hits   = _enemyLayer == 0
            ? Physics2D.OverlapCircleAll(transform.position, range)
            : Physics2D.OverlapCircleAll(transform.position, range, _enemyLayer);

        foreach (var h in hits)
        {
            var mc = h.GetComponent<MonsterController>()
                  ?? h.GetComponentInParent<MonsterController>();
            if (mc != null && !mc.IsDead) result.Add(mc);
        }
        return result;
    }

    private Vector2 FacingDir(float range)
    {
        var nearest = FindNearest(range);
        return nearest != null
            ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
            : _lastMoveDir;
    }

    /// <summary>from에서 dir 방향 dist 안에 '벽'(비트리거 솔리드, 적/플레이어 아님)이 있는지.</summary>
    private bool HasWallBehind(Vector2 from, Vector2 dir, float dist)
    {
        foreach (var h in Physics2D.RaycastAll(from, dir, dist))
        {
            if (h.collider == null || h.collider.isTrigger) continue;       // 트리거(적/픽업 등)는 벽 아님
            if (h.collider.CompareTag("Player")) continue;
            var mc = h.collider.GetComponent<MonsterController>()
                  ?? h.collider.GetComponentInParent<MonsterController>();
            if (mc != null) continue;                                       // 적은 벽 아님
            return true;                                                     // 비트리거 솔리드 = 벽
        }
        return false;
    }

    private static Vector2 Rotate(Vector2 v, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    /// <summary>무기 기본 데미지에 캐릭터 공격 배율과 추가 배율을 곱해 최종 데미지를 반환.</summary>
    private int ScaleDamage(int baseDamage, float extraMult = 1f)
    {
        float charMul = _stats != null ? _stats.attackMultiplier : _charAtkMul;
        return Mathf.RoundToInt(baseDamage * charMul * extraMult);
    }
}
