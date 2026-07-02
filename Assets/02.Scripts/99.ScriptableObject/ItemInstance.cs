using System.Collections.Generic;
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

    /// <summary>무기 랜덤 강화 옵션. 옵션 수 = gradeIndex (2강 1개 ~ 5강 4개). 합성 시 1개씩 추가.</summary>
    public List<WeaponAffix> affixes = new List<WeaponAffix>();

    /// <summary>반지 인접 버프로 추가되는 임시 등급 보너스. InventoryAnalyzer.Analyze()마다 초기화된다.</summary>
    [System.NonSerialized]
    public int RingGradeBonus;

    // 반지 인접 기믹(모두 Analyze()마다 초기화). 합산 규칙.
    [System.NonSerialized] public float RingAtkBonus;   // 다이아(ACC_006): 인접당 +1.0(공격력 +100%)
    [System.NonSerialized] public float RingSpdBonus;   // 금(ACC_004): 인접당 +1.0(공속 +100%)
    [System.NonSerialized] public float RingStunChance; // 뼈(ACC_002): 인접당 +0.25(피격 시 스턴 확률)
    [System.NonSerialized] public float RingSlowSec;    // 나무(ACC_001): 3초(인접 시 피격 시 슬로우)
    [System.NonSerialized] public float RingProjScaleBonus; // 강철(ACC_005): 인접당 +0.5(투사체 크기)
    [System.NonSerialized] public float RingProjSpeedBonus; // 강철(ACC_005): 인접당 -0.2(발사·비행 속도)

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
        // 무기는 합성 대상의 기존 옵션 유지 + 랜덤 1개 추가(중복 허용).
        if (data is SO_WeaponData wd) affixes.Add(WeaponAffixTable.Roll(wd));
        return true;
    }

    /// <summary>무기 옵션 수를 gradeIndex에 맞춘다(다른 경로로 등급이 생성된 경우 보정).</summary>
    public void SyncWeaponAffixes()
    {
        if (data is not SO_WeaponData wd) return;
        affixes ??= new List<WeaponAffix>();
        while (affixes.Count < gradeIndex) affixes.Add(WeaponAffixTable.Roll(wd));
        while (affixes.Count > gradeIndex) affixes.RemoveAt(affixes.Count - 1);
    }

    /// <summary>
    /// 같은 SO를 가리키는지 확인 (합성 가능 여부 판단용)
    /// </summary>
    public bool IsSameItem(ItemInstance other) =>
        other != null && data == other.data;
}
