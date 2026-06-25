// ============================================================
// BossAnimApply.cs  (Editor 전용)
// 슬라이스된 보스 클립 → 페이즈별 AnimatorController(P1=파랑/P2=빨강) 빌드 후 보스에 적용.
//  - 클립 이름의 "1페이즈"/"2페이즈" 접두로 페이즈를 구분.
//  - P1: 1페이즈 클립 사용(없으면 2페이즈 폴백) / P2: 2페이즈 클립 사용(없으면 1페이즈 폴백)
//  - BossAnimator가 BossPhaseController.IsPhase2 에 따라 P1↔P2 자동 전환.
//  - 패턴별 animState 지정 + 힐링 토템 스프라이트(석상) + 연결선 스텁.
// 메뉴: Team4/보스 애니 적용 (샌드박스)
// ============================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BagSurvivor.Monster;

public static class BossAnimApply
{
    private const string AnimDir   = "Assets/01.Scenes/Sandbox/BossAnims";
    private const string CtrlP1     = AnimDir + "/Boss_P1.controller";
    private const string CtrlP2     = AnimDir + "/Boss_P2.controller";
    private const string ScenePath = "Assets/01.Scenes/Sandbox/BossSandbox.unity";
    private const string StatuePng = "Assets/04.Images/02.Monsters/Boss/보스몬스터_석상.png";

    // 상태명, 루프 여부, 클립 검색 키워드
    private static readonly (string name, bool loop, string[] kws)[] States =
    {
        ("Idle",     true,  new[]{"대기"}),
        ("Move",     true,  new[]{"이동"}),
        ("Stun",     true,  new[]{"스턴"}),
        ("Death",    false, new[]{"사망"}),
        ("Charge",   false, new[]{"돌진"}),
        ("Melee",    false, new[]{"퀴"}),          // 할퀴기(파일명 '햘퀴기' 대응)
        ("Energy",   true,  new[]{"탄막"}),  // 패턴 끝날 때까지 루프
        ("Phantom",  true,  new[]{"늑대"}),  // 패턴 끝날 때까지 루프
        // Roar/RoarLoop는 포효 클립을 잘라 별도 생성(아래 BuildController에서 추가)
        ("Leap",     false, new[]{"점프"}),
        ("LeapLand", true,  new[]{"착지"}),  // 착지 후 동심원 동안 루프
        ("Cast",     false, new[]{"석상","소환"}), // 석상 소환 (HealTotem·StoneBreak 공유)
    };

    [MenuItem("Team4/보스 애니 적용 (샌드박스)")]
    public static void Apply()
    {
        var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null).ToList();
        if (clips.Count == 0) { Debug.LogError("[BossAnimApply] 클립 없음 — 먼저 '보스 샌드박스 애니 셋업' 실행"); return; }

        // 포효: '입 벌린(절정)' 프레임만 떼어낸 루프 클립(입 다문 끝부분 X)
        var roarFull = clips.FirstOrDefault(c => c.name.Contains("포효"));
        var roarLoop = roarFull != null ? MakeLoopClip(roarFull, RoarLoopStart, RoarLoopCount, AnimDir + "/RoarLoop.anim") : null;

        var p1 = BuildController(CtrlP1, "1페이즈", "2페이즈", clips, roarLoop);
        var p2 = BuildController(CtrlP2, "2페이즈", "1페이즈", clips, roarLoop);
        AssetDatabase.SaveAssets();

        // 씬 + 보스
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!File.Exists(ScenePath)) { Debug.LogError("[BossAnimApply] 샌드박스 씬 없음 — Tools/Boss/① 샌드박스 빌드 먼저"); return; }
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        var driver = Object.FindFirstObjectByType<BossPatternDriver>();
        if (driver == null) { Debug.LogError("[BossAnimApply] 씬에 보스(BossPatternDriver) 없음 — 샌드박스 빌드 먼저"); return; }
        GameObject boss = driver.gameObject;

        var animator = boss.GetComponent<Animator>(); if (animator == null) animator = boss.AddComponent<Animator>();
        animator.runtimeAnimatorController = p1;
        var ba = boss.GetComponent<BossAnimator>(); if (ba == null) ba = boss.AddComponent<BossAnimator>();
        ba.animator = animator;
        ba.phase1Controller = p1;
        ba.phase2Controller = p2;

        // 패턴별 animState
        SetAnim<Pattern_Charge>(boss, "Charge");
        SetAnim<Pattern_MeleeCombo>(boss, "Melee");
        SetAnim<Pattern_EnergyBlast>(boss, "Energy");
        SetAnim<Pattern_RoarWave>(boss, "Roar");
        SetAnim<Pattern_PhantomDash>(boss, "Phantom");
        SetAnim<Pattern_LeapBlast>(boss, "Leap");
        SetAnim<Pattern_HealTotem>(boss, "Cast");
        SetAnim<Pattern_StoneBreak>(boss, "Cast");

        ApplyTotem(boss);

        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BossAnimApply] 완료 — P1(파랑)/P2(빨강) 컨트롤러 빌드 + 페이즈 자동전환 배선. " +
                  "해당 페이즈 클립이 없는 모션은 다른 페이즈 클립으로 임시 폴백됩니다(콘솔 경고 = 부족 모션).");
    }

    // 포효 루프 구간(입 벌린 절정 프레임). 측정상 f5~f7이 가장 크게 벌어짐. 어색하면 조절.
    private const int RoarLoopStart = 5;
    private const int RoarLoopCount = 3;

    /// <summary>primaryPhase 클립 우선, 없으면 otherPhase 폴백으로 컨트롤러 빌드. roarLoop=포효 꼬리 루프.</summary>
    private static AnimatorController BuildController(string path, string primaryPhase, string otherPhase, List<AnimationClip> clips, AnimationClip roarLoop)
    {
        AnimationClip Pick(string[] kws)
        {
            bool Match(AnimationClip c, string ph) => c.name.Contains(ph) && kws.All(k => c.name.Contains(k));
            return clips.FirstOrDefault(c => Match(c, primaryPhase)) ?? clips.FirstOrDefault(c => Match(c, otherPhase));
        }

        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        AnimatorState idle = null;

        foreach (var (name, loop, kws) in States)
        {
            var clip = Pick(kws);
            if (clip == null) { Debug.LogWarning($"[BossAnimApply] {primaryPhase} '{name}' 클립 없음(폴백도 실패)"); continue; }
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            if (s.loopTime != loop) { s.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, s); EditorUtility.SetDirty(clip); }
            // 폴백이라 다른 페이즈 클립이면 경고(부족 모션 추적용)
            if (!clip.name.Contains(primaryPhase))
                Debug.LogWarning($"[BossAnimApply] {primaryPhase} '{name}' 전용 클립 없음 → '{clip.name}'로 임시 폴백(아트 필요)");
            var st = sm.AddState(name); st.motion = clip;
            if (name == "Idle") idle = st;
        }
        // 포효 꼬리 루프 상태(입 벌리고 계속)
        if (roarLoop != null) { var rl = sm.AddState("RoarLoop"); rl.motion = roarLoop; }
        if (idle != null) sm.defaultState = idle;
        return ctrl;
    }

    /// <summary>src 클립의 [start, start+count) 프레임만 떼어 루프 클립으로 만든다(입 벌린 포효 구간).</summary>
    private static AnimationClip MakeLoopClip(AnimationClip src, int start, int count, string path)
    {
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = AnimationUtility.GetObjectReferenceCurve(src, binding);
        if (keys == null || keys.Length == 0) return null;

        int s0 = Mathf.Clamp(start, 0, keys.Length - 1);
        int k = Mathf.Clamp(count, 1, keys.Length - s0);
        float fps = src.frameRate > 0f ? src.frameRate : 12f;
        var clip = new AnimationClip { frameRate = fps };
        var seg = new ObjectReferenceKeyframe[k];
        for (int i = 0; i < k; i++)
            seg[i] = new ObjectReferenceKeyframe { time = i / fps, value = keys[s0 + i].value };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, seg);
        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, s);

        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void SetAnim<T>(GameObject boss, string state) where T : BossPatternBase
    {
        var p = boss.GetComponent<T>();
        if (p != null) { p.animState = state; EditorUtility.SetDirty(p); }
    }

    private static void ApplyTotem(GameObject boss)
    {
        var heal = boss.GetComponent<Pattern_HealTotem>();
        if (heal == null || heal.totemPrefab == null) { Debug.LogWarning("[BossAnimApply] HealTotem/totemPrefab 없음 — 토템 스프라이트 생략"); return; }

        var statue = AssetDatabase.LoadAllAssetsAtPath(StatuePng).OfType<Sprite>().FirstOrDefault();
        string tp = AssetDatabase.GetAssetPath(heal.totemPrefab);
        if (string.IsNullOrEmpty(tp)) return;

        var contents = PrefabUtility.LoadPrefabContents(tp);
        var sr = contents.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) sr = contents.AddComponent<SpriteRenderer>();
        if (statue != null) sr.sprite = statue;
        sr.sortingOrder = 5;

        var lr = contents.GetComponent<LineRenderer>();
        if (lr == null) lr = contents.AddComponent<LineRenderer>();
        lr.enabled = false;            // TODO: 토템→보스 연결선(힐 시각화) 구현 시 활성화
        lr.positionCount = 2;
        lr.startWidth = lr.endWidth = 0.15f;

        PrefabUtility.SaveAsPrefabAsset(contents, tp);
        PrefabUtility.UnloadPrefabContents(contents);
    }
}
#endif
