// ============================================================
// BossRoomSetup.cs  (Editor 전용)
// 06.BossRoom를 '실전 형태'로 셋업하는 1-클릭 메뉴.
//  1) 보스 데이터 이전: _Sandbox(gitignore) → Resources/ScriptableObjects/Monsters (GUID 보존)
//  2) 보스 시작 체력 5000 (MonsterData_WolfBoss.maxHP)
//  3) BossHpBar 상승 기믹: 5000 시작, 30초마다 +125 (20분=1200초 → +5000 → 10000), 이후로도 같은 비율 무한 상승
//  4) 보스룸: MonsterPool(풀 스폰 필수) + 인게임 UI(BattleUI) + EventSystem 배치
// 메뉴: Team4/보스룸 셋업 (풀+UI+체력)
// ============================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using BagSurvivor.Monster;
using BagSurvivor.UI;

public static class BossRoomSetup
{
    private const string BossRoomScene = "Assets/01.Scenes/06.BossRoom.unity";
    private const string BattleUIPrefab = "Assets/03.Prefabs/05.UI/BattleUI.prefab";
    private const string BossHpBarPrefab = "Assets/03.Prefabs/05.UI/Boss_HpBar.prefab";
    private const string DataSrcDir = "Assets/_Sandbox/Boss/Data";
    private const string DataDstDir = "Assets/Resources/ScriptableObjects/Monsters";

    // 체력 상승 기믹 파라미터
    private const int   StartHP      = 5000;
    private const float GrowInterval = 30f;   // 30초마다
    private const int   GrowAmount   = 125;   // +125 (5000/(1200/30)=125 → 20분에 10000)

    [MenuItem("Team4/보스룸 셋업 (풀+UI+체력)")]
    public static void Run()
    {
        MigrateBossData();
        SetBossHpBarValues();
        SetupScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BossRoomSetup] 완료 — 보스 데이터 이전 + 체력 5000(+125/30s 무한 상승) + 보스룸 풀/UI 배치.");
    }

    /// <summary>보스/팬텀 데이터를 커밋 영역으로 이전하고, 보스 시작 체력을 5000으로.</summary>
    private static void MigrateBossData()
    {
        if (Directory.Exists(DataSrcDir))
        {
            EnsureFolder(DataDstDir);
            foreach (string raw in Directory.GetFiles(DataSrcDir, "*.asset"))
                MoveOverwrite(raw.Replace('\\', '/'), DataDstDir + "/" + Path.GetFileName(raw));
        }

        // 이전 후(또는 이미 이전됨) 경로에서 보스 데이터 로드해 체력 설정
        var data = AssetDatabase.LoadAssetAtPath<MonsterData>(DataDstDir + "/MonsterData_WolfBoss.asset")
                ?? AssetDatabase.LoadAssetAtPath<MonsterData>(DataSrcDir + "/MonsterData_WolfBoss.asset");
        if (data != null)
        {
            data.maxHP = StartHP;
            EditorUtility.SetDirty(data);
            Debug.Log($"[BossRoomSetup] 보스 시작 체력 = {StartHP}");
        }
        else Debug.LogWarning("[BossRoomSetup] MonsterData_WolfBoss 못 찾음 — 체력 설정 생략");
    }

    /// <summary>BossHpBar 프리팹의 상승 기믹/표시 기본값 설정(인게임·보스룸 인스턴스 공통).</summary>
    private static void SetBossHpBarValues()
    {
        if (AssetDatabase.LoadMainAssetAtPath(BossHpBarPrefab) == null)
        { Debug.LogWarning("[BossRoomSetup] Boss_HpBar 프리팹 없음 — 체력바 값 설정 생략"); return; }

        var contents = PrefabUtility.LoadPrefabContents(BossHpBarPrefab);
        var bar = contents.GetComponentInChildren<BossHpBar>(true);
        if (bar != null)
        {
            bar.growInterval = GrowInterval;
            bar.growAmount = GrowAmount;
            bar.standaloneMaxHP = StartHP;
            bar.standaloneCurHP = StartHP;
            PrefabUtility.SaveAsPrefabAsset(contents, BossHpBarPrefab);
            Debug.Log($"[BossRoomSetup] BossHpBar: 시작 {StartHP}, {GrowInterval}초마다 +{GrowAmount} (무한 상승)");
        }
        else Debug.LogWarning("[BossRoomSetup] Boss_HpBar에 BossHpBar 컴포넌트 없음");
        PrefabUtility.UnloadPrefabContents(contents);
    }

    /// <summary>06.BossRoom에 MonsterPool + BattleUI + EventSystem을 (없으면) 배치.</summary>
    private static void SetupScene()
    {
        if (!File.Exists(BossRoomScene)) { Debug.LogError("[BossRoomSetup] 보스룸 씬 없음: " + BossRoomScene); return; }
        var scene = EditorSceneManager.OpenScene(BossRoomScene, OpenSceneMode.Single);

        // 1) MonsterPool (풀 스폰 필수 — 늑대/토템/돌). 씬마다 필요(DontDestroyOnLoad 아님)
        if (Object.FindFirstObjectByType<MonsterPool>() == null)
        {
            new GameObject("MonsterPool").AddComponent<MonsterPool>();
            Debug.Log("[BossRoomSetup] MonsterPool 추가");
        }

        // 2) 인게임 UI(BattleUI) 그대로
        if (Object.FindFirstObjectByType<BossHpBar>(FindObjectsInactive.Include) == null)
        {
            var ui = AssetDatabase.LoadAssetAtPath<GameObject>(BattleUIPrefab);
            if (ui != null) { PrefabUtility.InstantiatePrefab(ui, scene); Debug.Log("[BossRoomSetup] BattleUI(인게임 UI) 배치"); }
            else Debug.LogWarning("[BossRoomSetup] BattleUI 프리팹 없음");
        }

        // 3) EventSystem (UI 입력 — 신 Input System 모듈)
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[BossRoomSetup] EventSystem 추가");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void MoveOverwrite(string src, string dest)
    {
        if (src == dest) return;
        if (AssetDatabase.LoadMainAssetAtPath(dest) != null) AssetDatabase.DeleteAsset(dest);
        string err = AssetDatabase.MoveAsset(src, dest);
        if (!string.IsNullOrEmpty(err)) Debug.LogWarning($"[BossRoomSetup] 이동 실패 {src}: {err}");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
