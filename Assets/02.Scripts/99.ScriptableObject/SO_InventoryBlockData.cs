using UnityEngine;

/// <summary>
/// 인벤토리 확장 아이템 SO.
/// 구매 즉시 ActiveRows / ActiveCols 를 늘려 잠긴 셀을 개방한다.
/// 그리드에 배치되지 않는 소모형 아이템이므로 cells 는 비워 둔다.
/// </summary>
[CreateAssetMenu(fileName = "InventoryBlock_", menuName = "Item/InventoryBlock")]
public class SO_InventoryBlockData : SO_ItemData
{
    [Header("인벤토리 확장 수치")]
    [Tooltip("구매 시 추가할 행 수 (세로 확장)")]
    public int expandRows = 1;

    [Tooltip("구매 시 추가할 열 수 (가로 확장)")]
    public int expandCols = 0;
}
