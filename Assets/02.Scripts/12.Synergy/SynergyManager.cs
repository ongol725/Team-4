using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagSurvivor.Monster;
using BagSurvivor.UI;

/// <summary>
/// 활성 시너지 목록(BattleLoadout.ActiveSynergies)을 받아
/// 스킬형/소환형 시너지를 실행하는 메인 관리자.
///
/// 사용법:
///   씬의 아무 오브젝트에 부착.
///   Inspector에서 _skillBindings, _summonBindings 배열에
///   (SynergyType + SynergyGrade) → SO_SkillData / SO_SummonData 를 매핑한다.
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
        public SynergyType  synergyType;
        public SynergyGrade grade;
        public SO_SummonData summon;
        public int          count = 1;
    }

    [Header("스킬형 시너지 바인딩")]
    [SerializeField] private SynergySkillBinding[]  _skillBindings;

    [Header("소환형 시너지 바인딩")]
    [SerializeField] private SynergySummonBinding[] _summonBindings;

    [Header("적 레이어")]
    [SerializeField] private LayerMask _enemyLayer;

    // ── 런타임 상태 ───────────────────────────────────────────────

    private GameManager               _gm;
    private Transform                 _player;
    private BattleLoadout             _loadout;
    private readonly List<Coroutine>  _skillRoutines = new();
    private readonly List<SummonController> _summons = new();
    private Transform                 _summonRoot;

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _gm = GameManager.Instance;
        if (_gm != null)
        {
            _gm.onLoadoutReady += OnLoadoutReady;
            if (_gm.CurrentLoadout != null) OnLoadoutReady(_gm.CurrentLoadout);
        }

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null) _player = playerGO.transform;

        _summonRoot = new GameObject("SummonRoot").transform;
        _summonRoot.SetParent(transform);
    }

    private void OnDestroy()
    {
        if (_gm != null) _gm.onLoadoutReady -= OnLoadoutReady;
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
        // 스킬 루프 중단
        foreach (var co in _skillRoutines) if (co != null) StopCoroutine(co);
        _skillRoutines.Clear();

        // 소환수 전부 제거
        foreach (var s in _summons) if (s != null) Destroy(s.gameObject);
        _summons.Clear();

        if (_loadout == null) return;

        int totalWeaponAtk = 0;
        foreach (var w in _loadout.Weapons) totalWeaponAtk += w.attackPower;

        foreach (var entry in _loadout.ActiveSynergies)
        {
            // 스킬형
            var skillBinding = FindSkillBinding(entry.type, entry.grade);
            if (skillBinding != null)
            {
                var co = StartCoroutine(SkillLoop(skillBinding.skill, totalWeaponAtk));
                _skillRoutines.Add(co);
                Debug.Log($"[SynergyManager] 스킬 루프 시작: {entry.type} {entry.grade} — {skillBinding.skill.skillName}");
            }

            // 소환형
            var summonBinding = FindSummonBinding(entry.type, entry.grade);
            if (summonBinding != null)
            {
                SpawnMinions(summonBinding.summon, summonBinding.count, totalWeaponAtk);
                Debug.Log($"[SynergyManager] 소환수 스폰: {entry.type} {entry.grade} — {summonBinding.summon.summonName} x{summonBinding.count}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 스킬 루프

    private IEnumerator SkillLoop(SO_SkillData skill, int weaponAtk)
    {
        int damage = Mathf.RoundToInt(weaponAtk * skill.dmgMultiplier);
        var wait   = new WaitForSeconds(skill.cooldown);

        while (true)
        {
            yield return wait;
            ExecuteSkill(skill, damage);
        }
    }

    private void ExecuteSkill(SO_SkillData skill, int damage)
    {
        if (_player == null) return;

        var enemies = GetEnemiesInRange(_player.position, skill.rangeRadius);
        Vector3 vfxPos = _player.position;

        switch (skill.targetType)
        {
            case SkillTargetType.RandomEnemy:
                if (enemies.Count > 0)
                {
                    var target = enemies[Random.Range(0, enemies.Count)];
                    vfxPos = target.transform.position;
                    HitEnemy(skill, target, damage);
                }
                break;

            case SkillTargetType.AreaCenter:
            case SkillTargetType.Self:
                foreach (var mc in enemies) HitEnemy(skill, mc, damage);
                // AoE는 플레이어 중심에 표시
                break;

            case SkillTargetType.Forward:
                MonsterController nearest = null;
                float minD = float.MaxValue;
                foreach (var mc in enemies)
                {
                    float d = Vector2.Distance(_player.position, mc.transform.position);
                    if (d < minD) { minD = d; nearest = mc; }
                }
                if (nearest != null)
                {
                    vfxPos = nearest.transform.position;
                    HitEnemy(skill, nearest, damage);
                }
                break;
        }

        SpawnVFX(skill, vfxPos);
    }

    private void HitEnemy(SO_SkillData skill, MonsterController mc, int damage)
    {
        Vector2 kb = ((Vector2)mc.transform.position - (Vector2)_player.position).normalized;
        float kbForce = skill.statusEffect == StatusEffectType.Knockback ? skill.effectValue : 0f;
        mc.TakeDamage(damage, kbForce, kb);
    }

    private void SpawnVFX(SO_SkillData skill, Vector3 pos)
    {
        if (skill.vfxPrefab != null)
        {
            var vfx = Instantiate(skill.vfxPrefab, pos, Quaternion.identity);
            float lifetime = skill.duration > 0f ? skill.duration : 2f;
            Destroy(vfx, lifetime);
            return;
        }

        // 테스트용: VFX 없을 때 색상 원으로 대체
        StartCoroutine(PlaceholderVFX(pos, skill.rangeRadius));
    }

    private IEnumerator PlaceholderVFX(Vector3 pos, float radius)
    {
        var go = new GameObject("VFX_Placeholder");
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        var tex = new Texture2D(64, 64);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float dx = x - 32f, dy = y - 32f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = dist < 28f ? 1f : (dist < 32f ? (32f - dist) / 4f : 0f);
            tex.SetPixel(x, y, new Color(1f, 0.9f, 0.1f, alpha));
        }
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
        sr.sortingOrder = 10;
        float scale = Mathf.Max(0.5f, radius * 0.08f);
        go.transform.localScale = Vector3.one * scale;

        float elapsed = 0f;
        while (elapsed < 0.4f)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(0.8f, 0f, elapsed / 0.4f);
            sr.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        Destroy(go);
    }

    // ─────────────────────────────────────────────────────────────
    // 소환형

    private void SpawnMinions(SO_SummonData data, int count, int weaponAtk)
    {
        int minionAtk = Mathf.RoundToInt(weaponAtk * data.atkMultiplier);

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * 1.5f;
            Vector3 pos    = _player != null
                ? _player.position + (Vector3)offset
                : (Vector3)offset;

            var go = new GameObject($"Summon_{data.summonID}_{i}");
            go.transform.SetParent(_summonRoot);
            go.transform.position = pos;

            var sc = go.AddComponent<SummonController>();
            sc.Init(data, _player, minionAtk, _enemyLayer);
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
        var hits = _enemyLayer == 0
            ? Physics2D.OverlapCircleAll(center, range)
            : Physics2D.OverlapCircleAll(center, range, _enemyLayer);
        foreach (var h in hits)
        {
            var mc = h.GetComponent<MonsterController>()
                  ?? h.GetComponentInParent<MonsterController>();
            if (mc != null && !mc.IsDead) result.Add(mc);
        }
        return result;
    }
}
