// ============================================================
// BossSandboxAnimSetup.cs  (Editor 전용)
// 보스 스프라이트 시트(04.Images/02.Monsters/Boss)를 일괄 슬라이스 →
// 모션별 AnimationClip + AnimatorController 생성 → BossSandbox 씬에 SandboxBoss 배치.
//  - 슬라이스: 가로 스트립, 열 수 = round(폭/1536), 1행 균등 분할 (Unity 6 ISpriteEditorDataProvider)
//  - 대형 텍스처(12288px)라 maxTextureSize=16384로 다운스케일 방지
// 메뉴: Team4/보스 샌드박스 애니 셋업
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

public static class BossSandboxAnimSetup
{
    private const string ImgDir    = "Assets/04.Images/02.Monsters/Boss";
    private const string AnimDir   = "Assets/01.Scenes/Sandbox/BossAnims";
    private const string CtrlPath  = AnimDir + "/SandboxBoss.controller";
    private const string ScenePath = "Assets/01.Scenes/Sandbox/BossSandbox.unity";

    private const int   FrameWidth = 1536; // 한 프레임 기준 폭(시트 폭/이 값 = 프레임 수)
    private const float PPU        = 256f;
    private const float FPS        = 12f;

    // 루프 재생할 모션(키워드 포함 시 loop)
    private static readonly string[] LoopKeywords = { "대기", "이동", "스턴", "탄막" };

    [MenuItem("Team4/보스 샌드박스 애니 셋업")]
    public static void Setup()
    {
        if (!Directory.Exists(ImgDir)) { Debug.LogError("[BossAnim] 이미지 폴더 없음: " + ImgDir); return; }
        if (!AssetDatabase.IsValidFolder(AnimDir)) CreateFolderRecursive(AnimDir);

        // 이름 바뀐 PNG로 새로 만들기 전에 옛 클립 정리(스테일 제거)
        foreach (var old in Directory.GetFiles(AnimDir, "*.anim"))
            AssetDatabase.DeleteAsset(old.Replace('\\', '/'));

        // 보스 시트만 처리(KakaoTalk 등 참조 이미지 제외)
        string[] pngs = Directory.GetFiles(ImgDir, "*.png").Select(p => p.Replace('\\', '/'))
            .Where(p => Path.GetFileName(p).Contains("보스몬스터")).OrderBy(p => p).ToArray();
        if (pngs.Length == 0) { Debug.LogError("[BossAnim] 보스 PNG 없음: " + ImgDir); return; }

        var clips = new List<AnimationClip>();
        AnimationClip idleClip = null;

        foreach (string png in pngs)
        {
            // [가드] 이미 Multiple(수동 슬라이스)로 설정된 파일은 절대 재임포트/재슬라이스하지 않는다.
            // (SliceGrid가 PPU·maxTextureSize·필터·프레임분할 등 설정을 덮어써 수동 작업을 날리는 사고 방지)
            // Single 등 아직 안 잘린 파일만 자동 슬라이스한다.
            var imp = AssetImporter.GetAtPath(png) as TextureImporter;
            bool alreadySliced = imp != null && imp.spriteImportMode == SpriteImportMode.Multiple;
            if (!alreadySliced)
            {
                if (SliceGrid(png) <= 0) { Debug.LogWarning("[BossAnim] 자동 슬라이스 실패(수동으로 잘라주세요): " + png); continue; }
            }

            Sprite[] frames = LoadSpritesSorted(png);
            if (frames.Length == 0) { Debug.LogWarning("[BossAnim] 슬라이스된 프레임 없음(Sprite Editor에서 Apply 했는지 확인): " + png); continue; }

            string baseName = Path.GetFileNameWithoutExtension(png);
            bool loop = LoopKeywords.Any(k => baseName.Contains(k));
            AnimationClip clip = CreateClip(frames, baseName, loop);
            clips.Add(clip);
            if (baseName.Contains("대기") && !baseName.Contains("돌진")) idleClip = clip; // '돌진대기' 오탐 제외
            Debug.Log($"[BossAnim] {baseName}: {frames.Length}프레임, loop={loop}");
        }

        if (clips.Count == 0) { Debug.LogError("[BossAnim] 생성된 클립 없음"); return; }
        if (idleClip == null) idleClip = clips[0];

        AnimatorController ctrl = BuildController(clips, idleClip);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        PlaceInScene(ctrl, idleClip);
        Debug.Log($"[BossAnim] 완료 — {clips.Count}개 모션 슬라이스/클립 생성, SandboxBoss 배치(기본=대기). " +
                  "씬에서 크기는 SandboxBoss 스케일로 조정하세요.");
    }

    // ── 전체 모션을 씬에 격자 배치(미리보기: 전부 루프) ──────────
    [MenuItem("Team4/보스 샌드박스 전체 모션 배치")]
    public static void PlaceAll()
    {
        if (!AssetDatabase.IsValidFolder(AnimDir))
        { Debug.LogError("[BossAnim] 애니 폴더 없음 — 먼저 '보스 샌드박스 애니 셋업' 실행: " + AnimDir); return; }

        string[] clipPaths = Directory.GetFiles(AnimDir, "*.anim").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();
        var clips = clipPaths.Select(AssetDatabase.LoadAssetAtPath<AnimationClip>).Where(c => c != null).ToList();
        if (clips.Count == 0) { Debug.LogError("[BossAnim] .anim 없음 — 먼저 애니 셋업 실행"); return; }

        // 씬 준비
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!File.Exists(ScenePath)) { Debug.LogError("[BossAnim] 샌드박스 씬 없음: " + ScenePath); return; }
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // 이전 미리보기 오브젝트 정리
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "SandboxBoss" || root.name.StartsWith("Boss_"))
                Object.DestroyImmediate(root);

        const int perRow = 4;
        const float sx = 12f, sy = 12f;
        for (int i = 0; i < clips.Count; i++)
        {
            AnimationClip clip = clips[i];

            // 미리보기는 전부 루프로(움직임 확인용). 실제 once/loop는 다음 작업에서 행동 배선 시 설정.
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            if (!s.loopTime) { s.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, s); EditorUtility.SetDirty(clip); }

            // 클립 1개짜리 미리보기 컨트롤러
            string pcPath = $"{AnimDir}/preview_{clip.name}.controller";
            AssetDatabase.DeleteAsset(pcPath);
            var pc = AnimatorController.CreateAnimatorControllerAtPath(pcPath);
            var st = pc.layers[0].stateMachine.AddState(clip.name);
            st.motion = clip;
            pc.layers[0].stateMachine.defaultState = st;

            var go = new GameObject("Boss_" + clip.name);
            var sr = go.AddComponent<SpriteRenderer>();
            var frames = AnimationUtility.GetObjectReferenceCurve(
                clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
            if (frames != null && frames.Length > 0) sr.sprite = frames[0].value as Sprite;
            sr.sortingOrder = 10;
            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = pc;

            int col = i % perRow, row = i / perRow;
            go.transform.position = new Vector3(col * sx, -row * sy, 0f);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BossAnim] 전체 {clips.Count}개 모션을 격자로 배치(전부 루프). Play로 동시에 움직임 확인하세요.");
    }

    // ── 크기 통일: 모든 보스 시트를 '2페이즈 이동' 크기에 맞춤 (PPU만 변경) ──
    // PNG를 직접 디코드해 콘텐츠 높이를 재고, spritePixelsPerUnit만 바꾼다.
    // 슬라이스 사각형/모드/기타 임포트 설정은 전혀 건드리지 않는다(PPU는 독립 설정).
    [MenuItem("Team4/보스 애니 크기 통일 (2p 이동 기준)")]
    public static void NormalizeSizes()
    {
        if (!Directory.Exists(ImgDir)) { Debug.LogError("[BossAnim] 이미지 폴더 없음: " + ImgDir); return; }
        string[] pngs = Directory.GetFiles(ImgDir, "*.png").Select(p => p.Replace('\\', '/'))
            .Where(p => Path.GetFileName(p).Contains("보스몬스터")).ToArray();

        // 기준 = 2페이즈 이동
        string refPng = pngs.FirstOrDefault(p => { var n = Path.GetFileName(p); return n.Contains("2페이즈") && n.Contains("이동"); });
        if (refPng == null) { Debug.LogError("[BossAnim] 기준 '2페이즈 이동' 파일을 못 찾음"); return; }
        int refH = ContentHeight(refPng);
        if (refH <= 0) { Debug.LogError("[BossAnim] 기준 콘텐츠 높이 측정 실패"); return; }
        var refImp = AssetImporter.GetAtPath(refPng) as TextureImporter;
        float refPpu = refImp != null ? refImp.spritePixelsPerUnit : 256f;

        int done = 0;
        foreach (string png in pngs)
        {
            int h = ContentHeight(png);
            if (h <= 0) { Debug.LogWarning("[BossAnim] 콘텐츠 높이 측정 실패: " + Path.GetFileName(png)); continue; }
            var imp = AssetImporter.GetAtPath(png) as TextureImporter;
            if (imp == null) continue;
            float ppu = refPpu * h / refH;                 // 높이 비례 → 같은 표시 크기
            if (Mathf.Abs(imp.spritePixelsPerUnit - ppu) < 0.05f) continue;
            imp.spritePixelsPerUnit = ppu;                 // PPU만 변경(슬라이스 보존)
            imp.SaveAndReimport();
            done++;
            Debug.Log($"[BossAnim] {Path.GetFileName(png)}: 콘텐츠H={h} → PPU={ppu:F1}");
        }
        Debug.Log($"[BossAnim] 크기 통일 완료 — {done}개 PPU 조정(2p 이동={refH}px 기준). " +
                  "이펙트 큰 시트(포효/탄막/늑대/햘퀴기/석상)는 보스 몸이 작아 보이면 개별 조정 요청하세요.");
    }

    /// <summary>PNG 파일을 직접 디코드(임포트 설정 무관)해 비투명 콘텐츠의 세로 높이(px)를 반환.</summary>
    private static int ContentHeight(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(tex, bytes)) { Object.DestroyImmediate(tex); return 0; }
        int w = tex.width, hh = tex.height;
        var px = tex.GetPixels32();
        int minY = int.MaxValue, maxY = int.MinValue;
        for (int y = 0; y < hh; y++)
        {
            int row = y * w; bool any = false;
            for (int x = 0; x < w; x += 4) { if (px[row + x].a > 10) { any = true; break; } }
            if (any) { if (y < minY) minY = y; if (y > maxY) maxY = y; }
        }
        Object.DestroyImmediate(tex);
        return (maxY >= minY) ? (maxY - minY + 1) : 0;
    }

    // ── 슬라이스 (Unity 6 ISpriteEditorDataProvider) ──────────────
    private static int SliceGrid(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return 0;

        importer.textureType        = TextureImporterType.Sprite;
        importer.spriteImportMode   = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PerSheetPPU(path); // 모션별 PPU로 캐릭터 크기를 이동(Move)에 맞춤
        importer.mipmapEnabled      = false;
        importer.filterMode         = FilterMode.Bilinear;
        importer.maxTextureSize     = 16384; // 12288px 다운스케일 방지(필수)
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return 0;
        int w = tex.width, h = tex.height;
        int cols = FrameCount(w, h);
        int cw = w / cols;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
        dp.InitSpriteEditorDataProvider();

        string baseName = Path.GetFileNameWithoutExtension(path);
        var rects = new List<SpriteRect>();
        var pairs = new List<SpriteNameFileIdPair>();
        for (int i = 0; i < cols; i++)
        {
            var sr = new SpriteRect
            {
                name      = $"{baseName}_{i}",
                spriteID  = GUID.Generate(),
                rect      = new Rect(i * cw, 0, cw, h), // 가로 스트립, 1행(아래원점 y=0)
                pivot     = new Vector2(0.5f, 0.5f),
                alignment = SpriteAlignment.Center
            };
            rects.Add(sr);
            pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
        }

        dp.SetSpriteRects(rects.ToArray());
        var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameIdDp != null) nameIdDp.SetNameFileIdPairs(pairs);
        dp.Apply();
        importer.SaveAndReimport();
        return cols;
    }

    /// <summary>시트 크기별 실제 프레임 수(콘텐츠 분석으로 확정). 셀폭 1536→8/6, 1024→12, 일부 특수.
    /// round(폭/1536)로는 12·10프레임짜리가 8칸으로 잘려 캐릭터가 옆으로 밀렸음.</summary>
    private static int FrameCount(int w, int h)
    {
        if (w == 12288 && h == 1536) return 8;  // 대기/이동/스턴/탄막
        if (w == 12288 && h == 1920) return 8;  // 석상
        if (w == 9216  && h == 1536) return 6;  // 사망
        if (w == 12288 && h == 1230) return 10; // 착지
        if (w == 7245) return 4;                // 할퀴기
        if (w == 12288) return 12;              // 늑대소환/돌진/석상소환/점프/포효 (셀 1024)
        return Mathf.Max(1, Mathf.RoundToInt(w / (float)FrameWidth)); // 기타 폴백
    }

    /// <summary>모션별 PPU. 시트마다 캐릭터를 다른 크기로 그려서, 프레임당 몸 면적(픽셀 수) 기준
    /// 이동(Move)에 맞춘 값(포즈에 덜 휘둘림). 낮을수록 크게 보임.</summary>
    private static float PerSheetPPU(string path)
    {
        string n = System.IO.Path.GetFileName(path);
        if (n.Contains("이동")) return 256f;   // 기준
        if (n.Contains("대기")) return 286f;
        if (n.Contains("돌진")) return 98f;
        if (n.Contains("퀴"))   return 240f;   // 할퀴기
        if (n.Contains("탄막")) return 274f;
        if (n.Contains("늑대")) return 92f;
        if (n.Contains("포효")) return 126f;
        if (n.Contains("점프")) return 130f;   // 더 크게(이동보다 약간 큼)
        if (n.Contains("착지")) return 122f;   // 더 크게
        if (n.Contains("스턴")) return 210f;
        if (n.Contains("사망")) return 186f;
        if (n.Contains("석상") && n.Contains("소환")) return 206f;
        if (n.Contains("석상")) return 273f;   // 토템 오브젝트
        return 256f;
    }

    private static Sprite[] LoadSpritesSorted(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(s => IndexFromName(s.name))
            .ToArray();
    }

    private static int IndexFromName(string n)
    {
        int u = n.LastIndexOf('_');
        if (u >= 0 && int.TryParse(n.Substring(u + 1), out int idx)) return idx;
        return 0;
    }

    // ── 클립 생성 ────────────────────────────────────────────────
    private static AnimationClip CreateClip(Sprite[] frames, string name, bool loop)
    {
        var clip = new AnimationClip { frameRate = FPS };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / FPS, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string clipPath = $"{AnimDir}/{name}.anim";
        AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    // ── 애니메이터 컨트롤러 ──────────────────────────────────────
    private static AnimatorController BuildController(List<AnimationClip> clips, AnimationClip idle)
    {
        AssetDatabase.DeleteAsset(CtrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        var sm = ctrl.layers[0].stateMachine;

        AnimatorState idleState = null;
        foreach (var clip in clips)
        {
            var st = sm.AddState(clip.name);
            st.motion = clip;
            if (clip == idle) idleState = st;
        }
        if (idleState != null) sm.defaultState = idleState;
        return ctrl;
    }

    // ── 씬 배치 ──────────────────────────────────────────────────
    // SandboxBoss 미리보기 오브젝트는 더 이상 만들지 않는다 — 실제 보스(WolfBoss)와 중복돼
    // '맵 중앙에 또 다른 보스'처럼 보였고, idle 오탐으로 돌진대기를 반복했음. 남아있던 미리보기만 제거.
    private static void PlaceInScene(AnimatorController ctrl, AnimationClip idle)
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!File.Exists(ScenePath)) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        var existing = GameObject.Find("SandboxBoss");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void CreateFolderRecursive(string folder)
    {
        string[] parts = folder.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
