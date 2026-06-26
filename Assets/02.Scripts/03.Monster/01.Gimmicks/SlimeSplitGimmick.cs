// ============================================================
// SlimeSplitGimmick.cs
// 슬라임(1003) 전용 분열 기믹
//  - 사망 시 분열 슬라임(1004)을 splitCount마리 스폰(작은 크기). "1번만 분열"이므로
//    분열체 프리팹에는 이 기믹을 붙이지 않는다(무한 분열 방지).
//  - 분열체는 방 클리어 카운트에 넣지 않음(보너스). 사망 시 풀 반환만 처리.
//  - MonsterController.OnDeath 이벤트에 반응 (컴포지션 패턴)
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class SlimeSplitGimmick : MonoBehaviour
    {
        [Header("분열 설정")]
        [Tooltip("사망 시 스폰할 분열 슬라임 수")]
        public int splitCount = 4;

        [Tooltip("분열 슬라임 프리팹 (MonsterController + 1004 데이터, 작은 크기, 분열 기믹 없음)")]
        public GameObject splitPrefab;

        [Tooltip("분열체가 흩어지는 반경(m)")]
        public float scatterRadius = 0.8f;

        private MonsterController controller;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
        }

        private void OnEnable()
        {
            controller.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            controller.OnDeath -= HandleDeath;
        }

        private void HandleDeath(MonsterController mc)
        {
            if (splitPrefab == null || MonsterPool.Instance == null) return;

            Vector3 center = transform.position;
            for (int i = 0; i < splitCount; i++)
            {
                Vector3 pos = center + (Vector3)(Random.insideUnitCircle * scatterRadius);
                MonsterController split = MonsterPool.Instance.Get(splitPrefab, pos);
                if (split == null) continue;

                // 보너스 처리: 방 클리어 통지는 하지 않음.
                // 스폰러에 디스폰 추적용으로 등록 → 플레이어가 방을 떠나면(복도 진입) 일반 몬스터처럼 같이 회수.
                if (RoomMonsterSpawner.Instance != null)
                    RoomMonsterSpawner.Instance.TrackExternalMonster(split);
                else
                    split.SetDeathCallback(m => MonsterPool.Instance.Return(m)); // 폴백: 스폰러 없으면 풀 반환만
            }
        }
    }
}
