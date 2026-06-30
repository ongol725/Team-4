using UnityEngine;
using BagSurvivor.Monster;

[RequireComponent(typeof(Rigidbody2D))]
public class ProjectileBase : MonoBehaviour
{
    private int   _damage;
    private int   _remainingHits = 1;
    private float _knockbackForce;
    private float _explosionRadius;
    private bool  _exploded;

    // 적 타격 시 추가 효과(시너지 Burn/즉사 등)를 적용하는 선택적 콜백. 기본 null.
    private System.Action<MonsterController> _onHit;

    /// <summary>적에게 명중할 때마다 호출될 콜백을 설정한다(시너지 고정효과 전달용).</summary>
    public void SetOnHit(System.Action<MonsterController> onHit) => _onHit = onHit;

    private bool  _homing;
    private float _speed;

    public void Init(Vector2 dir, int damage, float speed, float lifetime, int maxHits,
        float knockbackForce = 0f, float explosionRadius = 0f, bool homing = false)
    {
        _damage          = damage;
        _remainingHits   = Mathf.Max(1, maxHits);
        _knockbackForce  = knockbackForce;
        _explosionRadius = explosionRadius;
        _homing          = homing;
        _speed           = speed;

        var rb = GetComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.freezeRotation = false;
        rb.linearVelocity = dir.normalized * speed;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, lifetime);
    }

    // 유도(지팡이): 매 물리프레임 가장 가까운 적 방향으로 속도를 서서히 꺾는다.
    private void FixedUpdate()
    {
        if (!_homing) return;
        var rb = GetComponent<Rigidbody2D>();

        MonsterController best = null; float bd = float.MaxValue;
        foreach (var h in Physics2D.OverlapCircleAll(transform.position, 12f))
        {
            var mc = h.GetComponent<MonsterController>() ?? h.GetComponentInParent<MonsterController>();
            if (mc == null || mc.IsDead) continue;
            float d = Vector2.Distance(transform.position, mc.transform.position);
            if (d < bd) { bd = d; best = mc; }
        }
        if (best == null) return;

        Vector2 desired = ((Vector2)best.transform.position - (Vector2)transform.position).normalized * _speed;
        Vector2 v = Vector2.Lerp(rb.linearVelocity, desired, 6f * Time.fixedDeltaTime);
        rb.linearVelocity = v;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
    }

    private void OnDestroy()
    {
        // lifetime 종료 시에도 폭발 범위 피해 적용
        if (_explosionRadius > 0f && !_exploded && Application.isPlaying)
            Explode();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) return;

        if (_explosionRadius > 0f)
        {
            Explode();
            Destroy(gameObject);
            return;
        }

        var mc = other.GetComponent<MonsterController>()
              ?? other.GetComponentInParent<MonsterController>();
        if (mc == null)
        {
            // 몬스터가 아닌 단단한 충돌체(벽 등 비트리거)에 맞으면 투사체 소멸.
            // 몬스터·픽업·이펙트 등은 트리거 콜라이더이므로 통과한다.
            if (!other.isTrigger) Destroy(gameObject);
            return;
        }
        if (mc.IsDead) return;

        Vector2 kbDir = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
        mc.TakeDamage(_damage, _knockbackForce, kbDir);
        _onHit?.Invoke(mc);

        if (--_remainingHits <= 0)
            Destroy(gameObject);
    }

    private void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        var hits = Physics2D.OverlapCircleAll(transform.position, _explosionRadius);
        foreach (var h in hits)
        {
            var mc = h.GetComponent<MonsterController>()
                  ?? h.GetComponentInParent<MonsterController>();
            if (mc == null || mc.IsDead) continue;
            Vector2 kbDir = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
            mc.TakeDamage(_damage, _knockbackForce, kbDir);
            _onHit?.Invoke(mc);
        }
    }
}
