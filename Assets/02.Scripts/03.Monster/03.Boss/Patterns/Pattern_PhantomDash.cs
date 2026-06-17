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

        [Header("2페이즈 강화")]
        [Tooltip("2페이즈 소환 늑대 수(기획서 3→5)")]
        public int phase2SummonCount = 5;

        [Tooltip("1페이즈 늑대 돌진 대기 시간(초)")]
        public float phase1WolfStartDelay = 2f;

        [Tooltip("2페이즈 늑대 돌진 대기 시간(초, 기획서 2→1)")]
        public float phase2WolfStartDelay = 1f;

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

            int count = phase2Mode ? phase2SummonCount : summonCount;
            for (int i = 0; i < count; i++)
            {
                SummonAndDash(i, count);
                if (i < count - 1)
                    yield return new WaitForSeconds(summonInterval);
            }
        }

        private void SummonAndDash(int index, int count)
        {
            if (wolfPrefab == null || MonsterPool.Instance == null) return;

            // 보스 주변 균등 배치 위치에서 소환
            float ang = (360f / Mathf.Max(1, count)) * index * Mathf.Deg2Rad;
            Vector3 spawnPos = transform.position + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * summonRadius;

            MonsterController wolf = MonsterPool.Instance.Get(wolfPrefab, spawnPos);
            if (wolf == null) return;

            // 소환 시점 플레이어 방향(폴백). 실제 방향은 늑대가 돌진 직전 재확정.
            Vector2 dir = ((Vector2)(controller.PlayerTransform != null
                ? controller.PlayerTransform.position - spawnPos
                : (Vector3)Vector2.right)).normalized;

            PhantomWolfDash dash = wolf.GetComponent<PhantomWolfDash>();
            if (dash != null)
            {
                // 돌진 대기 시간 명시 세팅(풀 재사용 시 이전 값 잔존 방지). 2페이즈는 단축.
                dash.startDelay = phase2Mode ? phase2WolfStartDelay : phase1WolfStartDelay;
                dash.Dash(dir);
            }
        }

        // 회색=발동 사거리 / 파랑=늑대 소환 위치 반경
        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmo()) return;
            GizmoCircle(transform.position, useRange, Color.gray);
            GizmoCircle(transform.position, summonRadius, Color.cyan);
        }
    }
}
