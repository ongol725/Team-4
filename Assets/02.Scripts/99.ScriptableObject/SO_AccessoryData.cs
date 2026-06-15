using UnityEngine;

[CreateAssetMenu(fileName = "NewAccessory", menuName = "Items/Accessory")]
public class SO_AccessoryData : SO_ItemData
{
    [Header("장신구 효과")]
    [TextArea(2, 5)] public string adjacentGimmick;

    [Header("Lv.5 추가 기믹")]
    [TextArea(2, 5)] public string lv5Gimmick;
}
