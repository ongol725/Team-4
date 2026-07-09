// ============================================================
// TrapPlantSetup.cs  (Editor 전용)
// 함정(Trap.prefab)을 밟으면 함정식물이 나오도록 설정:
//  - Trap.prefab에 Trap_MonsterSpawn 효과 추가 + 소환 몬스터 = Prefab_TrapPlant, 개수 1
//  - TrapController가 한 트랩의 모든 효과를 실행하므로, 기존 Trap_Blind(어두움)+Trap_Stun(멈춤)에
//    더해 함정식물 소환이 같이 발동됨. (트랩 효과 코드는 수정하지 않음)
// 메뉴: Team4/함정에 함정식물 추가
// ※ Trap.prefab은 던전(06.Map) 팀원 영역이므로, 실행 전 팀원과 합의 권장.
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TrapPlantSetup
{
    private const string TrapPath  = "Assets/03.Prefabs/04.Tiles/Trap.prefab";
    private const string PlantPath = "Assets/03.Prefabs/02.Monsters/Prefab_TrapPlant.prefab";

    [MenuItem("Team4/함정에 함정식물 추가")]
    public static void Setup()
    {
        var plant = AssetDatabase.LoadAssetAtPath<GameObject>(PlantPath);
        if (plant == null) { Debug.LogError("[TrapPlant] Prefab_TrapPlant 없음: " + PlantPath); return; }

        var trapAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TrapPath);
        if (trapAsset == null) { Debug.LogError("[TrapPlant] Trap.prefab 없음: " + TrapPath); return; }

        var contents = PrefabUtility.LoadPrefabContents(TrapPath);

        var spawn = contents.GetComponent<Trap_MonsterSpawn>();
        if (spawn == null) spawn = contents.AddComponent<Trap_MonsterSpawn>();
        spawn.monsterPrefab = plant;
        spawn.spawnCount    = 1;
        spawn.spawnRadius   = 0.3f;

        PrefabUtility.SaveAsPrefabAsset(contents, TrapPath);
        PrefabUtility.UnloadPrefabContents(contents);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[TrapPlant] Trap.prefab에 Trap_MonsterSpawn 추가 완료 — 밟으면 함정식물 1마리 소환(+기존 어두움/멈춤)");
    }
}
#endif
