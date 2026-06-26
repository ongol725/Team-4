// ============================================================
// BossFinalizeAndPlace.cs  (Editor 전용)
// 샌드박스에서 완성한 보스를 '출시 형태'로 마무리하는 1-클릭 메뉴.
//  1) 이전(migration): 보스가 의존하는 산출물을 gitignore된 Sandbox →
//     커밋 영역으로 AssetDatabase.MoveAsset(GUID 보존, 참조 자동 추종).
//       - 클립(*.anim) + Boss_P1/Boss_P2.controller → 07.Animations/02.Monsters/03.Boss
//       - 이펙트 프리팹 + 그 컨트롤러            → 03.Prefabs/06.Gimmicks/Boss
//       - preview_* / SandboxBoss / Boss.controller / 씬 = 샌드박스 전용(이전 제외, stage에 안 생김)
//  2) 프리팹화: 씬의 WolfBoss → Prefab_WolfBoss.prefab (deathDelay=5, BossHpBarLink 부착)
//  3) 배치: 06.BossRoom 씬에 프리팹 배치(중복이면 생략) 후 저장
// 메뉴: Team4/보스 프리팹화 + 보스룸 배치
// ============================================================
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BagSurvivor.Monster;

public static class BossFinalizeAndPlace
{
    private const string SandboxDir   = "Assets/01.Scenes/Sandbox/BossAnims";
    private const string AnimDest     = "Assets/07.Animations/02.Monsters/03.Boss";
    private const string FxDest       = "Assets/03.Prefabs/06.Gimmicks/Boss";
    private const string PrefabPath   = "Assets/03.Prefabs/02.Monsters/Prefab_WolfBoss.prefab";
    private const string SandboxScene = "Assets/01.Scenes/Sandbox/BossSandbox.unity";
    private const string BossRoomScene = "Assets/01.Scenes/06.BossRoom.unity";

    // 이전 제외(샌드박스 전용): 이름이 이걸로 시작하거나 정확히 일치하면 옮기지 않음
    private static readonly string[] KeepInSandboxPrefix = { "preview_", "SandboxBoss" };
    private static readonly string[] KeepInSandboxExact  = { "Boss" }; // Boss.controller(구 단일 컨트롤러)

    [MenuItem("Team4/보스 프리팹화 + 보스룸 배치")]
    public static void Run()
    {
        if (!Directory.Exists(SandboxDir)) { Debug.LogError("[BossFinalize] 샌드박스 산출물 폴더 없음: " + SandboxDir); return; }
        if (!File.Exists(SandboxScene))    { Debug.LogError("[BossFinalize] 샌드박스 씬 없음 — 먼저 보스 애니 적용까지 끝내세요: " + SandboxScene); return; }

        int moved = Migrate();
        Debug.Log($"[BossFinalize] 이전 완료 — {moved}개 자산을 커밋 영역으로 이동(GUID 보존).");

        if (!Prefabize()) return;
        Place();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BossFinalize] 완료 — Prefab_WolfBoss 생성 + 06.BossRoom 배치. (Sandbox는 gitignore라 stage에 안 올라감)");
    }

    /// <summary>보스 의존 자산을 커밋 영역으로 이동. 옮긴 개수 반환.</summary>
    private static int Migrate()
    {
        EnsureFolder(AnimDest);
        EnsureFolder(FxDest);

        int count = 0;
        foreach (string raw in Directory.GetFiles(SandboxDir))
        {
            string src = raw.Replace('\\', '/');
            if (src.EndsWith(".meta")) continue; // .meta는 Unity가 함께 옮김
            string name = Path.GetFileNameWithoutExtension(src);
            string ext  = Path.GetExtension(src).ToLowerInvariant();

            if (KeepInSandboxPrefix.Any(p => name.StartsWith(p))) continue;
            if (KeepInSandboxExact.Contains(name)) continue;

            string destDir;
            if (ext == ".anim")
                destDir = AnimDest;
            else if (ext == ".controller")
                destDir = (name == "Boss_P1" || name == "Boss_P2") ? AnimDest : FxDest;
            else if (ext == ".prefab")
                destDir = FxDest;
            else
                continue; // 그 외 확장자는 무시

            if (MoveOverwrite(src, destDir + "/" + Path.GetFileName(src))) count++;
        }
        return count;
    }

    /// <summary>대상이 이미 있으면 지우고 이동(재실행 가능). 성공 시 true.</summary>
    private static bool MoveOverwrite(string src, string dest)
    {
        if (src == dest) return false;
        if (AssetDatabase.LoadMainAssetAtPath(dest) != null)
            AssetDatabase.DeleteAsset(dest);
        string err = AssetDatabase.MoveAsset(src, dest);
        if (!string.IsNullOrEmpty(err)) { Debug.LogWarning($"[BossFinalize] 이동 실패 {src} → {dest}: {err}"); return false; }
        return true;
    }

    private static bool Prefabize()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != SandboxScene)
            scene = EditorSceneManager.OpenScene(SandboxScene, OpenSceneMode.Single);

        var driver = Object.FindFirstObjectByType<BossPatternDriver>();
        if (driver == null) { Debug.LogError("[BossFinalize] 샌드박스 씬에 보스(BossPatternDriver) 없음."); return false; }
        GameObject boss = driver.gameObject;

        var mc = boss.GetComponent<MonsterController>();
        if (mc != null) mc.deathDelay = 5f;                       // 보스 사망 연출 5초
        if (boss.GetComponent<BossHpBarLink>() == null)
            boss.AddComponent<BossHpBarLink>();                   // 인게임 HP바 연결

        var phase = boss.GetComponent<BossPhaseController>();      // 페이즈 전환 시 (20,35)→(20,27): 아래로 8
        if (phase != null) phase.phaseCenterOffset = new Vector3(0f, -8f, 0f);

        EnsureFolder("Assets/03.Prefabs/02.Monsters");
        EditorUtility.SetDirty(boss);
        PrefabUtility.SaveAsPrefabAsset(boss, PrefabPath, out bool ok);
        if (!ok) { Debug.LogError("[BossFinalize] 프리팹 저장 실패: " + PrefabPath); return false; }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BossFinalize] 프리팹화 완료: " + PrefabPath);
        return true;
    }

    private static void Place()
    {
        if (!File.Exists(BossRoomScene)) { Debug.LogError("[BossFinalize] 보스룸 씬 없음: " + BossRoomScene); return; }
        var room = EditorSceneManager.OpenScene(BossRoomScene, OpenSceneMode.Single);
        var pos = new Vector3(20f, 35f, 0f); // 보스룸 보스 위치(전환 시 (20,27)로 하강)

        var existing = Object.FindFirstObjectByType<BossPatternDriver>();
        if (existing != null)
        {
            existing.transform.position = pos; // 이미 있으면 위치만 갱신(프리팹 값은 자동 반영)
            EditorUtility.SetDirty(existing);
            Debug.Log("[BossFinalize] 기존 보스 위치를 (20,35)로 갱신.");
        }
        else
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("[BossFinalize] 프리팹 로드 실패: " + PrefabPath); return; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room);
            inst.transform.position = pos;
            Debug.Log("[BossFinalize] 06.BossRoom에 Prefab_WolfBoss 배치 완료.");
        }
        EditorSceneManager.MarkSceneDirty(room);
        EditorSceneManager.SaveScene(room);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
