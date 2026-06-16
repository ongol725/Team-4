// ============================================================
// Pattern_Charge.cs
// 기본 패턴 - 돌진
//  기획서: Use_Range 8m, Attack_Wait(텔레그래프) 2s, 최대 돌진 거리 20m,
//          유저/벽과 충돌 시 즉시 돌진 종료 후 0.3초(crush_Delay) 정지,
//          20m 모두 이동 시 그대로 종료 → 추적. Cooldown 10s, Chance 25%
//  - 시작 시점 플레이어 방향으로 직선 돌진(유도 X). 유저와 충돌 시 1회 피해 + 즉시 종료.
//  - 벽 충돌 시 별도 광역 피해 없음(기획서 기준). 충돌 시에만 0.3초 정지.
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    public class Pattern_Charge : BossPatternBase
    {
        [Header("돌진 설정")]
        [Tooltip("최대 돌진 거리(m)")]
        public float dashDistance = 20f;

        [Tooltip("돌진 속도(m/s)")]
        public float dashSpeed = 22f;

        [Tooltip("돌진 중 플레이어 타격 반경(m)")]
        public float bodyHitRadius = 1.2f;

        [Header("충돌 처리")]
        [Tooltip("벽으로 인식할 레이어")]
        public LayerMask wallMask;

        [Tooltip("벽 감지 거리(보스 중심 기준, m)")]
        public float wallProbe = 0.8f;

        [Tooltip("유저/벽 충돌 시 정지 시간(초, crush_Delay)")]
        public float stopOnHit = 0.3f;

        [Header("연출 프리팹(선택)")]
        public GameObject telegraphPrefab;
        [Tooltip("충돌 시 임팩트 이펙트(광역 피해 없음, 연출용)")]
        public GameObject impactEffectPrefab;

        private void Reset()
        {
            patternName = "Charge";
            isSpecial = false;
            useRange = 8f;
            telegraphTime = 2f;
            cooldown = 10f;
            chance = 25f;
        }

        protected override IEnumerator ExecuteRoutine()
        {
            // 시작 시점 방향 고정 (돌진은 직선)
            Vector2 dir = DirToPlayer();

            GameObject tele = ShowTelegraph(telegraphPrefab, transform.position, dir);
            yield return new WaitForSeconds(telegraphTime);
            ReturnPooled(tele);

            controller.BeginExternalMovement();
            controller.SetKnockbackImmune(true);

            float traveled = 0f;
            bool collided = false;

            while (traveled < dashDistance)
            {
                if (controller == null || controller.IsDead) break;

                // 벽 충돌 → 즉시 종료
                if (Physics2D.Raycast(transform.position, dir, wallProbe, wallMask))
                {
                    collided = true;
                    break;
                }

                controller.SetVelocity(dir * dashSpeed);

                // 유저 충돌 → 1회 피해 후 즉시 종료
                if (TryHitPlayer(transform.position, bodyHitRadius))
                {
                    collided = true;
                    break;
                }

                traveled += dashSpeed * Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            controller.SetVelocity(Vector2.zero);

            // 충돌(유저/벽)한 경우에만 정지 후딜(0.3초). 최대 거리 완주 시엔 바로 종료.
            if (collided)
            {
                if (impactEffectPrefab != null)
                    SpawnFromPool(impactEffectPrefab, transform.position, Quaternion.identity);
                yield return new WaitForSeconds(stopOnHit);
            }

            controller.SetKnockbackImmune(false);
            controller.EndExternalMovement();
        }

        // 노랑=돌진 경로/거리(플레이어 방향, 에디터선 오른쪽) / 회색=발동 사거리
        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmo()) return;
            Vector2 dir = (Application.isPlaying && controller != null) ? DirToPlayer() : Vector2.right;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)dir * dashDistance);
            GizmoCircle(transform.position, useRange, Color.gray);
        }
    }
}
