using UnityEngine;

[System.Serializable]
public class RingAdjacentBuff
{
    public int   attackPowerBonus;
    public float attackSpeedBonus;
    public int   hpBonus;
    public int   hpRegen;

    public bool HasEffect =>
        attackPowerBonus != 0 || attackSpeedBonus != 0f ||
        hpBonus != 0          || hpRegen != 0;
}

[CreateAssetMenu(fileName = "NewAccessory", menuName = "Items/Accessory")]
public class SO_AccessoryData : SO_ItemData
{
    [Header("인접 버프 (4방향 무기 아이템에 적용)")]
    public RingAdjacentBuff adjacentBuff;

    [Header("장신구 효과 설명")]
    [TextArea(2, 5)] public string adjacentGimmick;

    [Header("Lv.5 추가 기믹")]
    [TextArea(2, 5)] public string lv5Gimmick;
}
