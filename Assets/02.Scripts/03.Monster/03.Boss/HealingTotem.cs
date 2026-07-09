// ============================================================
// HealingTotem.cs
// 2페이즈 힐링 토템: 살아있는 동안 일정 간격으로 보스 체력을 회복.
//  기획서: 토템당 1초마다 보스 Max HP의 0.5% 회복(5개 모두면 초당 2.5%).
//  - 3타격 시 파괴(HittableObject) → 회복 중단 + 풀 반환.
//  - 회복은 실제 HP 회복(보스룸 전 최대체력 상승 기믹과는 별개).
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(HittableObject))]
    public class HealingTotem : MonoBehaviour
    {
        [Tooltip("회복 주기(초)")]
        public float healInterval = 1f;

        [Tooltip("틱당 회복량(보스 Max HP 비율). 0.005 = 0.5%")]
        public float healPercent = 0.005f;

        private MonsterController target;
        private Coroutine loop;

        /// <summary>회복 대상(보스)을 지정합니다. 소환 직후 호출.</summary>
        public void Activate(MonsterController boss)
        {
            target = boss;
        }

        private void OnEnable()
        {
            loop = StartCoroutine(HealLoop());
        }

        private void OnDisable()
        {
            if (loop != null) StopCoroutine(loop);
            loop = null;
            target = null;
        }

        private IEnumerator HealLoop()
        {
            var wait = new WaitForSeconds(healInterval);
            while (true)
            {
                yield return wait;
                if (target != null && !target.IsDead)
                    target.Heal(Mathf.Max(1, Mathf.RoundToInt(target.MaxHP * healPercent)));
            }
        }
    }
}
