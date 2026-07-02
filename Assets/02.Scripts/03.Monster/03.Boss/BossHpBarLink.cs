// ============================================================
// BossHpBarLink.cs
// 보스가 씬에 등장(보스룸 도착)하면 상단 BossHpBar를 자신에게 연결한다.
//  - 겹(layer) 모델: 보스 고유 최대체력(=기본 5겹)에 연결하고, 이후 BossHpBar가
//    런 경과시간에 따라 1분마다 1겹씩(최대 100겹) 스스로 쌓는다.
//  - HP 수치/겹 설정 등 세부는 BossHpBar 인스펙터에서 별도로 한다(여기선 연결만).
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

        private void Start()
        {
            var bar = FindFirstObjectByType<BossHpBar>(FindObjectsInactive.Include);
            if (bar == null)
            {
                Debug.LogWarning("[BossHpBarLink] 씬에서 BossHpBar를 찾지 못해 연결하지 못했습니다.");
                return;
            }

            var mc = GetComponent<MonsterController>();

            // 겹(layer) 모델로 전환: 던전 사전 성장 이월은 사용하지 않고,
            // 보스 고유 최대체력(=기본 5겹) 기준으로 BossHpBar가 경과시간에 따라 겹을 쌓는다.
            bar.gameObject.SetActive(true);
            bar.SetTarget(mc, bossName);
        }
    }
}
