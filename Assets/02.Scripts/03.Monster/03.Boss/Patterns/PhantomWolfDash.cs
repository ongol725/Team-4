// ============================================================
// PhantomWolfDash.cs
// 환영 늑대/분신용 돌진 동반 컴포넌트
//  - MonsterPool로 소환된 MonsterController 위에 부착
//  - Dash(dir) 호출 시 지정 방향으로 일정 거리 돌진 후 풀로 반환(자가 소멸)
//  - 돌진 중 접촉 피해는 MonsterController의 기존 접촉 데미지 로직이 처리
//  - PhantomDash 패턴과 2페이즈 분신 패턴이 공용으로 사용
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

        [Tooltip("돌진 거리(m)")]
        public float dashDistance = 25f;

        [Tooltip("돌진 시작 전 대기(텔레그래프, 초)")]
        public float startDelay = 0f;

        private MonsterController controller;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
        }

        /// <summary>지정 방향으로 돌진을 시작합니다(소환 직후 호출). 완료 시 풀로 반환.</summary>
        public void Dash(Vector2 dir)
        {
            StartCoroutine(DashRoutine(dir.sqrMagnitude < 0.0001f ? Vector2.right : dir.normalized));
        }

        private IEnumerator DashRoutine(Vector2 dir)
        {
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

            controller.BeginExternalMovement();
            controller.SetKnockbackImmune(true);

            // 진행 방향 좌우 반전
            // (스프라이트 방향은 MonsterController가 자체 처리하지 않으므로 여기선 velocity만 제어)
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

        private void ReturnToPool()
        {
            if (MonsterPool.Instance != null)
                MonsterPool.Instance.Return(controller);
            else
                gameObject.SetActive(false);
        }
    }
}
