// ============================================================
// BoarGimmick.cs
// 멧돼지 전용 돌진 기믹
// 일정 거리 이내 정지 → 준비 모션 → 돌진 → 휴식 반복
// MonsterController에 컴포넌트로 부착하여 사용 (컴포지션 패턴)
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class BoarGimmick : MonoBehaviour
    {
        // ==========================================
        // 인스펙터 설정 (수정 용이하도록 노출)
        // ==========================================
        [Header("돌진 설정")]
        [Tooltip("돌진 시작 거리 (이 거리 이하로 접근하면 돌진 준비)")]
        public float chargeStartDistance = 5f;

        [Tooltip("돌진 준비 시간 (초)")]
        public float chargeWindupTime = 1.5f;

        [Tooltip("돌진 최대 거리")]
        public float chargeMaxDistance = 10f;

        [Tooltip("돌진 속도 배율 (기본 이동속도 × 배율)")]
        public float chargeSpeedMultiplier = 3f;

        [Tooltip("돌진 후 휴식 시간 (초)")]
        public float restDuration = 3f;

        // ==========================================
        // 내부 변수
        // ==========================================
        private MonsterController controller;
        private bool isCharging = false;
        private bool isInChargeSequence = false;

        // 돌진 시각 효과용 (빨간 집중선)
        private LineRenderer chargeLine;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
        }

        private void Update()
        {
            if (controller == null || controller.IsDead) return;
            if (controller.PlayerTransform == null) return;

            // 이미 돌진 시퀀스 진행 중이면 무시
            if (isInChargeSequence) return;

            // 넉백 상태면 무시
            if (controller.CurrentState == MonsterState.Knockback) return;

            // 거리 체크
            float distance = controller.GetDistanceToPlayer();
            if (distance <= chargeStartDistance)
            {
                StartCoroutine(ChargeSequenceCoroutine());
            }
        }

        /// <summary>
        /// 돌진 전체 시퀀스: 정지 → 준비 → 돌진 → 충돌/최대거리 → 휴식 → 반복
        /// </summary>
        private IEnumerator ChargeSequenceCoroutine()
        {
            isInChargeSequence = true;

            // 1. 이동 제어를 위임받아 정지 (PauseMovement는 매 스텝 속도를 0으로 덮어써 돌진을
            //    막으므로, 보스 돌진과 동일하게 BeginExternalMovement로 직접 제어한다)
            controller.BeginExternalMovement();
            controller.SetVelocity(Vector2.zero);

            // 2. 돌진 방향 결정 (준비 시작 시점의 플레이어 위치)
            Vector2 chargeDirection = controller.GetDirectionToPlayer();
            Vector2 startPosition = transform.position;

            // 빨간 집중선 표시
            ShowChargeLine(chargeDirection);

            // 3. 준비 모션 대기 (1.5초)
            yield return new WaitForSeconds(chargeWindupTime);

            // 집중선 제거
            HideChargeLine();

            // 4. 돌진 시작
            isCharging = true;
            controller.SetKnockbackImmune(true); // 돌진 중 넉백 면역

            float chargeSpeed = controller.monsterData.moveSpeed * chargeSpeedMultiplier;
            float distanceTraveled = 0f;

            // isCharging이 false가 되면(플레이어 충돌) 즉시 중단
            while (distanceTraveled < chargeMaxDistance && isCharging)
            {
                if (controller.IsDead) break;

                controller.SetVelocity(chargeDirection * chargeSpeed);
                distanceTraveled = Vector2.Distance(startPosition, transform.position);

                yield return new WaitForFixedUpdate();
            }

            // 5. 돌진 종료
            isCharging = false;
            controller.SetKnockbackImmune(false);
            controller.SetVelocity(Vector2.zero);

            // 6. 휴식 (3초, 제자리 정지 유지)
            yield return new WaitForSeconds(restDuration);

            // 7. 이동 제어 반환 → 추적 재개
            controller.EndExternalMovement();
            isInChargeSequence = false;
        }

        /// <summary>
        /// 돌진 중 플레이어와 충돌했을 때 호출됩니다.
        /// 접촉 데미지는 MonsterController의 기본 접촉 데미지 시스템이 처리합니다.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isCharging) return;

            if (other.CompareTag("Player"))
            {
                // 돌진 중 플레이어 접촉 → 정지
                isCharging = false;
                controller.SetVelocity(Vector2.zero);
            }
        }

        /// <summary>
        /// 돌진 동선을 빨간 집중선으로 표시합니다.
        /// </summary>
        private void ShowChargeLine(Vector2 direction)
        {
            if (chargeLine == null)
            {
                GameObject lineObj = new GameObject("ChargeLine");
                lineObj.transform.SetParent(transform);
                chargeLine = lineObj.AddComponent<LineRenderer>();
                chargeLine.startWidth = 0.1f;
                chargeLine.endWidth = 0.1f;
                chargeLine.material = new Material(Shader.Find("Sprites/Default"));
                chargeLine.startColor = Color.red;
                chargeLine.endColor = new Color(1f, 0f, 0f, 0.3f);
                chargeLine.sortingOrder = 10;
            }

            chargeLine.enabled = true;
            chargeLine.positionCount = 2;
            chargeLine.SetPosition(0, transform.position);
            chargeLine.SetPosition(1, (Vector2)transform.position + direction * chargeMaxDistance);
        }

        /// <summary>
        /// 집중선을 숨깁니다.
        /// </summary>
        private void HideChargeLine()
        {
            if (chargeLine != null)
            {
                chargeLine.enabled = false;
            }
        }

        private void OnDisable()
        {
            // 오브젝트 풀 반환 시 초기화
            isCharging = false;
            isInChargeSequence = false;
            HideChargeLine();
            StopAllCoroutines();
        }
    }
}
