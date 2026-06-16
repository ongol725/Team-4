// ============================================================
// PhantomWolfDash.cs
// 환영 늑대/분신용 돌진 동반 컴포넌트
//  - MonsterPool로 소환된 MonsterController 위에 부착
//  - Dash(dir) 호출 시: 돌진 방향 예고(텔레그래프)를 startDelay 동안 표시 → 돌진 → 풀 반환
//  - 소환을 0.5초 간격으로 하고 각 늑대가 자기 소환 후 startDelay(2초) 뒤 돌진하므로,
//    돌진도 0.5초씩 어긋난다.
//  - 돌진 중 접촉 피해는 MonsterController의 기존 접촉 데미지 로직이 처리.
//  - PhantomDash 패턴과 2페이즈 분신 패턴이 공용으로 사용.
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class PhantomWolfDash : MonoBehaviour
    {
        [Header("돌진 설정")]
        [Tooltip("돌진 속도(m/s)")]
        public float dashSpeed = 16f;

        [Tooltip("돌진 거리(m, 기획서 Attack_Range 20m)")]
        public float dashDistance = 20f;

        [Tooltip("소환 후 돌진 시작까지 대기(초, 이 동안 방향 예고 표시)")]
        public float startDelay = 2f;

        [Header("연출 프리팹(선택)")]
        [Tooltip("돌진 방향 예고(텔레그래프) 프리팹")]
        public GameObject dirTelegraphPrefab;

        private MonsterController controller;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
        }

        /// <summary>지정 방향으로 돌진을 시작합니다(소환 직후 호출). 방향 예고 후 돌진, 완료 시 풀 반환.</summary>
        public void Dash(Vector2 dir)
        {
            StartCoroutine(DashRoutine(dir.sqrMagnitude < 0.0001f ? Vector2.right : dir.normalized));
        }

        private IEnumerator DashRoutine(Vector2 dir)
        {
            // 예고 동안 늑대가 추적하지 않도록 이동을 먼저 위임받아 정지
            controller.BeginExternalMovement();
            controller.SetKnockbackImmune(true);
            controller.SetVelocity(Vector2.zero);

            // 돌진 방향 예고: 소환 시점 방향으로 고정 표시
            GameObject tele = ShowTelegraph(dir);
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
            ReturnTelegraph(tele);

            float traveled = 0f;
            while (traveled < dashDistance)
            {
                if (controller == null || controller.IsDead) break;
                controller.SetVelocity(dir * dashSpeed);
                traveled += dashSpeed * Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            controller.SetVelocity(Vector2.zero);
            controller.SetKnockbackImmune(false);
            controller.EndExternalMovement();

            ReturnToPool();
        }

        private GameObject ShowTelegraph(Vector2 dir)
        {
            if (dirTelegraphPrefab == null) return null;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.Euler(0f, 0f, ang);
            if (GameObjectPool.Instance != null)
                return GameObjectPool.Instance.Get(dirTelegraphPrefab, transform.position, rot);
            return Instantiate(dirTelegraphPrefab, transform.position, rot);
        }

        private void ReturnTelegraph(GameObject go)
        {
            if (go == null) return;
            PooledObject po = go.GetComponent<PooledObject>();
            if (po != null) po.ReturnToPool();
            else go.SetActive(false);
        }

        private void ReturnToPool()
        {
            if (MonsterPool.Instance != null)
                MonsterPool.Instance.Return(controller);
            else
                gameObject.SetActive(false);
        }
    }
}
