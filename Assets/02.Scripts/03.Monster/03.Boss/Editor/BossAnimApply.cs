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
using UnityEditor.U2D.Sprites;
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

    // 상태명, 루프, 포함 키워드(kws), 제외 키워드(not). 포효는 인트로→루프라 커스텀(AddRoarStates).
    private static readonly (string name, bool loop, string[] kws, string[] not)[] States =
    {
        ("Idle",       true,  new[]{"탄막"},     null),          // 대기 모션 = 탄막패턴(기 모으기) 사용
        ("ChargeIdle", true,  new[]{"돌진대기"}, null),          // 돌진 전 대기(신규)
        ("Move",       true,  new[]{"이동"},     null),
        ("Stun",       true,  new[]{"스턴"},     null),
        ("Death",      false, new[]{"사망"},     null),
        ("Charge",     false, new[]{"돌진"},     new[]{"대기"}), // 돌진(대시), 돌진대기 제외
        ("Melee",      false, new[]{"퀴"},       null),          // 할퀴기(파일명 '햘퀴기' 대응)
        ("Energy",     true,  new[]{"탄막"},     null),          // 패턴 끝까지 루프
        ("Phantom",    true,  new[]{"늑대"},     null),          // 패턴 끝까지 루프
        ("Leap",       false, new[]{"점프"},     null),
        ("LeapLand",   true,  new[]{"착지"},     null),          // 착지 후 동심원 동안 루프
        ("Cast",       true,  new[]{"탄막"},     null),          // 힐토템/돌석상: 기모으기(탄막) 반복
    };

    [MenuItem("Team4/보스 애니 적용 (샌드박스)")]
    public static void Apply()
    {
        var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null).ToList();
        if (clips.Count == 0) { Debug.LogError("[BossAnimApply] 클립 없음 — 먼저 '보스 샌드박스 애니 셋업' 실행"); return; }

        var p1 = BuildController(CtrlP1, "1페이즈", "2페이즈", clips);
        var p2 = BuildController(CtrlP2, "2페이즈", "1페이즈", clips);
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

        var statueClip = clips.FirstOrDefault(c => c.name.Contains("석상") && !c.name.Contains("소환"));
        ApplyTotem(boss, statueClip);
        ApplyLeapWarning(boss);
        ApplyDashWarn(boss);
        ApplyPhantomWolf(boss);
        ApplyMeleeWarn(boss);
        ApplyFloorHazard(boss);

        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BossAnimApply] 완료 — P1(파랑)/P2(빨강) 컨트롤러 빌드 + 페이즈 자동전환 배선. " +
                  "해당 페이즈 클립이 없는 모션은 다른 페이즈 클립으로 임시 폴백됩니다(콘솔 경고 = 부족 모션).");
    }

    /// <summary>primaryPhase 클립 우선, 없으면 otherPhase 폴백으로 컨트롤러 빌드.</summary>
    private static AnimatorController BuildController(string path, string primaryPhase, string otherPhase, List<AnimationClip> clips)
    {
        AnimationClip Pick(string[] kws, string[] not)
        {
            bool Match(AnimationClip c, string ph) => c.name.Contains(ph) && kws.All(k => c.name.Contains(k))
                && (not == null || !not.Any(n => c.name.Contains(n)));
            return clips.FirstOrDefault(c => Match(c, primaryPhase)) ?? clips.FirstOrDefault(c => Match(c, otherPhase));
        }

        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        AnimatorState idle = null;

        foreach (var (name, loop, kws, not) in States)
        {
            var clip = Pick(kws, not);
            if (clip == null) { Debug.LogWarning($"[BossAnimApply] {primaryPhase} '{name}' 클립 없음(폴백도 실패)"); continue; }
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            if (s.loopTime != loop) { s.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, s); EditorUtility.SetDirty(clip); }
            // 폴백이라 다른 페이즈 클립이면 경고(부족 모션 추적용)
            if (!clip.name.Contains(primaryPhase))
                Debug.LogWarning($"[BossAnimApply] {primaryPhase} '{name}' 전용 클립 없음 → '{clip.name}'로 임시 폴백(아트 필요)");
            var st = sm.AddState(name); st.motion = clip;
            if (name == "Idle") idle = st;
        }
        AddRoarStates(sm, primaryPhase, otherPhase, clips);

        if (idle != null) sm.defaultState = idle;
        return ctrl;
    }

    /// <summary>포효: 인트로(1회) → 루프(반복) 두 상태 + 자동전환. Pattern_RoarWave는 "RoarIntro"만 재생.
    /// 1p: 포효(인트로)→포효(1)(루프) / 2p: 포효 전체모션(인트로)→포효(루프).</summary>
    private static void AddRoarStates(AnimatorStateMachine sm, string primary, string other, List<AnimationClip> clips)
    {
        // 포효는 새 파일(보스몬스터_{phase}_*)만 사용. 전체모션(1회) → 포효(반복) → 마무리모션(1회).
        string prefix = primary == "1페이즈" ? "보스몬스터_1페이즈" : "보스몬스터_2페이즈";
        AnimationClip Pick(System.Func<string, bool> extra) =>
            clips.FirstOrDefault(c => c.name.Contains(prefix) && c.name.Contains("포효") && extra(c.name));

        AnimationClip intro = Pick(n => n.Contains("전체"));                                        // 포효 전체모션
        AnimationClip loopC = Pick(n => !n.Contains("(1)") && !n.Contains("전체") && !n.Contains("마무리")) // 포효(반복)
                           ?? Pick(n => n.Contains("(1)"));
        AnimationClip outro = Pick(n => n.Contains("마무리"));                                       // 포효 마무리모션
        if (intro == null) intro = loopC;
        if (loopC == null) loopC = intro;
        if (intro == null) { Debug.LogWarning($"[BossAnimApply] {primary} 포효 클립 없음 — Roar 생략"); return; }

        SetClipLoop(intro, false);
        SetClipLoop(loopC, true);
        var introSt = sm.AddState("RoarIntro"); introSt.motion = intro;
        var loopSt  = sm.AddState("RoarLoop");  loopSt.motion = loopC;
        var tr = introSt.AddTransition(loopSt);
        tr.hasExitTime = true; tr.exitTime = 1f; tr.duration = 0f;

        if (outro != null) { SetClipLoop(outro, false); sm.AddState("RoarOutro").motion = outro; } // 패턴 끝에 1회(RoarWave가 재생)
    }

    private static void SetClipLoop(AnimationClip clip, bool loop)
    {
        var s = AnimationUtility.GetAnimationClipSettings(clip);
        if (s.loopTime != loop) { s.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, s); EditorUtility.SetDirty(clip); }
    }

    private const string RangeFxPng      = "Assets/04.Images/02.Monsters/Boss/보스패턴범위이펙트.png";
    private const string RangeWarnPrefab = AnimDir + "/RangeWarn.prefab";
    private const string RangeWarnAnim   = AnimDir + "/RangeWarn.anim";
    private const string RangeWarnCtrl   = AnimDir + "/RangeWarn.controller";
    private const string RangeMaskPrefab = AnimDir + "/RangeMask.prefab";
    private const int    RangeFxFrames   = 6;   // 가로 6프레임(투명 간격 검출 기준)

    private const string FloorWarnPng     = "Assets/04.Images/02.Monsters/Boss/보스바닥경고.png";
    private const string FloorWarnEdgePng = "Assets/04.Images/02.Monsters/Boss/보스바닥경고테두리.png";
    private const int    FloorWarnFrames  = 8;   // 4096x512 = 8프레임

    /// <summary>LeapBlast 경고 바닥 교체: 1단=보스바닥경고(꽉 찬 원), 2·3단=보스바닥경고테두리(링, 마스크 없음). 둘 다 8프레임 루프.</summary>
    private static void ApplyLeapWarning(GameObject boss)
    {
        var leap = boss.GetComponent<Pattern_LeapBlast>();
        if (leap == null) return;

        var floorWarn = BuildFloorWarn(FloorWarnPng, FloorWarnFrames, "FloorWarn");
        if (floorWarn == null) { Debug.LogWarning("[BossAnimApply] 보스바닥경고 프리팹 생성 실패 — LeapBlast 경고 생략"); return; }
        var floorEdge = BuildFloorWarn(FloorWarnEdgePng, FloorWarnFrames, "FloorWarnEdge");

        leap.telegraphPrefab        = floorWarn;                              // 착지 예고(첫 범위)
        leap.ringWarningPrefab      = floorWarn;                              // 1단(중심 원)
        leap.ringWarningPrefabOuter = floorEdge != null ? floorEdge : floorWarn; // 2·3단(테두리)
        leap.ringMaskPrefab         = null;                                  // 테두리가 이미 링 → 마스크 불필요
        leap.ringEffectPrefab       = null;
        leap.ring1Radius = 4.5f; leap.ring2Radius = 7f; leap.ring3Radius = 11.5f; // 바닥 크기 축소
        EditorUtility.SetDirty(leap);
    }

    /// <summary>가로 스트립 PNG를 cols프레임 루프 애니 프리팹으로 빌드(SpriteRenderer+Animator+PooledObject).</summary>
    private static GameObject BuildFloorWarn(string png, int cols, string baseName)
    {
        var imp = AssetImporter.GetAtPath(png) as TextureImporter;
        if (imp == null) { Debug.LogWarning($"[BossAnimApply] {png} 없음"); return null; }
        // 이미 Multiple(수동 슬라이스)이면 그대로 사용, 아니면 자동 슬라이스(가드 — 수동 작업 보존)
        Sprite[] frames = imp.spriteImportMode == SpriteImportMode.Multiple
            ? AssetDatabase.LoadAllAssetsAtPath(png).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray()
            : SliceStrip(png, cols);
        if (frames.Length == 0) { Debug.LogWarning($"[BossAnimApply] {png} 슬라이스 실패/없음"); return null; }

        // 콘텐츠(원/링)가 프레임보다 작으면(투명 여백) 그만큼 보정 — 실제 원이 반지름에 맞게.
        float ratio = ContentWidthRatio(png, cols);
        float comp = ratio > 0.05f ? 1f / ratio : 1f;

        string animPath = $"{AnimDir}/{baseName}.anim", ctrlPath = $"{AnimDir}/{baseName}.controller", prefabPath = $"{AnimDir}/{baseName}.prefab";
        var clip = new AnimationClip { frameRate = 12f };
        var binding = EditorCurveBinding.PPtrCurve("Fx", typeof(SpriteRenderer), "m_Sprite"); // 자식 Fx 대상(여백 보정 스케일 적용)
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 12f, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var cs = AnimationUtility.GetAnimationClipSettings(clip); cs.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, cs);
        AssetDatabase.DeleteAsset(animPath); AssetDatabase.CreateAsset(clip, animPath);

        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var st = ctrl.layers[0].stateMachine.AddState(baseName); st.motion = clip;
        ctrl.layers[0].stateMachine.defaultState = st;

        var go = new GameObject(baseName);
        var an = go.AddComponent<Animator>(); an.runtimeAnimatorController = ctrl;
        go.AddComponent<PooledObject>();
        var fx = new GameObject("Fx"); fx.transform.SetParent(go.transform, false);
        fx.transform.localScale = Vector3.one * comp;   // 여백 보정 → SpawnScaled가 root를 반지름에 맞춰도 실제 원이 정확
        var sr = fx.AddComponent<SpriteRenderer>(); sr.sprite = frames[0]; sr.sortingOrder = 4;
        AssetDatabase.DeleteAsset(prefabPath);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>스트립 PNG 프레임0의 비투명 콘텐츠 가로폭 / 프레임 가로폭(≤1). 여백 보정용.</summary>
    private static float ContentWidthRatio(string png, int cols)
    {
        byte[] bytes = File.ReadAllBytes(png);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(tex, bytes)) { Object.DestroyImmediate(tex); return 1f; }
        int w = tex.width, h = tex.height, fw = Mathf.Max(1, w / Mathf.Max(1, cols));
        var px = tex.GetPixels32();
        int minX = int.MaxValue, maxX = int.MinValue;
        for (int y = 0; y < h; y += 4)
            for (int x = 0; x < fw; x += 2)            // 프레임0만 검사
                if (px[y * w + x].a > 10) { if (x < minX) minX = x; if (x > maxX) maxX = x; }
        Object.DestroyImmediate(tex);
        return (maxX >= minX) ? (maxX - minX + 1) / (float)fw : 1f;
    }

    private const string MeleeFanPng = "Assets/04.Images/02.Monsters/Boss/보스몬스터 부채꼴.png";

    /// <summary>할퀴기(MeleeCombo) 경고를 '보스몬스터 부채꼴'(수동 슬라이스) 반복 애니로 연결.
    /// 꼭짓점(이미지 중앙 아랫부분)을 프리팹 root 원점에 맞춰, 보스 위치에서 +Y로 펼쳐지게 한다
    /// (MeleeCombo가 +Y를 플레이어 방향으로 회전·hitRange로 스케일).</summary>
    private static void ApplyMeleeWarn(GameObject boss)
    {
        var melee = boss.GetComponent<Pattern_MeleeCombo>();
        if (melee == null) return;
        var imp = AssetImporter.GetAtPath(MeleeFanPng) as TextureImporter;
        if (imp == null) { Debug.LogWarning("[BossAnimApply] 보스몬스터 부채꼴.png 없음"); return; }
        Sprite[] frames = imp.spriteImportMode == SpriteImportMode.Multiple
            ? AssetDatabase.LoadAllAssetsAtPath(MeleeFanPng).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray()
            : SliceStrip(MeleeFanPng, 8);
        if (frames.Length == 0) { Debug.LogWarning("[BossAnimApply] 부채꼴 슬라이스 없음"); return; }

        string animP = AnimDir + "/MeleeWarn.anim", ctrlP = AnimDir + "/MeleeWarn.controller", prefabP = AnimDir + "/MeleeWarn.prefab";
        var clip = new AnimationClip { frameRate = 12f };
        var binding = EditorCurveBinding.PPtrCurve("Fx", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 12f, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var cs = AnimationUtility.GetAnimationClipSettings(clip); cs.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, cs);
        AssetDatabase.DeleteAsset(animP); AssetDatabase.CreateAsset(clip, animP);

        AssetDatabase.DeleteAsset(ctrlP);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlP);
        var st = ctrl.layers[0].stateMachine.AddState("MeleeWarn"); st.motion = clip;
        ctrl.layers[0].stateMachine.defaultState = st;

        // 콘텐츠(부채꼴) 실제 bbox로 크기/꼭짓점을 맞춘다 — 투명 여백 때문에 '보는 것보다 멀리 때리는' 문제 해결.
        float ppu = frames[0].pixelsPerUnit;
        Rect rect = frames[0].rect;
        Vector2 pivot = frames[0].pivot; // rect 좌하단 기준 px
        var bb = ContentBBox(MeleeFanPng, rect);
        // 꼭짓점 = 이미지(프레임) 아래중앙 — 이 점이 몬스터에 붙어야 함(콘텐츠 아래끝이 아니라 프레임 아래변)
        float apexX = (rect.width * 0.5f - pivot.x) / ppu;
        float apexY = (0f - pivot.y) / ppu;
        // 시각 사거리 = 프레임 아래변 → 콘텐츠 위끝(부채꼴 호) = hitRange 가 되게 스케일
        float reachWorld = bb.ok ? bb.maxY / ppu : rect.height / ppu;
        float scale = reachWorld > 0.0001f ? melee.hitRange / reachWorld : 1f;

        var root = new GameObject("MeleeWarn");
        var an = root.AddComponent<Animator>(); an.runtimeAnimatorController = ctrl;
        root.AddComponent<PooledObject>();
        var fx = new GameObject("Fx"); fx.transform.SetParent(root.transform, false);
        fx.transform.localScale = Vector3.one * scale;
        fx.transform.localPosition = new Vector3(-apexX * scale, -apexY * scale, 0f); // 꼭짓점을 root 원점으로
        var sr = fx.AddComponent<SpriteRenderer>(); sr.sprite = frames[0]; sr.sortingOrder = 4;
        AssetDatabase.DeleteAsset(prefabP);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabP);
        Object.DestroyImmediate(root);

        melee.telegraphPrefab = prefab;
        EditorUtility.SetDirty(melee);
    }

    /// <summary>스트립 PNG에서 한 sprite rect 내 비투명 콘텐츠 bbox(px, rect 좌하단 기준, Y up).</summary>
    private static (float minX, float maxX, float minY, float maxY, bool ok) ContentBBox(string png, Rect rect)
    {
        byte[] bytes = File.ReadAllBytes(png);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(tex, bytes)) { Object.DestroyImmediate(tex); return (0, 0, 0, 0, false); }
        int W = tex.width;
        var px = tex.GetPixels32();
        int rx = (int)rect.x, ry = (int)rect.y, rw = (int)rect.width, rh = (int)rect.height;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        for (int y = 0; y < rh; y += 2)
            for (int x = 0; x < rw; x += 2)
                if (px[(ry + y) * W + (rx + x)].a > 10)
                { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
        Object.DestroyImmediate(tex);
        return (maxX >= minX) ? (minX, maxX, minY, maxY, true) : (0, 0, 0, 0, false);
    }

    private const string FloorPlatePng    = "Assets/04.Images/02.Monsters/Boss/바닥장판.png";
    private const string FloorHazardPrefab = AnimDir + "/FloorHazard.prefab";

    /// <summary>페이즈 전환 바닥 장판: '바닥장판' 애니를 오른쪽에, 좌우반전해 왼쪽에(같은 크기) 배치.
    /// FloorHazard(데미지) + 트리거 콜라이더(전체 폭)는 유지. BossPhaseController.floorHazardPrefab에 연결.</summary>
    private static void ApplyFloorHazard(GameObject boss)
    {
        var phase = boss.GetComponent<BossPhaseController>();
        if (phase == null) return;
        var imp = AssetImporter.GetAtPath(FloorPlatePng) as TextureImporter;
        if (imp == null) { Debug.LogWarning("[BossAnimApply] 바닥장판.png 없음"); return; }
        Sprite[] frames = imp.spriteImportMode == SpriteImportMode.Multiple
            ? AssetDatabase.LoadAllAssetsAtPath(FloorPlatePng).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray()
            : SliceStrip(FloorPlatePng, 12);
        if (frames.Length == 0) { Debug.LogWarning("[BossAnimApply] 바닥장판 슬라이스 없음"); return; }

        // 오른쪽/왼쪽 두 SpriteRenderer를 같은 프레임으로 동기 애니
        string animP = AnimDir + "/FloorHazard.anim", ctrlP = AnimDir + "/FloorHazard.controller";
        var clip = new AnimationClip { frameRate = 12f };
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 12f, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Right", typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Left", typeof(SpriteRenderer), "m_Sprite"), keys);
        var cs = AnimationUtility.GetAnimationClipSettings(clip); cs.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, cs);
        AssetDatabase.DeleteAsset(animP); AssetDatabase.CreateAsset(clip, animP);

        AssetDatabase.DeleteAsset(ctrlP);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlP);
        var st = ctrl.layers[0].stateMachine.AddState("FloorHazard"); st.motion = clip;
        ctrl.layers[0].stateMachine.defaultState = st;

        float w = frames[0].bounds.size.x, h = frames[0].bounds.size.y, halfW = w * 0.5f;
        var root = new GameObject("FloorHazard");
        var col = root.AddComponent<BoxCollider2D>(); col.isTrigger = true; col.size = new Vector2(w * 2f, h); // 전체 폭(오른+왼)
        root.AddComponent<FloorHazard>();   // 데미지(기본값 5/0.5)
        var an = root.AddComponent<Animator>(); an.runtimeAnimatorController = ctrl;

        var right = new GameObject("Right"); right.transform.SetParent(root.transform, false);
        right.transform.localPosition = new Vector3(halfW, 0f, 0f);
        var rsr = right.AddComponent<SpriteRenderer>(); rsr.sprite = frames[0]; rsr.sortingOrder = 2;

        var left = new GameObject("Left"); left.transform.SetParent(root.transform, false);
        left.transform.localPosition = new Vector3(-halfW, 0f, 0f);
        var lsr = left.AddComponent<SpriteRenderer>(); lsr.sprite = frames[0]; lsr.sortingOrder = 2; lsr.flipX = true; // 좌우반전

        AssetDatabase.DeleteAsset(FloorHazardPrefab);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, FloorHazardPrefab);
        Object.DestroyImmediate(root);

        phase.floorHazardPrefab = prefab;
        EditorUtility.SetDirty(phase);
        Debug.Log("[BossAnimApply] 바닥장판(오른쪽+좌우반전 왼쪽) FloorHazard 생성 + 연결");
    }

    private const string WolfPrefabPath    = "Assets/03.Prefabs/02.Monsters/Prefab_Wolf.prefab";
    private const string PhantomWolfPrefab = AnimDir + "/PhantomWolf.prefab";

    /// <summary>PhantomDash: 보라 네모 대신 실제 늑대(Prefab_Wolf) + PhantomWolfDash + 하늘색 반투명(영혼)으로
    /// PhantomWolf 프리팹을 만들어 연결. Prefab_Wolf 원본은 건드리지 않는다.</summary>
    private static void ApplyPhantomWolf(GameObject boss)
    {
        var phantom = boss.GetComponent<Pattern_PhantomDash>();
        if (phantom == null) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(WolfPrefabPath) == null) { Debug.LogWarning("[BossAnimApply] Prefab_Wolf 없음 — PhantomDash 생략"); return; }

        var contents = PrefabUtility.LoadPrefabContents(WolfPrefabPath);
        if (contents.GetComponent<PhantomWolfDash>() == null) contents.AddComponent<PhantomWolfDash>();
        var sr = contents.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.color = new Color(0.35f, 0.85f, 1f, 0.85f); // 더 진한 하늘색 영혼(잘 보이게) — MonsterController가 baseColor로 유지
        var mc = contents.GetComponent<MonsterController>();
        if (mc != null) mc.hpBarPrefab = null; // 팬텀은 체력바 숨김
        AssetDatabase.DeleteAsset(PhantomWolfPrefab);
        var prefab = PrefabUtility.SaveAsPrefabAsset(contents, PhantomWolfPrefab);
        PrefabUtility.UnloadPrefabContents(contents);

        phantom.wolfPrefab = prefab;
        EditorUtility.SetDirty(phantom);
        Debug.Log("[BossAnimApply] PhantomWolf(하늘색 영혼 늑대) 생성 + PhantomDash 연결");
    }

    private const string PathPng        = "Assets/FreePixelEffect/Path.png";   // 에셋스토어(gitignore)
    private const string DashWarnDir    = "Assets/03.Prefabs/06.Gimmicks";     // 커밋 위치(팀원 공유 — Path는 각자 임포트)
    private const string DashWarnPrefab = DashWarnDir + "/DashWarn.prefab";
    private const string DashWarnAnim   = DashWarnDir + "/DashWarn.anim";
    private const string DashWarnCtrl   = DashWarnDir + "/DashWarn.controller";
    private const string BoarPrefab     = "Assets/03.Prefabs/02.Monsters/Prefab_Boar.prefab";

    /// <summary>FreePixelEffect/Path(흐르는 쉐브론)로 빨간 돌진 경고선 프리팹을 만들고
    /// 보스 Charge.telegraphPrefab + 멧돼지 BoarGimmick.chargeWarnPrefab에 연결.
    /// Path는 gitignore 에셋이라, 미보유 시 멧돼지는 기존 LineRenderer로 폴백된다.</summary>
    private static void ApplyDashWarn(GameObject boss)
    {
        var imp = AssetImporter.GetAtPath(PathPng) as TextureImporter;
        if (imp == null) { Debug.LogWarning("[BossAnimApply] FreePixelEffect/Path.png 없음 — 돌진 경고선 생략(에셋 임포트 필요)"); return; }

        // 타일 드로우용 FullRect 메시 + 프레임=1유닛 + 픽셀 선명
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        var ts = new TextureImporterSettings(); imp.ReadTextureSettings(ts);
        ts.spriteMeshType = SpriteMeshType.FullRect; imp.SetTextureSettings(ts);
        imp.SaveAndReimport();

        var frames = AssetDatabase.LoadAllAssetsAtPath(PathPng).OfType<Sprite>()
            .OrderBy(s => s.rect.x).ToArray();
        if (frames.Length == 0) { Debug.LogWarning("[BossAnimApply] Path 프레임 슬라이스 없음 — 돌진 경고선 생략"); return; }

        // 흐르는 쉐브론 루프 클립(자식 'Fx' 대상)
        var clip = new AnimationClip { frameRate = 8f };
        var binding = EditorCurveBinding.PPtrCurve("Fx", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 8f, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var cs = AnimationUtility.GetAnimationClipSettings(clip); cs.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, cs);
        AssetDatabase.DeleteAsset(DashWarnAnim); AssetDatabase.CreateAsset(clip, DashWarnAnim);

        AssetDatabase.DeleteAsset(DashWarnCtrl);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(DashWarnCtrl);
        var st = ctrl.layers[0].stateMachine.AddState("Dash"); st.motion = clip;
        ctrl.layers[0].stateMachine.defaultState = st;

        // 프리팹: root(Animator+PooledObject) → child 'Fx'(타일 빨강 SpriteRenderer, x=길이는 런타임 설정)
        var root = new GameObject("DashWarn");
        var an = root.AddComponent<Animator>(); an.runtimeAnimatorController = ctrl;
        root.AddComponent<PooledObject>();
        var fxgo = new GameObject("Fx"); fxgo.transform.SetParent(root.transform, false);
        var sr = fxgo.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.color = new Color(1f, 0.15f, 0.15f, 0.65f);   // 반투명 빨강(조정 가능)
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(1f, 1.2f);
        sr.sortingOrder = 3;
        AssetDatabase.DeleteAsset(DashWarnPrefab);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, DashWarnPrefab);
        Object.DestroyImmediate(root);

        // 보스 Charge 연결
        var charge = boss.GetComponent<Pattern_Charge>();
        if (charge != null) { charge.telegraphPrefab = prefab; EditorUtility.SetDirty(charge); }

        // 멧돼지 프리팹 연결(있으면)
        var contents = AssetDatabase.LoadAssetAtPath<GameObject>(BoarPrefab) != null
            ? PrefabUtility.LoadPrefabContents(BoarPrefab) : null;
        if (contents != null)
        {
            var bg = contents.GetComponent<BoarGimmick>();
            if (bg != null) { bg.chargeWarnPrefab = prefab; PrefabUtility.SaveAsPrefabAsset(contents, BoarPrefab); }
            PrefabUtility.UnloadPrefabContents(contents);
        }
        Debug.Log("[BossAnimApply] 돌진 경고선(DashWarn) 빌드 + 보스 Charge/멧돼지 연결 완료");
    }

    /// <summary>가로 스트립 PNG를 cols개로 균등 슬라이스(1유닛/프레임)하고 정렬된 스프라이트 반환.</summary>
    private static Sprite[] SliceStrip(string path, int cols)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.mipmapEnabled = false;
        imp.maxTextureSize = 8192;       // 다운스케일 방지(3398px)
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return new Sprite[0];
        int w = tex.width, h = tex.height, cw = w / cols;
        imp.spritePixelsPerUnit = cw;     // 프레임 1유닛 → SpawnScaled가 반경에 맞춤
        imp.SaveAndReimport();

        var factory = new SpriteDataProviderFactories(); factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(imp); dp.InitSpriteEditorDataProvider();
        string baseName = Path.GetFileNameWithoutExtension(path);
        var rects = new List<SpriteRect>(); var pairs = new List<SpriteNameFileIdPair>();
        for (int i = 0; i < cols; i++)
        {
            var r = new SpriteRect { name = $"{baseName}_{i}", spriteID = GUID.Generate(),
                rect = new Rect(i * cw, 0, cw, h), pivot = new Vector2(0.5f, 0.5f), alignment = SpriteAlignment.Center };
            rects.Add(r); pairs.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
        }
        dp.SetSpriteRects(rects.ToArray());
        var nid = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nid != null) nid.SetNameFileIdPairs(pairs);
        dp.Apply();
        imp.SaveAndReimport();

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => { int u = s.name.LastIndexOf('_'); return (u >= 0 && int.TryParse(s.name.Substring(u + 1), out int n)) ? n : 0; })
            .ToArray();
    }

    private static void SetAnim<T>(GameObject boss, string state) where T : BossPatternBase
    {
        var p = boss.GetComponent<T>();
        if (p != null) { p.animState = state; EditorUtility.SetDirty(p); }
    }

    private static void ApplyTotem(GameObject boss, AnimationClip statueClip)
    {
        var heal = boss.GetComponent<Pattern_HealTotem>();
        if (heal == null || heal.totemPrefab == null) { Debug.LogWarning("[BossAnimApply] HealTotem/totemPrefab 없음 — 토템 스프라이트 생략"); return; }

        var statue = AssetDatabase.LoadAllAssetsAtPath(StatuePng).OfType<Sprite>().FirstOrDefault();
        string tp = AssetDatabase.GetAssetPath(heal.totemPrefab);
        if (string.IsNullOrEmpty(tp)) return;

        // 석상 클립 루프 컨트롤러(토템이 움직이도록)
        AnimatorController totemCtrl = null;
        if (statueClip != null)
        {
            var cs = AnimationUtility.GetAnimationClipSettings(statueClip);
            if (!cs.loopTime) { cs.loopTime = true; AnimationUtility.SetAnimationClipSettings(statueClip, cs); EditorUtility.SetDirty(statueClip); }
            string cpath = AnimDir + "/Totem.controller";
            AssetDatabase.DeleteAsset(cpath);
            totemCtrl = AnimatorController.CreateAnimatorControllerAtPath(cpath);
            var ts = totemCtrl.layers[0].stateMachine.AddState("Totem");
            ts.motion = statueClip;
            totemCtrl.layers[0].stateMachine.defaultState = ts;
        }

        var contents = PrefabUtility.LoadPrefabContents(tp);
        // Animator가 m_Sprite(path "")를 구동하므로 SpriteRenderer는 루트에
        var sr = contents.GetComponent<SpriteRenderer>();
        if (sr == null) sr = contents.AddComponent<SpriteRenderer>();
        if (statue != null) sr.sprite = statue;
        sr.sortingOrder = 5;
        if (totemCtrl != null)
        {
            var an = contents.GetComponent<Animator>();
            if (an == null) an = contents.AddComponent<Animator>();
            an.runtimeAnimatorController = totemCtrl;
        }

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
