using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagSurvivor.Monster;

/// <summary>
/// BattleLoadout의 무기 목록을 읽어 자동 공격을 처리한다.
/// 씬 전환 후에도 GameManager.CurrentLoadout을 즉시 반영한다.
///
/// 인스펙터 설정:
///   EnemyLayer — 몬스터가 속한 레이어 마스크 (미설정 시 전체 검색 폴백)
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("레이어 설정")]
    [SerializeField] private LayerMask _enemyLayer;

    [Header("근접 기본값")]
    [Tooltip("SO의 range=0인 근접 무기에 적용되는 범위(유닛)")]
    [SerializeField] private float _meleeRange = 1.5f;
    [Tooltip("근접 공격 시 넉백 거리")]
    [SerializeField] private float _meleeKnockback = 2f;

    private PlayerStats            _stats;
    private GameManager            _gm;
    private Vector2                _lastMoveDir = Vector2.right;
    private readonly List<Coroutine> _loops = new();

    // ─────────────────────────────────────────────────────────────

    private void Awake()  => _stats = GetComponent<PlayerStats>();

    private void Start()
    {
        _gm = GameManager.Instance;
        if (_gm != null)
        {
            _gm.onLoadoutReady += OnLoadoutReady;
            if (_gm.CurrentLoadout != null)
                OnLoadoutReady(_gm.CurrentLoadout);
        }
    }

    private void OnDestroy()
    {
        if (_gm != null) _gm.onLoadoutReady -= OnLoadoutReady;
    }

    private void Update()
    {
        // Rigidbody2D 속도에서 마지막 이동 방향 추적 (근접 기본 방향용)
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
            _lastMoveDir = rb.linearVelocity.normalized;
    }

    // ─────────────────────────────────────────────────────────────

    private void OnLoadoutReady(BattleLoadout loadout)
    {
        foreach (var co in _loops) if (co != null) StopCoroutine(co);
        _loops.Clear();
        foreach (var weapon in loadout.Weapons)
            _loops.Add(StartCoroutine(AttackLoop(weapon)));
    }

    private IEnumerator AttackLoop(WeaponLoadoutEntry entry)
    {
        float baseAps  = _stats != null ? _stats.attackSpeed : 1f;
        float weaponAps = entry.attackSpeed > 0f ? entry.attackSpeed : 1f;
        float interval  = 1f / (baseAps * weaponAps);

        while (true)
        {
            yield return new WaitForSeconds(interval);
            TryAttack(entry);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 공격 디스패치

    private void TryAttack(WeaponLoadoutEntry entry)
    {
        float range = entry.data.range > 0 ? entry.data.range : _meleeRange;

        switch (entry.data.attackStyleType)
        {
            case WeaponAttackStyleType.SingleTarget:
                AttackSingleTarget(entry, range);
                break;
            case WeaponAttackStyleType.MeleeFan:
                AttackMeleeFan(entry, range, 90f);
                break;
            case WeaponAttackStyleType.MeleeSingle:
                AttackMeleeSingle(entry, range);
                break;
            case WeaponAttackStyleType.SpreadShot:
                AttackSpread(entry, range, 55f, 3);
                break;
            default:
                // 미설정·미구현 스타일은 SingleTarget 폴백
                AttackSingleTarget(entry, range);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 구현된 공격 스타일 4종

    /// <summary>SingleTarget: 범위 내 가장 가까운 적 1인에게 투사체 발사</summary>
    private void AttackSingleTarget(WeaponLoadoutEntry entry, float range)
    {
        var target = FindNearest(range);
        if (target == null) return;
        Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        SpawnProjectile(entry, dir);
    }

    /// <summary>MeleeFan: 보는 방향 부채꼴 범위 내 모든 적에게 즉시 데미지</summary>
    private void AttackMeleeFan(WeaponLoadoutEntry entry, float range, float angleDeg)
    {
        Vector2 facing  = FacingDir(range);
        var     enemies = FindInFan(facing, range, angleDeg);
        foreach (var mc in enemies)
        {
            Vector2 kbDir = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
            mc.TakeDamage(entry.attackPower, _meleeKnockback, kbDir);
        }
        StartCoroutine(ShowMeleeFlash(entry.data, facing, range));
    }

    /// <summary>MeleeSingle: 보는 방향 단일 근접 타격</summary>
    private void AttackMeleeSingle(WeaponLoadoutEntry entry, float range)
    {
        var target = FindNearest(range);
        if (target == null) return;
        Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        target.TakeDamage(entry.attackPower, _meleeKnockback, dir);
        StartCoroutine(ShowMeleeFlash(entry.data, dir, range));
    }

    /// <summary>SpreadShot: 중앙 방향 기준으로 totalAngle 각도 범위에 bulletCount발 발사</summary>
    private void AttackSpread(WeaponLoadoutEntry entry, float range, float totalAngle, int bulletCount)
    {
        var    nearest = FindNearest(range);
        Vector2 center = nearest != null
            ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
            : _lastMoveDir;

        float startAngle = -totalAngle * 0.5f;
        float step       = bulletCount > 1 ? totalAngle / (bulletCount - 1) : 0f;
        for (int i = 0; i < bulletCount; i++)
            SpawnProjectile(entry, Rotate(center, startAngle + step * i));
    }

    // ─────────────────────────────────────────────────────────────
    // 투사체 생성

    private void SpawnProjectile(WeaponLoadoutEntry entry, Vector2 dir)
    {
        var   wd       = entry.data;
        float speed    = wd.projectileSpeed > 0f ? wd.projectileSpeed : 10f;
        float lifetime = wd.range > 0 ? (float)wd.range / speed : 3f;
        int   maxHits  = wd.maxTargets <= 0 ? 99 : wd.maxTargets;
        if (wd.projectileData != null) maxHits += wd.projectileData.pierceCount;

        GameObject go = wd.projectile != null
            ? Instantiate(wd.projectile, transform.position, Quaternion.identity)
            : BuildTempGO(wd.itemImage, wd.itemName);

        var proj = go.GetComponent<ProjectileBase>() ?? go.AddComponent<ProjectileBase>();
        proj.Init(dir, entry.attackPower, speed, lifetime, maxHits);
    }

    // 투사체 프리팹 없을 때 무기 아이콘으로 임시 투사체 생성
    private static GameObject BuildTempGO(Sprite icon, string weaponName)
    {
        var go = new GameObject($"Proj_{weaponName}");

        var sr        = go.AddComponent<SpriteRenderer>();
        sr.sprite     = icon;
        sr.sortingOrder = 10;
        go.transform.localScale = Vector3.one * 0.4f;

        var rb        = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        var col       = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.3f;

        return go;
    }

    // 근접 공격 임시 비주얼 (무기 아이콘 0.15초 표시)
    private IEnumerator ShowMeleeFlash(SO_WeaponData wd, Vector2 dir, float range)
    {
        var go       = new GameObject($"Melee_{wd.itemName}");
        float angle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        go.transform.position = (Vector2)transform.position + dir * (range * 0.6f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var sr          = go.AddComponent<SpriteRenderer>();
        sr.sprite       = wd.itemImage;
        sr.sortingOrder = 10;
        go.transform.localScale = Vector3.one * range * 0.8f;

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
}
