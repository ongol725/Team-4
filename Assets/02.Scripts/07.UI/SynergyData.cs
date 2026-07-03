// ============================================================
// SynergyData.cs
// 시너지 UI용 데이터 정의 (시너지 시스템 연결 전 Mock/전달용)
// ============================================================
using UnityEngine;

namespace BagSurvivor.UI
{
    public enum SynergyGrade { Bronze, Silver, Gold, Prism }

    [System.Serializable]
    public class SynergyInfo
    {
        public SynergyType  type;          // 전투 데이터 전달용 enum 값
        public string synergyName;         // 121001/122001
        public int count;
        public int nextThreshold;          // 다음 단계까지 필요한 수 (Gold 최대치 포함)
        public SynergyGrade grade;
        [TextArea] public string condition;    // 발동 조건 (구성 아이템 목록)
        [TextArea] public string description; // 122002
        [TextArea] public string effect;      // 122003
        public Sprite icon;
    }
}
