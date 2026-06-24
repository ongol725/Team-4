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

    public void Init(Vector2 dir, int damage, float speed, float lifetime, int maxHits,
        float knockbackForce = 0f, float explosionRadius = 0f)
    {
        _damage          = damage;
        _remainingHits   = Mathf.Max(1, maxHits);
        _knockbackForce  = knockbackForce;
        _explosionRadius = explosionRadius;

        var rb = GetComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.freezeRotation = false;
        rb.linearVelocity = dir.normalized * speed;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, lifetime);
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
        if (mc == null || mc.IsDead) return;

        Vector2 kbDir = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
        mc.TakeDamage(_damage, _knockbackForce, kbDir);

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
        }
    }
}
