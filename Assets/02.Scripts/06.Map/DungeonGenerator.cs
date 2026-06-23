using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using System.Collections.Generic;

public enum RoomType { Normal, Start, Shop, Elite, MiniBoss, Boss }
public enum RoomShape { Rectangle, Octagon, Cross, Parthenon }

public class Room
{
    public RectInt bounds;
    public RoomType type;
    public RoomShape shape;
    public Vector2Int entranceGridPos = new Vector2Int(-1, -1); // 입구 타일 그리드 좌표
    public int entranceDir = -1; // 방 기준 입구 방향: 0=북(위), 1=동(우), 2=남(아래), 3=서(좌)
}

public class DungeonGenerator : MonoBehaviour
{
    // ==========================================
    // 1. 인스펙터 설정 (기획자 제어 영역)
    // ==========================================
    [Header("던전 진행 설정")]
    [Range(1, 5)] public int currentFloor = 1;

[Header("전체 맵 크기")]
    [Tooltip("맵의 가로 한계선 (타일 수)")] public int mapWidth = 150;
    [Tooltip("맵의 세로 한계선 (타일 수)")] public int mapHeight = 150;
    
    [Header("방(Room) 규칙")]
    [Tooltip("생성할 총 방의 개수")] public int targetRoomCount = 10;
    [Tooltip("방의 최소 크기 (타일 수)")] public int minRoomSize = 6;
    [Tooltip("방의 최대 크기 (타일 수)")] public int maxRoomSize = 15;

    [Header("복도(Corridor) 규칙")]
    [Tooltip("복도의 넓이 (기본 2칸)")] public int corridorWidth = 2;
    [Tooltip("복도의 최소 길이")] public int minCorridorLength = 3;
    [Tooltip("복도의 최대 길이")] public int maxCorridorLength = 8;

    [Header("탐험 및 미니맵 설정")]
    [Tooltip("씬에 배치된 플레이어 캐릭터")] public Transform playerTransform;
    [Tooltip("미니맵 컨트롤러 레퍼런스")] public MinimapController minimapController;

    [Header("외부 모듈 레퍼런스")]
    [Tooltip("렌더러 (타일맵 그리기)")] public DungeonRenderer dungeonRenderer;
    [Tooltip("팝퓰레이터 (오브젝트 및 방 배치)")] public DungeonPopulator dungeonPopulator;

    // ==========================================
    // 2. 내부 데이터 변수
    // ==========================================
    //private int mapWidth = 150;
    //private int mapHeight = 150;
    private int[,] mapData;      // 0: 빈공간, 1: 바닥(방 및 복도), 3: 벽
    private List<Room> generatedRooms = new List<Room>();

    // ==========================================
    // 3. 던전 생성 코어 로직
    // ==========================================
    void Start()
    {
        // 동일한 게임 오브젝트에 컴포넌트가 함께 붙어있다면 자동으로 레퍼런스를 가져옵니다.
        if (minimapController == null) minimapController = GetComponent<MinimapController>();
        if (dungeonRenderer == null) dungeonRenderer = GetComponent<DungeonRenderer>();
        if (dungeonPopulator == null) dungeonPopulator = GetComponent<DungeonPopulator>();

        // 플레이어 트랜스폼이 할당되지 않은 경우 자동으로 검색
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
                if (playerMovement != null)
                {
                    playerTransform = playerMovement.transform;
                }
            }
        }

        // GameManager가 있으면 현재 층수를 동기화
        if (GameManager.Instance != null)
            currentFloor = GameManager.Instance.currentFloor;

        GenerateDungeon();
    }

    // 같은 씬에서 맵을 초기화하고 다시 생성합니다.
    public void RegenerateDungeon()
    {
        // 1. RoomControllers 및 하위 오브젝트 (문, 계단) 제거
        GameObject roomControllers = GameObject.Find("RoomControllers");
        if (roomControllers != null) Destroy(roomControllers);

        // 2. floorTilemap 자식 오브젝트 제거 (함정 등)
        if (dungeonRenderer != null && dungeonRenderer.floorTilemap != null)
        {
            Transform t = dungeonRenderer.floorTilemap.transform;
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        // 3. 층수 동기화 후 재생성
        if (GameManager.Instance != null)
            currentFloor = GameManager.Instance.currentFloor;

        GenerateDungeon();
    }

    public void GenerateDungeon()
    {
        // 데이터 초기화
        mapData = new int[mapWidth, mapHeight];
        generatedRooms.Clear();


// 1. 첫 번째 방 생성 (맵 전체 구역 중 랜덤한 곳)
        Room firstRoom = new Room();
        int w = Random.Range(minRoomSize, maxRoomSize + 1);
        int h = Random.Range(minRoomSize, maxRoomSize + 1);
        
        int startX = Random.Range(mapWidth / 5, mapWidth * 4 / 5);
        int startY = Random.Range(mapHeight / 5, mapHeight * 4 / 5);
        
        firstRoom.bounds = new RectInt(startX - w / 2, startY - h / 2, w, h);
        firstRoom.type = RoomType.Normal; // Start 지정 제거
        firstRoom.shape = RoomShape.Rectangle; // 기본 형태 설정
        //WriteRect(firstRoom.bounds, 1);
        DrawRoom(firstRoom);
        generatedRooms.Add(firstRoom);

        // 2. 방 뻗어나가기 (가지치기 방식)
        int attempts = 0;
        int maxAttempts = 3000;

        while (generatedRooms.Count < targetRoomCount && attempts < maxAttempts)
        {
            attempts++;
            if (TryGenerateBranch()) attempts = 0;
        }

        // 3. 후처리 작업
        if (dungeonPopulator != null)
        {
            dungeonPopulator.AssignSpecialRooms(mapData, generatedRooms, currentFloor);
        }

        // 보스, 미니보스, 엘리트 방은 모양을 무조건 직사각형으로 강제 복구
        foreach (Room r in generatedRooms)
        {
            if (r.type == RoomType.Boss || r.type == RoomType.MiniBoss || r.type == RoomType.Elite)
            {
                r.shape = RoomShape.Rectangle;
                DrawRoom(r);
            }
        }

        if (dungeonRenderer != null)
        {
            dungeonRenderer.GenerateWalls(mapData, mapWidth, mapHeight);
            dungeonRenderer.RenderTilemap(mapData, mapWidth, mapHeight, currentFloor);
        }

        //타일맵 렌더링 후 함정 및 추가 요소 배치
        if (dungeonPopulator != null)
        {
            Tilemap floorMap = (dungeonRenderer != null) ? dungeonRenderer.floorTilemap : null;
            dungeonPopulator.corridorWidth = corridorWidth;
            dungeonPopulator.GenerateTraps(mapData, generatedRooms, floorMap);
            dungeonPopulator.GenerateStairs(generatedRooms, floorMap);
            dungeonPopulator.SpawnMonsters(mapData, generatedRooms, floorMap);
        }
        
        if (minimapController != null)
        {
            minimapController.InitializeMinimap(mapWidth, mapHeight, mapData, generatedRooms, playerTransform);
        }

        int count0 = 0, count1 = 0, count2 = 0, count3 = 0;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int y = mapHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int val = mapData[x, y];
                if (val == 0) count0++;
                else if (val == 1) count1++;
                else if (val == 2) count2++;
                else if (val == 3) count3++;
                sb.Append(val.ToString());
            }
            sb.AppendLine();
        }
        try
        {
            System.IO.File.WriteAllText(Application.dataPath + "/map_debug.txt", sb.ToString());
            Debug.Log($"[DungeonGenerator] MapData saved to Assets/map_debug.txt. Counts -> Empty(0): {count0}, Floor(1): {count1}, Wall(3): {count3}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DungeonGenerator] Failed to save debug map: {ex.Message}");
        }

        // 4. '진짜' Start 방을 찾고, 없으면 첫 번째 방을 시작 방으로 임시 설정하여 카메라와 플레이어 순간이동
        Room actualStartRoom = generatedRooms.Find(r => r.type == RoomType.Start);
        if (actualStartRoom == null && generatedRooms.Count > 0)
        {
            Debug.LogWarning("Start 방을 찾지 못해 첫 번째 생성된 방을 시작 위치로 설정합니다.");
            actualStartRoom = generatedRooms[0];
            actualStartRoom.type = RoomType.Start;
        }
        
        if (actualStartRoom != null)
        {
            // 방의 중앙 근처에서 실제 바닥 타일(mapData == 1)인 좌표 검색 (L자 방 등에서 벽 내부 스폰 차단)
            Vector2Int spawnGridPos = new Vector2Int(
                Mathf.FloorToInt(actualStartRoom.bounds.center.x),
                Mathf.FloorToInt(actualStartRoom.bounds.center.y)
            );
            
            float minDistance = float.MaxValue;
            Vector2 center = actualStartRoom.bounds.center;
            
            for (int x = actualStartRoom.bounds.xMin; x < actualStartRoom.bounds.xMax; x++)
            {
                for (int y = actualStartRoom.bounds.yMin; y < actualStartRoom.bounds.yMax; y++)
                {
                    if (mapData[x, y] == 1) // 바닥 타일인 경우
                    {
                        float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            spawnGridPos = new Vector2Int(x, y);
                        }
                    }
                }
            }

            // 그리드 좌표를 실제 타일맵 월드 좌표로 변환 (타일맵의 위치/스케일 오프셋 반영)
            Vector3 worldPos;
            if (dungeonRenderer != null && dungeonRenderer.floorTilemap != null)
            {
                worldPos = dungeonRenderer.floorTilemap.GetCellCenterWorld(new Vector3Int(spawnGridPos.x, spawnGridPos.y, 0));
            }
            else
            {
                worldPos = new Vector3(spawnGridPos.x + 0.5f, spawnGridPos.y + 0.5f, 0f);
            }
            
            if (Camera.main != null)
                Camera.main.transform.position = new Vector3(worldPos.x, worldPos.y, -10f);

            if (playerTransform != null)
            {
                playerTransform.position = new Vector3(worldPos.x, worldPos.y, 0f);
                
                // Rigidbody2D가 있을 경우 물리 연산 오버라이드를 위해 위치를 강제 동기화하고 속도를 초기화
                Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.position = new Vector2(worldPos.x, worldPos.y);
                    rb.linearVelocity = Vector2.zero;
                }
            }
        }
        else
        {
            Debug.LogError("맵 생성 오류: 생성된 방이 하나도 없어 시작 위치를 설정할 수 없습니다!");
        }
    }

    bool TryGenerateBranch()
    {
        Room baseRoom = generatedRooms[Random.Range(0, generatedRooms.Count)];
        int dir = Random.Range(0, 4); // 0:상, 1:우, 2:하, 3:좌
        int corLen = Random.Range(minCorridorLength, maxCorridorLength + 1);
        int newW = Random.Range(minRoomSize, maxRoomSize + 1);
        int newH = Random.Range(minRoomSize, maxRoomSize + 1);
        RoomShape newShape = (RoomShape)Random.Range(0, 4);

        RectInt corridor = new RectInt();
        RectInt newRoom = new RectInt();

        // 방의 모양에 따라 파인 곳(void)을 피해 복도가 뻗어나갈 안전한 오프셋 계산
        int baseOffsetX = GetSafeOffsetX(baseRoom.shape, baseRoom.bounds.width, dir);
        int baseOffsetY = GetSafeOffsetY(baseRoom.shape, baseRoom.bounds.height, dir);

        // 새로운 방 입장에서 들어오는 방향(incomingDir)은 반대 방향임
        int incomingDir = (dir + 2) % 4; 
        int newOffsetX = GetSafeOffsetX(newShape, newW, incomingDir);
        int newOffsetY = GetSafeOffsetY(newShape, newH, incomingDir);

        // 단면이 너무 좁아서 복도 양옆으로 벽(여백)을 세울 공간이 없다면 생성을 취소하고 재시도
        if (baseOffsetX == -1 || baseOffsetY == -1 || newOffsetX == -1 || newOffsetY == -1)
        {
            return false;
        }

        // 방향에 맞춰 바깥쪽으로 뻗어 나가는 좌표 계산
        if (dir == 0) // 위
        {
            corridor = new RectInt(baseRoom.bounds.x + baseOffsetX - corridorWidth / 2, baseRoom.bounds.yMax, corridorWidth, corLen);
            newRoom = new RectInt(corridor.x + corridorWidth / 2 - newOffsetX, corridor.yMax, newW, newH);
        }
        else if (dir == 1) // 오른쪽
        {
            corridor = new RectInt(baseRoom.bounds.xMax, baseRoom.bounds.y + baseOffsetY - corridorWidth / 2, corLen, corridorWidth);
            newRoom = new RectInt(corridor.xMax, corridor.y + corridorWidth / 2 - newOffsetY, newW, newH);
        }
        else if (dir == 2) // 아래
        {
            corridor = new RectInt(baseRoom.bounds.x + baseOffsetX - corridorWidth / 2, baseRoom.bounds.yMin - corLen, corridorWidth, corLen);
            newRoom = new RectInt(corridor.x + corridorWidth / 2 - newOffsetX, corridor.yMin - newH, newW, newH);
        }
        else if (dir == 3) // 왼쪽
        {
            corridor = new RectInt(baseRoom.bounds.xMin - corLen, baseRoom.bounds.y + baseOffsetY - corridorWidth / 2, corLen, corridorWidth);
            newRoom = new RectInt(corridor.xMin - newW, corridor.y + corridorWidth / 2 - newOffsetY, newW, newH);
        }

        // 겹침 검사
        // 복도는 출발 방을 무시하고 패딩 1로 겹침 검사
        // 새 방은 무시하는 방 없이(크기가 0인 RectInt 전달) 패딩 2로 검사하여 기존 방들과 완전히 떨어지도록 보장
        if (IsSpaceValid(corridor, baseRoom.bounds, 1) && IsSpaceValid(newRoom, new RectInt(0, 0, 0, 0), 2))
        {
            // 핵심 검증: 복도 시작점이 실제 바닥(mapData==1)에 맞닿아 있는지 확인
            // ㄱ/ㄴ자 방의 파인 허공 구역에서 복도가 뻗어나가는 것을 방지
            if (!CorridorConnectsToFloor(corridor, dir)) return false;

            WriteRect(corridor, 1);
            
            Room newGeneratedRoom = new Room { bounds = newRoom, type = RoomType.Normal };
            newGeneratedRoom.shape = (RoomShape)Random.Range(0, 6);

            // 입구 정보 저장: 복도가 새 방 경계에 닿는 타일 중앙 좌표 + 방 기준 입구 방향
            if (dir == 0) // 복도가 위로 뻗음 → 새 방 남쪽이 입구
            {
                newGeneratedRoom.entranceGridPos = new Vector2Int(corridor.xMin + corridorWidth / 2, newRoom.yMin);
                newGeneratedRoom.entranceDir = 2;
            }
            else if (dir == 1) // 복도가 오른쪽 → 새 방 서쪽이 입구
            {
                newGeneratedRoom.entranceGridPos = new Vector2Int(newRoom.xMin, corridor.yMin + corridorWidth / 2);
                newGeneratedRoom.entranceDir = 3;
            }
            else if (dir == 2) // 복도가 아래 → 새 방 북쪽이 입구
            {
                newGeneratedRoom.entranceGridPos = new Vector2Int(corridor.xMin + corridorWidth / 2, newRoom.yMax - 1);
                newGeneratedRoom.entranceDir = 0;
            }
            else // 복도가 왼쪽 → 새 방 동쪽이 입구
            {
                newGeneratedRoom.entranceGridPos = new Vector2Int(newRoom.xMax - 1, corridor.yMin + corridorWidth / 2);
                newGeneratedRoom.entranceDir = 1;
            }

            DrawRoom(newGeneratedRoom);

            generatedRooms.Add(newGeneratedRoom);
            return true;
        }
        return false;
    }

    // 복도의 시작 단면이 실제 바닥 타일(mapData==1)과 맞닿아 있는지 검증합니다.
    // ㄱ자/ㄴ자 방의 파인 공허 구역에서 복도가 시작되는 것을 원천 차단합니다.
    bool CorridorConnectsToFloor(RectInt corridor, int dir)
    {
        if (dir == 0) // 위로 뻗는 복도: 시작 아랫면이 바닥과 맞닿아야 함
        {
            for (int x = corridor.xMin; x < corridor.xMax; x++)
            {
                int checkY = corridor.yMin - 1;
                if (checkY >= 0 && mapData[x, checkY] == 1) return true;
            }
        }
        else if (dir == 1) // 오른쪽으로 뻗는 복도: 시작 왼면이 바닥과 맞닿아야 함
        {
            for (int y = corridor.yMin; y < corridor.yMax; y++)
            {
                int checkX = corridor.xMin - 1;
                if (checkX >= 0 && mapData[checkX, y] == 1) return true;
            }
        }
        else if (dir == 2) // 아래로 뻗는 복도: 시작 윗면이 바닥과 맞닿아야 함
        {
            for (int x = corridor.xMin; x < corridor.xMax; x++)
            {
                int checkY = corridor.yMax;
                if (checkY < mapHeight && mapData[x, checkY] == 1) return true;
            }
        }
        else if (dir == 3) // 왼쪽으로 뻗는 복도: 시작 오른면이 바닥과 맞닿아야 함
        {
            for (int y = corridor.yMin; y < corridor.yMax; y++)
            {
                int checkX = corridor.xMax;
                if (checkX < mapWidth && mapData[checkX, y] == 1) return true;
            }
        }
        return false; // 한 칸도 바닥에 맞닿지 않으면 연결 불가
    }

    int GetSafeOffsetX(RoomShape shape, int w, int dir)
    {
        int min = 0;
        int max = w - 1;

        if (shape == RoomShape.Octagon)
        {
            min = 2;
            max = w - 3;
        }
        else if (shape == RoomShape.Cross)
        {
            min = 3;
            max = w - 4;
        }
        int margin = 1; // 복도가 방의 모서리에 딱 붙지 않도록 최소 1칸의 벽(여백)을 확보

        // 복도가 파인 공간을 침범하거나 너무 모서리에 붙지 않도록 안전 마진 계산
        int minOffset = min + margin + corridorWidth / 2;
        int maxOffset = max - margin - corridorWidth + 1 + corridorWidth / 2;
        
        // 여백을 주기엔 방의 단면이 너무 좁다면 -1 반환 (TryGenerateBranch에서 취소됨)
        if (minOffset > maxOffset) 
        {
            return -1;
        }

        return Random.Range(minOffset, maxOffset + 1);
    }

    int GetSafeOffsetY(RoomShape shape, int h, int dir)
    {
        int min = 0;
        int max = h - 1;

        if (shape == RoomShape.Octagon)
        {
            min = 2;
            max = h - 3;
        }
        else if (shape == RoomShape.Cross)
        {
            min = 3;
            max = h - 4;
        }
        int margin = 1; // 복도가 방의 모서리에 딱 붙지 않도록 최소 1칸의 벽(여백)을 확보

        int minOffset = min + margin + corridorWidth / 2;
        int maxOffset = max - margin - corridorWidth + 1 + corridorWidth / 2;
        
        // 여백을 주기엔 방의 단면이 너무 좁다면 -1 반환 (TryGenerateBranch에서 취소됨)
        if (minOffset > maxOffset) 
        {
            return -1;
        }

        return Random.Range(minOffset, maxOffset + 1);
    }

    bool IsSpaceValid(RectInt rect, RectInt ignoreRect, int padding = 1)
    {
        // 맵 이탈 방지
        if (rect.xMin < padding + 1 || rect.xMax >= mapWidth - (padding + 1) || rect.yMin < padding + 1 || rect.yMax >= mapHeight - (padding + 1)) return false;

        // 패딩(여백)을 포함하여 겹침 검사
        for (int x = rect.xMin - padding; x < rect.xMax + padding; x++)
        {
            for (int y = rect.yMin - padding; y < rect.yMax + padding; y++)
            {
                // 출발지 방 영역이라면 겹쳐도 정상으로 간주
                if (ignoreRect.Contains(new Vector2Int(x, y))) continue;

                if (mapData[x, y] != 0) return false;
            }
        }
        return true;
    }

    void WriteRect(RectInt rect, int val)
    {
        for (int x = rect.xMin; x < rect.xMax; x++)
            for (int y = rect.yMin; y < rect.yMax; y++)
                mapData[x, y] = val;
    }

    void DrawRoom(Room room)
    {
        int w = room.bounds.width;
        int h = room.bounds.height;

        for (int x = room.bounds.xMin; x < room.bounds.xMax; x++)
        {
            for (int y = room.bounds.yMin; y < room.bounds.yMax; y++)
            {
                // 현재 그리는 타일이 방의 왼쪽 아래 끝점으로부터 몇 칸 떨어져 있는지 (로컬 좌표)
                int localX = x - room.bounds.xMin;
                int localY = y - room.bounds.yMin;
                
                bool drawFloor = true;

                switch (room.shape)
                {
                    case RoomShape.Rectangle:
                        // 기본 사각형 (전부 칠함)
                        break; 

                    case RoomShape.Octagon:
                        // 팔각형: 네 꼭짓점 모서리 2칸씩을 깎아냄
                        if ((localX < 2 && localY < 2) || (localX > w - 3 && localY < 2) ||
                            (localX < 2 && localY > h - 3) || (localX > w - 3 && localY > h - 3))
                        {
                            drawFloor = false;
                        }
                        break;

                    case RoomShape.Cross:
                        // 십자가 방: 모서리를 깊게 파냄 (통로 연결을 위해 최소 8x8 이상일 때만)
                        if (w >= 8 && h >= 8)
                        {
                            int carveSize = 3;
                            if ((localX < carveSize && localY < carveSize) || 
                                (localX > w - carveSize - 1 && localY < carveSize) ||
                                (localX < carveSize && localY > h - carveSize - 1) || 
                                (localX > w - carveSize - 1 && localY > h - carveSize - 1))
                            {
                                drawFloor = false;
                            }
                        }
                        break;

case RoomShape.Parthenon:
                        // 파르테논 신전형: 방의 4등분 위치에 세로 기둥(받침/몸통/머리) 4개 배치
                        // 기둥 받침 1칸을 값 4로 표시하고, 실제 기둥 스프라이트는 렌더러가 세로로 그린다.
                        if (w >= 10 && h >= 10)
                        {
                            int offsetX = w / 4;
                            int offsetY = h / 4;

                            // 4등분 위치에 기둥 받침(1칸)을 둔다
                            bool isPillarBase =
                                (localX == offsetX && localY == offsetY) ||
                                (localX == w - offsetX - 1 && localY == offsetY) ||
                                (localX == offsetX && localY == h - offsetY - 1) ||
                                (localX == w - offsetX - 1 && localY == h - offsetY - 1);

                            if (isPillarBase)
                            {
                                mapData[x, y] = 4; // 기둥 받침 마킹 (렌더러가 바닥+기둥을 처리)
                                drawFloor = false; // 아래에서 바닥(1)으로 덮어쓰지 않도록
                            }
                        }
                        break;

                }

                // 깎이지 않은 부분만 바닥으로 칠함
                if (drawFloor)
                {
                    mapData[x, y] = 1;
                }
            }
        }
    }



}