using UnityEngine;

/// <summary>
/// 합성 1~5등급별 스탯. 배열 인덱스 0 = 1등급, 4 = 5등급.
/// </summary>
[System.Serializable]
public class ArmorGradeStats
{
    [Header("기본 스탯")]
    public int hpBonus;  // 체력 상승
    public int hpRegen;  // 10초당 체력 재생량
}

[CreateAssetMenu(fileName = "NewArmor", menuName = "Items/Armor")]
public class SO_ArmorData : SO_ItemData
{
    [Header("합성 등급별 스탯 (인덱스 0 = 1등급 ~ 4 = 5등급)")]
    public ArmorGradeStats[] gradeStats = new ArmorGradeStats[5];
}
