// ============================================================
// BossScaleAdjust.cs  (Editor 전용)
// 샌드박스 보스를 '모두 비례대로' 축소(60% → 40%, ×2/3).
//  - 보스 transform.localScale: 자식 시각효과(데미지감소 FX 등)까지 함께 축소
//  - 패턴/드라이버의 공간(반경·거리·사거리)·속도 float 필드: SpawnScaled가 절대 m값을
//    쓰므로 이 값들을 같이 줄여야 바닥경고·부채꼴 등 연출이 비례해 작아짐
//  - 시간(쿨다운·텔레그래프·간격)·확률·각도·횟수는 제외(공간 아님)
// 메뉴: Team4/보스 크기 비례 축소 (60%→40%)
// ※ 이 작업 후 '보스 애니 적용'을 다시 누르면 부채꼴 등 일부 연출이 콘텐츠 기준으로
//   재계산될 수 있으니, 곧바로 '보스 프리팹화 + 보스룸 배치'로 마무리하세요.
// ============================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BagSurvivor.Monster;

public static class BossScaleAdjust
{
    private const string SandboxScene = "Assets/01.Scenes/Sandbox/BossSandbox.unity";
    private const float Factor = 2f / 3f; // 현재 60% → 40%

    private static readonly string[] ScaleKeys = { "radius", "range", "distance", "reach", "width", "length", "speed", "size", "offset", "height" };
    private static readonly string[] SkipKeys  = { "time", "duration", "cooldown", "interval", "gap", "delay", "chance", "count", "percent", "ratio", "angle", "fps", "hp", "damage", "amount" };

    [MenuItem("Team4/보스 크기 비례 축소 (60%→40%)")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != SandboxScene)
        {
            if (!File.Exists(SandboxScene)) { Debug.LogError("[BossScale] 샌드박스 씬 없음: " + SandboxScene); return; }
            scene = EditorSceneManager.OpenScene(SandboxScene, OpenSceneMode.Single);
        }
        var driver = Object.FindFirstObjectByType<BossPatternDriver>();
        if (driver == null) { Debug.LogError("[BossScale] 보스(BossPatternDriver) 없음"); return; }
        GameObject boss = driver.gameObject;

        // 1) 트랜스폼(자식 시각효과 포함) 비례 축소
        boss.transform.localScale *= Factor;

        // 2) 패턴/드라이버의 공간·속도 필드 축소
        var changed = new List<string>();
        foreach (var mb in boss.GetComponents<MonoBehaviour>())
        {
            if (!(mb is BossPatternBase) && !(mb is BossPatternDriver)) continue;
            foreach (var f in mb.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType != typeof(float)) continue;
                string n = f.Name.ToLowerInvariant();
                if (SkipKeys.Any(k => n.Contains(k))) continue;
                if (!ScaleKeys.Any(k => n.Contains(k))) continue;
                float v = (float)f.GetValue(mb);
                f.SetValue(mb, v * Factor);
                changed.Add($"{mb.GetType().Name}.{f.Name}: {v:0.##} → {v * Factor:0.##}");
            }
            EditorUtility.SetDirty(mb);
        }

        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BossScale] 보스 스케일 ×{Factor:0.###} + 공간/속도 필드 {changed.Count}개 축소:\n  " +
                  string.Join("\n  ", changed) +
                  "\n곧바로 '보스 프리팹화 + 보스룸 배치'로 마무리하세요(재적용 시 일부 연출 재계산 주의).");
    }
}
#endif
