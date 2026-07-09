using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class DungeonPopulator : MonoBehaviour
{
    [Header("함정(Trap) 설정")]
    [Tooltip("설치할 함정 프리팹")] public GameObject trapPrefab;
    [Tooltip("방 1개당 생성할 최소 함정 수")] public int minTrapsPerRoom = 0;
    [Tooltip("방 1개당 생성할 최대 함정 수")] public int maxTrapsPerRoom = 2;

    [Header("계단 / 몬스터")]
    public GameObject stairPrefab;
    public GameObject[] monsterPrefabs;

    [Header("문(Door) 비주얼")]
    [Tooltip("닫힌 문 3x2 스프라이트: [0]좌상 [1]중상 [2]우상 [3]좌하 [4]중하 [5]우하")]
    public Sprite[] closedDoorSprites = new Sprite[6];
    [Tooltip("열린 문 3x2 스프라이트: [0]좌상 [1]중상 [2]우상 [3]좌하 [4]중하 [5]우하")]
    public Sprite[] openDoorSprites = new Sprite[6];
    [Tooltip("문 렌더링 순서 (타일맵보다 높게 설정)")]
    public int doorSortingOrder = 10;

    // DungeonGenerator에서 주입 (문 콜라이더 크기 계산용)
    [HideInInspector] public int corridorWidth = 2;

    public void AssignSpecialRooms(int[,] mapData, List<Room> generatedRooms, int currentFloor)
    {
        if (generatedRooms.Count < 3) return;

        List<Room> available = new List<Room>(generatedRooms);

        int startIdx = Random.Range(0, available.Count);
        Room startRoom = available[startIdx];
        startRoom.type = RoomType.Start;
        available.RemoveAt(startIdx);

        List<Room> deadEndRooms = new List<Room>();
        foreach (Room r in available)
        {
            if (CountEntrances(r, mapData) == 1)
                deadEndRooms.Add(r);
        }

        Vector2 startCenter = startRoom.bounds.center;
        Room bossRoom = null;

        if (deadEndRooms.Count > 0)
        {
            deadEndRooms.Sort((a, b) =>
                Vector2.Distance(b.bounds.center, startCenter).CompareTo(Vector2.Distance(a.bounds.center, startCenter))
            );
            bossRoom = deadEndRooms[0];
        }
        else
        {
            available.Sort((a, b) =>
                Vector2.Distance(b.bounds.center, startCenter).CompareTo(Vector2.Distance(a.bounds.center, startCenter))
            );
            bossRoom = available[0];
        }

        if (currentFloor == 5) bossRoom.type = RoomType.Boss;
        else if (currentFloor == 2 || currentFloor == 4) bossRoom.type = RoomType.MiniBoss;
        else bossRoom.type = RoomType.Elite;

        available.Remove(bossRoom);
    }

    private int CountEntrances(Room room, int[,] mapData)
    {
        int entrances = 0;
        int w = mapData.GetLength(0);
        int h = mapData.GetLength(1);

        for (int x = room.bounds.xMin; x < room.bounds.xMax; x++)
        {
            if (room.bounds.yMax < h && mapData[x, room.bounds.yMax] == 1)
            {
                if (x == room.bounds.xMin || mapData[x - 1, room.bounds.yMax] != 1) entrances++;
            }
            if (room.bounds.yMin - 1 >= 0 && mapData[x, room.bounds.yMin - 1] == 1)
            {
                if (x == room.bounds.xMin || mapData[x - 1, room.bounds.yMin - 1] != 1) entrances++;
            }
        }
        for (int y = room.bounds.yMin; y < room.bounds.yMax; y++)
        {
            if (room.bounds.xMax < w && mapData[room.bounds.xMax, y] == 1)
            {
                if (y == room.bounds.yMin || mapData[room.bounds.xMax, y - 1] != 1) entrances++;
            }
            if (room.bounds.xMin - 1 >= 0 && mapData[room.bounds.xMin - 1, y] == 1)
            {
                if (y == room.bounds.yMin || mapData[room.bounds.xMin - 1, y - 1] != 1) entrances++;
            }
        }
        return entrances;
    }

    public void GenerateTraps(int[,] mapData, List<Room> generatedRooms, Tilemap floorTilemap = null)
    {
        if (trapPrefab == null) return;

        foreach (Room room in generatedRooms)
        {
            // 시작방 + 보스급 특수방(엘리트/중간보스/보스 = 층별 '보스방')에는 함정을 두지 않는다.
            if (room.type == RoomType.Start ||
                room.type == RoomType.Elite ||
                room.type == RoomType.MiniBoss ||
                room.type == RoomType.Boss) continue;

            int trapCount = Random.Range(minTrapsPerRoom, maxTrapsPerRoom + 1);
            if (trapCount == 0) continue;

            HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();
            int maxAttempts = trapCount * 5;
            int attempts = 0;
            int spawnedCount = 0;

            while (spawnedCount < trapCount && attempts < maxAttempts)
            {
                attempts++;

                int randomX = Random.Range(room.bounds.xMin + 1, room.bounds.xMax - 1);
                int randomY = Random.Range(room.bounds.yMin + 1, room.bounds.yMax - 1);
                Vector2Int spawnPos = new Vector2Int(randomX, randomY);

                if (mapData[randomX, randomY] == 1 && !occupiedPositions.Contains(spawnPos))
                {
                    Vector3 localPos = new Vector3(randomX + 0.5f, randomY + 0.5f, 0f);
                    Vector3 worldPos = floorTilemap != null
                        ? floorTilemap.transform.TransformPoint(localPos)
                        : localPos;

                    GameObject trapObj = Instantiate(trapPrefab, worldPos, Quaternion.identity);
                    if (floorTilemap != null)
                        trapObj.transform.SetParent(floorTilemap.transform);

                    occupiedPositions.Add(spawnPos);
                    spawnedCount++;
                }
            }
        }
    }

    // 계단 생성은 SpawnMonsters 내부에서 RoomController와 함께 처리됩니다.
    // 이 메서드는 외부 호출 호환성을 위해 유지합니다.
    public void GenerateStairs(List<Room> generatedRooms, Tilemap floorTilemap = null) { }

    public void SpawnMonsters(int[,] mapData, List<Room> generatedRooms, Tilemap floorTilemap = null)
    {
        GameObject roomsParent = new GameObject("RoomControllers");
        if (floorTilemap != null)
            roomsParent.transform.SetParent(floorTilemap.layoutGrid.transform);

        // Room → RoomController 매핑 (인접 그래프를 런타임 컨트롤러로 옮기기 위해)
        var roomToController = new Dictionary<Room, RoomController>(generatedRooms.Count);

        foreach (Room room in generatedRooms)
        {
            Vector3 localPos = new Vector3(room.bounds.center.x, room.bounds.center.y, 0f);
            Vector3 worldPos = floorTilemap != null
                ? floorTilemap.transform.TransformPoint(localPos)
                : localPos;

            GameObject roomObj = new GameObject($"Room_{room.type}");
            roomObj.transform.position = worldPos;
            roomObj.transform.SetParent(roomsParent.transform);

            BoxCollider2D col = roomObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(room.bounds.width, room.bounds.height);

            RoomController controller = roomObj.AddComponent<RoomController>();
            controller.roomType = room.type;
            controller.roomBounds = room.bounds;
            roomToController[room] = controller;

            // 특수 방: 입구에 문(Door) + 상단 중앙에 계단(Stairs) 생성
            bool isSpecial = room.type == RoomType.Elite ||
                             room.type == RoomType.MiniBoss ||
                             room.type == RoomType.Boss;

            if (isSpecial)
            {
                // --- 문 생성 ---
                if (room.entranceGridPos.x >= 0)
                {
                    Vector3 doorLocalPos = new Vector3(room.entranceGridPos.x + 0.5f, room.entranceGridPos.y + 0.5f, 0f);
                    Vector3 doorWorldPos = floorTilemap != null
                        ? floorTilemap.transform.TransformPoint(doorLocalPos)
                        : doorLocalPos;

                    GameObject doorObj = new GameObject("Door");
                    doorObj.transform.position = doorWorldPos;
                    doorObj.transform.SetParent(roomObj.transform);

                    bool isVerticalCorridor = (room.entranceDir == 0 || room.entranceDir == 2);

                    // 문 가로 칸 수 = 통로 폭 + 양옆 1칸씩 여유 → 개구부를 완전히 덮어 옆으로 못 샘
                    int doorWidthTiles = corridorWidth + 2;

                    // 차단은 콜라이더가 담당. size는 로컬 기준(width×1) → 가로 통로면 90° 회전으로 세로 벽이 됨.
                    BoxCollider2D doorCol = doorObj.AddComponent<BoxCollider2D>();
                    doorCol.size = new Vector2(doorWidthTiles, 1f);
                    doorCol.enabled = false; // 초기에는 열린 상태

                    // 문 비주얼 + 열림/닫힘 토글 (가로 통로면 아트 90° 회전해 재사용)
                    DoorController doorController = doorObj.AddComponent<DoorController>();
                    doorController.Init(closedDoorSprites, openDoorSprites, !isVerticalCorridor, doorSortingOrder, doorWidthTiles);
                    controller.door = doorController;
                }

                // --- 계단 생성 (입구 반대편 중앙, 클리어 전까지 비활성) ---
                if (stairPrefab != null)
                {
                    int stairGridX, stairGridY;
                    int cx = Mathf.RoundToInt(room.bounds.center.x);
                    int cy = Mathf.RoundToInt(room.bounds.center.y);

                    switch (room.entranceDir)
                    {
                        case 0: // 입구=북 → 계단=남쪽 중앙
                            stairGridX = cx;
                            stairGridY = room.bounds.yMin + 1;
                            break;
                        case 1: // 입구=동 → 계단=서쪽 중앙
                            stairGridX = room.bounds.xMin + 1;
                            stairGridY = cy;
                            break;
                        case 3: // 입구=서 → 계단=동쪽 중앙
                            stairGridX = room.bounds.xMax - 2;
                            stairGridY = cy;
                            break;
                        default: // 입구=남(2) 또는 미설정 → 계단=북쪽 중앙
                            stairGridX = cx;
                            stairGridY = room.bounds.yMax - 2;
                            break;
                    }

                    Vector3 stairLocalPos = new Vector3(stairGridX + 0.5f, stairGridY + 0.5f, 0f);
                    Vector3 stairWorldPos = floorTilemap != null
                        ? floorTilemap.transform.TransformPoint(stairLocalPos)
                        : stairLocalPos;

                    GameObject stairObj = Instantiate(stairPrefab, stairWorldPos, Quaternion.identity);
                    stairObj.transform.SetParent(roomObj.transform);
                    stairObj.SetActive(false); // 방 클리어 후 활성화

                    controller.stairs = stairObj;
                }
            }
        }

        // 생성된 인접 그래프(Room.connections)를 RoomController로 옮긴다 (near/far 밴드 판정용)
        foreach (var kv in roomToController)
        {
            foreach (Room neighbor in kv.Key.connections)
                if (roomToController.TryGetValue(neighbor, out RoomController nc))
                    kv.Value.connectedRooms.Add(nc);
        }
    }
}
