using UnityEngine;

/// <summary>
/// 인벤토리에서 아이템 한 칸의 런타임 상태.
/// SO는 '설계도'이고 현재 합성 등급은 이 클래스가 관리한다.
/// 반지(SO_AccessoryData)는 gradeIndex를 무시한다.
/// </summary>
[System.Serializable]
public class ItemInstance
{
    public SO_ItemData data;

    [Range(0, 4)]
    public int gradeIndex;     // 0 = 1등급, 4 = 5등급

    /// <summary>반지 인접 버프로 추가되는 임시 등급 보너스. InventoryAnalyzer.Analyze()마다 초기화된다.</summary>
    [System.NonSerialized]
    public int RingGradeBonus;

    // ─────────────────────────────────────────────────────────────

    public bool HasGrades =>
        data is SO_WeaponData || data is SO_ArmorData;

    public int DisplayGrade => gradeIndex + 1;

    public WeaponGradeStats CurrentWeaponStats =>
        data is SO_WeaponData wd && gradeIndex < wd.gradeStats.Length
            ? wd.gradeStats[gradeIndex]
            : null;

    public ArmorGradeStats CurrentArmorStats =>
        data is SO_ArmorData ad && gradeIndex < ad.gradeStats.Length
            ? ad.gradeStats[gradeIndex]
            : null;

    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 합성 시도. 5등급 초과 불가. 성공하면 true 반환.
    /// </summary>
    public bool TryUpgrade()
    {
        if (!HasGrades || gradeIndex >= 4) return false;
        gradeIndex++;
        return true;
    }

    /// <summary>
    /// 같은 SO를 가리키는지 확인 (합성 가능 여부 판단용)
    /// </summary>
    public bool IsSameItem(ItemInstance other) =>
        other != null && data == other.data;
}
