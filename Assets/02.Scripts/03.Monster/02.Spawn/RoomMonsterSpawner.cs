// ============================================================
// RoomMonsterSpawner.cs
// 방 단위 몬스터 스폰 매니저 (절차적 던전 대응)
//
// [설계 요약]
//  - 출현 구성(어떤 몹): 일반방은 층별 'id 순번 윈도우'에서 랜덤. 특수방은 지정 프리팹.
//  - 스탯 배율(얼마나 셈): DifficultyScaler(경과시간 비례)에서 받아 스폰 시 주입.
//  - 06.Map(팀원 담당) 코드는 수정하지 않고, RoomController가 열어둔 public 확장점
//    (OnPlayerEnterRoom / RegisterMonster / NotifyMonsterDead / roomBounds)에만 연결.
//
// [인스펙터에서 자유 조절]
//  - normalMonsters: 일반 몬스터를 약→강 순으로 배치 (0번=가장 약함)
//  - floorConfigs: 몇 층에 몇 번~몇 번 몬스터가, 몇 마리 나오는지
//  - specialRules: Elite/MiniBoss/Boss 방에 어떤 몬스터가 몇 마리 나오는지
// ============================================================
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

namespace BagSurvivor.Monster
{
    public class RoomMonsterSpawner : MonoBehaviour
    {
        // 층별 일반방 스폰 설정 (출현 순번 윈도우 + 마리 수 범위)
        [System.Serializable]
        public class FloorSpawnConfig
        {
            [Tooltip("적용 층 (1~5)")]
            public int floor = 1;

            [Tooltip("등장 몬스터 시작 순번 (1-based, normalMonsters 기준)")]
            public int minTier = 1;

            [Tooltip("등장 몬스터 끝 순번 (1-based, 포함)")]
            public int maxTier = 5;

            [Tooltip("방 1개당 최소 스폰 수")]
            public int minCount = 3;

            [Tooltip("방 1개당 최대 스폰 수")]
            public int maxCount = 6;
        }

        // 특수방(Elite/MiniBoss/Boss) 스폰 설정
        [System.Serializable]
        public class SpecialRoomRule
        {
            [Tooltip("적용 방 타입")]
            public RoomType roomType = RoomType.Elite;

            [Tooltip("등장 몬스터 프리팹 후보 (랜덤 선택)")]
            public GameObject[] monsterPrefabs;

            [Tooltip("방 1개당 최소 스폰 수")]
            public int minCount = 1;

            [Tooltip("방 1개당 최대 스폰 수")]
            public int maxCount = 1;
        }

        [Header("참조 (미지정 시 자동 검색/생성)")]
        public DungeonGenerator dungeonGenerator;
        public MonsterPool pool;
        public DifficultyScaler difficulty;

        [Header("일반 몬스터 풀 (약→강 순서: 0번=가장 약함)")]
        [Tooltip("1001~1015 몬스터 프리팹을 순서대로 배치")]
        public GameObject[] normalMonsters;

        [Header("층별 일반방 스폰 (설정 없는 층은 스폰 안 함, 예: 5층)")]
        public List<FloorSpawnConfig> floorConfigs = new List<FloorSpawnConfig>
        {
            new FloorSpawnConfig { floor = 1, minTier = 1,  maxTier = 5,  minCount = 3, maxCount = 6 },
            new FloorSpawnConfig { floor = 2, minTier = 4,  maxTier = 8,  minCount = 4, maxCount = 7 },
            new FloorSpawnConfig { floor = 3, minTier = 7,  maxTier = 12, minCount = 5, maxCount = 8 },
            new FloorSpawnConfig { floor = 4, minTier = 11, maxTier = 15, minCount = 6, maxCount = 9 },
            // 5층: 보스층 → 일반방 스폰 없음 (의도적으로 비움)
        };

        [Header("특수방 스폰 (Elite / MiniBoss / Boss)")]
        public List<SpecialRoomRule> specialRules = new List<SpecialRoomRule>();

        [Header("거리 기반 스폰 (시작방→보스방 난이도 보간)")]
        [Tooltip("켜면 일반방을 시작방/보스방과의 거리로 스폰(끄면 위 floorConfigs 사용)")]
        public bool useDistanceBasedSpawn = true;

        [Tooltip("시작방 근처(가장 쉬움): 등장 몬스터 시작 순번")]
        public int easyTierMin = 1;
        [Tooltip("시작방 근처(가장 쉬움): 등장 몬스터 끝 순번")]
        public int easyTierMax = 3;
        [Tooltip("보스방 근처(가장 어려움): 등장 몬스터 시작 순번")]
        public int hardTierMin = 12;
        [Tooltip("보스방 근처(가장 어려움): 등장 몬스터 끝 순번")]
        public int hardTierMax = 15;
        [Tooltip("시작방 근처 마리 수")]
        public int easyCount = 3;
        [Tooltip("보스방 근처 마리 수")]
        public int hardCount = 9;

        [Header("스폰 위치 옵션")]
        [Tooltip("방 가장자리(벽)와 띄울 그리드 여백")]
        public int edgeMargin = 1;
        [Tooltip("바닥 타일을 찾기 위한 위치 재시도 횟수")]
        public int maxPositionAttempts = 25;

        [Header("풀 예열 (Prewarm)")]
        [Tooltip("현재 층 등장 몬스터를 종류별로 미리 생성해 풀에 적재(첫 스폰 끊김 방지). 0이면 끄기")]
        public int prewarmPerType = 8;

        [Header("타일맵 직접 지정 (선택: 미지정 시 DungeonGenerator에서 자동 참조)")]
        [Tooltip("바닥 타일맵 직접 지정 (테스트/특수 상황용)")]
        public Tilemap floorTilemapOverride;
        [Tooltip("벽 타일맵 직접 지정 (테스트/특수 상황용)")]
        public Tilemap wallTilemapOverride;

        private Tilemap floorTilemap;
        private Tilemap wallTilemap;
        private readonly HashSet<RoomController> subscribed = new HashSet<RoomController>();

        // 던전 재생성(층 이동) 감지용 — RoomControllers 컨테이너 인스턴스가 바뀌면 재구독
        private GameObject lastRoomsContainer;
        private float rescanTimer;

        // 현재 스폰되어 활성 상태인 몬스터들 (복도 진입 시 일괄 디스폰용)
        private readonly List<MonsterController> activeMonsters = new List<MonsterController>();
        private Transform playerTf;
        private RoomController currentNormalRoom; // 플레이어가 현재 들어가 있는 일반방

        private IEnumerator Start()
        {
            if (dungeonGenerator == null)
                dungeonGenerator = FindFirstObjectByType<DungeonGenerator>();

            EnsurePool();
            EnsureDifficulty();
            CacheTilemaps();

            // 던전 생성(다른 컴포넌트 Start)이 끝나 RoomController들이 생길 때까지 대기
            int guard = 0;
            while (GameObject.Find("RoomControllers") == null && guard < 300)
            {
                guard++;
                yield return null;
            }
            if (floorTilemap == null) CacheTilemaps();

            RescanRooms();
            PrewarmPool();
            lastRoomsContainer = GameObject.Find("RoomControllers");
        }

        // 같은 씬에서 던전이 재생성(층 이동)되면 RoomControllers 컨테이너가 새 인스턴스로 교체됨.
        // 이를 감지해 타일맵을 재캐시하고 새 방들을 다시 구독한다. (1초 주기로만 검사 — 부하 최소화)
        private void Update()
        {
            // 매 프레임: 일반방 진입/이탈 추적 → 진입 시 (재)스폰, 이탈(복도 등) 시 디스폰
            CheckRoomPresence();

            // 1초 주기: 던전 재생성(층 이동) 감지 → 정리 후 재구독·재예열
            rescanTimer += Time.unscaledDeltaTime;
            if (rescanTimer < 1f) return;
            rescanTimer = 0f;

            GameObject container = GameObject.Find("RoomControllers");
            if (container != null && container != lastRoomsContainer)
            {
                lastRoomsContainer = container;
                floorTilemap = null;
                wallTilemap = null;
                DespawnAllMonsters();       // 이전 층 몬스터 정리
                currentNormalRoom = null;
                subscribed.Clear();         // 파괴된 이전 방 구독 정리
                CacheTilemaps();
                RescanRooms();
                PrewarmPool();
            }
        }

        // 일반방 진입/이탈 추적: 일반방을 떠나면 디스폰, (다시) 들어오면 재스폰.
        // → 복도로 나갔다 돌아오면 몬스터가 다시 나옴. (특수방은 OnPlayerEnterRoom 1회 스폰 유지)
        private void CheckRoomPresence()
        {
            RoomController room = GetPlayerNormalRoom();
            if (room == currentNormalRoom) return;

            if (currentNormalRoom != null) DespawnAllMonsters(); // 일반방 이탈 → 디스폰
            if (room != null) SpawnForRoom(room);                // 일반방 진입 → (재)스폰
            currentNormalRoom = room;
        }

        /// <summary>플레이어가 들어가 있는 '일반방'을 반환(없으면 null).</summary>
        private RoomController GetPlayerNormalRoom()
        {
            if (playerTf == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTf = p.transform;
                else return currentNormalRoom;
            }

            Vector2 pos = playerTf.position;
            foreach (RoomController rc in subscribed)
            {
                if (rc == null || rc.roomType != RoomType.Normal) continue;
                Collider2D col = rc.GetComponent<Collider2D>();
                if (col != null && col.OverlapPoint(pos)) return rc;
            }
            return null;
        }

        /// <summary>현재 층 등장 몬스터(+특수방)를 종류별로 미리 생성해 풀에 적재(첫 스폰 끊김 방지).</summary>
        private void PrewarmPool()
        {
            if (pool == null || prewarmPerType <= 0) return;
            int floor = dungeonGenerator != null ? dungeonGenerator.currentFloor : 1;

            FloorSpawnConfig cfg = floorConfigs.Find(c => c != null && c.floor == floor);
            if (cfg != null && normalMonsters != null && normalMonsters.Length > 0)
            {
                int lo = Mathf.Clamp(cfg.minTier - 1, 0, normalMonsters.Length - 1);
                int hi = Mathf.Clamp(cfg.maxTier - 1, 0, normalMonsters.Length - 1);
                if (hi < lo) { int t = lo; lo = hi; hi = t; }
                for (int i = lo; i <= hi; i++)
                    if (normalMonsters[i] != null) pool.Prewarm(normalMonsters[i], prewarmPerType);
            }

            foreach (SpecialRoomRule r in specialRules)
            {
                if (r == null || r.monsterPrefabs == null) continue;
                foreach (GameObject p in r.monsterPrefabs)
                    if (p != null) pool.Prewarm(p, Mathf.Max(1, r.maxCount));
            }
        }

        /// <summary>현재 활성 몬스터를 모두 풀로 반환합니다. (사망이 아닌 디스폰이라 방 클리어는 통지하지 않음)</summary>
        public void DespawnAllMonsters()
        {
            for (int i = activeMonsters.Count - 1; i >= 0; i--)
            {
                MonsterController m = activeMonsters[i];
                if (m == null) continue;
                m.SetDeathCallback(null);   // 디스폰이므로 사망 콜백 차단
                if (pool != null) pool.Return(m);
            }
            activeMonsters.Clear();
        }

        private void EnsurePool()
        {
            if (pool != null) return;
            pool = MonsterPool.Instance;
            if (pool == null) pool = FindFirstObjectByType<MonsterPool>();
            if (pool == null)
            {
                GameObject poolObj = new GameObject("MonsterPool");
                pool = poolObj.AddComponent<MonsterPool>();
            }
        }

        private void EnsureDifficulty()
        {
            if (difficulty != null) return;
            difficulty = DifficultyScaler.Instance;
            if (difficulty == null) difficulty = FindFirstObjectByType<DifficultyScaler>();
            if (difficulty == null)
            {
                GameObject diffObj = new GameObject("DifficultyScaler");
                difficulty = diffObj.AddComponent<DifficultyScaler>();
            }
        }

        private void CacheTilemaps()
        {
            // 직접 지정이 있으면 우선 사용
            if (floorTilemapOverride != null) floorTilemap = floorTilemapOverride;
            if (wallTilemapOverride != null) wallTilemap = wallTilemapOverride;

            if (floorTilemap == null && dungeonGenerator != null && dungeonGenerator.dungeonRenderer != null)
            {
                floorTilemap = dungeonGenerator.dungeonRenderer.floorTilemap;
                if (wallTilemap == null) wallTilemap = dungeonGenerator.dungeonRenderer.wallTilemap;
            }
        }

        /// <summary>
        /// 모든 RoomController의 진입 이벤트를 구독합니다.
        /// 던전 재생성(RegenerateDungeon) 후 외부에서 다시 호출해도 안전합니다.
        /// </summary>
        public void RescanRooms()
        {
            subscribed.RemoveWhere(rc => rc == null);

            RoomController[] rooms = FindObjectsByType<RoomController>(FindObjectsSortMode.None);
            foreach (RoomController rc in rooms)
            {
                if (rc == null || subscribed.Contains(rc)) continue;

                subscribed.Add(rc);

                // 일반방: 진입/이탈을 직접 추적(재진입 시 재스폰)하므로 이벤트 구독 안 함.
                // 특수방(Elite/MiniBoss/Boss): 문 잠금 타이밍과 동기화되도록 1회 진입 이벤트로 스폰.
                if (rc.roomType != RoomType.Normal)
                {
                    RoomController room = rc; // 클로저 캡처용 지역 복사
                    if (room.OnPlayerEnterRoom == null)
                        room.OnPlayerEnterRoom = new UnityEngine.Events.UnityEvent();
                    room.OnPlayerEnterRoom.AddListener(() => SpawnForRoom(room));
                }
            }
        }

        /// <summary>특정 방에 몬스터를 스폰합니다. (RoomController.OnPlayerEnterRoom에서 호출)</summary>
        private void SpawnForRoom(RoomController rc)
        {
            if (rc == null || pool == null) return;

            int floor = dungeonGenerator != null ? dungeonGenerator.currentFloor : 1;
            float hpMul = difficulty != null ? difficulty.GetHpMultiplier() : 1f;
            float atkMul = difficulty != null ? difficulty.GetAttackMultiplier() : 1f;

            if (rc.roomType == RoomType.Normal)
                SpawnNormal(rc, floor, hpMul, atkMul);
            else
                SpawnSpecial(rc, hpMul, atkMul); // Start/Shop은 규칙이 없어 자동으로 스폰 안 됨
        }

        private void SpawnNormal(RoomController rc, int floor, float hpMul, float atkMul)
        {
            if (normalMonsters == null || normalMonsters.Length == 0) return;

            int tierMin, tierMax, count;

            // 거리 기반: 시작방→보스방 거리 비율로 난이도(등장 순번·마리 수) 보간
            if (useDistanceBasedSpawn && TryDistanceDifficulty(rc, out tierMin, out tierMax, out count))
            {
                // 보간값 사용
            }
            else
            {
                // 폴백: 층별 설정
                FloorSpawnConfig cfg = floorConfigs.Find(c => c != null && c.floor == floor);
                if (cfg == null) return;
                tierMin = cfg.minTier;
                tierMax = cfg.maxTier;
                count = Random.Range(cfg.minCount, cfg.maxCount + 1);
            }

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = PickFromTier(tierMin, tierMax);
                SpawnOne(rc, prefab, hpMul, atkMul);
            }
        }

        /// <summary>시작방/보스방과의 거리 비율(0=시작,1=보스)로 등장 순번·마리 수를 보간합니다.</summary>
        private bool TryDistanceDifficulty(RoomController rc, out int tierMin, out int tierMax, out int count)
        {
            tierMin = tierMax = count = 0;

            RoomController start = FindRoomOfType(RoomType.Start);
            RoomController boss = FindRoomOfType(RoomType.Boss);
            if (start == null || boss == null) return false;

            Vector2 c = rc.roomBounds.center;
            float dStart = Vector2.Distance(c, start.roomBounds.center);
            float dBoss = Vector2.Distance(c, boss.roomBounds.center);
            float sum = dStart + dBoss;
            float t = sum > 0.001f ? Mathf.Clamp01(dStart / sum) : 0f; // 0=시작방 근처, 1=보스방 근처

            tierMin = Mathf.RoundToInt(Mathf.Lerp(easyTierMin, hardTierMin, t));
            tierMax = Mathf.RoundToInt(Mathf.Lerp(easyTierMax, hardTierMax, t));
            count   = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(easyCount, hardCount, t)));
            if (tierMax < tierMin) tierMax = tierMin;
            return true;
        }

        private RoomController FindRoomOfType(RoomType type)
        {
            foreach (RoomController rc in FindObjectsByType<RoomController>(FindObjectsSortMode.None))
                if (rc != null && rc.roomType == type) return rc;
            return null;
        }

        private void SpawnSpecial(RoomController rc, float hpMul, float atkMul)
        {
            SpecialRoomRule rule = specialRules.Find(r => r != null && r.roomType == rc.roomType);
            if (rule == null || rule.monsterPrefabs == null || rule.monsterPrefabs.Length == 0) return;

            int count = Random.Range(rule.minCount, rule.maxCount + 1);
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = rule.monsterPrefabs[Random.Range(0, rule.monsterPrefabs.Length)];
                SpawnOne(rc, prefab, hpMul, atkMul);
            }
        }

        /// <summary>normalMonsters 배열에서 [minTier, maxTier] (1-based, 포함) 범위의 프리팹을 랜덤 선택.</summary>
        private GameObject PickFromTier(int minTier, int maxTier)
        {
            if (normalMonsters == null || normalMonsters.Length == 0) return null;

            int lo = Mathf.Clamp(minTier - 1, 0, normalMonsters.Length - 1);
            int hi = Mathf.Clamp(maxTier - 1, 0, normalMonsters.Length - 1);
            if (hi < lo) { int t = lo; lo = hi; hi = t; }

            for (int a = 0; a < 8; a++)
            {
                int idx = Random.Range(lo, hi + 1);
                if (normalMonsters[idx] != null) return normalMonsters[idx];
            }
            // 폴백: 범위 내 첫 non-null
            for (int idx = lo; idx <= hi; idx++)
                if (normalMonsters[idx] != null) return normalMonsters[idx];
            return null;
        }

        private void SpawnOne(RoomController rc, GameObject prefab, float hpMul, float atkMul)
        {
            if (prefab == null) return;

            Vector3 pos;
            if (!TryGetSpawnPosition(rc.roomBounds, out pos)) return;

            MonsterController mc = pool.Get(prefab, pos, hpMul, atkMul);
            if (mc == null) return;

            activeMonsters.Add(mc);

            // 방에 등록 (특수방의 문 잠금/클리어 카운트와 연동)
            rc.RegisterMonster();

            // 사망 시: 활성 목록에서 제거 + 방에 클리어 통지 + 풀 반환
            RoomController room = rc;
            mc.SetDeathCallback(m =>
            {
                activeMonsters.Remove(m);
                room.NotifyMonsterDead();
                pool.Return(m);
            });
        }

        /// <summary>방 bounds 내부에서 실제 '바닥 타일' 위치를 찾아 월드 좌표로 반환합니다.</summary>
        private bool TryGetSpawnPosition(RectInt bounds, out Vector3 world)
        {
            world = Vector3.zero;

            if (floorTilemap == null)
            {
                world = new Vector3(bounds.center.x, bounds.center.y, 0f);
                return true;
            }

            int xMin = bounds.xMin + edgeMargin;
            int xMax = bounds.xMax - edgeMargin;
            int yMin = bounds.yMin + edgeMargin;
            int yMax = bounds.yMax - edgeMargin;
            if (xMax <= xMin) xMax = xMin + 1;
            if (yMax <= yMin) yMax = yMin + 1;

            for (int t = 0; t < maxPositionAttempts; t++)
            {
                int gx = Random.Range(xMin, xMax);
                int gy = Random.Range(yMin, yMax);
                Vector3Int cell = new Vector3Int(gx, gy, 0);

                bool isFloor = floorTilemap.HasTile(cell);
                bool isWall = wallTilemap != null && wallTilemap.HasTile(cell);
                if (isFloor && !isWall)
                {
                    world = floorTilemap.GetCellCenterWorld(cell);
                    return true;
                }
            }
            return false;
        }
    }
}
