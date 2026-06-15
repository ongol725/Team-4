using UnityEngine;

// 1. 단 하나의 최상위 부모 클래스
public abstract class SO_ItemData : ScriptableObject
{
    [Header("공통 데이터")]
    public string itemID;
    public string itemName;
    [TextArea(2, 5)] public string itemDescription;
    public Sprite itemImage;

    [Header("블록 형태")]
    [Tooltip("아이템이 차지하는 셀 목록.\n좌상단 = (0,0), x = 행(아래 증가), y = 열(오른쪽 증가)\n예) ㄱ자: (0,0)(0,1)(1,1)")]
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

    // cells를 그리드 origin 위치 기준 절대 좌표로 변환
    // origin = 아이템 좌상단 셀의 그리드 좌표
    public Vector2Int[] GetWorldCells(Vector2Int origin)
    {
        if (cells == null || cells.Length == 0)
            return new[] { origin };

        var result = new Vector2Int[cells.Length];
        for (int i = 0; i < cells.Length; i++)
            result[i] = new Vector2Int(origin.x + cells[i].x, origin.y + cells[i].y);

        return result;
    }
}

public enum WeaponRarity { Common, Rare, Epic, Legendary }
public enum WeaponGrade { Grade1 = 1, Grade2, Grade3, Grade4, Grade5 }
public enum SynergyType { None, Fire, Ice, Lightning, Poison, Holy, Dark }

// 2. 부모를 상속받는 자식 클래스들
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Items/Weapon")]
public class SO_WeaponData : SO_ItemData
{
    [Header("무기 식별")]
    public WeaponRarity rarity;
    public WeaponGrade grade;

    [Header("무기 스탯")]
    public int attackPower;
    public float attackSpeed;

    [Header("상점")]
    public int cost;

    [Header("시너지")]
    [Tooltip("최대 3개까지 설정 가능")]
    public SynergyType[] synergies = new SynergyType[3];

    [Header("투사체")]
    [Tooltip("원거리 무기일 경우 발사체 프리팹 연결. 근거리면 비워두세요.")]
    public GameObject projectile;
}

[CreateAssetMenu(fileName = "NewArmor", menuName = "Items/Armor")]
public class SO_ArmorData : SO_ItemData
{
    [Header("방어구 전용 데이터")]
    public int defense;
}
