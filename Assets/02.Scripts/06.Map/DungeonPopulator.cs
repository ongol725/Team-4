using UnityEngine;
using System.Collections.Generic;

public class DungeonPopulator : MonoBehaviour
{
    [Header("함정(Trap) 설정")]
    [Tooltip("설치할 함정 프리팹")] public GameObject trapPrefab;
    [Tooltip("방 1개당 생성할 최소 함정 수")] public int minTrapsPerRoom = 0;
    [Tooltip("방 1개당 생성할 최대 함정 수")] public int maxTrapsPerRoom = 2;

    [Header("추가 기능 뼈대 (미구현)")]
    public GameObject stairPrefab;
    public GameObject[] monsterPrefabs;

    public void AssignSpecialRooms(int[,] mapData, List<Room> generatedRooms, int currentFloor)
    {
        if (generatedRooms.Count < 3) return;

        // 1. 특수 방을 배정할 '후보 방 목록'을 새로 만듭니다.
        List<Room> available = new List<Room>(generatedRooms);

        // 2. [무작위 스폰] 아무 방이나 랜덤으로 골라서 Start 방으로 지정합니다.
        int startIdx = Random.Range(0, available.Count);
        Room startRoom = available[startIdx];
        startRoom.type = RoomType.Start;
        available.RemoveAt(startIdx); // 다른 특수 방이 겹치지 않게 후보에서 제외

        // 3. 보스/엘리트 방 지정 (출입구가 1개이며 가장자리에 있는 방)
        List<Room> deadEndRooms = new List<Room>();
        foreach (Room r in available)
        {
            if (CountEntrances(r, mapData) == 1)
            {
                deadEndRooms.Add(r);
            }
        }

        Vector2 startCenter = startRoom.bounds.center;
        Room bossRoom = null;

        if (deadEndRooms.Count > 0)
        {
            // 입구가 1개인 방 중 시작 지점(Start Room)에서 가장 먼 방 선택
            deadEndRooms.Sort((a, b) => 
                Vector2.Distance(b.bounds.center, startCenter).CompareTo(Vector2.Distance(a.bounds.center, startCenter))
            );
            bossRoom = deadEndRooms[0];
        }
        else
        {
            // 안전장치: 입구가 1개인 방이 없다면 남은 방 중 시작 지점에서 가장 먼 방 선택
            available.Sort((a, b) => 
                Vector2.Distance(b.bounds.center, startCenter).CompareTo(Vector2.Distance(a.bounds.center, startCenter))
            );
            bossRoom = available[0];
        }

        if (currentFloor == 5) bossRoom.type = RoomType.Boss;
        else if (currentFloor == 2 || currentFloor == 4) bossRoom.type = RoomType.MiniBoss;
        else bossRoom.type = RoomType.Elite;

        available.Remove(bossRoom); // 후보에서 제외

        // 4. 남은 방들 중에서 상점(Shop)을 랜덤으로 지정합니다.
        if (available.Count > 0)
        {
            int shopIdx = Random.Range(0, available.Count);
            available[shopIdx].type = RoomType.Shop;
            available.RemoveAt(shopIdx);
        }
    }

    private int CountEntrances(Room room, int[,] mapData)
    {
        int entrances = 0;
        int w = mapData.GetLength(0);
        int h = mapData.GetLength(1);

        for (int x = room.bounds.xMin; x < room.bounds.xMax; x++) 
        {
            // Top edge
            if (room.bounds.yMax < h && mapData[x, room.bounds.yMax] == 1) {
                if (x == room.bounds.xMin || mapData[x - 1, room.bounds.yMax] != 1) entrances++;
            }
            // Bottom edge
            if (room.bounds.yMin - 1 >= 0 && mapData[x, room.bounds.yMin - 1] == 1) {
                if (x == room.bounds.xMin || mapData[x - 1, room.bounds.yMin - 1] != 1) entrances++;
            }
        }
        for (int y = room.bounds.yMin; y < room.bounds.yMax; y++) 
        {
            // Right edge
            if (room.bounds.xMax < w && mapData[room.bounds.xMax, y] == 1) {
                if (y == room.bounds.yMin || mapData[room.bounds.xMax, y - 1] != 1) entrances++;
            }
            // Left edge
            if (room.bounds.xMin - 1 >= 0 && mapData[room.bounds.xMin - 1, y] == 1) {
                if (y == room.bounds.yMin || mapData[room.bounds.xMin - 1, y - 1] != 1) entrances++;
            }
        }
        return entrances;
    }

    public void GenerateTraps(int[,] mapData, List<Room> generatedRooms)
    {
        if (trapPrefab == null) return;

        // 모든 방을 순회합니다.
        foreach (Room room in generatedRooms)
        {
            // 시작 방(Start)이나 보스 방(Boss)에는 함정을 생성하지 않으려면 여기서 예외 처리
            if (room.type == RoomType.Start || room.type == RoomType.Boss) continue;

            int trapCount = Random.Range(minTrapsPerRoom, maxTrapsPerRoom + 1);
            if (trapCount == 0) continue;

            // 이미 함정이 설치된 좌표를 기억하여 중복 설치를 막습니다.
            HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();

            // 안전장치: 방 크기보다 더 많은 함정을 설치하려고 하면 무한루프에 빠질 수 있으므로 최대 시도 횟수 제한
            int maxAttempts = trapCount * 5; 
            int attempts = 0;
            int spawnedCount = 0;

            while (spawnedCount < trapCount && attempts < maxAttempts)
            {
                attempts++;

                // 방의 테두리(벽과 닿는 부분)를 제외한 내부 영역에서 무작위 좌표 선택
                // bounds.xMin + 1 부터 bounds.xMax - 1 까지
                int randomX = Random.Range(room.bounds.xMin + 1, room.bounds.xMax - 1);
                int randomY = Random.Range(room.bounds.yMin + 1, room.bounds.yMax - 1);
                Vector2Int spawnPos = new Vector2Int(randomX, randomY);

                // 선택된 좌표가 1. 방(1)이고, 2. 다른 함정이 없다면 생성!
                if (mapData[randomX, randomY] == 1 && !occupiedPositions.Contains(spawnPos))
                {
                    // 월드 좌표는 타일맵 중앙이 기준이므로 +0.5f 씩 보정해 줍니다.
                    // (만약 함정 스프라이트의 중심축(Pivot)이 정중앙(Center)이라면 이렇게 해야 칸 중앙에 예쁘게 놓입니다.)
                    Vector3 worldPos = new Vector3(randomX + 0.5f, randomY + 0.5f, 0f);
                    
                    Instantiate(trapPrefab, worldPos, Quaternion.identity);
                    
                    occupiedPositions.Add(spawnPos);
                    spawnedCount++;
                }
            }
        }
    }

    // 추후 구현될 기능 뼈대
    public void GenerateStairs(List<Room> generatedRooms)
    {
        // TODO: Boss 방이나 특수 위치에 다음 층으로 가는 계단 배치
    }

    public void SpawnMonsters(int[,] mapData, List<Room> generatedRooms)
    {
        // 몬스터 생성 대신, 각 방의 크기에 맞는 투명한 트리거 구역(RoomController)을 맵에 생성합니다.
        // 몬스터 스폰이나 기타 기능은 다른 개발자분이 RoomController의 OnPlayerEnterRoom 이벤트에 연결하여 사용할 수 있습니다.
        
        GameObject roomsParent = new GameObject("RoomControllers");

        foreach (Room room in generatedRooms)
        {
            // 방의 정중앙 좌표 계산 (Tilemap 기준이므로 +0.5f 보정 없이 정확한 bounds.center 사용)
            Vector3 centerPos = new Vector3(room.bounds.center.x, room.bounds.center.y, 0f);
            
            // 각 방마다 빈 게임 오브젝트 생성
            GameObject roomObj = new GameObject($"Room_{room.type}");
            roomObj.transform.position = centerPos;
            roomObj.transform.SetParent(roomsParent.transform);

            // 트리거 충돌체 세팅
            BoxCollider2D col = roomObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(room.bounds.width, room.bounds.height);

            // 룸 컨트롤러 부착 및 데이터 세팅
            RoomController controller = roomObj.AddComponent<RoomController>();
            controller.roomType = room.type;
            controller.roomBounds = room.bounds;
        }
    }
}
