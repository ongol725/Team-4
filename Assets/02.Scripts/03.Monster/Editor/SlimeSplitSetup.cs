// ============================================================
// SlimeSplitSetup.cs  (Editor 전용)
// 슬라임 분열 기능 셋업 자동화:
//  1) Prefab_SplitSlime 생성 = Prefab_Slime 복제 → 데이터 1004, 크기 0.5배, 분열 기믹 없음
//  2) Prefab_Slime(1003)에 SlimeSplitGimmick 추가 + splitPrefab 연결(splitCount=4)
// 메뉴: Team4/슬라임 분열 셋업
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using BagSurvivor.Monster;

public static class SlimeSplitSetup
{
    private const string SlimePath     = "Assets/03.Prefabs/02.Monsters/Prefab_Slime.prefab";
    private const string SplitPath     = "Assets/03.Prefabs/02.Monsters/Prefab_SplitSlime.prefab";
    private const string SplitDataPath = "Assets/Resources/ScriptableObjects/Monsters/MonsterData_1004_SplitSlime.asset";
    private const float  SplitScale    = 0.5f;
    private const int    SplitCount    = 4;

    [MenuItem("Team4/슬라임 분열 셋업")]
    public static void Setup()
    {
        var splitData = AssetDatabase.LoadAssetAtPath<MonsterData>(SplitDataPath);
        if (splitData == null) { Debug.LogError("[SlimeSplit] 1004 데이터 없음: " + SplitDataPath); return; }

        var slimeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SlimePath);
        if (slimeAsset == null) { Debug.LogError("[SlimeSplit] Prefab_Slime 없음: " + SlimePath); return; }

        // 1) Prefab_SplitSlime 생성 (독립 복제 → 데이터 1004, 작게, 분열 기믹 제거)
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(slimeAsset);
        PrefabUtility.UnpackPrefabInstance(tmp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        var mc = tmp.GetComponentInChildren<MonsterController>();
        if (mc != null) mc.monsterData = splitData;
        tmp.transform.localScale = Vector3.one * SplitScale;

        var dupGimmick = tmp.GetComponentInChildren<SlimeSplitGimmick>();
        if (dupGimmick != null) Object.DestroyImmediate(dupGimmick); // 분열체는 더 안 쪼개짐

        // 분열체: 스폰 후 0.5초 정지(분열 연출) 뒤 추적
        var mcHost = mc != null ? mc.gameObject : tmp;
        var freeze = mcHost.GetComponent<SpawnFreezeGimmick>();
        if (freeze == null) freeze = mcHost.AddComponent<SpawnFreezeGimmick>();
        freeze.freezeTime = 0.5f;

        var splitPrefab = PrefabUtility.SaveAsPrefabAsset(tmp, SplitPath);
        Object.DestroyImmediate(tmp);

        // 2) Prefab_Slime(1003)에 분열 기믹 추가 + 연결
        var contents = PrefabUtility.LoadPrefabContents(SlimePath);
        var mcRoot = contents.GetComponentInChildren<MonsterController>();
        var host = mcRoot != null ? mcRoot.gameObject : contents;

        var gimmick = host.GetComponent<SlimeSplitGimmick>();
        if (gimmick == null) gimmick = host.AddComponent<SlimeSplitGimmick>();
        gimmick.splitCount = SplitCount;
        gimmick.splitPrefab = splitPrefab;

        PrefabUtility.SaveAsPrefabAsset(contents, SlimePath);
        PrefabUtility.UnloadPrefabContents(contents);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SlimeSplit] 셋업 완료 — Prefab_SplitSlime 생성(데이터 1004, x{SplitScale}) + Prefab_Slime에 분열 기믹({SplitCount}마리) 연결");
    }
}
#endif
