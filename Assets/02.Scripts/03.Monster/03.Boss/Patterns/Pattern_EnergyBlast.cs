// ============================================================
// Pattern_EnergyBlast.cs
// 기본 패턴 - 에너지 블래스트 (원거리)
//  기획서: Use_Range 10m, Telegraph 1s, 360° 6방향(60° 간격) 6발 발사,
//          Cooldown 12s, Chance 25%
//  - 텔레그래프 후 보스 중심에서 사방으로 투사체 발사 (풀 사용)
//  - volleyCount회 반복 발사하며 각 회마다 angleStep만큼 회전(나선 연출)
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    public class Pattern_EnergyBlast : BossPatternBase
    {
        [Header("발사 설정")]
        [Tooltip("한 번에 발사할 방향 수 (360°를 균등 분할)")]
        public int projectilesPerVolley = 6;

        [Tooltip("발사 횟수(연속 볼리)")]
        public int volleyCount = 1;

        [Tooltip("볼리 사이 간격(초)")]
        public float volleyInterval = 0.2f;

        [Tooltip("볼리마다 추가 회전 오프셋(도). 나선 연출용. 0이면 고정")]
        public float angleStepPerVolley = 0f;

        [Tooltip("투사체 속도(m/s)")]
        public float projectileSpeed = 8f;

        [Header("연출/투사체 프리팹")]
        [Tooltip("발사할 투사체 프리팹 (BossProjectile 컴포넌트 필요)")]
        public GameObject projectilePrefab;

        [Tooltip("텔레그래프 표식 프리팹(선택)")]
        public GameObject telegraphPrefab;

        private void Reset()
        {
            patternName = "EnergyBlast";
            isSpecial = false;
            useRange = 10f;
            telegraphTime = 1f;
            cooldown = 12f;
            chance = 25f;
        }

        protected override IEnumerator ExecuteRoutine()
        {
            GameObject tele = telegraphPrefab != null
                ? SpawnFromPool(telegraphPrefab, transform.position, Quaternion.identity) : null;
            yield return new WaitForSeconds(telegraphTime);
            ReturnPooled(tele);

            int count = Mathf.Max(1, projectilesPerVolley);
            float baseStep = 360f / count;

            for (int v = 0; v < Mathf.Max(1, volleyCount); v++)
            {
                float offset = angleStepPerVolley * v;
                for (int i = 0; i < count; i++)
                {
                    float ang = (baseStep * i + offset) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    FireProjectile(dir);
                }

                if (v < volleyCount - 1)
                    yield return new WaitForSeconds(volleyInterval);
            }
        }

        private void FireProjectile(Vector2 dir)
        {
            if (projectilePrefab == null) return;
            GameObject go = SpawnFromPool(projectilePrefab, transform.position, Quaternion.identity);
            if (go == null) return;

            BossProjectile proj = go.GetComponent<BossProjectile>();
            if (proj != null) proj.Launch(dir, projectileSpeed, controller.Attack);
        }
    }
}
