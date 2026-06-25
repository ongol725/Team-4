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

        [Header("2페이즈 강화")]
        [Tooltip("2페이즈 2번째 돌진의 짧은 경고선 시간(초). 플레이어 향해 재돌진")]
        public float phase2SecondDashTelegraph = 0.5f;

        [Header("연출 프리팹(선택)")]
        public GameObject telegraphPrefab;
        [Tooltip("충돌 시 임팩트 이펙트(광역 피해 없음, 연출용)")]
        public GameObject impactEffectPrefab;

        // 경고선(텔레그래프) 동안은 대기, 실제 돌진 시작 때 돌진 애니 재생
        protected override bool AutoPlayAnimOnExecute => false;

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
            controller.BeginExternalMovement();
            controller.SetKnockbackImmune(true);

            // 1회 돌진 (텔레그래프 → 직선 돌진 → 충돌 시 정지 후딜)
            yield return DoOneDash(telegraphTime);

            // 2페이즈 강화: 짧은 경고선(0.5초) 후 플레이어 향해 1번 더 돌진
            if (phase2Mode)
                yield return DoOneDash(phase2SecondDashTelegraph);

            controller.SetKnockbackImmune(false);
            controller.EndExternalMovement();
        }

        /// <summary>돌진 1회. telegraphSec 동안 플레이어 방향 경고선 표시 후 직선 돌진(시작 시점 방향 고정).</summary>
        private IEnumerator DoOneDash(float telegraphSec)
        {
            Vector2 dir = DirToPlayer();

            GameObject tele = ShowTelegraph(telegraphPrefab, transform.position, dir);
            // DashWarn 류 경고선이면 돌진 거리만큼 앞으로 타일(자식 SpriteRenderer)
            if (tele != null)
            {
                var sr = tele.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sr.drawMode != SpriteDrawMode.Simple)
                {
                    sr.size = new Vector2(dashDistance, sr.size.y);
                    sr.transform.localPosition = new Vector3(dashDistance * 0.5f, 0f, 0f);
                }
            }
            yield return new WaitForSeconds(telegraphSec);   // 경고선 동안 대기(대기 애니 유지)
            ReturnPooled(tele);

            // 실제 돌진 시작 → 돌진 애니(매 돌진마다 처음부터 재시작 → 2페이즈 2회 돌진 시 2번 출력)
            if (bossAnimator != null) bossAnimator.PlayPattern(animState, true);

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
