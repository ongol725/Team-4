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
        w.meleeReach  = 1.6f;                  // 캐릭터 몸 밖으로 띄움
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
        // 1) 22개 전부 확정 프레임 수 기준 균등 그리드로 슬라이스(원본좌표, 중심 피벗).
        //    값 = (cols, rows). WPN_008만 4×3, 나머지는 가로 1줄.
        var slice = new Dictionary<string, (int cols, int rows)>
        {
            { "WPN_001", (4, 1) },  { "WPN_002", (12, 1) }, { "WPN_003", (12, 1) }, { "WPN_004", (12, 1) },
            { "WPN_005", (8, 1) },  { "WPN_006", (4, 1) },  { "WPN_007", (4, 1) },  { "WPN_008", (4, 3) },
            { "WPN_009", (4, 1) },  { "WPN_010", (8, 1) },  { "WPN_011", (6, 1) },  { "WPN_012", (12, 1) },
            { "WPN_013", (12, 1) }, { "WPN_014", (12, 1) }, { "WPN_015", (4, 1) },  { "WPN_016", (4, 1) },
            { "WPN_025", (15, 1) }, { "WPN_026", (16, 1) }, { "WPN_027", (20, 1) }, { "WPN_028", (4, 1) },
            { "WPN_029", (24, 1) }, { "WPN_030", (14, 1) },

            // ── 17~24 신규 업로드 — 가로 1줄 스트립. 프레임 수는 [잠정값] (사용자 확정 후 교체) ──
            { "WPN_019", (16, 1) }, // 대검   7140px
            { "WPN_020", (16, 1) }, // 카타나 7140px
            { "WPN_021", (1, 1) },  // 스피어 594x570 — 단일 합성 이미지(시트 아님)
            { "WPN_022", (16, 1) }, // 플레일 7896px
            { "WPN_023", (16, 1) }, // 메이스 4840px
            { "WPN_024", (16, 1) }, // 몽둥이 7140px (현재 020과 동일 파일 — 별도 이미지 필요)
        };
        foreach (var kv in slice)
        {
            string p = $"{SheetDir}/{kv.Key}.png";
            if (AssetImporter.GetAtPath(p) != null) GridSlice(p, kv.Value.cols, kv.Value.rows);
        }

        // ── 4×2 격자(256셀·2px 간격) 시트 — 간격 기반 슬라이스(8프레임) ──
        var gridGap = new Dictionary<string, (int cw, int ch, int gx, int gy, int cols, int rows)>
        {
            { "WPN_017", (256, 256, 2, 2, 4, 2) }, // 바주카 8프레임
            { "WPN_018", (256, 256, 2, 2, 4, 2) }, // 레일건 8프레임
        };
        foreach (var kv in gridGap)
        {
            string p = $"{SheetDir}/{kv.Key}.png";
            if (AssetImporter.GetAtPath(p) != null)
                GridSliceBySize(p, kv.Value.cw, kv.Value.ch, kv.Value.gx, kv.Value.gy, kv.Value.cols, kv.Value.rows);
        }

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
            // 근접 무기 시트는 모션이 프레임에 포함된 베이크드 → 제자리 재생(회전 스윕 없음)
            if (weapon.attackStyleType == WeaponAttackStyleType.MeleeFan ||
                weapon.attackStyleType == WeaponAttackStyleType.MeleeSingle)
                weapon.meleeMotion = MeleeMotionType.Baked;
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

    /// <summary>PNG 헤더(IHDR)에서 원본 픽셀 크기를 직접 읽는다. 임포트 다운스케일과 무관.</summary>
    private static (int w, int h) PngSize(string assetPath)
    {
        try
        {
            using var fs = System.IO.File.OpenRead(assetPath);
            var b = new byte[24];
            if (fs.Read(b, 0, 24) < 24) return (0, 0);
            int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
            int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
            return (w, h);
        }
        catch { return (0, 0); }
    }

    /// <summary>
    /// 지정 png를 cols×rows 균등 그리드로 슬라이스한다(중심 피벗).
    /// rect는 반드시 '원본 해상도' 좌표여야 한다(스프라이트 rect는 원본 기준). PNG 헤더에서 원본 크기를
    /// 직접 읽어 계산하므로, maxTextureSize 다운스케일(>2048)이 있어도 좌표가 어긋나지 않는다.
    /// </summary>
    private static void GridSlice(string pngPath, int cols, int rows)
    {
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"[WeaponProjectileSetup] 임포터 없음: {pngPath}"); return; }

        var (W, H) = PngSize(pngPath);
        if (W <= 0 || H <= 0) { Debug.LogWarning($"[WeaponProjectileSetup] PNG 크기 읽기 실패: {pngPath}"); return; }

        int cw = W / cols, ch = H / rows;
        if (cw <= 0 || ch <= 0) return;

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled       = false;
        importer.alphaIsTransparency = true;

        string prefix = Path.GetFileNameWithoutExtension(pngPath);
        var metas = new List<SpriteMetaData>();
        int idx = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                metas.Add(new SpriteMetaData
                {
                    name      = $"{prefix}_{idx}",
                    rect      = new Rect(c * cw, H - (r + 1) * ch, cw, ch), // 원본 좌표(좌하단 원점)
                    alignment = (int)SpriteAlignment.Center,
                    pivot     = new Vector2(0.5f, 0.5f),
                });
                idx++;
            }

#pragma warning disable CS0618 // spritesheet은 deprecated이나 그리드 일괄 슬라이스에 여전히 동작
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        Debug.Log($"[WeaponProjectileSetup] 슬라이스: {prefix} → {cols}x{rows}, 셀 {cw}x{ch} (원본 {W}x{H})");
    }
}
