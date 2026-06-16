// ============================================================
// MonsterSpawnSetup.cs (Editor 전용)
//  - 현재 열린 씬에 MonsterSpawnSystem(스포너+풀+난이도)을 자동 생성/갱신
//  - 1001~1015 프리팹을 MonsterData.index 순서로 normalMonsters에 자동 배선
//  - MiniBoss 방 규칙(tiger/bear/ogre) 자동 설정
//  - 메뉴: BagSurvivor > Setup > Create / Update MonsterSpawnSystem
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

            // 4) floorConfigs (비어 있으면 기본값 주입: 5층은 의도적으로 없음)
            if (spawner.floorConfigs == null || spawner.floorConfigs.Count == 0)
            {
                spawner.floorConfigs = new List<RoomMonsterSpawner.FloorSpawnConfig>
                {
                    new RoomMonsterSpawner.FloorSpawnConfig { floor = 1, minTier = 1,  maxTier = 5,  minCount = 3, maxCount = 6 },
                    new RoomMonsterSpawner.FloorSpawnConfig { floor = 2, minTier = 4,  maxTier = 8,  minCount = 4, maxCount = 7 },
                    new RoomMonsterSpawner.FloorSpawnConfig { floor = 3, minTier = 7,  maxTier = 12, minCount = 5, maxCount = 8 },
                    new RoomMonsterSpawner.FloorSpawnConfig { floor = 4, minTier = 11, maxTier = 15, minCount = 6, maxCount = 9 },
                };
            }

            // 5) specialRules: MiniBoss = tiger(1016)/bear(1017)/ogre(1018)
            //    (Elite/Boss는 추후 기획 확정 시 추가 — 현재 미설정 = 스폰 안 함)
            List<GameObject> miniBossPrefabs = new List<GameObject>();
            if (byIndex.ContainsKey(1016)) miniBossPrefabs.Add(byIndex[1016]);
            if (byIndex.ContainsKey(1017)) miniBossPrefabs.Add(byIndex[1017]);
            if (byIndex.ContainsKey(1018)) miniBossPrefabs.Add(byIndex[1018]);

            RoomMonsterSpawner.SpecialRoomRule miniRule = null;
            if (spawner.specialRules == null) spawner.specialRules = new List<RoomMonsterSpawner.SpecialRoomRule>();
            foreach (var r in spawner.specialRules)
                if (r != null && r.roomType == RoomType.MiniBoss) { miniRule = r; break; }
            if (miniRule == null)
            {
                miniRule = new RoomMonsterSpawner.SpecialRoomRule { roomType = RoomType.MiniBoss, minCount = 1, maxCount = 1 };
                spawner.specialRules.Add(miniRule);
            }
            miniRule.monsterPrefabs = miniBossPrefabs.ToArray();

            // 6) 변경 사항 저장 처리
            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(sysGO);
            EditorSceneManager.MarkSceneDirty(sysGO.scene);
            Selection.activeGameObject = sysGO;

            string warn = missing.Count > 0 ? "  (프리팹 없는 id: " + string.Join(",", missing) + " → 해당 tier는 자동 스킵)" : "";
            Debug.Log($"[MonsterSpawnSetup] 완료. normalMonsters {assigned}/15, MiniBoss {miniRule.monsterPrefabs.Length}종." +
                      $" DungeonGenerator {(gen != null ? "연결됨" : "런타임 탐색")}.{warn}\n씬을 저장(Ctrl+S)하세요.");
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