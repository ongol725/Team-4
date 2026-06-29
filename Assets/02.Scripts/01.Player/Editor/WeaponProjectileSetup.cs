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

    /// <summary>지정 png를 cols×rows 균등 그리드로 재슬라이스한다(중심 피벗). 프레임명 = 파일명_인덱스.</summary>
    private static void GridSlice(string pngPath, int cols, int rows)
    {
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"[WeaponProjectileSetup] 임포터 없음: {pngPath}"); return; }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        if (tex == null) { Debug.LogWarning($"[WeaponProjectileSetup] 텍스처 로드 실패: {pngPath}"); return; }

        int cw = tex.width / cols, ch = tex.height / rows;
        if (cw <= 0 || ch <= 0) return;

        string prefix = Path.GetFileNameWithoutExtension(pngPath);
        var metas = new List<SpriteMetaData>();
        int idx = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                metas.Add(new SpriteMetaData
                {
                    name      = $"{prefix}_{idx}",
                    // Unity 텍스처 좌표는 좌하단 기준 → 위쪽 행부터 채우기 위해 y를 뒤집는다
                    rect      = new Rect(c * cw, tex.height - (r + 1) * ch, cw, ch),
                    alignment = (int)SpriteAlignment.Center,
                    pivot     = new Vector2(0.5f, 0.5f),
                });
                idx++;
            }

        importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable CS0618 // spritesheet은 deprecated이나 그리드 일괄 슬라이스에 여전히 동작
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        Debug.Log($"[WeaponProjectileSetup] 재슬라이스: {prefix} → {cols}x{rows} ({metas.Count}프레임)");
    }
}
