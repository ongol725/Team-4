// ============================================================
// BossProjectile.cs
// 보스 패턴용 투사체 (풀링 대응)
//  - GameObjectPool로 스폰 후 Launch(dir, speed, damage)로 발사 정보 주입
//  - 직선 이동, 플레이어 적중 시 피해 + 풀 반환
//  - 수명(lifeTime) 만료 시에도 풀 반환 (PooledObject 또는 자체 타이머)
// 프리팹 요구: Collider2D(IsTrigger), Rigidbody2D(권장), PooledObject(권장)
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(Collider2D))]
    public class BossProjectile : MonoBehaviour
    {
        [Header("기본 설정")]
        [Tooltip("적중 후에도 사라지지 않고 관통할지 여부")]
        public bool piercing = false;

        [Tooltip("자체 수명(초). PooledObject가 있으면 그쪽 autoReturnAfter를 우선 사용 권장")]
        public float lifeTime = 5f;

        [Tooltip("이 레이어에 닿으면 소멸(벽/지형). 비워두면 무시")]
        public LayerMask wallLayers;

        private Vector2 dir;
        private float speed;
        private int damage;
        private float maxDistance; // >0이면 이 거리만큼 비행 후 소멸 (0=무제한, 수명으로만 관리)
        private float traveled;
        private float timer;
        private PooledObject pooled;

        private void Awake()
        {
            pooled = GetComponent<PooledObject>();
        }

        private void OnEnable()
        {
            timer = 0f;
            traveled = 0f;
        }

        /// <summary>발사 정보를 주입합니다(스폰 직후 호출). maxDist>0이면 그 거리에서 소멸.</summary>
        public void Launch(Vector2 direction, float moveSpeed, int dmg, float maxDist = 0f)
        {
            dir = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;
            speed = moveSpeed;
            damage = dmg;
            maxDistance = maxDist;
            traveled = 0f;

            // 진행 방향으로 회전 정렬
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        private void Update()
        {
            float step = speed * Time.deltaTime;
            transform.position += (Vector3)(dir * step);
            traveled += step;

            // 최대 비행거리 도달 시 소멸
            if (maxDistance > 0f && traveled >= maxDistance) { ReturnSelf(); return; }

            // PooledObject가 수명을 관리하지 않을 때만 자체 타이머로 반환
            if (pooled == null || pooled.autoReturnAfter <= 0f)
            {
                timer += Time.deltaTime;
                if (timer >= lifeTime) ReturnSelf();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerHealth ph = other.GetComponentInParent<PlayerHealth>();
                if (ph != null && !ph.IsDead) ph.TakeDamage(damage);
                if (!piercing) ReturnSelf();
                return;
            }

            // 벽/지형에 닿으면 소멸 (관통이어도 벽에는 막힘)
            if ((wallLayers.value & (1 << other.gameObject.layer)) != 0)
                ReturnSelf();
        }

        private void ReturnSelf()
        {
            if (pooled != null) pooled.ReturnToPool();
            else gameObject.SetActive(false);
        }
    }
}
