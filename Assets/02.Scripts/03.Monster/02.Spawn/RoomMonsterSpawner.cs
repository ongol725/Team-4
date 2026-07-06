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

            [Tooltip("방 1개당 동시 생존 상한 (지속 스폰 시 이 수를 유지하며 죽은 만큼 보충) — 밴드별 값이 0일 때 기본값으로 쓰임")]
            public int maxAlive = 6;

            [Tooltip("밴드별 동시 생존 상한 (0이면 위 maxAlive 사용). 구역별로 스폰 수를 다르게 줄 때 설정")]
            public int nearMaxAlive = 0;
            public int midMaxAlive = 0;
            public int farMaxAlive = 0;

            [Tooltip("보충 스폰 주기(초)")]
            public float spawnInterval = 2f;

            /// <summary>밴드별 동시 생존 상한 (밴드 값이 0이면 maxAlive로 폴백).</summary>
            public int BandMaxAlive(int band)
            {
                int v = band == 0 ? nearMaxAlive : band == 1 ? midMaxAlive : farMaxAlive;
                return v > 0 ? v : maxAlive;
            }

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

            [Tooltip("방어력 고정 오버라이드 (-1 = SO값 사용). 예: 2층 중간보스 defense 30 고정")]
            public int defenseOverride = -1;

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

        [Header("밴드 분류 (near=시작방 인접 / far=잠긴방 최근접 / 나머지=mid)")]
        [Tooltip("near 링 폭: 시작방에서 가장 가까운 일반방 거리의 이 배수 이내면 near로 간주(방 연결 그래프가 없어 거리로 근사). 클수록 near 방이 많아짐")]
        public float nearRingFactor = 1.5f;

        [Header("스폰 위치 옵션")]
        [Tooltip("방 가장자리(벽)와 띄울 그리드 여백")]
        public int edgeMargin = 1;
        [Tooltip("바닥 타일을 찾기 위한 위치 재시도 횟수")]
        public int maxPositionAttempts = 25;
        [Tooltip("플레이어와 이 거리보다 가까운 위치는 스폰 후보에서 제외(스폰 직후 접촉 피해 방지)")]
        public float minSpawnDistanceFromPlayer = 2.5f;

        [Header("풀 예열 (Prewarm)")]
        [Tooltip("현재 층 등장 몬스터를 종류별로 미리 생성해 풀에 적재(첫 스폰 끊김 방지). 0이면 끄기")]
        public int prewarmPerType = 8;

        // 일반방 동시 생존 상한 배수(고정 3배). 특수방(엘리트/보스) 미적용.
        private const int NORMAL_SPAWN_MULT = 3;

        [Header("일반방 갇힘 전투 (건전/아이작식)")]
        [Tooltip("일반방 총 스폰량 최소 — 진입 시 방마다 [min,max]에서 랜덤 추첨")]
        public int normalRoomTotalMin = 30;
        [Tooltip("일반방 총 스폰량 최대")]
        public int normalRoomTotalMax = 70;
        [Tooltip("일반방 최대 웨이브 수 — 총량을 이 수 이하로 나눠 웨이브마다 일괄 투입(루즈함 방지)")]
        public int normalRoomMaxWaves = 4;

        // 생존+예고 대기 수가 이 값 이하로 줄면 다음 웨이브 투입(마지막 한둘 쫓는 지루함 방지)
        private const int WAVE_NEXT_THRESHOLD = 2;

        [Header("스폰 예고(텔레그래프) — 독립 기능")]
        [Tooltip("적 생성 전 위치에 표시할 스프라이트(예: Sanctuary_Gd). 미지정 시 예고 없이 즉시 스폰")]
        public Sprite spawnIndicator;
        [Tooltip("예고 표시 후 실제 스폰까지 대기(초)")]
        public float spawnTelegraphSeconds = 0.5f;
        [Tooltip("예고 표시 지름(월드 단위)")]
        public float spawnIndicatorSize = 1f;

        private int _pendingSpawns; // 예고 대기 중 스폰 수(상한 초과 방지)
        private readonly List<Coroutine>  _telegraphRoutines = new List<Coroutine>();
        private readonly List<GameObject> _spawnMarkers      = new List<GameObject>();

        // ── 특수 이벤트 방 (Phase1: 강자/늪) ──
        private readonly Dictionary<RoomController, RoomEventType> _roomEvents = new Dictionary<RoomController, RoomEventType>();
        private float          _roomEnemyAtkMul = 1f; // 현재 방 적 공격력 배수(강자)
        private bool           _swampApplied;         // 늪 이속 페널티 적용 중
        private PlayerMovement _playerMove;

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

        // 외부 기믹(분열 등)이 디스폰 추적에 등록할 수 있도록 노출
        public static RoomMonsterSpawner Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

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
                _roomEvents.Clear();        // 이전 층 이벤트 방 배정 초기화
                EndRoomEvent(null);         // 진행 중이던 이벤트 효과 원복
                CacheTilemaps();
                RescanRooms();
                PrewarmPool();
            }
        }

        // 일반방 진입/이탈 추적: 이탈(복도 등) 시 정지+디스폰+효과원복만 담당.
        // 스폰·이벤트 시작은 문 잠금 시점(RoomController → OnPlayerEnterRoom → StartNormalFight)에 수행.
        private void CheckRoomPresence()
        {
            RoomController room = GetPlayerNormalRoom();
            if (room == currentNormalRoom) return;

            if (currentNormalRoom != null) { StopContinuous(); DespawnAllMonsters(); EndRoomEvent(currentNormalRoom); } // 이탈 → 정지+디스폰+효과원복
            currentNormalRoom = room;
        }

        // ── 이벤트 방 발동/해제 ──────────────────────────────────────
        private void TriggerRoomEvent(RoomController rc)
        {
            if (rc == null || !_roomEvents.TryGetValue(rc, out var ev) || ev == RoomEventType.None) return;

            RoomEventBanner.Show(RoomEventInfo.Name(ev), RoomEventInfo.Desc(ev), RoomEventInfo.Color(ev));

            switch (ev)
            {
                case RoomEventType.Strong:
                    RoomEventState.GoldMultiplier = 2f; // 처치 골드 +100%
                    _roomEnemyAtkMul = 2f;              // 적 공격력 +100%(스폰 시 적용)
                    break;
                case RoomEventType.Swamp:
                    if (!_swampApplied) { SetPlayerSpeedMul(0.5f); _swampApplied = true; } // 이속 -50%
                    _roomEnemyAtkMul = 1f;
                    break;
            }
        }

        private void EndRoomEvent(RoomController rc)
        {
            RoomEventState.Reset();  // 골드 배율 원복
            _roomEnemyAtkMul = 1f;
            if (_swampApplied) { SetPlayerSpeedMul(2f); _swampApplied = false; } // 이속 원복(×2로 복구)
        }

        private void SetPlayerSpeedMul(float m)
        {
            if (_playerMove == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _playerMove = p.GetComponent<PlayerMovement>();
            }
            if (_playerMove != null) _playerMove.speedMultiplier *= m; // 다른 배율(과부화 등)과 곱연산으로 공존
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
                        if (p != null && warmed.Add(p)) pool.Prewarm(p, prewarmPerType * NORMAL_SPAWN_MULT);
                }
            }

            foreach (SpecialRoomRule r in specialRules)
            {
                if (r == null || r.monsterPrefabs == null) continue;
                foreach (GameObject p in r.monsterPrefabs)
                    if (p != null) pool.Prewarm(p, Mathf.Max(1, r.maxCount));
            }
        }

        /// <summary>외부(분열 슬라임 등)에서 만든 몬스터를 디스폰 추적에만 등록합니다.
        /// 방 클리어 카운트에는 넣지 않음(보너스). 플레이어가 방을 떠나면 일반 몬스터와 함께 회수됩니다.</summary>
        public void TrackExternalMonster(MonsterController mc)
        {
            if (mc == null || pool == null) return;
            activeMonsters.Add(mc);
            mc.SetDeathCallback(m =>
            {
                activeMonsters.Remove(m);
                pool.Return(m);
            });
        }

        /// <summary>현재 활성 몬스터를 모두 풀로 반환합니다. (사망이 아닌 디스폰이라 방 클리어는 통지하지 않음)
        /// 진행 중인 예산 스폰/예고도 함께 정지 — 문 재개방(플레이어 이탈) 후 빈 방에 계속 스폰되는 것 방지.</summary>
        public void DespawnAllMonsters()
        {
            StopContinuous();
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

                // 일반방: 특수 이벤트 배정 — 기본 강자 25% / 늪 25% / 없음 50%.
                // 메타 업그레이드(황금의 기운): 강자의 방(골드2배) 확률만 +5%p/Lv 가산.
                if (rc.roomType == RoomType.Normal && !_roomEvents.ContainsKey(rc))
                {
                    RoomEventType ev = RoomEventType.None;
                    float strongChance = 0.25f + MetaUpgrades.GoldRoomBonus;
                    float r = Random.value;
                    if (r < strongChance)              ev = RoomEventType.Strong;
                    else if (r < strongChance + 0.25f) ev = RoomEventType.Swamp;
                    _roomEvents[rc] = ev;
                }

                // 전투방 공통: 문 잠금 타이밍(진입 확인 후)과 동기화되도록 OnPlayerEnterRoom으로 스폰.
                //  - 특수방(Elite/MiniBoss/Boss): 규칙 기반 1회 스폰
                //  - 일반방: 총량(30~70 랜덤) 갇힘 전투 시작 + 클리어 시 잔여 정리
                RoomController room = rc; // 클로저 캡처용 지역 복사
                if (room.OnPlayerEnterRoom == null)
                    room.OnPlayerEnterRoom = new UnityEngine.Events.UnityEvent();

                if (rc.roomType != RoomType.Normal)
                {
                    room.OnPlayerEnterRoom.AddListener(() => SpawnForRoom(room));
                }
                else
                {
                    room.OnPlayerEnterRoom.AddListener(() => StartNormalFight(room));
                    if (room.OnRoomCleared == null)
                        room.OnRoomCleared = new UnityEngine.Events.UnityEvent();
                    room.OnRoomCleared.AddListener(() => OnNormalRoomCleared(room));
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

        // ── 일반방 갇힘 전투 (총량 고정) ─────────────────────────────────────
        /// <summary>일반방 갇힘 전투 시작(문 잠금 직전 OnPlayerEnterRoom에서 호출).
        /// 총량을 방에 선등록해 웨이브 사이 전멸로 문이 일찍 열리지 않게 하고, 예산 스폰을 가동한다.</summary>
        private void StartNormalFight(RoomController rc)
        {
            if (rc == null || pool == null) return;

            int floor = dungeonGenerator != null ? dungeonGenerator.currentFloor : 1;
            FloorSpawnConfig cfg = floorConfigs.Find(c => c != null && c.floor == floor);
            if (cfg == null) return; // 설정 없는 층: 등록 없음 → RoomController가 즉시 클리어(잠금 없음)

            int band = ComputeBand(rc);
            GameObject[] pool2 = cfg.BandPool(band);
            if (pool2 == null || pool2.Length == 0) return;

            // 메타 업그레이드(몬스터 증원): 총량 배율 적용 (재화 획득 기회↑)
            int total = Mathf.Max(1, Mathf.RoundToInt(
                Random.Range(normalRoomTotalMin, normalRoomTotalMax + 1) * MetaUpgrades.SpawnCountMul));
            rc.RegisterMonsters(total); // 미스폰 포함 총량 선등록 — 전부 처치해야 문 개방

            TriggerRoomEvent(rc); // 강자/늪 이벤트는 잠금(전투 시작) 시점에 발동

            StopContinuous();
            continuousRoutine = StartCoroutine(BudgetedSpawn(rc, total, cfg, band));
            Debug.Log($"[Normal] 갇힘 전투 시작 — 총 {total}마리");
        }

        /// <summary>일반방 클리어: 스폰 정지 + 잔여(분열체 등 보너스) 정리 + 이벤트 효과 종료 → 완전 비전투.</summary>
        private void OnNormalRoomCleared(RoomController rc)
        {
            StopContinuous();
            DespawnAllMonsters();
            EndRoomEvent(rc);
        }

        private void StopContinuous()
        {
            if (continuousRoutine != null) { StopCoroutine(continuousRoutine); continuousRoutine = null; }

            // 예고 대기 중이던 스폰·마커 정리(방 이탈/층 변경 시 유령 스폰 방지)
            foreach (var co in _telegraphRoutines) if (co != null) StopCoroutine(co);
            _telegraphRoutines.Clear();
            foreach (var m in _spawnMarkers) if (m != null) Destroy(m);
            _spawnMarkers.Clear();
            _pendingSpawns = 0;
        }

        /// <summary>일반방 갇힘 전투: 총량(budget)을 최대 normalRoomMaxWaves개의 웨이브로 나눠 일괄 투입.
        /// 현재 웨이브 생존이 임계 이하로 줄면 다음 웨이브 투입 — 찔끔찔끔 보충되는 루즈함 방지.
        /// 소진 후 코루틴 종료 — 잔여 생존 몬스터 처치는 사망 콜백(NotifyMonsterDead)이 클리어를 판정한다.</summary>
        private IEnumerator BudgetedSpawn(RoomController rc, int budget, FloorSpawnConfig cfg, int band)
        {
            GameObject[] pool2 = cfg.BandPool(band);

            // 총량을 웨이브 수로 균등 분배(나머지는 앞 웨이브부터 +1)
            int waves = Mathf.Clamp(normalRoomMaxWaves, 1, budget);
            int baseSize = budget / waves;
            int extra = budget % waves;
            var checkWait = new WaitForSeconds(0.25f);

            for (int w = 0; w < waves; w++)
            {
                int waveSize = baseSize + (w < extra ? 1 : 0);
                Debug.Log($"[Normal] 웨이브 {w + 1}/{waves} — {waveSize}마리 투입");

                // 웨이브 일괄 투입 (몇 마리씩 프레임 분산 — 같은 프레임 스파이크 방지)
                for (int i = 0; i < waveSize; i++)
                {
                    float hpMul = difficulty != null ? difficulty.GetHpMultiplier() : 1f;
                    float atkMul = (difficulty != null ? difficulty.GetAttackMultiplier() : 1f) * _roomEnemyAtkMul; // 강자의 방: 적 공격력 배수
                    var co = StartCoroutine(SpawnWithTelegraph(rc, pool2[Random.Range(0, pool2.Length)], hpMul, atkMul, preCounted: true));
                    _telegraphRoutines.Add(co);
                    if ((i & 3) == 3) yield return null; // 4마리마다 한 프레임 휴식
                }

                if (w == waves - 1) break; // 마지막 웨이브는 대기 불필요

                // 생존+예고 대기가 임계 이하로 줄 때까지 대기 → 다음 웨이브
                while (activeMonsters.Count + _pendingSpawns > WAVE_NEXT_THRESHOLD)
                    yield return checkWait;
            }
        }

        /// <summary>밴드(0=near,1=mid,2=far)를 결정.
        /// far = 잠긴 특수방에 가장 가까운 일반방, near = 시작방 최근접 링, 나머지 = mid.
        /// (방 연결 그래프가 없어 방 중심 거리로 근사)</summary>
        /// <summary>방의 밴드 이름(near/mid/far). 통계(사망존 등) 표시용.</summary>
        public string GetZoneName(RoomController rc)
        {
            switch (ComputeBand(rc)) { case 0: return "near"; case 1: return "mid"; default: return "far"; }
        }

        private int ComputeBand(RoomController rc)
        {
            RoomController start = FindRoomOfType(RoomType.Start);
            RoomController lockRoom = FindLockedRoom();

            // near 우선: 시작방과 복도 1칸으로 직접 연결된 방 (시작방 인접 = 항상 near)
            if (start != null && start.connectedRooms.Contains(rc)) return 0;

            // far: 잠긴 특수방(보스/엘리트/중간보스)과 복도 1칸으로 직접 연결된 방
            if (lockRoom != null && lockRoom.connectedRooms.Contains(rc)) return 2;

            // 나머지는 전부 mid
            return 1;
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
            // 4층 미니보스만 보스 패턴(BossPatternDriver) 작동 — 2층 등 다른 곳은 일반 몬스터
            bool bossPattern = floor == 4 && rc.roomType == RoomType.MiniBoss;
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = PickSpecialPrefab(rule);
                SpawnOne(rc, prefab, hp, atk, bossPattern, rule.defenseOverride);
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

        // 일반방: 위치를 먼저 정해 예고(마커) 표시 후 그 자리에 스폰.
        // preCounted=true면 방 카운트에 이미 선등록된 스폰 → 실패 시 카운트를 환급해 문이 안 열리는 잠금 고착을 방지.
        private IEnumerator SpawnWithTelegraph(RoomController rc, GameObject prefab, float hpMul, float atkMul, bool preCounted = false)
        {
            if (prefab == null) { if (preCounted) rc.NotifyMonsterDead(); yield break; }

            Vector3 pos;
            if (!TryGetSpawnPosition(rc.roomBounds, out pos)) { if (preCounted) rc.NotifyMonsterDead(); yield break; }

            _pendingSpawns++;

            GameObject marker = null;
            if (spawnIndicator != null && spawnTelegraphSeconds > 0f)
            {
                marker = CreateSpawnMarker(pos);
                _spawnMarkers.Add(marker);
                yield return new WaitForSeconds(spawnTelegraphSeconds);
                if (marker != null) { _spawnMarkers.Remove(marker); Destroy(marker); }
            }

            _pendingSpawns = Mathf.Max(0, _pendingSpawns - 1);
            SpawnAt(rc, prefab, pos, hpMul, atkMul, preCounted: preCounted);
        }

        // 예고 마커 생성(2초간 표시, 살짝 명멸/확대해 눈에 띄게)
        private GameObject CreateSpawnMarker(Vector3 pos)
        {
            var go = new GameObject("SpawnMarker");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spawnIndicator;
            sr.sortingOrder = 5; // 바닥 위, 몬스터 아래쯤
            var b = spawnIndicator.bounds;
            float ext = Mathf.Max(b.extents.x, b.extents.y);
            go.transform.localScale = Vector3.one * (ext > 0.001f ? spawnIndicatorSize * 0.5f / ext : spawnIndicatorSize);
            go.AddComponent<SpawnMarkerPulse>(); // 명멸 연출(자기완결)
            return go;
        }

        private void SpawnOne(RoomController rc, GameObject prefab, float hpMul, float atkMul, bool enableBossPattern = false, int defenseOverride = -1)
        {
            if (prefab == null) return;
            Vector3 pos;
            if (!TryGetSpawnPosition(rc.roomBounds, out pos)) return;
            SpawnAt(rc, prefab, pos, hpMul, atkMul, enableBossPattern, defenseOverride);
        }

        // 지정 위치에 실제 스폰(SpawnOne/예고 공용).
        // preCounted=true: 방 카운트 선등록분(일반방 갇힘 전투) → 여기서 재등록하지 않고, 실패 시 환급.
        private void SpawnAt(RoomController rc, GameObject prefab, Vector3 pos, float hpMul, float atkMul, bool enableBossPattern = false, int defenseOverride = -1, bool preCounted = false)
        {
            if (prefab == null) { if (preCounted) rc.NotifyMonsterDead(); return; }

            // 엘리트 프리팹이면 인스펙터 배율(기본 HP 5배)을 난이도 배율에 곱함
            var elite = prefab.GetComponent<EliteMonster>();
            if (elite != null) { hpMul *= elite.hpMultiplier; atkMul *= elite.attackMultiplier; }

            MonsterController mc = pool.Get(prefab, pos, hpMul, atkMul);
            if (mc == null) { if (preCounted) rc.NotifyMonsterDead(); return; }

            mc.SetDefenseOverride(defenseOverride); // 층별 방어력 고정(예: 2층 중간보스 30) — 풀 재사용 시 -1로 복원됨

            // 보스 패턴 구동기: 4층 미니보스만 켬(평소/풀재사용엔 꺼서 일반 몬스터로 동작)
            var driver = mc.GetComponent<BossPatternDriver>();
            if (driver != null) driver.enabled = enableBossPattern;

            activeMonsters.Add(mc);

            // 방에 등록 (문 잠금/클리어 카운트와 연동) — 선등록분(preCounted)은 중복 등록 금지
            if (!preCounted) rc.RegisterMonster();

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
                if (!isFloor || isWall) continue;

                Vector3 candidate = floorTilemap.GetCellCenterWorld(cell);

                // 플레이어와 너무 가까우면 재시도(스폰 직후 접촉 피해 방지)
                if (playerTf != null &&
                    Vector2.Distance(candidate, playerTf.position) < minSpawnDistanceFromPlayer)
                    continue;

                world = candidate;
                return true;
            }
            return false;
        }
    }
}
