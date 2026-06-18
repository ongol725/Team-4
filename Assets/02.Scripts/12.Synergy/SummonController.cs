using System.Collections;
using UnityEngine;
using BagSurvivor.Monster;

/// <summary>
/// 소환형 시너지로 생성된 소환수의 AI 동작.
/// MinionSynergyRunner가 Init()을 호출해 데이터를 주입한다.
/// </summary>
public class SummonController : MonoBehaviour
{
    private SO_SummonData _data;
    private Transform     _player;
    private int           _attackPower;
    private LayerMask     _enemyLayer;

    private float _atkTimer;
    private float _uniqueSkillTimer;

    // OrbitPlayer 전용
    private float _orbitAngle;
    private float _orbitRadius = 2.5f;

    // ─────────────────────────────────────────────────────────────

    public void Init(SO_SummonData data, Transform player, int attackPower, LayerMask enemyLayer)
    {
        _data        = data;
        _player      = player;
        _attackPower = attackPower;
        _enemyLayer  = enemyLayer;

        _atkTimer         = 0f;
        _uniqueSkillTimer = 0f;

        if (data.modelPrefab != null)
        {
            var model = Instantiate(data.modelPrefab, transform);
            model.transform.localPosition = Vector3.zero;
        }
        else if (GetComponentInChildren<SpriteRenderer>() == null)
        {
            // 테스트용 플레이스홀더: 초록 원
            var sr = gameObject.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = x - 16f, dy = y - 16f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, dist < 14f ? Color.white : Color.clear);
            }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            sr.color = new Color(0.2f, 0.9f, 0.3f, 1f);
            sr.sortingOrder = 5;
        }
    }

    private void Update()
    {
        if (_data == null || _player == null) return;

        _atkTimer         += Time.deltaTime;
        _uniqueSkillTimer += Time.deltaTime;

        switch (_data.aiType)
        {
            case SummonAIType.FollowAttack:  UpdateFollowAttack();  break;
            case SummonAIType.OrbitPlayer:   UpdateOrbitPlayer();   break;
            case SummonAIType.Stationary:    UpdateStationary();    break;
        }

        if (_data.uniqueSkill != null && _uniqueSkillTimer >= _data.uniqueSkillCooldown)
        {
            _uniqueSkillTimer = 0f;
            StartCoroutine(ExecuteSkill(_data.uniqueSkill));
        }
    }

    // ─────────────────────────────────────────────────────────────
    // AI 패턴

    private void UpdateFollowAttack()
    {
        var target = FindNearest(_data.atkRange * 3f);

        // 이동: 적이 있으면 적 방향, 없으면 플레이어 방향
        Vector2 dest = target != null
            ? (Vector2)target.transform.position
            : (Vector2)_player.position;

        MoveToward(dest);

        // 공격
        if (_atkTimer >= _data.atkCooldown)
        {
            var atkTarget = FindNearest(_data.atkRange);
            if (atkTarget != null)
            {
                _atkTimer = 0f;
                Vector2 kb = ((Vector2)atkTarget.transform.position - (Vector2)transform.position).normalized;
                atkTarget.TakeDamage(_attackPower, 1f, kb);
            }
        }
    }

    private void UpdateOrbitPlayer()
    {
        // 플레이어 주변 회전
        _orbitAngle += 90f * Time.deltaTime;
        float rad = _orbitAngle * Mathf.Deg2Rad;
        transform.position = (Vector2)_player.position
            + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * _orbitRadius;

        // 사거리 내 적 공격
        if (_atkTimer >= _data.atkCooldown)
        {
            var target = FindNearest(_data.atkRange);
            if (target != null)
            {
                _atkTimer = 0f;
                Vector2 kb = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                target.TakeDamage(_attackPower, 1f, kb);
            }
        }
    }

    private void UpdateStationary()
    {
        if (_atkTimer >= _data.atkCooldown)
        {
            var target = FindNearest(_data.atkRange);
            if (target != null)
            {
                _atkTimer = 0f;
                Vector2 kb = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                target.TakeDamage(_attackPower, 1f, kb);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 스킬 실행 (uniqueSkill)

    private IEnumerator ExecuteSkill(SO_SkillData skill)
    {
        int damage = Mathf.RoundToInt(_attackPower * skill.dmgMultiplier);
        var enemies = GetEnemiesInRange(skill.rangeRadius);

        switch (skill.targetType)
        {
            case SkillTargetType.RandomEnemy:
                if (enemies.Count > 0)
                {
                    var rnd = enemies[Random.Range(0, enemies.Count)];
                    ApplySkillHit(skill, rnd, damage);
                }
                break;

            case SkillTargetType.AreaCenter:
            case SkillTargetType.Self:
                foreach (var mc in enemies)
                    ApplySkillHit(skill, mc, damage);
                break;

            case SkillTargetType.Forward:
                var nearest = FindNearest(skill.rangeRadius);
                if (nearest != null) ApplySkillHit(skill, nearest, damage);
                break;
        }

        if (skill.vfxPrefab != null)
        {
            var vfx = Instantiate(skill.vfxPrefab, transform.position, Quaternion.identity);
            if (skill.duration > 0f) Destroy(vfx, skill.duration);
            else                     Destroy(vfx, 2f);
        }

        yield return null;
    }

    private void ApplySkillHit(SO_SkillData skill, MonsterController mc, int damage)
    {
        Vector2 kb = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
        float kbForce = skill.statusEffect == StatusEffectType.Knockback ? skill.effectValue : 0f;
        mc.TakeDamage(damage, kbForce, kb);
    }

    // ─────────────────────────────────────────────────────────────
    // 유틸

    private void MoveToward(Vector2 dest)
    {
        float dist = Vector2.Distance(transform.position, dest);
        if (dist < 0.3f) return;
        transform.position = Vector2.MoveTowards(
            transform.position, dest, _data.moveSpeed * Time.deltaTime);
    }

    private MonsterController FindNearest(float range)
    {
        MonsterController best = null;
        float minDist = float.MaxValue;
        foreach (var mc in GetEnemiesInRange(range))
        {
            float d = Vector2.Distance(transform.position, mc.transform.position);
            if (d < minDist) { minDist = d; best = mc; }
        }
        return best;
    }

    private System.Collections.Generic.List<MonsterController> GetEnemiesInRange(float range)
    {
        var result = new System.Collections.Generic.List<MonsterController>();
        var hits = _enemyLayer == 0
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
}
