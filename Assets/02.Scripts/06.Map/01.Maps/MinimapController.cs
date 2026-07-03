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

    // 현재 방 통로 위치 표시(빨간 삼각형) — 독립 기능. false로 끄기 가능.
    private const bool ENABLE_EXIT_DOTS = true;
    private const int  EXIT_TRI_DEPTH   = 9; // 삼각형 깊이(타일) — 복도(바깥) 방향으로 뻗는 길이(3→9, 3배)
    private static readonly Color ExitDotColor = new Color(1f, 0.15f, 0.15f); // 밝은 빨강
    private int _currentRoomIndex = -1;
    private List<Vector2[]> _currentExitTris = new List<Vector2[]>(); // 각 원소 = [밑변끝a, 밑변끝b, 꼭짓점]

    // 방문한 적 있는 방들(누적) — 이 방들의 출구 중 '아직 안 밝혀진(안개 속)' 복도만 표시한다.
    private readonly HashSet<int> _visitedRooms = new HashSet<int>();

    // 클리어된 방 표시 — 클리어 시 미니맵 색을 어두운 회청으로 바꿔 "끝난 방"을 한눈에 구분
    private static readonly Color ClearedRoomColor = new Color(0.35f, 0.35f, 0.40f);
    private readonly HashSet<RectInt> _clearedRoomBounds = new HashSet<RectInt>();          // RoomController.roomBounds == Room.bounds 로 매칭
    private readonly HashSet<RoomController> _subscribedClearRooms = new HashSet<RoomController>();
    private float _clearScanTimer;

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

        // 층 전환(던전 재생성) 시 통로 표기 누적 상태 초기화
        _visitedRooms.Clear();
        _currentExitTris.Clear();
        _currentRoomIndex = -1;
        lastPlayerPos = new Vector2Int(-1, -1);

        // 클리어 방 표시 초기화 (RoomController 구독은 Update에서 지연 수행 — 생성 순서 무관)
        _clearedRoomBounds.Clear();
        _subscribedClearRooms.Clear();
        _clearScanTimer = 1f; // 다음 Update에서 즉시 1회 스캔

        minimapTexture = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, false);
        minimapTexture.filterMode = FilterMode.Point; // 픽셀아트 느낌 살리기
        minimapUI.texture = minimapTexture;

        RefreshMinimap();
    }

    void Update()
    {
        // 초기화되지 않았거나 플레이어가 없으면 작동하지 않음
        if (playerTransform == null || mapData == null) return;

        // 1초 주기: 새로 생긴 RoomController의 클리어 이벤트 구독(던전 생성 순서와 무관하게 지연 구독)
        _clearScanTimer += Time.deltaTime;
        if (_clearScanTimer >= 1f)
        {
            _clearScanTimer = 0f;
            SubscribeRoomClears();
        }

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

        // 통로 표기 갱신: 방 진입/복도 이동 추적 후, 방문한 방들의 '안 지나간' 출구만 표시
        if (ENABLE_EXIT_DOTS) UpdateExitTriangles(currentGridPos);
    }

    // 방문한 방을 누적하고, 밟은 복도 셀을 기록한 뒤,
    // 방문한 모든 방의 출구 중 '아직 지나가지 않은' 통로만 삼각형으로 다시 그린다.
    private void UpdateExitTriangles(Vector2Int pos)
    {
        if (rooms == null) return;

        int idx = -1;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].bounds.Contains(pos)) { idx = i; break; }
        }

        if (idx >= 0) _visitedRooms.Add(idx); // 방문 방 누적
        _currentRoomIndex = idx;

        // 이동/탐사에 따라 밝혀진 복도가 바뀌므로 방문 방들의 출구를 매 이동마다 재계산.
        _currentExitTris.Clear();
        foreach (int ri in _visitedRooms)
            _currentExitTris.AddRange(ComputeExitTriangles(rooms[ri]));
        RefreshMinimap();
    }

    // 방↔복도 경계선을 밑변으로, 복도(바깥) 방향으로 꼭짓점이 뻗는 삼각형 목록을 만든다.
    // 각 변(상/하/좌/우)에서 복도와 맞닿은 연속 구간마다 삼각형 1개.
    private List<Vector2[]> ComputeExitTriangles(Room room)
    {
        var tris = new List<Vector2[]>();
        RectInt b = room.bounds;
        int x0 = b.xMin, x1 = b.xMax - 1, y0 = b.yMin, y1 = b.yMax - 1; // 방 내부 셀 범위(포함)
        int d = EXIT_TRI_DEPTH;

        bool Floor(int x, int y) =>
            x >= 0 && x < mapWidth && y >= 0 && y < mapHeight && mapData[x, y] == 1;

        // 입구(depth1) 바깥(depth2~d)으로 뻗는 복도 중 '아직 안 밝혀진(안개)' 셀이 있으면 true → 표기 유지.
        // 방 입장 시 입구 1칸은 자동으로 밝혀지므로 depth2부터 검사한다.
        bool Hidden(int ex, int ey, int dx, int dy)
        {
            for (int k = 1; k < d; k++)
            {
                int x = ex + dx * k, y = ey + dy * k;
                if (Floor(x, y) && !isExplored[x, y]) return true;
            }
            return false;
        }
        bool HiddenV(int cx, int dx, int s, int e) { for (int y = s; y <= e; y++) if (Hidden(cx, y, dx, 0)) return true; return false; }
        bool HiddenH(int cy, int dy, int s, int e) { for (int x = s; x <= e; x++) if (Hidden(x, cy, 0, dy)) return true; return false; }

        const float baseScale = 0.8f; // 밑변 크기(현재의 80%)
        const float baseOut   = 2f;   // 밑변을 방 경계에서 복도 쪽으로 2칸 밀기(+ 방 그리드 클리핑 병행)

        // 오른쪽(East): 경계 x=b.xMax, 복도셀 (b.xMax, y) — 꼭짓점 +x
        ScanRuns(y0, y1, y => Floor(b.xMax, y), (s, e) =>
        {
            if (!HiddenV(b.xMax, 1, s, e)) return; // 이미 밝혀진 복도면 표기 안 함
            float mid = (s + e + 1) * 0.5f, half = (e + 1 - s) * baseScale, bx = b.xMax + baseOut;
            tris.Add(new[] { new Vector2(bx, mid - half), new Vector2(bx, mid + half), new Vector2(b.xMax + d, mid) });
        });
        // 왼쪽(West): 경계 x=b.xMin, 복도셀 (b.xMin-1, y) — 꼭짓점 -x
        ScanRuns(y0, y1, y => Floor(b.xMin - 1, y), (s, e) =>
        {
            if (!HiddenV(b.xMin - 1, -1, s, e)) return;
            float mid = (s + e + 1) * 0.5f, half = (e + 1 - s) * baseScale, bx = b.xMin - baseOut;
            tris.Add(new[] { new Vector2(bx, mid - half), new Vector2(bx, mid + half), new Vector2(b.xMin - d, mid) });
        });
        // 위(North): 경계 y=b.yMax, 복도셀 (x, b.yMax) — 꼭짓점 +y
        ScanRuns(x0, x1, x => Floor(x, b.yMax), (s, e) =>
        {
            if (!HiddenH(b.yMax, 1, s, e)) return;
            float mid = (s + e + 1) * 0.5f, half = (e + 1 - s) * baseScale, by = b.yMax + baseOut;
            tris.Add(new[] { new Vector2(mid - half, by), new Vector2(mid + half, by), new Vector2(mid, b.yMax + d) });
        });
        // 아래(South): 경계 y=b.yMin, 복도셀 (x, b.yMin-1) — 꼭짓점 -y
        ScanRuns(x0, x1, x => Floor(x, b.yMin - 1), (s, e) =>
        {
            if (!HiddenH(b.yMin - 1, -1, s, e)) return;
            float mid = (s + e + 1) * 0.5f, half = (e + 1 - s) * baseScale, by = b.yMin - baseOut;
            tris.Add(new[] { new Vector2(mid - half, by), new Vector2(mid + half, by), new Vector2(mid, b.yMin - d) });
        });

        return tris;
    }

    // [from..to] 범위에서 pred가 참인 연속 구간마다 onRun(start,end) 호출
    private void ScanRuns(int from, int to, System.Func<int, bool> pred, System.Action<int, int> onRun)
    {
        int runStart = -1;
        for (int i = from; i <= to; i++)
        {
            bool ok = pred(i);
            if (ok && runStart < 0) runStart = i;
            else if (!ok && runStart >= 0) { onRun(runStart, i - 1); runStart = -1; }
        }
        if (runStart >= 0) onRun(runStart, to);
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

    /// <summary>씬의 RoomController들을 스캔해 클리어 이벤트를 구독한다.
    /// 클리어 순간 해당 방 bounds를 기록하고 미니맵을 다시 그려 색을 바꾼다.</summary>
    private void SubscribeRoomClears()
    {
        _subscribedClearRooms.RemoveWhere(rc => rc == null); // 파괴된(이전 층) 방 정리

        foreach (RoomController rc in FindObjectsByType<RoomController>(FindObjectsSortMode.None))
        {
            if (rc == null || _subscribedClearRooms.Contains(rc)) continue;
            _subscribedClearRooms.Add(rc);

            // 구독 전에 이미 클리어된 방(스캔 지연 사이)도 반영
            if (rc.IsCleared && _clearedRoomBounds.Add(rc.roomBounds)) RefreshMinimap();

            RoomController room = rc; // 클로저 캡처
            if (room.OnRoomCleared == null)
                room.OnRoomCleared = new UnityEngine.Events.UnityEvent();
            room.OnRoomCleared.AddListener(() =>
            {
                if (_clearedRoomBounds.Add(room.roomBounds)) RefreshMinimap();
            });
        }
    }

    // 방 색: 클리어된 방은 타입과 무관하게 어두운 회청("끝난 방"), 아니면 타입별 색
    private Color RoomColorFor(Room room)
    {
        return _clearedRoomBounds.Contains(room.bounds) ? ClearedRoomColor : RoomColor(room.type);
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
                                pixelColor = RoomColorFor(room);
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
        // 방의 통로 위치를 빨간 삼각형으로 덧칠(안개와 무관하게 항상 표시 — 어두워도 통로 방향 확인)
        if (ENABLE_EXIT_DOTS && _currentExitTris != null)
        {
            foreach (var t in _currentExitTris)
                FillTriangle(pixels, t[0], t[1], t[2], ExitDotColor);

            // 삼각형이 방 안으로 삐져나오지 않도록, 방 셀을 삼각형 위에 다시 칠해 클리핑한다.
            foreach (Room room in rooms)
            {
                Color rc = RoomColorFor(room);
                RectInt bnd = room.bounds;
                for (int yy = bnd.yMin; yy < bnd.yMax; yy++)
                    for (int xx = bnd.xMin; xx < bnd.xMax; xx++)
                        if (xx >= 0 && xx < mapWidth && yy >= 0 && yy < mapHeight
                            && (revealFullMinimap || isExplored[xx, yy]) && mapData[xx, yy] == 1)
                            pixels[yy * mapWidth + xx] = rc;
            }
        }

        minimapTexture.SetPixels(pixels);
        minimapTexture.Apply();
    }

    // 방 타입별 미니맵 색상 (타일 렌더·삼각형 클리핑 공통 사용)
    private static Color RoomColor(RoomType type)
    {
        switch (type)
        {
            case RoomType.Start:    return Color.green;
            case RoomType.Shop:     return Color.yellow;
            case RoomType.Elite:    return new Color(0.86f, 0.08f, 0.24f);
            case RoomType.MiniBoss: return new Color(0.5f, 0f, 0.5f);
            case RoomType.Boss:     return new Color(0.55f, 0f, 0f);
            default:                return Color.blue; // 일반 방
        }
    }

    // 텍스처(픽셀 버퍼)에 삼각형을 채운다. 좌표는 타일 단위(픽셀=타일).
    private void FillTriangle(Color[] px, Vector2 a, Vector2 b, Vector2 c, Color col)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), 0, mapWidth - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt (Mathf.Max(a.x, Mathf.Max(b.x, c.x))), 0, mapWidth - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), 0, mapHeight - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt (Mathf.Max(a.y, Mathf.Max(b.y, c.y))), 0, mapHeight - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f); // 픽셀 중심
                if (PointInTriangle(p, a, b, c)) px[y * mapWidth + x] = col;
            }
        }
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b);
        float d2 = Cross(p, b, c);
        float d3 = Cross(p, c, a);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos); // 세 변에 대해 같은 쪽이면 내부
    }

    private static float Cross(Vector2 p, Vector2 a, Vector2 b) =>
        (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

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