// ============================================================
// RangedAttackGimmick.cs
// 원거리 몬스터(궁수/코볼트 등) 전용 발사 기믹
//  - 플레이어가 사거리(attackRange) 안에 들어오면 쿨다운마다 투사체 발사.
//  - 이동(사거리에서 멈춤)은 MonsterController의 stopDistance로 처리(셋업 시 attackRange로 지정).
//  - 투사체는 BossProjectile 재사용(플레이어에 피해). 풀 있으면 GameObjectPool, 없으면 Instantiate.
//  - MonsterController에 컴포넌트로 부착 (컴포지션 패턴)
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    /// <summary>조준 방식</summary>
    public enum AimMode
    {
        AtPlayerPosition,    // 플레이어 현재 위치로 발사(고블린/스켈레톤)
        PlayerMoveDirection, // 플레이어가 이동하는 방향으로 발사(코볼트 창)
    }

    [RequireComponent(typeof(MonsterController))]
    public class RangedAttackGimmick : MonoBehaviour
    {
        [Header("발사 설정")]
        [Tooltip("조준 방식: 플레이어 위치 / 플레이어 이동방향")]
        public AimMode aimMode = AimMode.AtPlayerPosition;

        [Tooltip("이 거리 안에 플레이어가 있으면 발사")]
        public float attackRange = 7f;

        [Tooltip("발사 쿨다운(초)")]
        public float fireCooldown = 1.5f;

        [Tooltip("발사 직전 준비(텔레그래프) 시간(초). 이 동안 빨간 외곽선 표시")]
        public float telegraphTime = 0.4f;

        [Tooltip("스폰(태어난) 직후 첫 발사까지 대기시간(초)")]
        public float spawnDelay = 0.5f;

        [Tooltip("투사체 속도(m/s)")]
        public float projectileSpeed = 8f;

        [Tooltip("투사체 최대 비행 거리(m)")]
        public float projectileMaxRange = 12f;

        [Header("투사체 프리팹")]
        [Tooltip("발사할 투사체 (BossProjectile 컴포넌트 필요)")]
        public GameObject projectilePrefab;

        private MonsterController controller;
        private AttackTelegraphOutline outline;
        private float cooldownTimer;
        private bool firing;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
            outline = GetComponent<AttackTelegraphOutline>();
            if (outline == null) outline = gameObject.AddComponent<AttackTelegraphOutline>();
        }

        private void OnEnable()
        {
            cooldownTimer = spawnDelay; // 태어나자마자 쏘지 않고 spawnDelay만큼 대기 후 첫 발사
            firing = false;
            outline?.Show(false);
        }

        private void OnDisable()
        {
            firing = false;
            outline?.Show(false); // 풀 반환 시 외곽선 정리
        }

        private void Update()
        {
            if (controller == null || controller.IsDead || controller.PlayerTransform == null) return;
            if (firing) return; // 준비~발사 진행 중

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                return;
            }

            if (controller.GetDistanceToPlayer() <= attackRange)
                StartCoroutine(TelegraphAndFire());
        }

        /// <summary>발사 준비(빨간 외곽선) → 발사 → 쿨다운. 준비 도중 사망 시 안전 종료.</summary>
        private IEnumerator TelegraphAndFire()
        {
            firing = true;
            if (telegraphTime > 0f)
            {
                outline?.Show(true);
                float t = 0f;
                while (t < telegraphTime)
                {
                    if (controller == null || controller.IsDead) { outline?.Show(false); firing = false; yield break; }
                    t += Time.deltaTime;
                    yield return null;
                }
                outline?.Show(false);
            }
            if (controller != null && !controller.IsDead) Fire();
            cooldownTimer = fireCooldown;
            firing = false;
        }

        private void Fire()
        {
            if (projectilePrefab == null) return;

            Vector2 dir = AimDirection();
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

            GameObject go = GameObjectPool.Instance != null
                ? GameObjectPool.Instance.Get(projectilePrefab, transform.position, Quaternion.identity)
                : Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            if (go == null) return;

            var proj = go.GetComponent<BossProjectile>();
            if (proj != null) proj.Launch(dir, projectileSpeed, controller.Attack, projectileMaxRange);
        }

        /// <summary>조준 방향 계산. 이동방향 모드는 플레이어 속도 방향, 정지 시 위치 조준으로 폴백.</summary>
        private Vector2 AimDirection()
        {
            if (aimMode == AimMode.PlayerMoveDirection)
            {
                var prb = controller.PlayerTransform.GetComponent<Rigidbody2D>();
                if (prb != null && prb.linearVelocity.sqrMagnitude > 0.01f)
                    return prb.linearVelocity.normalized;
                // 플레이어가 멈춰 있으면 현재 위치 조준으로 폴백
            }
            return controller.GetDirectionToPlayer();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
