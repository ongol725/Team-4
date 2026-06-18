using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagSurvivor.Monster;

public class SummonController : MonoBehaviour
{
    private SO_SummonData _data;
    private Transform     _player;
    private int           _attackPower;
    private LayerMask     _enemyLayer;

    private float _atkTimer;
    private float _uniqueSkillTimer;

    // ── FollowAttack FSM ─────────────────────────────────────────
    private enum SummonState { Wander, Chase }
    private SummonState  _state = SummonState.Wander;
    private MonsterController _chaseTarget;

    // 배회 전용
    private Vector2 _wanderDest;
    private float   _wanderTimer;
    private const float WanderRadius    = 5f;   // 플레이어 주변 배회 반경
    private const float WanderInterval  = 2.5f; // 새 웨이포인트 갱신 간격
    private const float DetectRange     = 8f;   // 적 감지 거리
    private const float LeashRange      = 14f;  // 이 거리 이상 벗어나면 플레이어로 복귀

    // OrbitPlayer 전용
    private float _orbitAngle;
    private const float OrbitRadius = 2.5f;

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
            var sr  = gameObject.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = x - 16f, dy = y - 16f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, dist < 14f ? Color.white : Color.clear);
            }
            tex.Apply();
            sr.sprite       = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            sr.color        = new Color(0.2f, 0.9f, 0.3f, 1f);
            sr.sortingOrder = 5;
        }

        // 초기 배회 목적지 설정
        PickNewWanderDest();
    }

    private void Update()
    {
        if (_data == null || _player == null) return;

        _atkTimer         += Time.deltaTime;
        _uniqueSkillTimer += Time.deltaTime;

        switch (_data.aiType)
        {
            case SummonAIType.FollowAttack: UpdateFollowAttack(); break;
            case SummonAIType.OrbitPlayer:  UpdateOrbitPlayer();  break;
            case SummonAIType.Stationary:   UpdateStationary();   break;
        }

        if (_data.uniqueSkill != null && _uniqueSkillTimer >= _data.uniqueSkillCooldown)
        {
            _uniqueSkillTimer = 0f;
            StartCoroutine(ExecuteSkill(_data.uniqueSkill));
        }
    }

    // ─────────────────────────────────────────────────────────────
    // FollowAttack FSM

    private void UpdateFollowAttack()
    {
        // 플레이어와 너무 멀면 즉시 복귀
        float distToPlayer = Vector2.Distance(transform.position, _player.position);
        if (distToPlayer > LeashRange)
        {
            _state = SummonState.Wander;
            PickNewWanderDest();
            MoveToward(_player.position);
            return;
        }

        switch (_state)
        {
            case SummonState.Wander:
                UpdateWander();
                break;
            case SummonState.Chase:
                UpdateChase();
                break;
        }
    }

    private void UpdateWander()
    {
        // 감지 범위 내 적 확인 → Chase 전환
        var detected = FindNearest(DetectRange);
        if (detected != null)
        {
            _chaseTarget = detected;
            _state = SummonState.Chase;
            return;
        }

        // 웨이포인트 타이머
        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f || Vector2.Distance(transform.position, _wanderDest) < 0.4f)
            PickNewWanderDest();

        MoveToward(_wanderDest);
    }

    private void UpdateChase()
    {
        // 타겟 무효화 확인
        if (_chaseTarget == null || _chaseTarget.IsDead)
        {
            _chaseTarget = null;
            _state = SummonState.Wander;
            PickNewWanderDest();
            return;
        }

        // 감지 범위 이탈 시 배회 복귀
        float distToTarget = Vector2.Distance(transform.position, _chaseTarget.transform.position);
        if (distToTarget > DetectRange * 1.5f)
        {
            _chaseTarget = null;
            _state = SummonState.Wander;
            PickNewWanderDest();
            return;
        }

        // 공격 사거리 밖이면 접근
        if (distToTarget > _data.atkRange)
            MoveToward(_chaseTarget.transform.position);

        // 공격
        if (_atkTimer >= _data.atkCooldown)
        {
            _atkTimer = 0f;
            Vector2 kb = ((Vector2)_chaseTarget.transform.position - (Vector2)transform.position).normalized;
            _chaseTarget.TakeDamage(_attackPower, 1f, kb);
        }
    }

    private void PickNewWanderDest()
    {
        _wanderTimer = WanderInterval;
        Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(WanderRadius * 0.3f, WanderRadius);
        _wanderDest = (Vector2)_player.position + offset;
    }

    // ─────────────────────────────────────────────────────────────
    // OrbitPlayer

    private void UpdateOrbitPlayer()
    {
        _orbitAngle += 90f * Time.deltaTime;
        float rad = _orbitAngle * Mathf.Deg2Rad;
        transform.position = (Vector2)_player.position
            + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * OrbitRadius;

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
    // Stationary

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
    // 유니크 스킬

    private IEnumerator ExecuteSkill(SO_SkillData skill)
    {
        int damage  = Mathf.RoundToInt(_attackPower * skill.dmgMultiplier);
        var enemies = GetEnemiesInRange(skill.rangeRadius);

        switch (skill.targetType)
        {
            case SkillTargetType.RandomEnemy:
                if (enemies.Count > 0)
                    ApplySkillHit(skill, enemies[Random.Range(0, enemies.Count)], damage);
                break;
            case SkillTargetType.AreaCenter:
            case SkillTargetType.Self:
                foreach (var mc in enemies) ApplySkillHit(skill, mc, damage);
                break;
            case SkillTargetType.Forward:
                var nearest = FindNearest(skill.rangeRadius);
                if (nearest != null) ApplySkillHit(skill, nearest, damage);
                break;
        }

        if (skill.vfxPrefab != null)
        {
            var vfx = Instantiate(skill.vfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, skill.duration > 0f ? skill.duration : 2f);
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

    private List<MonsterController> GetEnemiesInRange(float range)
    {
        var result = new List<MonsterController>();

        // 몬스터 콜라이더가 IsTrigger=true 이므로 ContactFilter2D.useTriggers 필수
        var filter = new ContactFilter2D();
        filter.useTriggers = true;
        if (_enemyLayer != 0) filter.SetLayerMask(_enemyLayer);
        else                  filter.NoFilter();

        var cols = new List<Collider2D>();
        Physics2D.OverlapCircle((Vector2)transform.position, range, filter, cols);

        foreach (var h in cols)
        {
            var mc = h.GetComponent<MonsterController>()
                  ?? h.GetComponentInParent<MonsterController>();
            if (mc != null && !mc.IsDead) result.Add(mc);
        }
        return result;
    }
}
