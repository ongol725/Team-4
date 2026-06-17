using System.Collections.Generic;
using BagSurvivor.UI;

/// <summary>
/// 특정 반지가 무기에 준 인접 버프 기록 1건.
/// </summary>
public class RingBuffRecord
{
    public string ringName;   // 반지 아이템명
    public string effectDesc; // 버프 효과 설명 (adjacentGimmick 또는 수치 파생)
}

/// <summary>
/// 무기 1개의 전투 진입 시 유효 데이터.
/// SO_WeaponData는 투사체·사거리·maxTargets 등 공통 정보를 직접 참조한다.
/// </summary>
public class WeaponLoadoutEntry
{
    public SO_WeaponData data;
    public int           effectiveGrade; // gradeIndex + RingGradeBonus (0~4)
    public int           attackPower;    // effectiveGrade 기준 등급 스탯
    public float         attackSpeed;
    public System.Collections.Generic.List<RingBuffRecord> RingBuffs = new();
}

/// <summary>
/// 활성화된 시너지 1개의 전투 진입 시 상태.
/// </summary>
public class ActiveSynergyEntry
{
    public SynergyType  type;
    public SynergyGrade grade;  // Bronze / Silver / Gold
    public int          count;  // 현재 보유 고유 아이템 종류 수
}

/// <summary>
/// 상점·인벤토리 팝업이 닫히는 순간 확정되는 전투 종합 정보.
/// BattleLoadoutBuilder.OnLoadoutReady 이벤트로 전달된다.
/// </summary>
public class BattleLoadout
{
    /// <summary>배치된 무기 전체 (반지 인접 버프 적용 등급 포함)</summary>
    public readonly List<WeaponLoadoutEntry> Weapons         = new();

    /// <summary>방어구 합산 체력 보너스</summary>
    public int TotalHpBonus;

    /// <summary>방어구 합산 10초당 체력 재생</summary>
    public int TotalHpRegen;

    /// <summary>Bronze 이상 활성화된 시너지 목록 (Gold → Silver → Bronze 순 정렬)</summary>
    public readonly List<ActiveSynergyEntry> ActiveSynergies = new();
}
