using UnityEngine;

namespace BagSurvivor.Monster
{
    /// <summary>
    /// 사망 시 확률로 반지 1개를 드랍(6종 균등 랜덤).
    ///  - 일반: dropChance 사용.
    ///  - useFloorBasedChance: 같은 프리팹이 여러 층에 등장하는 중간보스용(2층 50% / 4층 이상 100%).
    ///  - 분열 슬라임: 그룹당 1개 — 더 분열하지 않고(gen0) 다른 분열체가 모두 죽은 '마지막'일 때만.
    /// </summary>
    [RequireComponent(typeof(MonsterController))]
    public class RingDropOnDeath : MonoBehaviour
    {
        [Tooltip("사망 시 반지 드랍 확률 (0~1). 슬라임=그룹당 이 확률로 1회")]
        [Range(0f, 1f)] public float dropChance = 1f;

        [Tooltip("체크 시 층 기반 확률(2층 0.5 / 4층+ 1.0) 사용 — 여러 층에 나오는 중간보스용")]
        public bool useFloorBasedChance = false;

        private MonsterController controller;
        private EliteSlimeSplitGimmick slimeSplit;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
            slimeSplit = GetComponent<EliteSlimeSplitGimmick>();
        }

        private void OnEnable()  { if (controller != null) controller.OnDeath += HandleDeath; }
        private void OnDisable() { if (controller != null) controller.OnDeath -= HandleDeath; }

        private void HandleDeath(MonsterController mc)
        {
            // 분열 슬라임: 그룹당 1개 — 아직 분열이 남았거나(gen>0) 다른 슬라임이 살아있으면 대기
            if (slimeSplit != null)
            {
                if (slimeSplit.generation > 0 || AnyOtherSlimeAlive()) return;
                RingDropService.TryDrop(transform.position, dropChance);
                return;
            }

            float chance = useFloorBasedChance ? FloorChance() : dropChance;
            RingDropService.TryDrop(transform.position, chance);
        }

        /// <summary>자신 외에 살아있는 분열 슬라임이 있는지.</summary>
        private bool AnyOtherSlimeAlive()
        {
            foreach (var s in FindObjectsByType<EliteSlimeSplitGimmick>(FindObjectsSortMode.None))
            {
                if (s == slimeSplit) continue;
                var c = s.GetComponent<MonsterController>();
                if (s.gameObject.activeInHierarchy && (c == null || !c.IsDead)) return true;
            }
            return false;
        }

        /// <summary>현재 층 기반 드랍 확률(2층 이하 0.5, 그 이상 1.0).</summary>
        private static float FloorChance()
        {
            var dg = FindFirstObjectByType<DungeonGenerator>();
            int floor = dg != null ? dg.currentFloor : 4;
            return floor <= 2 ? 0.5f : 1f;
        }
    }
}
