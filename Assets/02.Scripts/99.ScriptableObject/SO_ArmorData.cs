using UnityEngine;

[CreateAssetMenu(fileName = "NewArmor", menuName = "Items/Armor")]
public class SO_ArmorData : SO_ItemData
{
    [Header("방어구 스탯")]
    public int defense;

    [Header("고유 능력")]
    [TextArea(2, 5)] public string ability;
}
