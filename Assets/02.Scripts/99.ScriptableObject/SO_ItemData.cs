using UnityEngine;

// 1. 단 하나의 최상위 부모 클래스
public abstract class SO_ItemData : ScriptableObject
{
    [Header("공통 데이터")]
    public string itemID;
    public string itemName;
    [TextArea(2, 5)] public string itemDescription;
    public Sprite itemImage;

    [Header("등급 및 상점")]
    public ItemRarity rarity;
    public ItemGrade grade;
    public int cost;

    [Header("시너지")]
    [Tooltip("최대 3개까지 설정 가능")]
    public SynergyType[] synergies = new SynergyType[3];

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

public enum ItemRarity { Common, Rare, Epic, Legendary }
public enum ItemGrade { Grade1 = 1, Grade2, Grade3, Grade4, Grade5 }
public enum SynergyType
{
    None = 0,
    Assassin = 1,       // 암살단
    SwordMaster = 2,    // 소드마스터
    HolyKnight = 3,     // 성기사단
    DemonLord = 4,      // 마왕
    BloodBerserker = 5, // 피의광전사
    Tycoon = 6,         // 대부호
    Executioner = 7,    // 처형자
    SpiritMage = 8,     // 정령술사
    GearShift = 9,      // 기어시프트
    Pinball = 10,       // 핀볼
    Overload = 11,      // 과부화
    Electro = 12,       // 일렉트로
    Impregnable = 13,   // 난공불락
    Titan = 14,         // 티탄
    Fairy = 15,         // 페어리
}
