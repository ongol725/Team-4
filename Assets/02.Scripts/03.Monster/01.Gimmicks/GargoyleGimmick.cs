// ============================================================
// GargoyleGimmick.cs
// 가고일 전용 "우는 천사" 기믹
// 플레이어 정면 시야각(80도) 내에 있으면 이동 정지
// MonsterController에 컴포넌트로 부착하여 사용 (컴포지션 패턴)
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class GargoyleGimmick : MonoBehaviour
    {
        // ==========================================
        // 인스펙터 설정 (수정 용이하도록 노출)
        // ==========================================
        [Header("우는 천사 설정")]
        [Tooltip("플레이어의 정면 시야각 (도 단위, 기본 80도)")]
        [Range(10f, 180f)]
        public float visionAngle = 80f;

        // ==========================================
        // 내부 변수
        // ==========================================
        private MonsterController controller;
        private bool isBeingWatched = false;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
        }

        private void Update()
        {
            if (controller == null || controller.IsDead) return;
            if (controller.PlayerTransform == null) return;

            // 현재 넉백 상태면 시야각 체크하지 않음
            if (controller.CurrentState == MonsterState.Knockback) return;

            bool wasWatched = isBeingWatched;
            isBeingWatched = IsInPlayerVision();

            // 상태 변화가 있을 때만 이동 제어 호출 (매 프레임 호출 방지)
            if (isBeingWatched && !wasWatched)
            {
                // 시야각에 들어옴 → 이동 정지
                controller.PauseMovement();
            }
            else if (!isBeingWatched && wasWatched)
            {
                // 시야각에서 벗어남 → 즉시 이동 재개
                controller.ResumeMovement();
            }
        }

        /// <summary>
        /// 몬스터가 플레이어의 정면 시야각 내에 있는지 판정합니다.
        /// 플레이어의 이동 방향을 정면(facing) 방향으로 간주합니다.
        /// </summary>
        /// <returns>시야각 내에 있으면 true</returns>
        private bool IsInPlayerVision()
        {
            Transform player = controller.PlayerTransform;
            if (player == null) return false;

            // 플레이어의 이동 방향을 facing으로 간주
            // Rigidbody2D가 있으면 velocity 사용, 없으면 localScale 기반 추정
            Vector2 playerFacing = GetPlayerFacingDirection(player);

            // 플레이어가 정지 상태(방향 없음)면 마지막 방향 유지 또는 기본 오른쪽
            if (playerFacing.sqrMagnitude < 0.01f)
            {
                // 정지 상태에서는 현재 바라보는 방향 유지 (스프라이트 방향 기반)
                SpriteRenderer playerSprite = player.GetComponentInChildren<SpriteRenderer>();
                if (playerSprite != null)
                {
                    playerFacing = playerSprite.flipX ? Vector2.left : Vector2.right;
                }
                else
                {
                    playerFacing = Vector2.right; // 기본값
                }
            }

            // 플레이어 → 몬스터 방향 벡터
            Vector2 dirToMonster = ((Vector2)transform.position - (Vector2)player.position).normalized;

            // 두 벡터 사이 각도 계산
            float angle = Vector2.Angle(playerFacing, dirToMonster);

            // 시야각의 절반(한쪽)과 비교
            return angle <= visionAngle / 2f;
        }

        /// <summary>
        /// 플레이어의 현재 facing 방향을 구합니다.
        /// </summary>
        private Vector2 GetPlayerFacingDirection(Transform player)
        {
            // Rigidbody2D의 velocity를 facing 방향으로 사용
            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            if (playerRb != null && playerRb.linearVelocity.sqrMagnitude > 0.01f)
            {
                return playerRb.linearVelocity.normalized;
            }

            return Vector2.zero;
        }

        /// <summary>
        /// 에디터에서 시야각 범위를 시각적으로 확인할 수 있도록 기즈모를 그립니다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (controller == null || controller.PlayerTransform == null) return;

            // 플레이어 위치에서 시야각 범위를 부채꼴로 표시
            Transform player = controller.PlayerTransform;
            Vector2 facing = GetPlayerFacingDirection(player);
            if (facing.sqrMagnitude < 0.01f) facing = Vector2.right;

            float halfAngle = visionAngle / 2f;
            float radius = 3f; // 표시용 반경

            Gizmos.color = isBeingWatched ? Color.red : Color.green;

            // 부채꼴 양쪽 끝선
            Vector3 leftDir = Quaternion.Euler(0, 0, halfAngle) * facing;
            Vector3 rightDir = Quaternion.Euler(0, 0, -halfAngle) * facing;

            Gizmos.DrawLine(player.position, player.position + leftDir * radius);
            Gizmos.DrawLine(player.position, player.position + rightDir * radius);

            // 몬스터 위치에 구체
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
