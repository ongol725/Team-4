// ============================================================
// BossHpBarLink.cs
// 보스가 씬에 등장(보스룸 도착)하면 상단 BossHpBar를 자신에게 연결한다.
//  - 시작부터 보이며 점점 오르던 HP바를, 보스룸 도착 시 보스 실제 HP에 연결하고
//    최대체력 상승(growInterval)을 멈춰 전투를 시작하는 매커니즘의 '연결' 부분.
//  - HP 수치/상승값 등 세부 설정은 BossHpBar 인스펙터에서 별도로 한다(여기선 연결만).
// ============================================================
using UnityEngine;
using BagSurvivor.UI;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class BossHpBarLink : MonoBehaviour
    {
        [Tooltip("HP바에 표시할 보스 이름")]
        public string bossName = "보스";

        [Tooltip("연결 시 최대체력 상승을 멈출지(보스룸 도착 = 전투 시작)")]
        public bool stopGrowOnLink = true;

        private void Start()
        {
            var bar = FindFirstObjectByType<BossHpBar>(FindObjectsInactive.Include);
            if (bar == null)
            {
                Debug.LogWarning("[BossHpBarLink] 씬에서 BossHpBar를 찾지 못해 연결하지 못했습니다.");
                return;
            }

            var mc = GetComponent<MonsterController>();

            // 던전에서 오른 최대 체력을 보스 실제 체력으로 이월(있을 때만). 보스는 그 값에서 풀피로 시작.
            if (BossHpCarry.Has)
                mc.IncreaseMaxHP(BossHpCarry.MaxHP - mc.MaxHP);

            bar.gameObject.SetActive(true);
            bar.SetTarget(mc, bossName);
            if (stopGrowOnLink) bar.growInterval = 0f; // 상승 멈춤 → 전투 시작
        }
    }
}
