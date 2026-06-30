// ============================================================
// WeaponProjectileSetup.cs  (Editor 전용)
//
// 메뉴: BagSurvivor > Setup > ④ Fill Weapon Attack Frames
//   - 03.Prefabs/03.Weapons/WPN_xxx.png 시트의 서브스프라이트를 번호순으로 로드해
//     itemID가 일치하는 무기 SO(SO_WeaponData.attackFrames)에 할당한다.
//   - 슬라이스가 깨진 WPN_012(마도서)·WPN_013(번개구슬)은 12×1 그리드로 재슬라이스한다.
//
// 런타임: 원거리 무기는 투사체 애니메이션, 근접 무기는 휘두르는 이펙트로 사용된다.
// ============================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WeaponProjectileSetup
{
    private const string SheetDir  = "Assets/03.Prefabs/03.Weapons";
    private const string WeaponDir = "Assets/Resources/ScriptableObjects/Weapons";

    // ── 실험: WPN02(8프레임 검 스윙 시트, 4×2)를 장검(WPN_002)에 적용 ──
    [MenuItem("BagSurvivor/Setup/⑤ Test: WPN02 → 장검(WPN_002)")]
    public static void TestW02ToLongsword()
    {
        string png = $"{SheetDir}/WPN02.png";
        if (AssetImporter.GetAtPath(png) == null) { Debug.LogError($"[Test] 파일 없음: {png}"); return; }

        // 8프레임 · 256셀 · 2px 간격 · 4열×2행 · 중앙 피벗 (행 우선: 위 4 → 아래 4)
        GridSliceBySize(png, 256, 256, 2, 2, 4, 2);

        var w = LoadWeaponByItemID("WPN_002");
        if (w == null) { Debug.LogError("[Test] WPN_002(장검) SO를 찾지 못함"); return; }

        var frames = LoadFramesSorted(png);
        if (frames.Length == 0) { Debug.LogError("[Test] w02 슬라이스 결과 없음"); return; }

        w.attackFrames = frames;
        if (w.attackFps <= 0f) w.attackFps = 12f;
        w.meleeMotion = MeleeMotionType.Swing; // 스윙
        EditorUtility.SetDirty(w);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Test] w02({frames.Length}프레임) → 장검(WPN_002) 스윙 적용 완료. 인게임에서 장검 장착 후 공격해보세요.");
    }

    private static SO_WeaponData LoadWeaponByItemID(string id)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:SO_WeaponData", new[] { WeaponDir }))
        {
            var w = AssetDatabase.LoadAssetAtPath<SO_WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
            if (w != null && w.itemID == id) return w;
        }
        return null;
    }

    /// <summary>셀 크기 + 간격 기반 그리드 슬라이스(중앙 피벗). 임포트 설정도 권장값으로 강제.</summary>
    private static void GridSliceBySize(string pngPath, int cellW, int cellH, int gapX, int gapY, int cols, int rows)
    {
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"[Slice] 임포터 없음: {pngPath}"); return; }

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled       = false;
        importer.alphaIsTransparency = true;
        importer.filterMode          = FilterMode.Bilinear;
        importer.maxTextureSize      = 4096; // 2062px 등 2048 초과분 다운스케일 방지(좌표 어긋남 차단)
        importer.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        int H = tex != null ? tex.height : (cellH * rows + gapY * (rows - 1));

        string prefix = Path.GetFileNameWithoutExtension(pngPath);
        var metas = new List<SpriteMetaData>();
        int idx = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int x = c * (cellW + gapX);
                int y = H - (r + 1) * cellH - r * gapY; // 위 행부터(좌하단 원점)
                metas.Add(new SpriteMetaData
                {
                    name      = $"{prefix}_{idx}",
                    rect      = new Rect(x, y, cellW, cellH),
                    alignment = (int)SpriteAlignment.Center,
                    pivot     = new Vector2(0.5f, 0.5f),
                });
                idx++;
            }
#pragma warning disable CS0618
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        Debug.Log($"[Slice] {prefix}: {cols}x{rows}, 셀 {cellW}x{cellH}, 간격 {gapX}px, 프레임 {metas.Count}개");
    }

    [MenuItem("BagSurvivor/Setup/④ Fill Weapon Attack Frames")]
    public static void FillWeaponAttackFrames()
    {
        // 1) 슬라이스가 잘못된 시트 재슬라이스 (실제 프레임 수 기준 균등 그리드, 중심 피벗)
        //    값 = (cols, rows). WPN_008만 4×3 격자, 나머지는 가로 1줄.
        var reslice = new Dictionary<string, (int cols, int rows)>
        {
            { "WPN_002", (12, 1) }, { "WPN_004", (12, 1) }, { "WPN_005", (8, 1) },
            { "WPN_007", (4, 1) },  { "WPN_008", (4, 3) },  { "WPN_012", (12, 1) },
            { "WPN_013", (12, 1) }, { "WPN_014", (12, 1) }, { "WPN_015", (4, 1) },
            { "WPN_025", (15, 1) }, { "WPN_026", (16, 1) },
        };
        foreach (var kv in reslice)
            GridSlice($"{SheetDir}/{kv.Key}.png", kv.Value.cols, kv.Value.rows);

        // 2) 무기 SO 로드 (itemID → 에셋)
        var weaponsById = new Dictionary<string, SO_WeaponData>();
        foreach (var guid in AssetDatabase.FindAssets("t:SO_WeaponData", new[] { WeaponDir }))
        {
            var w = AssetDatabase.LoadAssetAtPath<SO_WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
            if (w != null && !string.IsNullOrEmpty(w.itemID)) weaponsById[w.itemID] = w;
        }

        // 3) 각 시트의 프레임을 itemID가 일치하는 무기에 할당
        int filled = 0;
        foreach (var png in Directory.GetFiles(SheetDir, "WPN_*.png"))
        {
            string id = Path.GetFileNameWithoutExtension(png); // 예: WPN_001
            if (!weaponsById.TryGetValue(id, out var weapon))
            {
                Debug.LogWarning($"[WeaponProjectileSetup] itemID '{id}' 무기 SO를 찾지 못함: {png}");
                continue;
            }

            var frames = LoadFramesSorted(png.Replace('\\', '/'));
            if (frames.Length == 0)
            {
                Debug.LogWarning($"[WeaponProjectileSetup] 프레임 없음: {png}");
                continue;
            }

            weapon.attackFrames = frames;
            if (weapon.attackFps <= 0f) weapon.attackFps = 12f;
            EditorUtility.SetDirty(weapon);
            filled++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WeaponProjectileSetup] 무기 공격 프레임 {filled}개 적용 완료");
    }

    /// <summary>시트의 모든 서브스프라이트를 이름 뒤 번호순으로 정렬해 반환.</summary>
    private static Sprite[] LoadFramesSorted(string pngPath)
    {
        var list = new List<Sprite>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(pngPath))
            if (asset is Sprite sp) list.Add(sp);
        list.Sort((a, b) => TrailingNumber(a.name).CompareTo(TrailingNumber(b.name)));
        return list.ToArray();
    }

    private static int TrailingNumber(string name)
    {
        int i = name.Length;
        while (i > 0 && char.IsDigit(name[i - 1])) i--;
        return (i < name.Length && int.TryParse(name.Substring(i), out int n)) ? n : 0;
    }

    /// <summary>
    /// 지정 png를 cols×rows 균등 그리드로 재슬라이스한다(중심 피벗).
    /// 추가로 모든 셀의 '내용물 영역'을 셀 로컬 좌표로 합집합해 공통 투명 여백을 잘라낸다(트림).
    /// → 정규화 시 그림이 꽉 차 보이고, 모든 프레임이 같은 창을 써서 애니메이션 크기가 일관된다.
    /// </summary>
    private static void GridSlice(string pngPath, int cols, int rows)
    {
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"[WeaponProjectileSetup] 임포터 없음: {pngPath}"); return; }

        // 픽셀 분석을 위해 임시로 읽기 가능 활성화 후 리임포트
        bool prevReadable = importer.isReadable;
        if (!prevReadable) { importer.isReadable = true; importer.SaveAndReimport(); }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        if (tex == null) { Debug.LogWarning($"[WeaponProjectileSetup] 텍스처 로드 실패: {pngPath}"); return; }

        int W = tex.width, H = tex.height;
        int cw = W / cols, ch = H / rows;
        if (cw <= 0 || ch <= 0) return;

        // 셀 로컬 내용물 합집합 창 (모든 프레임 공통). 알파>임계 픽셀의 경계.
        const byte THR = 10;
        int ux0 = cw, uy0 = ch, ux1 = -1, uy1 = -1;
        Color32[] px = null;
        try { px = tex.GetPixels32(); } catch { px = null; } // GetPixels32: 좌하단 원점, index=y*W+x
        if (px != null)
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int ox = c * cw, oy = H - (r + 1) * ch; // 셀 원점(좌하단)
                    for (int ly = 0; ly < ch; ly++)
                        for (int lx = 0; lx < cw; lx++)
                            if (px[(oy + ly) * W + (ox + lx)].a > THR)
                            {
                                if (lx < ux0) ux0 = lx; if (lx > ux1) ux1 = lx;
                                if (ly < uy0) uy0 = ly; if (ly > uy1) uy1 = ly;
                            }
                }
        }
        bool trim = ux1 >= ux0 && uy1 >= uy0;
        int wx = trim ? ux0 : 0, wy = trim ? uy0 : 0;
        int ww = trim ? (ux1 - ux0 + 1) : cw, wh = trim ? (uy1 - uy0 + 1) : ch;

        string prefix = Path.GetFileNameWithoutExtension(pngPath);
        var metas = new List<SpriteMetaData>();
        int idx = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int ox = c * cw, oy = H - (r + 1) * ch;
                metas.Add(new SpriteMetaData
                {
                    name      = $"{prefix}_{idx}",
                    rect      = new Rect(ox + wx, oy + wy, ww, wh),
                    alignment = (int)SpriteAlignment.Center,
                    pivot     = new Vector2(0.5f, 0.5f),
                });
                idx++;
            }

        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.isReadable       = prevReadable; // 원복(보통 false)
#pragma warning disable CS0618 // spritesheet은 deprecated이나 그리드 일괄 슬라이스에 여전히 동작
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        Debug.Log($"[WeaponProjectileSetup] 재슬라이스: {prefix} → {cols}x{rows}, 셀 {cw}x{ch} → 트림창 {ww}x{wh}");
    }
}
