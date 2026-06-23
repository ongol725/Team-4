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
        // 층별 일반방 스폰 설정 (가까운/중간/먼 밴드별 몬스터 풀 + 지속 스폰 파라미터)
        //  - 방의 거리(시작방→특수방)로 near/mid/far 밴드를 정하고, 그 밴드 풀에서 지속 스폰.
        [System.Serializable]
        public class FloorSpawnConfig
        {
            [Tooltip("적용 층 (1~5)")]
            public int floor = 1;

            [Tooltip("가까운 방(시작방 근처): 등장 몬스터 프리팹")]
            public GameObject[] nearMonsters;

            [Tooltip("중간 방: 등장 몬스터 프리팹")]
            public GameObject[] midMonsters;

            [Tooltip("먼 방(특수방 직전, 가장 어려움): 등장 몬스터 프리팹")]
            public GameObject[] farMonsters;

            [Tooltip("방 1개당 동시 생존 상한 (지속 스폰 시 이 수를 유지하며 죽은 만큼 보충)")]
            public int maxAlive = 6;

            [Tooltip("보충 스폰 주기(초)")]
            public float spawnInterval = 2f;

            /// <summary>밴드 인덱스(0=near,1=mid,2=far)에 해당하는 풀을 반환. 비면 상위 밴드로 폴백.</summary>
            public GameObject[] BandPool(int band)
            {
                GameObject[][] all = { nearMonsters, midMonsters, farMonsters };
                for (int b = Mathf.Clamp(band, 0, 2); b >= 0; b--)
                    if (all[b] != null && all[b].Length > 0) return all[b];
                for (int b = 0; b < 3; b++)
                    if (all[b] != null && all[b].Length > 0) return all[b];
                return null;
            }
        }

        // 특수방(Elite/MiniBoss/Boss) 스폰 설정
        [System.Serializable]
        public class SpecialRoomRule
        {
            [Tooltip("적용 방 타입")]
            public RoomType roomType = RoomType.Elite;

            [Tooltip("적용 층 (0 = 모든 층). 같은 타입을 층마다 다르게 쓰려면 지정 — 예: Elite 1층=슬라임, 3층=좀비")]
            public int floor = 0;

            [Tooltip("등장 몬스터 프리팹 후보 (랜덤 선택)")]
            public GameObject[] monsterPrefabs;

            [Tooltip("같은 타입의 다른 방에서 직전에 뽑힌 몬스터를 제외하고 뽑음 (중간보스 2↔4 중복 방지)")]
            public bool noRepeatAcrossRooms = false;

            [Tooltip("HP 배율 (층별 난이도 차등 — 예: 4층 중간보스 2배)")]
            public float hpMultiplier = 1f;

            [Tooltip("공격 배율 (층별 난이도 차등)")]
            public float attackMultiplier = 1f;

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

        [Header("층별 일반방 스폰 (near/mid/far 밴드 프리팹은 인스펙터에서 배정 / 5층은 비움)")]
        public List<FloorSpawnConfig> floorConfigs = new List<FloorSpawnConfig>
        {
            new FloorSpawnConfig { floor = 1, maxAlive = 5, spawnInterval = 2.0f },
            new FloorSpawnConfig { floor = 2, maxAlive = 6, spawnInterval = 2.0f },
            new FloorSpawnConfig { floor = 3, maxAlive = 7, spawnInterval = 1.8f },
            new FloorSpawnConfig { floor = 4, maxAlive = 8, spawnInterval = 1.6f },
            new FloorSpawnConfig { floor = 5, maxAlive = 8, spawnInterval = 1.6f }, // 일반방=가고일/오크, 보스는 잠긴 Boss방
        };

        [Header("특수방 스폰 (Elite / MiniBoss / Boss)")]
        public List<SpecialRoomRule> specialRules = new List<SpecialRoomRule>();

        [Header("밴드 경계 (시작방→특수방 거리비율 t)")]
        [Tooltip("t < 이 값 → 가까운(near) 밴드")]
        [Range(0f, 1f)] public float nearThreshold = 0.4f;
        [Tooltip("t < 이 값 → 중간(mid) 밴드, 그 이상은 먼(far) 밴드")]
        [Range(0f, 1f)] public float midThreshold = 0.75f;

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
        private Coroutine continuousRoutine;       // 현재 일반방 지속 스폰 코루틴

        // 같은 씬에서 층 이동(던전 재생성)해도 이 스폰러 인스턴스는 유지됨 → 중간보스 직전 선택을
        // 인스턴스 필드로 기억해 2↔4층 중복을 방지. (타입별 마지막 선택 프리팹)
        private readonly Dictionary<RoomType, GameObject> lastSpecialPick = new Dictionary<RoomType, GameObject>();

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
                StopContinuous();           // 이전 층 지속 스폰 정지
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

            if (currentNormalRoom != null) { StopContinuous(); DespawnAllMonsters(); } // 이탈 → 정지+디스폰
            if (room != null) StartContinuous(room);                                   // 진입 → 지속 스폰 시작
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
            if (cfg != null)
            {
                // 이 층의 near/mid/far 모든 밴드 몬스터를 종류별로 예열
                GameObject[][] bands = { cfg.nearMonsters, cfg.midMonsters, cfg.farMonsters };
                var warmed = new HashSet<GameObject>();
                foreach (GameObject[] b in bands)
                {
                    if (b == null) continue;
                    foreach (GameObject p in b)
                        if (p != null && warmed.Add(p)) pool.Prewarm(p, prewarmPerType);
                }
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

        /// <summary>특수방(Elite/MiniBoss/Boss)에 1회 스폰합니다. (RoomController.OnPlayerEnterRoom에서 호출)</summary>
        private void SpawnForRoom(RoomController rc)
        {
            if (rc == null || pool == null || rc.roomType == RoomType.Normal) return;

            float hpMul = difficulty != null ? difficulty.GetHpMultiplier() : 1f;
            float atkMul = difficulty != null ? difficulty.GetAttackMultiplier() : 1f;
            SpawnSpecial(rc, hpMul, atkMul); // Start/Shop은 규칙이 없어 자동으로 스폰 안 됨
        }

        // ── 일반방 지속 스폰 ────────────────────────────────────────────────
        private void StartContinuous(RoomController rc)
        {
            StopContinuous();
            continuousRoutine = StartCoroutine(ContinuousSpawn(rc));
        }

        private void StopContinuous()
        {
            if (continuousRoutine != null) { StopCoroutine(continuousRoutine); continuousRoutine = null; }
        }

        /// <summary>일반방에 머무는 동안 밴드 풀에서 maxAlive를 유지하며 지속 스폰합니다(죽은 만큼 보충).</summary>
        private IEnumerator ContinuousSpawn(RoomController rc)
        {
            int floor = dungeonGenerator != null ? dungeonGenerator.currentFloor : 1;
            FloorSpawnConfig cfg = floorConfigs.Find(c => c != null && c.floor == floor);
            if (cfg == null) yield break; // 5층 등 설정 없는 층은 일반방 스폰 안 함

            int band = ComputeBand(rc);                 // 0=near,1=mid,2=far
            GameObject[] pool2 = cfg.BandPool(band);
            if (pool2 == null || pool2.Length == 0) yield break;

            int maxAlive = Mathf.Max(1, cfg.maxAlive);
            float interval = Mathf.Max(0.1f, cfg.spawnInterval);
            var wait = new WaitForSeconds(interval);

            while (true)
            {
                // 동시 생존이 상한 미만이면 1마리 보충 (진입 직후엔 빠르게 상한까지 채워짐)
                if (activeMonsters.Count < maxAlive)
                {
                    float hpMul = difficulty != null ? difficulty.GetHpMultiplier() : 1f;
                    float atkMul = difficulty != null ? difficulty.GetAttackMultiplier() : 1f;
                    SpawnOne(rc, pool2[Random.Range(0, pool2.Length)], hpMul, atkMul);
                }

                // 상한 미달이면 빠르게 채우고(다음 프레임), 가득 차면 interval 대기
                if (activeMonsters.Count < maxAlive) yield return null;
                else yield return wait;
            }
        }

        /// <summary>시작방→특수방(Elite/MiniBoss/Boss) 거리비율 t로 밴드(0=near,1=mid,2=far)를 결정.</summary>
        private int ComputeBand(RoomController rc)
        {
            RoomController start = FindRoomOfType(RoomType.Start);
            RoomController lockRoom = FindLockedRoom();
            if (start == null || lockRoom == null) return 0;

            Vector2 c = rc.roomBounds.center;
            float dStart = Vector2.Distance(c, start.roomBounds.center);
            float dLock = Vector2.Distance(c, lockRoom.roomBounds.center);
            float sum = dStart + dLock;
            float t = sum > 0.001f ? Mathf.Clamp01(dStart / sum) : 0f; // 0=시작방 근처, 1=특수방 근처

            if (t < nearThreshold) return 0;
            if (t < midThreshold) return 1;
            return 2;
        }

        private RoomController FindRoomOfType(RoomType type)
        {
            foreach (RoomController rc in FindObjectsByType<RoomController>(FindObjectsSortMode.None))
                if (rc != null && rc.roomType == type) return rc;
            return null;
        }

        /// <summary>그 층의 잠긴 특수방(Elite/MiniBoss/Boss 중 존재하는 것)을 반환 — far 거리 기준점.</summary>
        private RoomController FindLockedRoom()
        {
            RoomController found = null;
            foreach (RoomController rc in FindObjectsByType<RoomController>(FindObjectsSortMode.None))
            {
                if (rc == null) continue;
                if (rc.roomType == RoomType.Elite || rc.roomType == RoomType.MiniBoss || rc.roomType == RoomType.Boss)
                    return rc;
            }
            return found;
        }

        private void SpawnSpecial(RoomController rc, float hpMul, float atkMul)
        {
            int floor = dungeonGenerator != null ? dungeonGenerator.currentFloor : 1;

            // 층 지정 규칙 우선(Elite 1층=슬라임/3층=좀비), 없으면 floor=0(모든 층) 규칙으로 폴백
            SpecialRoomRule rule = specialRules.Find(r => r != null && r.roomType == rc.roomType && r.floor == floor);
            if (rule == null)
                rule = specialRules.Find(r => r != null && r.roomType == rc.roomType && r.floor == 0);
            if (rule == null || rule.monsterPrefabs == null || rule.monsterPrefabs.Length == 0) return;

            int count = Random.Range(rule.minCount, rule.maxCount + 1);
            float hp = hpMul * Mathf.Max(0.01f, rule.hpMultiplier);   // 층별 차등 배율
            float atk = atkMul * Mathf.Max(0.01f, rule.attackMultiplier);
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = PickSpecialPrefab(rule);
                SpawnOne(rc, prefab, hp, atk);
            }
        }

        /// <summary>특수방 규칙에서 프리팹 1개 선택. noRepeat면 같은 타입 직전 선택을 제외(중간보스 2↔4 중복방지).</summary>
        private GameObject PickSpecialPrefab(SpecialRoomRule rule)
        {
            GameObject[] cands = rule.monsterPrefabs;
            GameObject prev = null;
            if (rule.noRepeatAcrossRooms) lastSpecialPick.TryGetValue(rule.roomType, out prev);

            GameObject pick = cands[Random.Range(0, cands.Length)];
            // 후보가 2개 이상이고 직전과 같으면 다른 것으로 재추첨(중복 회피)
            if (rule.noRepeatAcrossRooms && prev != null && cands.Length > 1)
            {
                int guard = 0;
                while (pick == prev && guard++ < 8) pick = cands[Random.Range(0, cands.Length)];
            }
            if (rule.noRepeatAcrossRooms) lastSpecialPick[rule.roomType] = pick;
            return pick;
        }

        private void SpawnOne(RoomController rc, GameObject prefab, float hpMul, float atkMul)
        {
            if (prefab == null) return;

            Vector3 pos;
            if (!TryGetSpawnPosition(rc.roomBounds, out pos)) return;

            // 엘리트 프리팹이면 인스펙터 배율(기본 HP 5배)을 난이도 배율에 곱함
            var elite = prefab.GetComponent<EliteMonster>();
            if (elite != null) { hpMul *= elite.hpMultiplier; atkMul *= elite.attackMultiplier; }

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
