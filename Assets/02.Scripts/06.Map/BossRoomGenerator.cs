using UnityEngine;
using UnityEngine.Tilemaps;

public class BossRoomGenerator : MonoBehaviour
{
    // ──────────────────────────────────────────
    // 프리팹 전환 (추후 수동 제작 맵으로 교체 시)
    // ──────────────────────────────────────────
    [Header("프리팹 전환")]
    [Tooltip("true = 아래 prefab을 사용 / false = 아래 설정으로 절차적 생성")]
    public bool usePrefab = false;
    [Tooltip("usePrefab = true 일 때 사용할 보스룸 프리팹")]
    public GameObject bossRoomPrefab;

    // ──────────────────────────────────────────
    // 절차적 생성 설정
    // ──────────────────────────────────────────
    [Header("방 크기 (절차적 생성)")]
    [Tooltip("바닥 타일 기준 가로 크기")] public int roomWidth  = 30;
    [Tooltip("바닥 타일 기준 세로 크기")] public int roomHeight = 30;

    [Header("타일맵 연결")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;

    [Header("타일 종류")]
    public TileBase floorTile;
    [Tooltip("체스판 무늬용 두 번째 바닥 타일 (비워두면 단색)")]
    public TileBase altFloorTile;
    public TileBase wallTile;

    [Header("바닥 장식(Decor) - 베이스 사이사이 랜덤 배치")]
    [Tooltip("단독으로 박히는 장식 타일들")]
    public TileBase[] floorSingleTiles;
    [Range(0f, 1f)] [Tooltip("각 바닥 칸이 단독 장식 타일이 될 확률")]
    public float floorSingleChance = 0.05f;
    [Tooltip("여러 칸이 한 묶음인 장식 세트")]
    public FloorDecorSet[] floorDecorSets;
    [Tooltip("배치 시도 횟수")]
    public int floorDecorSetAttempts = 8;

    [Header("플레이어 스폰")]
    [Tooltip("씬에 배치된 플레이어 오브젝트")] public Transform playerTransform;
    [Tooltip("방 중앙 기준 스폰 오프셋 (타일 단위)")] public Vector2Int spawnOffset = Vector2Int.zero;

    // ──────────────────────────────────────────
    // 내부 상태
    // ──────────────────────────────────────────
    private GameObject spawnedPrefabInstance;

    void Start()
    {
        // GameManager 층수 동기화
        if (GameManager.Instance != null)
            GameManager.Instance.currentFloor = 5;

        if (usePrefab)
            SpawnPrefab();
        else
            GenerateRoom();
    }

    // ──────────────────────────────────────────
    // 프리팹 스폰
    // ──────────────────────────────────────────
    void SpawnPrefab()
    {
        if (bossRoomPrefab == null)
        {
            Debug.LogError("[BossRoomGenerator] bossRoomPrefab이 연결되지 않았습니다.");
            return;
        }

        if (spawnedPrefabInstance != null)
            Destroy(spawnedPrefabInstance);

        spawnedPrefabInstance = Instantiate(bossRoomPrefab, Vector3.zero, Quaternion.identity);
        Debug.Log("[BossRoomGenerator] 보스룸 프리팹 스폰 완료.");
    }

    // ──────────────────────────────────────────
    // 절차적 생성
    // ──────────────────────────────────────────
    void GenerateRoom()
    {
        if (floorTilemap == null || floorTile == null)
        {
            Debug.LogError("[BossRoomGenerator] floorTilemap 또는 floorTile이 연결되지 않았습니다.");
            return;
        }

        floorTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();

        // 바닥 배치 (roomWidth × roomHeight)
        for (int x = 0; x < roomWidth; x++)
        {
            for (int y = 0; y < roomHeight; y++)
            {
                TileBase tile = (altFloorTile != null && (x + y) % 2 != 0)
                    ? altFloorTile
                    : floorTile;
                floorTilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        // 바닥 장식(단독/세트) 배치 — 방 전체가 바닥
        FloorDecorPlacer.Apply(floorTilemap, roomWidth, roomHeight,
            (x, y) => true,
            floorSingleTiles, floorSingleChance, floorDecorSets, floorDecorSetAttempts);

        // 벽 배치 (바닥 경계 1칸 바깥)
        if (wallTilemap != null && wallTile != null)
        {
            for (int x = -1; x <= roomWidth; x++)
            {
                for (int y = -1; y <= roomHeight; y++)
                {
                    bool isFloor = x >= 0 && x < roomWidth && y >= 0 && y < roomHeight;
                    if (!isFloor)
                        wallTilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
                }
            }
        }

        // 플레이어 스폰 위치: 방 중앙 + 오프셋
        PlacePlayer();

        Debug.Log($"[BossRoomGenerator] 보스룸 생성 완료 ({roomWidth}x{roomHeight}).");
    }

    void PlacePlayer()
    {
        if (playerTransform == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null) playerTransform = obj.transform;
        }
        if (playerTransform == null) return;

        float centerX = roomWidth  * 0.5f + spawnOffset.x;
        float centerY = roomHeight * 0.5f + spawnOffset.y;
        Vector3 worldPos = floorTilemap != null
            ? floorTilemap.GetCellCenterWorld(new Vector3Int(
                Mathf.FloorToInt(centerX),
                Mathf.FloorToInt(centerY), 0))
            : new Vector3(centerX, centerY, 0f);

        playerTransform.position = worldPos;

        var rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.position = worldPos; rb.linearVelocity = Vector2.zero; }

        if (Camera.main != null)
            Camera.main.transform.position = new Vector3(worldPos.x, worldPos.y, -10f);
    }

    // ──────────────────────────────────────────
    // 에디터에서 미리보기용 (Scene 뷰 Gizmo)
    // ──────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (usePrefab) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawCube(
            new Vector3(roomWidth * 0.5f, roomHeight * 0.5f, 0f),
            new Vector3(roomWidth, roomHeight, 0f));
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireCube(
            new Vector3(roomWidth * 0.5f, roomHeight * 0.5f, 0f),
            new Vector3(roomWidth, roomHeight, 0f));
    }
#endif
}
