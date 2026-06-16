// ============================================================
// DifficultyScaler.cs
// 시간 비례 난이도(몬스터 스탯 배율) 관리
//  - 런 시작부터 경과시간에 비례해 HP·공격력 배율을 산출
//  - 스폰 시점에 RoomMonsterSpawner가 이 배율을 몬스터에 주입
//  - 층은 RoomMonsterSpawner가 '어떤 몬스터가 나오는가(출현 구성)'로 처리하고,
//    여기서는 '얼마나 강한가(스탯 배율)'만 담당 → 두 축 분리
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    public class DifficultyScaler : MonoBehaviour
    {
        public static DifficultyScaler Instance { get; private set; }

        [Header("시간 비례 스탯 배율 (1분 경과당 증가율)")]
        [Tooltip("1분당 HP 증가율 (예: 0.15 → 10분이면 HP ×2.5)")]
        public float hpRatePerMinute = 0.15f;
        [Tooltip("1분당 공격력 증가율 (예: 0.08 → 10분이면 공격 ×1.8)")]
        public float attackRatePerMinute = 0.08f;

        [Header("배율 상한")]
        public float maxHpMultiplier = 6f;
        public float maxAttackMultiplier = 4f;

        [Header("옵션")]
        [Tooltip("씬 전환 후에도 경과시간을 유지(DontDestroyOnLoad). 주의: 같은 GameObject의 스포너/풀까지 함께 유지되어 재시작 시 문제가 됩니다. 기본 false 권장. 층 간 시간 누적은 GameManager 등 별도 보관으로 처리하세요.")]
        public bool persistAcrossScenes = false;

        private float runStartTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
            runStartTime = Time.time;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>런 시작부터 경과한 분(minute).</summary>
        public float ElapsedMinutes => Mathf.Max(0f, (Time.time - runStartTime) / 60f);

        public float GetHpMultiplier()
        {
            return Mathf.Min(maxHpMultiplier, 1f + ElapsedMinutes * hpRatePerMinute);
        }

        public float GetAttackMultiplier()
        {
            return Mathf.Min(maxAttackMultiplier, 1f + ElapsedMinutes * attackRatePerMinute);
        }

        /// <summary>경과시간을 0으로 초기화합니다. (새 런 시작 시 호출)</summary>
        public void ResetTimer()
        {
            runStartTime = Time.time;
        }
    }
}
