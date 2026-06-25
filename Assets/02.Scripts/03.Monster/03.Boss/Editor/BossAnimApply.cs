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
        ("Roar",     true,  new[]{"포효"}),  // 반복 애니(입 벌리고 포효) — 패턴 끝까지 루프
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
        if (idle != null) sm.defaultState = idle;
        return ctrl;
    }

    private const string RangeFxPng      = "Assets/04.Images/02.Monsters/Boss/보스패턴범위이펙트.png";
    private const string RangeWarnPrefab = AnimDir + "/RangeWarn.prefab";
    private const string RangeWarnAnim   = AnimDir + "/RangeWarn.anim";
    private const string RangeWarnCtrl   = AnimDir + "/RangeWarn.controller";
    private const string RangeMaskPrefab = AnimDir + "/RangeMask.prefab";
    private const int    RangeFxFrames   = 6;   // 가로 6프레임(투명 간격 검출 기준)

    /// <summary>LeapBlast 경고 바닥(telegraph·ringWarning)을 '보스패턴범위이펙트' 반복 애니 프리팹으로 교체.
    /// (다른 패턴이 공유하는 telePrefab은 안 건드림)</summary>
    private static void ApplyLeapWarning(GameObject boss)
    {
        var leap = boss.GetComponent<Pattern_LeapBlast>();
        if (leap == null) return;
        if (AssetImporter.GetAtPath(RangeFxPng) == null) { Debug.LogWarning("[BossAnimApply] 보스패턴범위이펙트.png 없음 — 경고 바닥 교체 생략"); return; }

        Sprite[] frames = SliceStrip(RangeFxPng, RangeFxFrames);
        if (frames.Length == 0) { Debug.LogWarning("[BossAnimApply] 범위이펙트 슬라이스 실패"); return; }

        // 반복 클립
        var clip = new AnimationClip { frameRate = 12f };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 12f, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var cs = AnimationUtility.GetAnimationClipSettings(clip); cs.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, cs);
        AssetDatabase.DeleteAsset(RangeWarnAnim); AssetDatabase.CreateAsset(clip, RangeWarnAnim);

        AssetDatabase.DeleteAsset(RangeWarnCtrl);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(RangeWarnCtrl);
        var st = ctrl.layers[0].stateMachine.AddState("RangeWarn"); st.motion = clip;
        ctrl.layers[0].stateMachine.defaultState = st;

        var go = new GameObject("RangeWarn");
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = frames[0]; sr.sortingOrder = 4;
        sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask; // 마스크(안쪽) 밖만 표시 → 도넛
        var an = go.AddComponent<Animator>(); an.runtimeAnimatorController = ctrl;
        go.AddComponent<PooledObject>();
        AssetDatabase.DeleteAsset(RangeWarnPrefab);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, RangeWarnPrefab);
        Object.DestroyImmediate(go);

        // 안쪽(이미 터진 범위) 가리개 — SpriteMask 프리팹
        var mgo = new GameObject("RangeMask");
        var sm = mgo.AddComponent<SpriteMask>(); sm.sprite = frames[0];
        mgo.AddComponent<PooledObject>();
        AssetDatabase.DeleteAsset(RangeMaskPrefab);
        var maskPrefab = PrefabUtility.SaveAsPrefabAsset(mgo, RangeMaskPrefab);
        Object.DestroyImmediate(mgo);

        leap.telegraphPrefab = prefab;   // 착지 예고(첫 범위, 마스크 없음 → 꽉 찬 원)
        leap.ringWarningPrefab = prefab; // 동심원 경고 바닥
        leap.ringMaskPrefab = maskPrefab; // 2단+ 안쪽 가림 → 도넛
        leap.ringEffectPrefab = null;    // 원래 더미 네모 폭발 제거(범위 애니가 시각 담당)
        EditorUtility.SetDirty(leap);
    }

    private const string PathPng        = "Assets/FreePixelEffect/Path.png";   // 에셋스토어(gitignore)
    private const string DashWarnPrefab = AnimDir + "/DashWarn.prefab";
    private const string DashWarnAnim   = AnimDir + "/DashWarn.anim";
    private const string DashWarnCtrl   = AnimDir + "/DashWarn.controller";
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
