using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class MinimapController : MonoBehaviour
{
    [Header("미니맵 UI 설정")]
    [Tooltip("미니맵 텍스처를 표시할 UI")] public RawImage minimapUI;
    [Tooltip("미니맵에 표시될 플레이어 위치 아이콘")] public RectTransform playerMinimapIcon;
    
    [Header("탐험 설정")]
    [Tooltip("플레이어 주변을 밝힐 시야 반경 (타일 수)")] public int sightRadius = 3;
    [Tooltip("테스트용: 미니맵 전체 밝히기")] public bool revealFullMinimap = false;

    // 내부 데이터
    private int mapWidth, mapHeight;
    private int[,] mapData;
    private bool[,] isExplored;
    private List<Room> rooms; // 방 정보 (모양, 위치 등)
    private Transform playerTransform;
    
    private Texture2D minimapTexture;
    private Vector2Int lastPlayerPos = new Vector2Int(-1, -1);
    private bool lastRevealState = false;
    
    private int originalSightRadius;
    private Coroutine blindCoroutine;

    // 방 진입 시 그 방과 연결된 복도를 이미 다 밝혔는지 기록(매 이동마다 BFS 재실행 방지)
    private readonly HashSet<Room> corridorsRevealedRooms = new HashSet<Room>();

    // 🌟 던전 생성기(DungeonGenerator)가 던전을 다 만들고 나서 이 함수를 호출해 줄 겁니다.
    public void InitializeMinimap(int width, int height, int[,] data, List<Room> generatedRooms, Transform player)
    {
        originalSightRadius = sightRadius;
        mapWidth = width;
        mapHeight = height;
        mapData = data;
        rooms = generatedRooms;
        playerTransform = player;

        isExplored = new bool[mapWidth, mapHeight];
        minimapTexture = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, false);
        minimapTexture.filterMode = FilterMode.Point; // 픽셀아트 느낌 살리기
        minimapUI.texture = minimapTexture;

        RefreshMinimap();
    }

    void Update()
    {
        // 초기화되지 않았거나 플레이어가 없으면 작동하지 않음
        if (playerTransform == null || mapData == null) return;

        // 전체 밝히기 토글 감지
        if (revealFullMinimap != lastRevealState)
        {
            lastRevealState = revealFullMinimap;
            RefreshMinimap();
        }

        // 플레이어 그리드 좌표 계산 (Tilemap 오프셋이 존재할 경우를 고려해 WorldToCell 사용)
        Vector3Int cellPos = Vector3Int.zero;
        Tilemap tilemap = GetComponent<Tilemap>();
        if (tilemap == null) tilemap = FindFirstObjectByType<Tilemap>();
        
        if (tilemap != null)
        {
            cellPos = tilemap.WorldToCell(playerTransform.position);
        }
        else
        {
            cellPos = new Vector3Int(Mathf.FloorToInt(playerTransform.position.x), Mathf.FloorToInt(playerTransform.position.y), 0);
        }
        Vector2Int currentGridPos = new Vector2Int(cellPos.x, cellPos.y);

        // 플레이어 아이콘 UI 실시간 이동
        if (playerMinimapIcon != null && minimapUI != null)
        {
            float normalizedX = (float)currentGridPos.x / mapWidth;
            float normalizedY = (float)currentGridPos.y / mapHeight;
            Rect minimapRect = minimapUI.rectTransform.rect;
            playerMinimapIcon.anchoredPosition = new Vector2(
                minimapRect.width * normalizedX - (minimapRect.width * minimapUI.rectTransform.pivot.x),
                minimapRect.height * normalizedY - (minimapRect.height * minimapUI.rectTransform.pivot.y)
            );
        }

        // 플레이어가 새로운 칸으로 이동했을 때만 시야 업데이트
        if (currentGridPos != lastPlayerPos)
        {
            lastPlayerPos = currentGridPos;
            UpdateExploration(currentGridPos);
        }
    }

    // 시야 밝히기 로직 (기존 던전 제너레이터에 있던 코드 그대로 이사)
    private void UpdateExploration(Vector2Int playerPos)
    {
        bool changed = false;

        // 1. 방 전체 밝히기 (플레이어가 방 안에 있으면 그 방을 전부 밝힘)
        foreach (Room room in rooms)
        {
            if (room.bounds.Contains(playerPos))
            {
                // 방 경계보다 1칸씩 더 넓게 범위를 잡아서 바깥쪽 벽과 복도 입구를 밝힙니다.
                for (int x = room.bounds.xMin - 1; x <= room.bounds.xMax; x++)
                {
                    for (int y = room.bounds.yMin - 1; y <= room.bounds.yMax; y++)
                    {
                        // 맵 범위를 벗어나지 않도록 안전 검사
                        if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
                        {
                            if (mapData[x, y] != 0 && !isExplored[x, y])
                            {
                                isExplored[x, y] = true;
                                changed = true;
                            }
                        }
                    }
                }

                // 방과 연결된 복도를 전부 밝힘(방 진입 시 1회). 다른 방은 밝히지 않음.
                if (corridorsRevealedRooms.Add(room))
                {
                    if (RevealConnectedCorridors(room)) changed = true;
                }
                break;
            }
        }

        // 2. 플레이어 주변 원형 반경 밝히기
        for (int x = playerPos.x - sightRadius; x <= playerPos.x + sightRadius; x++)
        {
            for (int y = playerPos.y - sightRadius; y <= playerPos.y + sightRadius; y++)
            {
                if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
                {
                    if (Vector2.Distance(playerPos, new Vector2(x, y)) <= sightRadius)
                    {
                        if (mapData[x, y] != 0 && !isExplored[x, y])
                        {
                            isExplored[x, y] = true;
                            changed = true;
                        }
                    }
                }
            }
        }

        if (changed) RefreshMinimap();
    }

    // 방과 연결된 복도 칸(mapData==1, 어떤 방에도 안 속함)을 BFS로 전부 밝힘.
    // 방 경계 바로 바깥의 복도 입구를 시드로 확장하며, 다른 방을 만나면 그 방은 밝히지 않고 정지.
    // 반환: 새로 밝혀진 칸이 있으면 true.
    private bool RevealConnectedCorridors(Room room)
    {
        var queue   = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        bool changed = false;

        // 방 경계(±1) 주변에서 복도 입구를 시드로 넣음
        for (int x = room.bounds.xMin - 1; x <= room.bounds.xMax; x++)
            for (int y = room.bounds.yMin - 1; y <= room.bounds.yMax; y++)
                TryEnqueueCorridor(x, y, queue, visited);

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            if (!isExplored[c.x, c.y]) { isExplored[c.x, c.y] = true; changed = true; }

            TryEnqueueCorridor(c.x + 1, c.y, queue, visited);
            TryEnqueueCorridor(c.x - 1, c.y, queue, visited);
            TryEnqueueCorridor(c.x, c.y + 1, queue, visited);
            TryEnqueueCorridor(c.x, c.y - 1, queue, visited);
        }
        return changed;
    }

    // 복도 바닥(mapData==1이면서 어떤 방에도 속하지 않는 칸)만 큐에 추가. 방/벽/맵 밖은 무시.
    private void TryEnqueueCorridor(int x, int y, Queue<Vector2Int> queue, HashSet<Vector2Int> visited)
    {
        if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return;
        if (mapData[x, y] != 1) return;      // 바닥이 아니면(벽/빈공간) 제외
        if (IsInsideAnyRoom(x, y)) return;   // 방 칸이면 확장하지 않음(복도만 대상)

        var p = new Vector2Int(x, y);
        if (visited.Add(p)) queue.Enqueue(p);
    }

    private bool IsInsideAnyRoom(int x, int y)
    {
        var p = new Vector2Int(x, y);
        foreach (Room r in rooms)
            if (r.bounds.Contains(p)) return true;
        return false;
    }

    // 미니맵 텍스처 다시 그리기 (기존 코드 이사)
    private void RefreshMinimap()
    {
        Color[] pixels = new Color[mapWidth * mapHeight];
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int index = y * mapWidth + x;
                
                if (revealFullMinimap || isExplored[x, y])
                {
                    int tileData = mapData[x, y];
                    if (tileData == 3)
                    {
                        pixels[index] = new Color32(0x78, 0x4c, 0x28, 0xff); // 벽 #784c28
                    }
                    else if (tileData == 1) // 방 또는 복도
                    {
                        Color pixelColor = new Color32(0x80, 0x08, 0x0d, 0xff); // 기본 복도 #80080d (어떤 방에도 속하지 않으면 복도)
                        foreach (Room room in rooms)
                        {
                            if (room.bounds.Contains(new Vector2Int(x, y)))
                            {
                                pixelColor = new Color32(0xca, 0xbf, 0xa9, 0xff); // 기본 방 바닥 #cabfa9
                                if (room.type == RoomType.Start) pixelColor = new Color32(0x10, 0x87, 0x13, 0xff); // 시작방 #108713
                                else if (room.type == RoomType.Shop) pixelColor = Color.yellow;
                                else if (room.type == RoomType.Elite) pixelColor = new Color(0.86f, 0.08f, 0.24f);
                                else if (room.type == RoomType.MiniBoss) pixelColor = new Color(0.5f, 0f, 0.5f);
                                else if (room.type == RoomType.Boss) pixelColor = new Color(0.55f, 0f, 0f);
                                break;
                            }
                        }
                        pixels[index] = pixelColor;
                    }
                    else
                    {
                        pixels[index] = Color.black; // 빈 공간
                    }
                }
                else
                {
                    pixels[index] = Color.black; // 안 밝혀진 곳은 검은색
                }
            }
        }
        minimapTexture.SetPixels(pixels);
        minimapTexture.Apply();
    }

    // 외부(함정 등)에서 시야를 일정 시간 차단할 때 호출
    public void SetBlind(float duration)
    {
        if (blindCoroutine != null) StopCoroutine(blindCoroutine);
        blindCoroutine = StartCoroutine(BlindRoutine(duration));
    }

    private System.Collections.IEnumerator BlindRoutine(float duration)
    {
        if (minimapUI != null) minimapUI.gameObject.SetActive(false);
        sightRadius = 0;
        
        yield return new WaitForSeconds(duration);
        
        sightRadius = originalSightRadius;
        if (minimapUI != null) minimapUI.gameObject.SetActive(true);
        
        if (playerTransform != null)
        {
            UpdateExploration(new Vector2Int(Mathf.FloorToInt(playerTransform.position.x), Mathf.FloorToInt(playerTransform.position.y)));
        }
    }
}