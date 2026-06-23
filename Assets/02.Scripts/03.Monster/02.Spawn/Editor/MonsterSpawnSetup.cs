// ============================================================
// MonsterSpawnSetup.cs (Editor 전용)
//  - 현재 열린 씬에 MonsterSpawnSystem(스포너+풀+난이도)을 자동 생성/갱신
//  - 층별 일반방 near/mid/far 밴드를 MonsterData.index 기준으로 자동 배선(지속 스폰)
//  - MiniBoss 방 규칙(호랑이1016/곰1017/오우거1018) + 2↔4 중복방지 자동 설정
//  - 메뉴: BagSurvivor > Setup > Create / Update MonsterSpawnSystem
//
//  [밴드 구성 — 누적식]  near=가장 약함 / mid=+1종 / far=그 층 전체
//   1층 쥐(1001)/들개(1002)/슬라임(1003)   2층 고블린(1005)/고블린아처(1006)
//   3층 코볼트(1007)/멧돼지(1008)/좀비(1009) 4층 스켈전사(1010)/스켈궁수(1011)/늑대(1013)
//   5층 가고일(1014)/오크(1015)             ※1012 함정식물·1004 분열체는 직접 스폰 안 함
// ============================================================
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using BagSurvivor.Monster;

namespace BagSurvivor.MonsterEditor
{
    public static class MonsterSpawnSetup
    {
        private const string MonsterPrefabFolder = "Assets/03.Prefabs/02.Monsters";

        [MenuItem("BagSurvivor/Setup/Create or Update MonsterSpawnSystem")]
        public static void CreateOrUpdate()
        {
            // 1) 오브젝트 확보 (없으면 생성)
            GameObject sysGO = GameObject.Find("MonsterSpawnSystem");
            if (sysGO == null)
            {
                sysGO = new GameObject("MonsterSpawnSystem");
                Undo.RegisterCreatedObjectUndo(sysGO, "Create MonsterSpawnSystem");
            }

            MonsterPool pool = GetOrAdd<MonsterPool>(sysGO);
            DifficultyScaler diff = GetOrAdd<DifficultyScaler>(sysGO);
            RoomMonsterSpawner spawner = GetOrAdd<RoomMonsterSpawner>(sysGO);

            spawner.pool = pool;
            spawner.difficulty = diff;
            spawner.floorTilemapOverride = null; // 실제 DungeonGenerator에서 자동 참조
            spawner.wallTilemapOverride = null;

            // 던전 생성기 자동 연결 (없어도 런타임 Start에서 다시 탐색)
            DungeonGenerator gen = Object.FindFirstObjectByType<DungeonGenerator>();
            if (gen != null) spawner.dungeonGenerator = gen;

            // 2) 프리팹 index 매핑
            Dictionary<int, GameObject> byIndex = LoadMonsterPrefabsByIndex();

            // 3) normalMonsters (tier 1~15 = id 1001~1015)
            GameObject[] normal = new GameObject[15];
            int assigned = 0;
            List<int> missing = new List<int>();
            for (int t = 1; t <= 15; t++)
            {
                int id = 1000 + t;
                if (byIndex.TryGetValue(id, out GameObject pf)) { normal[t - 1] = pf; assigned++; }
                else missing.Add(id);
            }
            spawner.normalMonsters = normal;

            // 4) floorConfigs: 층별 near/mid/far 밴드 (항상 재배선 — 누적식)
            spawner.floorConfigs = new List<RoomMonsterSpawner.FloorSpawnConfig>
            {
                MakeFloor(byIndex, 1, 5, 2.0f, new[]{1001}, new[]{1001,1002}, new[]{1001,1002,1003}),
                MakeFloor(byIndex, 2, 6, 2.0f, new[]{1005}, new[]{1005,1006}, new[]{1005,1006}),
                MakeFloor(byIndex, 3, 7, 1.8f, new[]{1007}, new[]{1007,1008}, new[]{1007,1008,1009}),
                MakeFloor(byIndex, 4, 8, 1.6f, new[]{1010}, new[]{1010,1011}, new[]{1010,1011,1013}),
                MakeFloor(byIndex, 5, 8, 1.6f, new[]{1014}, new[]{1014,1015}, new[]{1014,1015}),
            };

            // 5) specialRules: MiniBoss = 호랑이(1016)/곰(1017)/오우거(1018), 2↔4 중복방지
            //    (Elite 1·3층은 전용 엘리트 프리팹 제작 후 별도 셋업에서 추가)
            List<GameObject> miniBossPrefabs = new List<GameObject>();
            if (byIndex.ContainsKey(1016)) miniBossPrefabs.Add(byIndex[1016]);
            if (byIndex.ContainsKey(1017)) miniBossPrefabs.Add(byIndex[1017]);
            if (byIndex.ContainsKey(1018)) miniBossPrefabs.Add(byIndex[1018]);

            if (spawner.specialRules == null) spawner.specialRules = new List<RoomMonsterSpawner.SpecialRoomRule>();
            // 기존 MiniBoss 규칙 제거 후 2층/4층 분리 재생성 (noRepeat 공유 키로 2↔4 중복방지)
            spawner.specialRules.RemoveAll(r => r != null && r.roomType == RoomType.MiniBoss);
            spawner.specialRules.Add(MakeMiniBoss(miniBossPrefabs.ToArray(), 2, 1f, 1f));   // 2층: 기본
            spawner.specialRules.Add(MakeMiniBoss(miniBossPrefabs.ToArray(), 4, 2f, 2f));   // 4층: 스탯 2배

            // 5-2) Elite 방: 1층=슬라임 엘리트, 3층=좀비 엘리트 (Team4/엘리트 셋업으로 프리팹 생성 후 연결됨)
            SetEliteRule(spawner, 1, "Assets/03.Prefabs/02.Monsters/Prefab_EliteSlime.prefab");
            SetEliteRule(spawner, 3, "Assets/03.Prefabs/02.Monsters/Prefab_EliteZombie.prefab");

            // 6) 변경 사항 저장 처리
            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(sysGO);
            EditorSceneManager.MarkSceneDirty(sysGO.scene);
            Selection.activeGameObject = sysGO;

            string warn = missing.Count > 0 ? "  (프리팹 없는 id: " + string.Join(",", missing) + " → 해당 tier는 자동 스킵)" : "";
            Debug.Log($"[MonsterSpawnSetup] 완료. normalMonsters {assigned}/15, MiniBoss {miniBossPrefabs.Count}종(2층 ×1 / 4층 ×2)." +
                      $" DungeonGenerator {(gen != null ? "연결됨" : "런타임 탐색")}.{warn}\n씬을 저장(Ctrl+S)하세요.");
        }

        /// <summary>중간보스 규칙 1개 생성(층별 배율 차등). noRepeat는 RoomType 키 공유라 2↔4 중복방지됨.</summary>
        private static RoomMonsterSpawner.SpecialRoomRule MakeMiniBoss(GameObject[] prefabs, int floor, float hpMul, float atkMul)
        {
            return new RoomMonsterSpawner.SpecialRoomRule
            {
                roomType = RoomType.MiniBoss,
                floor = floor,
                monsterPrefabs = prefabs,
                noRepeatAcrossRooms = true,
                hpMultiplier = hpMul,
                attackMultiplier = atkMul,
                minCount = 1,
                maxCount = 1,
            };
        }

        /// <summary>Elite 방(층별) 규칙 설정: 해당 층 Elite 방에 지정 엘리트 프리팹 1마리 스폰.</summary>
        private static void SetEliteRule(RoomMonsterSpawner s, int floor, string prefabPath)
        {
            GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (pf == null)
            {
                Debug.LogWarning($"[MonsterSpawnSetup] 엘리트 프리팹 없음(층 {floor}): {prefabPath}\n→ 'Team4/엘리트 셋업'을 먼저 실행하세요.");
                return;
            }
            if (s.specialRules == null) s.specialRules = new List<RoomMonsterSpawner.SpecialRoomRule>();
            var rule = s.specialRules.Find(r => r != null && r.roomType == RoomType.Elite && r.floor == floor);
            if (rule == null)
            {
                rule = new RoomMonsterSpawner.SpecialRoomRule { roomType = RoomType.Elite, floor = floor };
                s.specialRules.Add(rule);
            }
            rule.monsterPrefabs = new[] { pf };
            rule.minCount = 1;
            rule.maxCount = 1;
            rule.noRepeatAcrossRooms = false;
        }

        /// <summary>층 설정 1개 생성: 인덱스 배열을 프리팹 배열로 변환해 near/mid/far에 배선.</summary>
        private static RoomMonsterSpawner.FloorSpawnConfig MakeFloor(
            Dictionary<int, GameObject> byIndex, int floor, int maxAlive, float interval,
            int[] near, int[] mid, int[] far)
        {
            return new RoomMonsterSpawner.FloorSpawnConfig
            {
                floor = floor,
                maxAlive = maxAlive,
                spawnInterval = interval,
                nearMonsters = ResolveBand(byIndex, near),
                midMonsters = ResolveBand(byIndex, mid),
                farMonsters = ResolveBand(byIndex, far),
            };
        }

        /// <summary>인덱스 배열 → 존재하는 프리팹만 모아 반환(없는 id는 건너뜀).</summary>
        private static GameObject[] ResolveBand(Dictionary<int, GameObject> byIndex, int[] ids)
        {
            var list = new List<GameObject>();
            foreach (int id in ids)
                if (byIndex.TryGetValue(id, out GameObject pf) && pf != null) list.Add(pf);
            return list.ToArray();
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        private static Dictionary<int, GameObject> LoadMonsterPrefabsByIndex()
        {
            Dictionary<int, GameObject> map = new Dictionary<int, GameObject>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { MonsterPrefabFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (pf == null) continue;
                MonsterController mc = pf.GetComponent<MonsterController>();
                if (mc == null || mc.monsterData == null) continue;
                map[mc.monsterData.index] = pf;
            }
            return map;
        }
    }
}