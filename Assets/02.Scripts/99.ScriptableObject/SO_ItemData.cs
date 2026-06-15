using UnityEngine;

// 1. 단 하나의 최상위 부모 클래스
public abstract class SO_ItemData : ScriptableObject
{
    [Header("공통 데이터")]
    public string itemName;
    public Sprite itemImage;

    [Header("블록 형태")]
    [Tooltip("아이템이 차지하는 셀 목록 (0,0 기준 상대 좌표). 직사각형 외 L자·T자 등 자유 형태 가능")]
    public Vector2Int[] cells;

    // 편의용: cells가 비어있으면 (0,0) 단일 셀로 취급
    public bool OccupiesCell(Vector2Int pos)
    {
        if (cells == null || cells.Length == 0)
            return pos == Vector2Int.zero;

        foreach (var c in cells)
            if (c == pos) return true;

        return false;
    }

    // 회전 없이 cells를 월드 오프셋(origin)으로 변환
    public Vector2Int[] GetWorldCells(Vector2Int origin)
    {
        if (cells == null || cells.Length == 0)
            return new[] { origin };

        var result = new Vector2Int[cells.Length];
        for (int i = 0; i < cells.Length; i++)
            result[i] = origin + cells[i];

        return result;
    }
}

// 2. 부모를 상속받는 자식 클래스들
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Items/Weapon")]
public class SO_WeaponData : SO_ItemData
{
    [Header("무기 전용 데이터")]
    public int attackPower;
}

[CreateAssetMenu(fileName = "NewArmor", menuName = "Items/Armor")]
public class SO_ArmorData : SO_ItemData
{
    [Header("방어구 전용 데이터")]
    public int defense;
}
