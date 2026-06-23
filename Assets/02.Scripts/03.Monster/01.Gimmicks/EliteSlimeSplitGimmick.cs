// ============================================================
// EliteSlimeSplitGimmick.cs
// 슬라임 엘리트(1층 Elite 방) 전용 분열 기믹 — "엘리트 보스" 연출.
//  - 사망 시 같은 프리팹을 splitCount마리 분열(세대=generation). generation>0인 동안만 분열.
//    → rootGeneration회 분열(예: 2회 × 3마리 = 1→3→9, 총 13마리).
//  - 분열체는 같은 Elite 방에 RegisterMonster + 사망 시 NotifyMonsterDead → "전부 죽어야 문 열림".
//    (MonsterController.OnDeath가 SpawnOne의 사망콜백보다 먼저 호출되므로, 분열체 등록(+N)이
//     부모 사망 통지(-1)보다 먼저 일어나 방 카운트가 0으로 떨어지지 않는다.)
//  - 세대마다 크기·체력이 줄어든다(childScaleMul / childHpMul).
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class EliteSlimeSplitGimmick : MonoBehaviour
    {
        [Header("분열 설정")]
        [Tooltip("최초(루트) 분열 가능 횟수. 예: 2 → 1→N→N² (2회 분열)")]
        public int rootGeneration = 2;

        [Tooltip("한 번 분열 시 생성 수")]
        public int splitCount = 3;

        [Tooltip("분열체 흩어짐 반경(m)")]
        public float scatterRadius = 1.2f;

        [Tooltip("세대마다 곱해지는 크기 비율")]
        public float childScaleMul = 0.7f;

        [Tooltip("세대마다 곱해지는 체력 비율")]
        public float childHpMul = 0.5f;

        [Tooltip("분열로 스폰할 프리팹 (보통 자기 자신 = 슬라임 엘리트 프리팹)")]
        public GameObject selfPrefab;

        // 런타임: 이 인스턴스의 남은 분열 횟수. OnEnable에서 루트값으로 리셋되고,
        // 분열체로 생성될 때 부모가 HandleDeath에서 generation-1로 덮어쓴다.
        [System.NonSerialized] public int generation;

        private MonsterController controller;
        private RoomController room;     // 소속 Elite 방 (사망 위치로 탐색해 캐시)
        private Vector3 baseScale;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
            baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            generation = rootGeneration;              // 루트 기본값(분열체는 부모가 덮어씀)
            transform.localScale = baseScale;         // 풀 재사용 시 크기 복원
            room = null;
            controller.suppressGoldDrop = generation > 0; // 분열하는 세대는 드롭 안 함(최종만 드롭)
            controller.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            controller.OnDeath -= HandleDeath;
        }

        private void HandleDeath(MonsterController mc)
        {
            if (generation <= 0 || selfPrefab == null || MonsterPool.Instance == null) return;

            RoomController rc = FindRoom();
            float parentHp = controller.HpMul;
            float parentAtk = controller.AtkMul;
            Vector3 center = transform.position;

            for (int i = 0; i < splitCount; i++)
            {
                Vector3 pos = center + (Vector3)(Random.insideUnitCircle * scatterRadius);
                MonsterController child = MonsterPool.Instance.Get(selfPrefab, pos, parentHp * childHpMul, parentAtk);
                if (child == null) continue;

                var g = child.GetComponent<EliteSlimeSplitGimmick>();
                if (g != null)
                {
                    g.generation = generation - 1;                  // 다음 세대
                    g.room = rc;
                    child.transform.localScale = transform.localScale * childScaleMul;
                }
                // 최종 세대(더 이상 분열 안 함)만 골드 드롭
                child.suppressGoldDrop = (generation - 1) > 0;

                // 같은 방에 등록 → 전부 죽어야 문 열림
                if (rc != null)
                {
                    rc.RegisterMonster();
                    RoomController roomRef = rc;
                    child.SetDeathCallback(m =>
                    {
                        roomRef.NotifyMonsterDead();
                        MonsterPool.Instance.Return(m);
                    });
                }
                else
                {
                    child.SetDeathCallback(m => MonsterPool.Instance.Return(m));
                }
            }
        }

        /// <summary>현재 위치를 포함하는 RoomController를 탐색(캐시). 분열체는 부모가 주입해 둠.</summary>
        private RoomController FindRoom()
        {
            if (room != null) return room;
            Vector2 pos = transform.position;
            foreach (RoomController rc in FindObjectsByType<RoomController>(FindObjectsSortMode.None))
            {
                if (rc == null) continue;
                Collider2D col = rc.GetComponent<Collider2D>();
                if (col != null && col.OverlapPoint(pos)) { room = rc; break; }
            }
            return room;
        }
    }
}
