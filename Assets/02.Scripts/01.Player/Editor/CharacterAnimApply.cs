// ============================================================
// CharacterAnimApply.cs  (Editor 전용)
// 04.Images/03.Players의 5개 캐릭터 시트(상/하/좌/우/중간, 각 8프레임 슬라이스 완료)로
// 방향 애니 클립 + 2D 방향 블렌드트리 컨트롤러를 만들어 Player 프리팹에 입힌다.
//  - 슬라이스는 절대 다시 하지 않는다(이미 8프레임으로 잘라놓음). 기존 스프라이트만 사용.
//  - 클립 path는 ""(루트 SpriteRenderer 기준) — Player는 루트에 SpriteRenderer가 있음.
// 메뉴: Team4/캐릭터 애니 적용
// ============================================================
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CharacterAnimApply
{
    private const string ImgDir       = "Assets/04.Images/03.Players";
    private const string OutDir       = "Assets/07.Animations/01.Players";
    private const string CtrlPath     = OutDir + "/Player.controller";
    private const string PlayerPrefab = "Assets/03.Prefabs/01.Players/Player.prefab";
    private const float  FPS          = 12f;

    // (출력 클립명, PNG 파일, 블렌드 위치) — 중간=Idle@(0,0)
    private static readonly (string clip, string png, Vector2 pos)[] Dirs =
    {
        ("Player_Idle",  "Character_Move_Idle.png",   new Vector2( 0f,  0f)),
        ("Player_Up",    "Character_Move_Back.png",   new Vector2( 0f,  1f)),
        ("Player_Down",  "Character_Move_Front.png",  new Vector2( 0f, -1f)),
        ("Player_Left",  "characters_ Move_left.png", new Vector2(-1f,  0f)),
        ("Player_Right", "Character_Move_Right.png",  new Vector2( 1f,  0f)),
    };

    [MenuItem("Team4/캐릭터 애니 적용")]
    public static void Apply()
    {
        if (!AssetDatabase.IsValidFolder(OutDir)) { Debug.LogError("[CharAnim] 출력 폴더 없음: " + OutDir); return; }

        // 1) 클립 생성(8프레임, 루프). 슬라이스는 건드리지 않음.
        var clips = new System.Collections.Generic.Dictionary<string, AnimationClip>();
        foreach (var d in Dirs)
        {
            string png = ImgDir + "/" + d.png;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(png).OfType<Sprite>()
                .OrderByDescending(s => s.rect.y).ThenBy(s => s.rect.x).ToArray(); // 행 위→아래, 열 좌→우
            if (sprites.Length == 0) { Debug.LogWarning($"[CharAnim] 슬라이스 없음(건너뜀): {png}"); continue; }

            var clip = new AnimationClip { frameRate = FPS };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / FPS, value = sprites[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var s = AnimationUtility.GetAnimationClipSettings(clip); s.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, s);

            string outPath = OutDir + "/" + d.clip + ".anim";
            AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(clip, outPath);
            clips[d.clip] = clip;
            Debug.Log($"[CharAnim] {d.clip}: {sprites.Length}프레임");
        }
        if (clips.Count == 0) { Debug.LogError("[CharAnim] 생성된 클립 없음 — 슬라이스 확인"); return; }

        // 2) 컨트롤러: 2D Simple Directional 블렌드트리(MoveX/MoveY)
        AssetDatabase.DeleteAsset(CtrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveY", AnimatorControllerParameterType.Float);

        var bt = new BlendTree
        {
            name = "Move",
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveY",
        };
        foreach (var d in Dirs)
            if (clips.TryGetValue(d.clip, out var c)) bt.AddChild(c, d.pos);
        AssetDatabase.AddObjectToAsset(bt, ctrl); // 블렌드트리를 컨트롤러의 서브에셋으로 저장

        var sm = ctrl.layers[0].stateMachine;
        var state = sm.AddState("Move");
        state.motion = bt;
        sm.defaultState = state;

        AssetDatabase.SaveAssets();

        // 3) Player 프리팹에 Animator + 컨트롤러 + 방향 애니 컴포넌트
        var contents = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        var animator = contents.GetComponent<Animator>(); if (animator == null) animator = contents.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        if (contents.GetComponent<PlayerDirectionalAnimator>() == null) contents.AddComponent<PlayerDirectionalAnimator>();
        PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefab);
        PrefabUtility.UnloadPrefabContents(contents);

        AssetDatabase.SaveAssets();
        Debug.Log("[CharAnim] 완료 — 5개 클립 + 방향 컨트롤러 + Player 프리팹 배선(정지=Idle, 입력 방향 클립).");
    }

    // ── 크기 맞춤: 5개 이동 시트를 Main_Character와 같은 표시 크기로(PPU만 변경, 슬라이스 보존) ──
    [MenuItem("Team4/캐릭터 애니 크기 맞춤 (Main_Character 기준)")]
    public static void MatchSize()
    {
        string refPng = ImgDir + "/Main_Character.png";
        int refH = ContentHeight(refPng);
        if (refH <= 0) { Debug.LogError("[CharAnim] Main_Character 콘텐츠 높이 측정 실패"); return; }
        var refImp = AssetImporter.GetAtPath(refPng) as TextureImporter;
        float refPpu = refImp != null ? refImp.spritePixelsPerUnit : 100f;

        int done = 0;
        foreach (var d in Dirs)
        {
            string png = ImgDir + "/" + d.png;
            int h = ContentHeight(png);
            if (h <= 0) { Debug.LogWarning("[CharAnim] 콘텐츠 높이 측정 실패: " + d.png); continue; }
            var imp = AssetImporter.GetAtPath(png) as TextureImporter;
            if (imp == null) continue;
            float ppu = refPpu * h / refH;                  // 콘텐츠 높이 비례 → 같은 표시 크기
            if (Mathf.Abs(imp.spritePixelsPerUnit - ppu) < 0.05f) continue;
            imp.spritePixelsPerUnit = ppu;                  // PPU만 변경(슬라이스/클립 보존)
            imp.SaveAndReimport();
            done++;
            Debug.Log($"[CharAnim] {d.png}: 콘텐츠H={h} → PPU={ppu:F1}");
        }
        Debug.Log($"[CharAnim] 크기 맞춤 완료 — {done}개 PPU 조정(Main_Character {refH}px 기준).");
    }

    /// <summary>PNG를 직접 디코드(임포트 설정 무관)해 비투명 콘텐츠의 세로 높이(px) 반환. 가로 스트립 가정.</summary>
    private static int ContentHeight(string path)
    {
        if (!File.Exists(path)) return 0;
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
}
#endif
