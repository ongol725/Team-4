using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class DungeonRenderer : MonoBehaviour
{
    [System.Serializable]
    public struct FloorTheme
    {
        public string themeName;
        public TileBase floorTile;
        [Tooltip("체스판 무늬를 위한 두 번째 바닥 타일 (비워두면 기본 바닥만 사용)")]
        public TileBase altFloorTile;
        public TileBase wallTile; // (롤백용) 기존 방식의 단일 타일

        [Header("고급 오토 타일링용 (사용 시 활성화)")]
        [Tooltip("9개(또는 13개) 이상의 스프라이트를 규칙에 맞게 넣어주세요.")]
        public Sprite[] advancedWallSprites;

        [Header("기둥 타일 (Parthenon 방)")]
        public Sprite[] pillarSprites;

        [Header("벽 정면(face) 타일 - 북쪽 벽 높이 표현")]
        [Tooltip("방 북쪽 벽의 윗면 아래로 노출되는 정면(높이) 스프라이트. 비워두면 정면을 그리지 않음.")]
        public Sprite wallFaceSprite;
    }

    [Header("렌더링 옵션")]
    [Tooltip("체크 시 13개의 스프라이트를 사용해 코너를 자동으로 계산하여 렌더링합니다. 체크 해제 시 기존 단일 타일로 롤백합니다.")]
    public bool useAdvancedAutoTiling = true;

    [Header("렌더링 연결")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    
    [Header("층별 테마 타일 설정 (1층부터 순서대로)")]
    public FloorTheme[] floorThemes;

    [Header("기본 타일 (테마 누락 시 예비용)")]
    public TileBase defaultFloorTile;
    [Tooltip("체스판 무늬를 위한 두 번째 바닥 타일 (예비용)")]
    public TileBase defaultAltFloorTile;
    public TileBase defaultWallTile;

    // 맵 데이터와 크기를 받아서 8방향 벽을 계산합니다.
    public void GenerateWalls(int[,] mapData, int mapWidth, int mapHeight)
    {
        for (int x = 1; x < mapWidth - 1; x++)
        {
            for (int y = 1; y < mapHeight - 1; y++)
            {
                // 현재 타일이 빈 공간(0)일 때만 검사
                if (mapData[x, y] == 0)
                {
                    bool shouldBeWall = false;

                    // 주변 8방향(대각선 포함)을 모두 검사하여 하나라도 바닥이나 복도가 있으면 벽으로 만듦
                    for (int i = -1; i <= 1; i++)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            if (mapData[x + i, y + j] == 1)
                            {
                                shouldBeWall = true;
                                break; // 하나라도 찾으면 더 이상 검사할 필요 없음
                            }
                        }
                        if (shouldBeWall) break;
                    }

                    if (shouldBeWall)
                    {
                        mapData[x, y] = 3;
                    }
                }
            }
        }
    }

    // 완성된 mapData를 바탕으로 실제 타일맵에 타일을 렌더링합니다.
    public void RenderTilemap(int[,] mapData, int mapWidth, int mapHeight, int currentFloor)
    {
        if (floorTilemap != null) 
        {
            floorTilemap.ClearAllTiles();
            TilemapRenderer floorRenderer = floorTilemap.GetComponent<TilemapRenderer>();
            if (floorRenderer != null) floorRenderer.sortingOrder = 0; // 바닥은 맨 아래
        }
        
        if (wallTilemap != null) 
        {
            wallTilemap.ClearAllTiles();
            TilemapRenderer wallRenderer = wallTilemap.GetComponent<TilemapRenderer>();
            if (wallRenderer != null) wallRenderer.sortingOrder = 1; // 벽은 바닥보다 위
        }

        // 타일 캐싱 (성능 최적화: 스프라이트당 하나의 Tile 객체만 생성하여 재사용)
        Dictionary<Sprite, Tile> tileCache = new Dictionary<Sprite, Tile>();

        // 현재 층에 맞는 타일 가져오기
        TileBase activeFloorTile = defaultFloorTile;
        TileBase activeAltFloorTile = defaultAltFloorTile;
        TileBase activeWallTile = defaultWallTile;
        Sprite[] activeWallSprites = null;
        Sprite activeFaceSprite = null;
        Sprite[] activePillarSprites = null;

        if (floorThemes != null && currentFloor >= 1 && currentFloor <= floorThemes.Length)
        {
            if (floorThemes[currentFloor - 1].floorTile != null) activeFloorTile = floorThemes[currentFloor - 1].floorTile;
            if (floorThemes[currentFloor - 1].altFloorTile != null) activeAltFloorTile = floorThemes[currentFloor - 1].altFloorTile;
            if (floorThemes[currentFloor - 1].wallTile != null) activeWallTile = floorThemes[currentFloor - 1].wallTile;
            activeWallSprites = floorThemes[currentFloor - 1].advancedWallSprites;
            activeFaceSprite = floorThemes[currentFloor - 1].wallFaceSprite;
            activePillarSprites = floorThemes[currentFloor - 1].pillarSprites;
        }

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (mapData[x, y] == 1) 
                {
                    if (floorTilemap != null) 
                    {
                        TileBase tileToPlace = activeFloorTile;
                        // activeAltFloorTile이 존재하면 (x + y)가 홀수일 때 교차 타일 배치
                        if (activeAltFloorTile != null && ((x + y) % 2 != 0))
                        {
                            tileToPlace = activeAltFloorTile;
                        }
                        if (tileToPlace != null) floorTilemap.SetTile(pos, tileToPlace);
                    }
                }
                else if (mapData[x, y] == 3) 
                {
                    if (useAdvancedAutoTiling && activeWallSprites != null && activeWallSprites.Length >= 9)
                    {
                        Sprite wallSprite = GetOrientedWallSprite(mapData, x, y, mapWidth, mapHeight, activeWallSprites);
                        if (wallSprite != null)
                        {
                            if (!tileCache.ContainsKey(wallSprite))
                            {
                                Tile newTile = ScriptableObject.CreateInstance<Tile>();
                                newTile.sprite = wallSprite;
                                tileCache[wallSprite] = newTile;
                            }
                            if (wallTilemap != null) wallTilemap.SetTile(pos, tileCache[wallSprite]);
                        }
                    }
                    else
                    {
                        // 기존 방식 (롤백)
                        if (wallTilemap != null && activeWallTile != null) wallTilemap.SetTile(pos, activeWallTile);
                    }

                    // 정면(face) 패스: 북쪽 벽(남쪽 칸이 바닥)이면 정면 타일을 남쪽 칸 위에 시각적으로 오버레이한다.
                    // mapData는 변경하지 않으므로 충돌·이동·길찾기 로직에는 영향을 주지 않는다.
                    if (useAdvancedAutoTiling && activeFaceSprite != null
                        && y - 1 >= 0 && mapData[x, y - 1] == 1 && wallTilemap != null)
                    {
                        if (!tileCache.ContainsKey(activeFaceSprite))
                        {
                            Tile faceTile = ScriptableObject.CreateInstance<Tile>();
                            faceTile.sprite = activeFaceSprite;
                            tileCache[activeFaceSprite] = faceTile;
                        }
                        wallTilemap.SetTile(new Vector3Int(x, y - 1, 0), tileCache[activeFaceSprite]);
                    }
                }
                else if (mapData[x, y] == 4)
                {
                    // 기둥 받침: 바닥을 깔고 그 위에 세로 기둥(받침/몸통/머리)을 오버레이한다.
                    if (floorTilemap != null && activeFloorTile != null)
                        floorTilemap.SetTile(pos, activeFloorTile);
                    if (useAdvancedAutoTiling && activePillarSprites != null
                        && activePillarSprites.Length >= 3 && wallTilemap != null)
                    {
                        // [0]=받침(아래) [1]=몸통(중간) [2]=머리(위), 위(y+)로 쌓는다
                        for (int i = 0; i < 3; i++)
                        {
                            Sprite ps = activePillarSprites[i];
                            if (ps == null) continue;
                            if (!tileCache.ContainsKey(ps))
                            {
                                Tile pt = ScriptableObject.CreateInstance<Tile>();
                                pt.sprite = ps;
                                tileCache[ps] = pt;
                            }
                            wallTilemap.SetTile(new Vector3Int(x, y + i, 0), tileCache[ps]);
                        }
                    }
                }
            }
        }
    }

    // 비트마스크(Bitmask)를 이용해 맵 데이터를 분석하여 올바른 벽 스프라이트를 반환합니다.
    private Sprite GetOrientedWallSprite(int[,] mapData, int x, int y, int w, int h, Sprite[] sprites)
    {
        // 주변 8방향의 벽(Wall) 여부를 확인합니다.
        // IsWall은 맵 밖이거나 바닥(1)이 아니면 true(벽)를 반환합니다.
        int nw = IsWall(mapData, x - 1, y + 1, w, h) ? 1 : 0;
        int n  = IsWall(mapData, x,     y + 1, w, h) ? 2 : 0;
        int ne = IsWall(mapData, x + 1, y + 1, w, h) ? 4 : 0;
        int w_ = IsWall(mapData, x - 1, y,     w, h) ? 8 : 0;
        int e  = IsWall(mapData, x + 1, y,     w, h) ? 16 : 0;
        int sw = IsWall(mapData, x - 1, y - 1, w, h) ? 32 : 0;
        int s  = IsWall(mapData, x,     y - 1, w, h) ? 64 : 0;
        int se = IsWall(mapData, x + 1, y - 1, w, h) ? 128 : 0;

        int pattern = nw | n | ne | w_ | e | sw | s | se;

        // 직교 4방향(상하좌우) 마스크: N(2) | W(8) | E(16) | S(64) = 90
        int mask_cardinal = 90;

        // 1. 4방향 모두 벽인 경우 (외곽 코너 또는 중앙 채우기)
        if ((pattern & mask_cardinal) == mask_cardinal) 
        {
            if ((pattern & 128) == 0) return sprites[0]; // SE 바닥 -> Top Left Outer (┌)
            if ((pattern & 32) == 0)  return sprites[2]; // SW 바닥 -> Top Right Outer (┐)
            if ((pattern & 4) == 0)   return sprites[6]; // NE 바닥 -> Bottom Left Outer (└)
            if ((pattern & 1) == 0)   return sprites[8]; // NW 바닥 -> Bottom Right Outer (┘)
            return sprites[4]; // 대각선도 모두 벽이거나 예외 상황이면 Center Fill (■)
        }

        // 2. 3방향 벽인 경우 (직선 외곽 벽)
        if ((pattern & mask_cardinal) == (2 | 8 | 16))  return sprites[1]; // S가 바닥 -> Top Outer (─)
        if ((pattern & mask_cardinal) == (64 | 8 | 16)) return sprites[7]; // N이 바닥 -> Bottom Outer (─)
        if ((pattern & mask_cardinal) == (2 | 64 | 8))  return sprites[3]; // E가 바닥 -> Left Outer (│)
        if ((pattern & mask_cardinal) == (2 | 64 | 16)) return sprites[5]; // W가 바닥 -> Right Outer (│)

        // 3. 2방향 벽인 경우 (내부 코너) - 내부 코너 전용 타일이 없으므로, 통짜 벽(■) 대신
        //    직선 외곽 벽 타일로 처리해 복도-방 연결부를 매끄럽게 잇는다.
        // 위로 가는 복도(N+E/N+W): 정면(face)이 이어지므로 직선 윗면으로 통일
        if ((pattern & mask_cardinal) == (2 | 16)) return sprites[1];  // N+E 벽 -> Top 직선(─)
        if ((pattern & mask_cardinal) == (2 | 8))  return sprites[1];  // N+W 벽 -> Top 직선(─)
        // 아래로 가는 복도(S+W/S+E): 정면이 없으므로 외부 코너 타일로 꺾어줌
        if ((pattern & mask_cardinal) == (64 | 8))  return sprites[6]; // S+W 벽(안쪽 NE) -> BL 코너(└) _39
        if ((pattern & mask_cardinal) == (64 | 16)) return sprites[8]; // S+E 벽(안쪽 NW) -> BR 코너(┘) _23

        // 4. 예외: 1블록 두께 벽 (방과 방 사이 1칸 띄워진 곳 등, 거의 발생 안 함)
        if ((pattern & mask_cardinal) == (8 | 16)) return sprites[1]; // 가로 1칸 벽 -> Top Outer
        if ((pattern & mask_cardinal) == (2 | 64)) return sprites[3]; // 세로 1칸 벽 -> Left Outer

        // 5. 예외: 1방향 끝부분 
        if ((pattern & mask_cardinal) == 2) return sprites[3];
        if ((pattern & mask_cardinal) == 8) return sprites[1];
        if ((pattern & mask_cardinal) == 16) return sprites[1];
        if ((pattern & mask_cardinal) == 64) return sprites[3];

        // 기본값: 완전히 고립된 벽 또는 예외
        return sprites[4];
    }

    private bool IsWall(int[,] mapData, int x, int y, int w, int h)
    {
        // 맵 밖은 벽(외부 공간)으로 취급
        if (x < 0 || x >= w || y < 0 || y >= h) return true;
        // 바닥(1)이 아니면 벽(3) 또는 빈 공간(0)이므로 벽 판정에 포함
        return mapData[x, y] != 1;
    }

    private bool IsFloor(int[,] mapData, int x, int y, int w, int h)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return false;
        return mapData[x, y] == 1;
    }
}
