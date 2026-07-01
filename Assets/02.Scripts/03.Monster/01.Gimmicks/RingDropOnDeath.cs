using UnityEngine;

namespace BagSurvivor.Monster
{
    /// <summary>
    /// 사망 시 확률로 반지 1개를 드랍한다(6종 균등 랜덤).
    /// 슬라임처럼 분열하는 몬스터는 최종 분열체 사망 시에만 드랍한다.
    /// 엘리트/중간보스 프리팹에 부착하고 dropChance만 인스펙터로 지정.
    /// </summary>
    [RequireComponent(typeof(MonsterController))]
    public class RingDropOnDeath : MonoBehaviour
    {
        [Tooltip("사망 시 반지 드랍 확률 (0~1). 1층 슬라임 0.3, 2층 중간보스 0.5, 3·4층 1.0")]
        [Range(0f, 1f)] public float dropChance = 1f;

        private MonsterController controller;
        private EliteSlimeSplitGimmick slimeSplit;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
            slimeSplit = GetComponent<EliteSlimeSplitGimmick>();
        }

        private void OnEnable()
        {
            if (controller != null) controller.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (controller != null) controller.OnDeath -= HandleDeath;
        }

        private void HandleDeath(MonsterController mc)
        {
            // 분열 몬스터: 아직 분열이 남은 중간 세대면 드랍하지 않음(최종 분열체에서만)
            if (slimeSplit != null && slimeSplit.generation > 0) return;

            RingDropService.TryDrop(transform.position, dropChance);
        }
    }
}
