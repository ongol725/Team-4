// ============================================================
// Pattern_HealTotem.cs
// 2페이즈 신규 특수 - 힐링 토템 소환
//  기획서: Telegraph 1.5s, 토템 5개 소환(보스 주변), 각 3타격 파괴,
//          토템당 1초마다 보스 Max HP 0.5% 회복, Cooldown 35s, Chance 10%
//  - 소환 후 패턴은 종료(토템은 독립적으로 회복/파괴 처리). 플레이어가 부숴야 회복 저지.
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    public class Pattern_HealTotem : BossPatternBase
    {
        [Header("힐링 토템 설정")]
        [Tooltip("소환할 토템 수")]
        public int totemCount = 5;

        [Tooltip("토템 소환 반경(보스 중심에서, m)")]
        public float summonRadius = 4f;

        [Header("프리팹")]
        [Tooltip("힐링 토템 프리팹 (HittableObject + HealingTotem + PooledObject)")]
        public GameObject totemPrefab;

        [Tooltip("소환 텔레그래프 표식(선택)")]
        public GameObject telegraphPrefab;

        private void Reset()
        {
            patternName = "HealTotem";
            isSpecial = true;
            phase2Only = true; // 2페이즈 전용
            useRange = 100f; // 보스가 언제든 소환 가능
            telegraphTime = 1.5f;
            cooldown = 35f;
            chance = 10f;
        }

        protected override IEnumerator ExecuteRoutine()
        {
            GameObject tele = telegraphPrefab != null
                ? SpawnFromPool(telegraphPrefab, transform.position, Quaternion.identity) : null;
            yield return new WaitForSeconds(telegraphTime);
            ReturnPooled(tele);

            for (int i = 0; i < totemCount; i++)
            {
                float ang = (360f / Mathf.Max(1, totemCount)) * i * Mathf.Deg2Rad;
                Vector3 pos = transform.position + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * summonRadius;

                GameObject go = SpawnFromPool(totemPrefab, pos, Quaternion.identity);
                if (go == null) continue;

                var totem = go.GetComponent<HealingTotem>();
                if (totem != null) totem.Activate(controller);
            }
            // 토템은 독립적으로 동작하므로 패턴은 즉시 종료
        }

        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmo()) return;
            GizmoCircle(transform.position, summonRadius, Color.green);
        }
    }
}
