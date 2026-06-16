// ============================================================
// Pattern_PhantomDash.cs
// 특수 패턴 - 환영 대시
//  기획서: Use_Range 15m, Telegraph 2s, 늑대 3마리 소환(간격 0.5s) 후 순차 돌진,
//          Cooldown 20s, Chance 7%
//  - 소환은 기존 MonsterPool 사용(환영 늑대 프리팹: MonsterController + PhantomWolfDash)
//  - 텔레그래프 후 보스 주변에서 늑대를 0.5초 간격으로 소환, 각자 플레이어 방향으로 돌진
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    public class Pattern_PhantomDash : BossPatternBase
    {
        [Header("환영 대시 설정")]
        [Tooltip("소환할 늑대 수")]
        public int summonCount = 3;

        [Tooltip("소환 간격(초)")]
        public float summonInterval = 0.5f;

        [Tooltip("늑대 소환 위치 반경(보스 중심에서, m)")]
        public float summonRadius = 2f;

        [Header("소환물 프리팹")]
        [Tooltip("환영 늑대 프리팹 (MonsterController + PhantomWolfDash 필요, MonsterPool로 스폰)")]
        public GameObject wolfPrefab;

        [Tooltip("소환 텔레그래프 표식(선택)")]
        public GameObject telegraphPrefab;

        private void Reset()
        {
            patternName = "PhantomDash";
            isSpecial = true;
            useRange = 15f;
            telegraphTime = 2f;
            cooldown = 20f;
            chance = 7f;
        }

        protected override IEnumerator ExecuteRoutine()
        {
            GameObject tele = telegraphPrefab != null
                ? SpawnFromPool(telegraphPrefab, transform.position, Quaternion.identity) : null;
            yield return new WaitForSeconds(telegraphTime);
            ReturnPooled(tele);

            for (int i = 0; i < summonCount; i++)
            {
                SummonAndDash(i);
                if (i < summonCount - 1)
                    yield return new WaitForSeconds(summonInterval);
            }
        }

        private void SummonAndDash(int index)
        {
            if (wolfPrefab == null || MonsterPool.Instance == null) return;

            // 보스 주변 균등 배치 위치에서 소환
            float ang = (360f / Mathf.Max(1, summonCount)) * index * Mathf.Deg2Rad;
            Vector3 spawnPos = transform.position + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * summonRadius;

            MonsterController wolf = MonsterPool.Instance.Get(wolfPrefab, spawnPos);
            if (wolf == null) return;

            // 소환 시점 플레이어 방향으로 돌진
            Vector2 dir = ((Vector2)(controller.PlayerTransform != null
                ? controller.PlayerTransform.position - spawnPos
                : (Vector3)Vector2.right)).normalized;

            PhantomWolfDash dash = wolf.GetComponent<PhantomWolfDash>();
            if (dash != null) dash.Dash(dir);
        }
    }
}
