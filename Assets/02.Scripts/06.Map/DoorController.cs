using UnityEngine;

/// <summary>
/// 특수방 입구 문. 타일 스프라이트로 열림/닫힘을 표현한다.
///  - 차단(플레이어 막기)은 Collider2D가 담당(통로 폭+여유) → 옆으로 새지 않게 완전 차단.
///  - 비주얼은 좌/중/우 3종 컬럼을 width만큼 배치(가운데는 반복)해 개구부를 가득 채움.
///  - 6장 입력은 3x2: [0]좌상 [1]중상 [2]우상 [3]좌하 [4]중하 [5]우하 → 좌=0/3, 중=1/4, 우=2/5 컬럼.
///  - 가로 통로면 전체를 90° 회전해 재사용.
///  - 생성 직후 DungeonPopulator가 Init()으로 주입한다.
/// </summary>
public class DoorController : MonoBehaviour
{
    private SpriteRenderer[] cells; // 길이 = width*2 (2행)
    private int[] srcIndex;         // 각 셀이 참조할 6-배열 인덱스
    private Sprite[] closedSprites;
    private Sprite[] openSprites;
    private Collider2D col;

    /// <summary>DungeonPopulator가 문 오브젝트 생성 직후 호출. widthTiles: 문 가로 칸 수(통로+여유).</summary>
    public void Init(Sprite[] closed, Sprite[] open, bool rotate, int sortingOrder, int widthTiles)
    {
        closedSprites = closed;
        openSprites = open;
        col = GetComponent<Collider2D>();

        if (widthTiles < 3) widthTiles = 3;
        BuildCells(widthTiles, sortingOrder);
        if (rotate) transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // 가로 통로용 재사용

        Open(); // 초기 열린 상태
    }

    /// <summary>width×2 셀을 문 중심 기준으로 배치. 양끝은 좌/우 컬럼, 가운데는 중앙 컬럼 반복.</summary>
    private void BuildCells(int width, int sortingOrder)
    {
        cells = new SpriteRenderer[width * 2];
        srcIndex = new int[width * 2];
        float x0 = -(width - 1) / 2f; // 중앙 정렬

        for (int c = 0; c < width; c++)
        {
            int srcCol = (c == 0) ? 0 : (c == width - 1) ? 2 : 1; // 좌/우/중
            for (int r = 0; r < 2; r++)                            // r=0 상, r=1 하
            {
                int i = c * 2 + r;
                GameObject go = new GameObject("DoorCell_" + c + "_" + r);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(x0 + c, r == 0 ? 0.5f : -0.5f, 0f);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = sortingOrder;
                cells[i] = sr;
                srcIndex[i] = r * 3 + srcCol; // 상행=0,1,2 / 하행=3,4,5
            }
        }
    }

    private void SetSprites(Sprite[] set)
    {
        if (set == null || cells == null) return;
        for (int i = 0; i < cells.Length; i++)
        {
            int s = srcIndex[i];
            cells[i].sprite = (s < set.Length) ? set[s] : null;
        }
    }

    public void Open()
    {
        if (col != null) col.enabled = false;
        SetSprites(openSprites);
    }

    public void Close()
    {
        if (col != null) col.enabled = true;
        SetSprites(closedSprites);
    }
}
