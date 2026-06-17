// ============================================================
// Pattern_RoarWave.cs
// 특수 패턴 - 포효 (전방 직선 투사체 연사)
//  기획서: Use_Range 15m, Telegraph 1.5s, 시전 시점 플레이어 방향으로
//          초승달 투사체 5발을 0.5초 간격(Hit_Delay)으로 직선 발사.
//          각 투사체 폭 약 5m, 최대 30m 비행, 유저/벽/최대사거리에서 소멸(유도 X).
//          5발 발사 후 2초 정지 → 추적 재개. Cooldown 15s, Chance 7%.
//  - 발사 방향은 시전 순간 고정(유저가 움직여도 궤도 불변).
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    public class Pattern_RoarWave : BossPatternBase
    {
        [Header("포효 발사 설정")]
        [Tooltip("발사할 투사체 수")]
        public int projectileCount = 5;

        [Tooltip("투사체 간 발사 간격(초, Hit_Delay)")]
        public float fireInterval = 0.5f;

        [Tooltip("투사체 비행 속도(m/s)")]
        public float projectileSpeed = 12f;

        [Tooltip("투사체 최대 비행 거리(m, Attack_Range)")]
        public float maxRange = 30f;

        [Tooltip("발사 후 제자리 정지 시간(초)")]
        public float recoveryTime = 2f;

        [Header("2페이즈 강화")]
        [Tooltip("사용 후 보스가 받는 피해 감소율(0~1). 0.3 = 30% 감소")]
        [Range(0f, 1f)] public float phase2DamageReduction = 0.3f;

        [Tooltip("받는 피해 감소 지속 시간(초)")]
        public float phase2BuffDuration = 10f;

        [Header("연출/투사체 프리팹")]
        [Tooltip("초승달 투사체 프리팹 (BossProjectile 필요). 폭 ~5m는 프리팹 스케일/콜라이더로 표현")]
        public GameObject projectilePrefab;

        [Tooltip("텔레그래프(예고) 표식")]
        public GameObject telegraphPrefab;

        private void Reset()
        {
            patternName = "RoarWave";
            isSpecial = true;
            useRange = 15f;
            telegraphTime = 1.5f;
            cooldown = 15f;
            chance = 7f;
        }

        protected override IEnumerator ExecuteRoutine()
        {
            // 발사 방향 = 시전 시점 플레이어 방향으로 고정
            Vector2 dir = DirToPlayer();

            GameObject tele = ShowTelegraph(telegraphPrefab, transform.position, dir);
            yield return new WaitForSeconds(telegraphTime);
            ReturnPooled(tele);

            for (int i = 0; i < projectileCount; i++)
            {
                FireCrescent(dir);
                if (i < projectileCount - 1)
                    yield return new WaitForSeconds(fireInterval);
            }

            // 발사 후 제자리 정지(숨 고르기)
            if (recoveryTime > 0f)
                yield return new WaitForSeconds(recoveryTime);

            // 2페이즈 강화: 사용 종료 후 일정 시간 받는 피해 감소(방어 버프)
            if (phase2Mode && controller != null)
                controller.ApplyDamageReduction(phase2DamageReduction, phase2BuffDuration);
        }

        private void FireCrescent(Vector2 dir)
        {
            if (projectilePrefab == null) return;
            GameObject go = SpawnFromPool(projectilePrefab, transform.position, Quaternion.identity);
            if (go == null) return;

            BossProjectile proj = go.GetComponent<BossProjectile>();
            if (proj != null) proj.Launch(dir, projectileSpeed, controller.Attack, maxRange);
        }

        // 노랑 선=발사 방향/사거리(30m), 회색=발동 사거리(15m)
        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmo()) return;
            Vector2 dir = (Application.isPlaying && controller != null) ? DirToPlayer() : Vector2.right;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)dir * maxRange);
            GizmoCircle(transform.position, useRange, Color.gray);
        }
    }
}
