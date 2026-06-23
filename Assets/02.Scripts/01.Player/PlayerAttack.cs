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
    private readonly List<Coroutine> _loops = new();
    private BattleLoadout            _currentLoadout;
    private bool                     _paused;
    private bool                     _inCombatZone;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _rb    = GetComponent<Rigidbody2D>();
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
        float  charSpeedMul = _stats != null ? _stats.attackSpeedMultiplier : 1f;

        // 5단계 공격 속도 보정
        float spdBoost = 1f;
        if (g5 && id == "WPN_009") spdBoost = 1f / 0.85f; // 활: 쿨타임 -15%
        if (g5 && id == "WPN_020") spdBoost = 1.3f;        // 카타나: 공속 +30%

        // 최종 쿨타임 = 무기 쿨타임 / 캐릭터 공속 배율
        float interval = 1f / (baseAps * weaponAps * spdBoost * charSpeedMul);
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

        switch (entry.data.attackStyleType)
        {
            // ── SingleTarget ───────────────────────────────────────
            case WeaponAttackStyleType.SingleTarget:
            {
                int   shots      = 1;
                float dmgMult    = 1f;
                float spdMult    = 1f;
                float scaleMult  = 1f;
                int   pierce     = 0;
                int   targetCnt  = 1;

                if (g5)
                {
                    switch (id)
                    {
                        case "WPN_001": shots     = 2;     break; // 단검: 2발 투척
                        case "WPN_006": pierce    = 99;    break; // 쇠뇌: 무제한 관통
                        case "WPN_007": dmgMult   = 1.3f;  break; // 권총: 데미지 +30%
                        case "WPN_010": spdMult   = 1.5f;  break; // 부메랑: 속도 증가
                        case "WPN_011": targetCnt = 2;     break; // 지팡이: 적 2명
                        case "WPN_012": scaleMult = 1.5f;  break; // 마도서: 크기 1.5배
                        case "WPN_013": targetCnt = 2;     break; // 번개구슬: 적 2명
                        case "WPN_016": scaleMult = 1.5f; pierce = 1; break; // 수리검: 크기 + 관통
                    }
                }

                if (targetCnt > 1)
                    AttackMultiTarget(entry, range, targetCnt, dmgMult, spdMult, scaleMult, pierce);
                else
                    AttackSingleTarget(entry, range, shots, dmgMult, spdMult, scaleMult, pierce);
                break;
            }

            // ── MeleeFan ───────────────────────────────────────────
            case WeaponAttackStyleType.MeleeFan:
            {
                // 무기별 기본 각도
                float angle = id switch
                {
                    "WPN_002" => 90f,
                    "WPN_004" => 90f,
                    "WPN_019" => 100f,
                    "WPN_020" => 120f,
                    "WPN_023" => 60f,  // 메이스: 좁은 부채꼴
                    "WPN_027" => 120f, // 할버드: 넓은 부채꼴
                    "WPN_029" => 360f, // 워해머: 전방위
                    "WPN_030" => 180f, // 사이드: 반원
                    _         => 90f,
                };
                float kb         = _meleeKnockback;
                float flashScale = 1f;

                if (g5)
                {
                    switch (id)
                    {
                        case "WPN_002": angle      = 135f;  break;            // 장검
                        case "WPN_019": angle      = 180f; kb *= 2f; break;  // 대검
                        case "WPN_020": flashScale = 1.5f;  break;            // 카타나: 이펙트 크기
                        case "WPN_024": kb        *= 2f;    break;            // 몽둥이: 넉백 2배
                        case "WPN_029": range     *= 1.5f;  break;            // 워해머: 범위 1.5배
                        case "WPN_030": angle      = 360f;  break;            // 사이드: 전방위
                    }
                }

                // 채찍: 오른쪽 → 왼쪽 순차 공격 (5단계: 반격 추가)
                if (id == "WPN_004")
                {
                    AttackMeleeFanDir(entry, range, angle, kb, flashScale, Vector2.right);
                    if (g5)
                        StartCoroutine(DelayedFanDir(entry, range, angle, kb, flashScale, Vector2.left, 0.25f));
                    break;
                }

                // 할버드 5단계: 부채꼴 + 찌르기 검기 방출
                if (g5 && id == "WPN_027")
                {
                    AttackMeleeFan(entry, range, angle, kb, flashScale);
                    AttackPierceLine(entry, range, 99);
                    break;
                }

                AttackMeleeFan(entry, range, angle, kb, flashScale);
                break;
            }

            // ── MeleeSingle ────────────────────────────────────────
            case WeaponAttackStyleType.MeleeSingle:
            {
                bool splash = false;
                int  burst  = 1;

                if (g5)
                {
                    switch (id)
                    {
                        case "WPN_003": range  += 5f;  break; // 철퇴: 사거리 +5
                        case "WPN_005": splash  = true; break; // 도끼: 스플래시
                        case "WPN_025": burst   = 2;    break; // 너클: 연타 2배
                    }
                }

                if (burst > 1)
                    StartCoroutine(AttackMeleeBurst(entry, range, burst));
                else
                    AttackMeleeSingle(entry, range, splash);
                break;
            }

            // ── SpreadShot ─────────────────────────────────────────
            case WeaponAttackStyleType.SpreadShot:
            {
                float spreadAngle = 55f;
                int   bullets     = 3;
                if (g5 && id == "WPN_008") { spreadAngle = 60f; bullets = 4; }
                AttackSpread(entry, range, spreadAngle, bullets);
                break;
            }

            // ── BurstFire ──────────────────────────────────────────
            case WeaponAttackStyleType.BurstFire:
            {
                int   count     = id == "WPN_018" ? 5 : 3; // 레일건 기본 5발, 라이플 기본 3발
                float kbPerShot = id == "WPN_015" ? 0.3f : 0f; // 라이플: 미세 넉백
                if (g5)
                {
                    if (id == "WPN_015") count = 5;
                    if (id == "WPN_018") count = 8;
                }
                StartCoroutine(AttackBurst(entry, range, count, kbPerShot));
                break;
            }

            // ── PierceLine ─────────────────────────────────────────
            case WeaponAttackStyleType.PierceLine:
            {
                float lineRange = g5 && id == "WPN_021" ? range * 1.5f : range;
                AttackPierceLine(entry, lineRange, 99); // 스피어: 기본 무제한 관통
                break;
            }

            // ── Sniper ─────────────────────────────────────────────
            case WeaponAttackStyleType.Sniper:
            {
                int pierce = g5 && id == "WPN_028" ? 3 : 0; // 장궁 5단계: 관통 3명
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

    private void AttackSingleTarget(WeaponLoadoutEntry entry, float range,
        int shots = 1, float dmgMult = 1f, float spdMult = 1f,
        float scaleMult = 1f, int pierce = 0)
    {
        var    nearest = FindNearest(range);
        Vector2 center = nearest != null
            ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
            : _lastMoveDir;

        if (shots <= 1)
        {
            SpawnProjectile(entry, center, dmgMult, spdMult, scaleMult, pierce);
        }
        else
        {
            float spread = 15f;
            float step   = shots > 1 ? spread * 2f / (shots - 1) : 0f;
            for (int i = 0; i < shots; i++)
                SpawnProjectile(entry, Rotate(center, -spread + step * i), dmgMult, spdMult, scaleMult, pierce);
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
        float angleDeg, float knockback, float flashScale = 1f)
    {
        ApplyMeleeFan(entry, FacingDir(range), range, angleDeg, knockback, flashScale);
    }

    private void AttackMeleeFanDir(WeaponLoadoutEntry entry, float range,
        float angleDeg, float knockback, float flashScale, Vector2 fixedDir)
    {
        ApplyMeleeFan(entry, fixedDir.normalized, range, angleDeg, knockback, flashScale);
    }

    private void ApplyMeleeFan(WeaponLoadoutEntry entry, Vector2 facing,
        float range, float angleDeg, float knockback, float flashScale)
    {
        var enemies = angleDeg >= 360f
            ? GetEnemiesInRange(range)
            : FindInFan(facing, range, angleDeg);

        int meleeDmg = ScaleDamage(entry.attackPower);
        foreach (var mc in enemies)
        {
            Vector2 kbDir = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
            mc.TakeDamage(meleeDmg, knockback, kbDir);
        }
        StartCoroutine(ShowMeleeFlash(entry.data, facing, range, flashScale));
    }

    private IEnumerator DelayedFanDir(WeaponLoadoutEntry entry, float range,
        float angleDeg, float knockback, float flashScale, Vector2 dir, float delay)
    {
        yield return new WaitForSeconds(delay);
        AttackMeleeFanDir(entry, range, angleDeg, knockback, flashScale, dir);
    }

    private void AttackMeleeSingle(WeaponLoadoutEntry entry, float range, bool splash = false)
    {
        var    target = FindNearest(range);
        Vector2 dir   = target != null
            ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized
            : _lastMoveDir;

        if (target != null)
        {
            target.TakeDamage(ScaleDamage(entry.attackPower), _meleeKnockback, dir);

            if (splash)
            {
                foreach (var mc in GetEnemiesInRange(range))
                {
                    if (mc == target) continue;
                    Vector2 kbDir = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
                    mc.TakeDamage(ScaleDamage(entry.attackPower, 0.5f), _meleeKnockback * 0.5f, kbDir);
                }
            }
        }
        StartCoroutine(ShowMeleeFlash(entry.data, dir, range));
    }

    private static readonly WaitForSeconds _waitMeleeBurst = new(0.12f);
    private IEnumerator AttackMeleeBurst(WeaponLoadoutEntry entry, float range, int count)
    {
        for (int i = 0; i < count; i++)
        {
            AttackMeleeSingle(entry, range);
            yield return _waitMeleeBurst;
        }
    }

    private void AttackSpread(WeaponLoadoutEntry entry, float range,
        float totalAngle, int bulletCount)
    {
        var nearest = FindNearest(range);
        Vector2 center = nearest != null
            ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
            : _lastMoveDir;

        float start = -totalAngle * 0.5f;
        float step  = bulletCount > 1 ? totalAngle / (bulletCount - 1) : 0f;
        for (int i = 0; i < bulletCount; i++)
            SpawnProjectile(entry, Rotate(center, start + step * i));
    }

    private static readonly WaitForSeconds _waitBurst = new(0.1f);
    private IEnumerator AttackBurst(WeaponLoadoutEntry entry, float range,
        int count, float kbPerShot = 0f)
    {
        var nearest = FindNearest(range);
        Vector2 dir = nearest != null
            ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
            : _lastMoveDir;

        for (int i = 0; i < count; i++)
        {
            SpawnProjectile(entry, dir, knockbackForce: kbPerShot);
            yield return _waitBurst;
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

    private void SpawnProjectile(WeaponLoadoutEntry entry, Vector2 dir,
        float dmgMult = 1f, float spdMult = 1f, float scaleMult = 1f,
        int pierce = 0, float knockbackForce = 0f)
    {
        var   wd       = entry.data;
        float rawSpeed = wd.projectileSpeed > 0f ? wd.projectileSpeed : 10f;
        float speed    = rawSpeed * spdMult;
        float lifetime = wd.range > 0 ? (float)wd.range / rawSpeed : 3f;
        int   damage   = ScaleDamage(entry.attackPower, dmgMult);
        int   maxHits  = wd.maxTargets > 0 ? wd.maxTargets : 1; // 0 = 기본 1타
        if (wd.projectileData != null) maxHits += wd.projectileData.pierceCount;
        maxHits += pierce;

        GameObject go = wd.projectile != null
            ? Instantiate(wd.projectile, transform.position, Quaternion.identity)
            : BuildTempGO(wd.itemImage, wd.itemName);

        go.transform.position = transform.position; // BuildTempGO는 위치를 설정하지 않으므로 항상 보정

        if (scaleMult != 1f)
            go.transform.localScale *= scaleMult;

        var proj = go.GetComponent<ProjectileBase>() ?? go.AddComponent<ProjectileBase>();
        proj.Init(dir, damage, speed, lifetime, maxHits, knockbackForce);
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

    private IEnumerator ShowMeleeFlash(SO_WeaponData wd, Vector2 dir,
        float range, float scaleMult = 1f)
    {
        var go      = new GameObject($"Melee_{wd.itemName}");
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        go.transform.position = (Vector2)transform.position + dir * (range * 0.6f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var sr          = go.AddComponent<SpriteRenderer>();
        sr.sprite       = wd.itemImage;
        sr.sortingOrder = 10;
        go.transform.localScale = Vector3.one * range * 0.8f * scaleMult;

        yield return new WaitForSeconds(0.15f);
        if (go != null) Destroy(go);
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

    private static Vector2 Rotate(Vector2 v, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    /// <summary>무기 기본 데미지에 캐릭터 공격 배율과 추가 배율을 곱해 최종 데미지를 반환.</summary>
    private int ScaleDamage(int baseDamage, float extraMult = 1f)
    {
        float charMul = _stats != null ? _stats.attackMultiplier : 1f;
        return Mathf.RoundToInt(baseDamage * charMul * extraMult);
    }
}
