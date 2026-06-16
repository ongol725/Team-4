// ============================================================
// Pattern_Charge.cs
// 기본 패턴 - 돌진
//  기획서: Use_Range 8m, Attack_Wait(텔레그래프) 2s, 돌진 거리 20m,
//          벽 충돌 시 0.3초 후 충돌 판정(crush), Cooldown 10s, Chance 25%
//  - 시작 시점 플레이어 방향으로 직선 돌진. 경로상 플레이어 타격.
//  - 벽 충돌 시 멈추고 0.3초 후 충돌 지점 광역 판정.
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

        [Header("벽 충돌(crush)")]
        [Tooltip("벽으로 인식할 레이어")]
        public LayerMask wallMask;

        [Tooltip("벽 충돌 후 충돌 판정까지 지연(초)")]
        public float crushDelay = 0.3f;

        [Tooltip("벽 충돌 광역 판정 반경(m)")]
        public float crushRadius = 3f;

        [Tooltip("벽 감지 거리(보스 중심 기준, m)")]
        public float wallProbe = 0.8f;

        [Header("연출 프리팹(선택)")]
        public GameObject telegraphPrefab;
        public GameObject crushEffectPrefab;

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
            bool hitWall = false;
            bool hitPlayerOnce = false;

            while (traveled < dashDistance)
            {
                if (controller == null || controller.IsDead) break;

                // 벽 감지 (진행 방향으로 짧게 탐침)
                if (Physics2D.Raycast(transform.position, dir, wallProbe, wallMask))
                {
                    hitWall = true;
                    break;
                }

                controller.SetVelocity(dir * dashSpeed);

                // 경로상 플레이어 타격 (1회)
                if (!hitPlayerOnce && TryHitPlayer(transform.position, bodyHitRadius))
                    hitPlayerOnce = true;

                traveled += dashSpeed * Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            controller.SetVelocity(Vector2.zero);

            // 벽 충돌 시 crush 판정
            if (hitWall)
            {
                yield return new WaitForSeconds(crushDelay);
                if (crushEffectPrefab != null)
                    SpawnFromPool(crushEffectPrefab, transform.position, Quaternion.identity);
                TryHitPlayer(transform.position, crushRadius);
            }

            controller.SetKnockbackImmune(false);
            controller.EndExternalMovement();
        }
    }
}
