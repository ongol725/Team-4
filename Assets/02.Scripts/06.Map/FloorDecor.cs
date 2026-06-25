using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 여러 칸이 하나의 묶음인 바닥 장식 세트. tiles는 width*height개,
/// 윗줄→아랫줄, 왼→오 순서. (좌상단이 tiles[0])
/// </summary>
[System.Serializable]
public class FloorDecorSet
{
    public string name;
    public int width = 1;
    public int height = 1;
    [Tooltip("width*height개. 윗줄→아랫줄, 왼→오 순서 (좌상단=tiles[0])")]
    public TileBase[] tiles;
}

/// <summary>
/// 베이스 바닥 위에 단독 장식 타일과 묶음(세트) 장식을 랜덤 배치하는 공용 유틸.
/// DungeonRenderer(랜덤 던전)·BossRoomGenerator(보스룸) 양쪽에서 사용한다.
/// </summary>
public static class FloorDecorPlacer
{
    /// <param name="isFloor">(x,y)가 장식을 깔아도 되는 바닥인지</param>
    public static void Apply(
        Tilemap floor, int width, int height,
        System.Func<int, int, bool> isFloor,
        TileBase[] singles, float singleChance,
        FloorDecorSet[] sets, int setAttempts)
    {
        if (floor == null || isFloor == null) return;

        // 1) 단독 장식: 각 바닥 칸을 chance 확률로 단독 타일로 교체
        if (singles != null && singles.Length > 0 && singleChance > 0f)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    if (!isFloor(x, y)) continue;
                    if (Random.value >= singleChance) continue;
                    TileBase t = singles[Random.Range(0, singles.Length)];
                    if (t != null) floor.SetTile(new Vector3Int(x, y, 0), t);
                }
        }

        // 2) 세트 장식: setAttempts회 시도. footprint가 전부 바닥이고 미사용이면 스탬프
        if (sets != null && sets.Length > 0 && setAttempts > 0)
        {
            bool[,] used = new bool[width, height];
            for (int a = 0; a < setAttempts; a++)
            {
                FloorDecorSet set = sets[Random.Range(0, sets.Length)];
                if (set == null || set.tiles == null || set.width < 1 || set.height < 1) continue;
                if (set.tiles.Length < set.width * set.height) continue;

                // 좌상단(=윗줄 왼쪽) 앵커. y는 위로 갈수록 큼 → 아랫줄은 y가 작아짐
                int ax = Random.Range(0, width);
                int ay = Random.Range(0, height);

                bool ok = true;
                for (int j = 0; j < set.width && ok; j++)
                    for (int i = 0; i < set.height && ok; i++)
                    {
                        int cx = ax + j;
                        int cy = ay - i;
                        if (cx < 0 || cx >= width || cy < 0 || cy >= height) { ok = false; break; }
                        if (!isFloor(cx, cy) || used[cx, cy]) { ok = false; break; }
                    }
                if (!ok) continue;

                for (int j = 0; j < set.width; j++)
                    for (int i = 0; i < set.height; i++)
                    {
                        int cx = ax + j;
                        int cy = ay - i;
                        used[cx, cy] = true;
                        TileBase t = set.tiles[i * set.width + j];
                        if (t != null) floor.SetTile(new Vector3Int(cx, cy, 0), t);
                    }
            }
        }
    }
}
