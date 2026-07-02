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

    // 현재 방 통로 입구 표시(빨간 점) — 독립 기능. false로 끄기 가능.
    private const bool ENABLE_EXIT_DOTS = true;
    private static readonly Color ExitDotColor = new Color(1f, 0.15f, 0.15f); // 밝은 빨강
    private int _currentRoomIndex = -1;
    private List<Vector2Int> _currentExitCells = new List<Vector2Int>();

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

        // 현재 방이 바뀌면 통로 입구(빨간 점) 갱신
        if (ENABLE_EXIT_DOTS) UpdateCurrentRoomExits(currentGridPos);
    }

    // 플레이어가 속한 방을 찾아, 방이 바뀌면 그 방의 통로 입구 셀을 계산하고 미니맵을 다시 그린다.
    // 방 밖(통로)이면 표시를 비운다.
    private void UpdateCurrentRoomExits(Vector2Int pos)
    {
        if (rooms == null) return;

        int idx = -1;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].bounds.Contains(pos)) { idx = i; break; }
        }

        if (idx == _currentRoomIndex) return; // 변화 없음
        _currentRoomIndex = idx;
        _currentExitCells = (idx >= 0) ? ComputeExitCells(rooms[idx]) : new List<Vector2Int>();
        RefreshMinimap();
    }

    // 방 테두리 바로 바깥 한 겹에서 바닥(통로)인 셀 = 통로 입구. 미니맵에 빨간 점으로 찍는다.
    private List<Vector2Int> ComputeExitCells(Room room)
    {
        var list = new List<Vector2Int>();
        RectInt b = room.bounds;
        for (int x = b.xMin - 1; x <= b.xMax; x++)
        {
            for (int y = b.yMin - 1; y <= b.yMax; y++)
            {
                var cell = new Vector2Int(x, y);
                if (b.Contains(cell)) continue;                 // 방 내부는 제외(테두리 밖만)
                if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) continue;
                if (mapData[x, y] == 1) list.Add(cell);         // 바닥(통로) → 입구
            }
        }
        return list;
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
                        pixels[index] = Color.gray; // 벽
                    }
                    else if (tileData == 1) // 방 또는 복도
                    {
                        Color pixelColor = Color.red; // 기본 복도 색상 (어떤 방에도 속하지 않으면 복도)
                        foreach (Room room in rooms)
                        {
                            if (room.bounds.Contains(new Vector2Int(x, y)))
                            {
                                pixelColor = Color.blue; // 기본 방
                                if (room.type == RoomType.Start) pixelColor = Color.green;
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
        // 현재 방의 통로 입구를 빨간 점으로 덧칠(안개와 무관하게 항상 표시 — 어두워도 통로 방향 확인)
        if (ENABLE_EXIT_DOTS && _currentExitCells != null)
        {
            foreach (var c in _currentExitCells)
            {
                if (c.x >= 0 && c.x < mapWidth && c.y >= 0 && c.y < mapHeight)
                    pixels[c.y * mapWidth + c.x] = ExitDotColor;
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