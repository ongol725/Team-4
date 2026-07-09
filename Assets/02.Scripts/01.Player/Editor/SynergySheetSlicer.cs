using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace BagSurvivor.EditorTools
{
    /// <summary>
    /// 시너지 스프라이트 시트(08.Synergy)를 격자(grid)로 재슬라이스한다.
    /// 새 아트는 가로 스트립(512 높이) 또는 4x4 그리드 형태라, 기존 슬라이싱과 맞지 않으므로
    /// 이 메뉴로 다시 자른 뒤 'BagSurvivor/Setup/③ Fill Synergy Icons'를 실행해 SO 에셋에 재연결한다.
    ///
    /// ▶ 슬라이스 순서: 좌→우, 상→하 (스프라이트 이름 {파일}_0,_1,… → 메뉴 ③의 숫자 정렬과 일치)
    /// ▶ Unity 6: 레거시 TextureImporter.spritesheet 대신 ISpriteEditorDataProvider 사용
    /// </summary>
    public static class SynergySheetSlicer
    {
        private const string Dir = "Assets/03.Prefabs/08.Synergy";

        // (파일명(확장자 제외), 열, 행)
        private static readonly (string name, int cols, int rows)[] Sheets =
        {
            // ── 가로 스트립 (512px 셀 기준) ──
            ("Fairy",        8, 1),
            ("Pinball",      8, 1),
            ("Sanctuary_Br", 8, 1), ("Sanctuary_Gd", 8, 1), ("Sanctuary_Sv", 8, 1),
            ("Spirit_Br",    8, 1), ("Spirit_Gd",    8, 1), ("Spirit_Sv",    8, 1),
            ("Shuriken_Sv",  8, 1),
            ("GiantGolem",   8, 1),
            ("Devil_Slash",  8, 1),
            ("Devil_Wave",   8, 1),
            ("OVERLOAD1_Pr", 6, 1),   // 12288×2048, 6프레임
            ("OVERLOAD3_Pr", 8, 1),   // 4096×512,   8프레임 (픽셀스캔 검증)
            // 6184×512 = 8×773 (773은 소수 → 8프레임이 유일한 정수 그리드, 경계겹침 0%)
            ("Devil_Fireball",  8, 1),
            ("SwordWave",       8, 1),
            ("spirit_attack",   12, 1),  // 6144×512, 12프레임 (셀별 콘텐츠 검증)
            ("OVERLOAD2_Pr",    8, 1),
            ("OVERLOAD2.1_Pr",  8, 1),
            ("StoneDrop_Common", 12, 1), // 12288×1536, 12프레임 (셀별 콘텐츠 검증)
            // 일렉트로 번개 기둥: 30956×2048, 본체 중심 간격 ≈3090px → 10프레임 (셀 3095px, 빈 프레임 없음)
            ("ElectroShockwave", 10, 1),
            // 대정령(프리즘) 타격 이펙트: 2048×1024, 512px 셀 4×2 = 8프레임
            ("Elemental_Explosion_SpriteSheet", 4, 2),
            // ── 4x4 그리드 ──
            ("Gold_Coin1", 4, 4), ("Gold_Coin2", 4, 4), ("Gold_Coin3", 4, 4), ("Gold_Coin4", 4, 4),
            ("RichCoin_BOMB1", 4, 4), ("RichCoin_BOMB2", 4, 4), ("RichCoin_BOMB3", 4, 4), ("RichCoin_BOMB4", 4, 4),
            ("Shockwave_Common", 4, 4),  // 7372×7372, 16프레임
            ("Shockwave_RED",    4, 4),  // 7340×7340, 16프레임
            ("Scythe_Black",     4, 4),  // 7340×7340, 16프레임
            ("Scythe_BlackRed",  4, 4),  // 7340×7340, 16프레임
            ("Scythe_DarkRed",   4, 4),  // 1835×1835, 16프레임 (Black/BlackRed 동일 구조, 셀 459px)
        };

        [MenuItem("BagSurvivor/Setup/⓪ Slice Synergy Sheets (Grid)")]
        public static void Slice()
        {
            int done = 0, skipped = 0;
            var factory = new SpriteDataProviderFactories();
            factory.Init();

            foreach (var s in Sheets)
            {
                if (SliceSheet(factory, s)) done++;
                else                        skipped++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Slicer] 재슬라이스 완료 — 성공 {done}개, 건너뜀 {skipped}개. " +
                      $"이어서 'BagSurvivor/Setup/③ Fill Synergy Icons'를 실행해 프레임을 재연결하세요.");
        }

        /// <summary>Sheets 목록에 등록된 시트 하나만 재슬라이스한다.
        /// (전체 메뉴 ⓪은 모든 시트의 spriteID를 재생성해 기존 참조가 갱신될 때까지 diff가 커지므로,
        ///  신규 시트 추가 시에는 이 메서드로 대상만 슬라이스 → ③ 메뉴로 재연결한다)</summary>
        public static bool SliceSingle(string sheetName)
        {
            foreach (var s in Sheets)
            {
                if (s.name != sheetName) continue;
                var factory = new SpriteDataProviderFactories();
                factory.Init();
                bool ok = SliceSheet(factory, s);
                AssetDatabase.Refresh();
                return ok;
            }
            Debug.LogWarning($"[Slicer] Sheets 목록에 없는 시트: {sheetName}");
            return false;
        }

        static bool SliceSheet(SpriteDataProviderFactories factory, (string name, int cols, int rows) s)
        {
            string path = $"{Dir}/{s.name}.png";

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Slicer] 임포터 없음(파일 미존재?): {path}");
                return false;
            }

            // 1) 스프라이트/멀티플 + 최대 해상도 확보 후 1차 임포트 (정확한 픽셀 크기 확보)
            importer.textureType      = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.maxTextureSize   = 16384;
            importer.mipmapEnabled    = false;
            importer.SaveAndReimport();

            // 임포트 후 importer 참조가 갱신될 수 있어 재취득
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var tex  = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (importer == null || tex == null)
            {
                Debug.LogWarning($"[Slicer] 텍스처 로드 실패: {path}");
                return false;
            }

            int W = tex.width, H = tex.height;
            int cw = W / s.cols, ch = H / s.rows;
            if (cw <= 0 || ch <= 0)
            {
                Debug.LogWarning($"[Slicer] 잘못된 격자: {s.name} ({W}x{H} / {s.cols}x{s.rows})");
                return false;
            }

            // 2) 데이터 프로바이더로 격자 SpriteRect 생성
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>();
            int idx = 0;
            for (int row = 0; row < s.rows; row++)
            for (int col = 0; col < s.cols; col++)
            {
                float x = col * cw;
                float y = H - (row + 1) * ch; // Unity rect 원점은 좌하단 → 위 행부터 채우기
                rects.Add(new SpriteRect
                {
                    name      = $"{s.name}_{idx}",
                    spriteID  = GUID.Generate(),
                    rect      = new Rect(x, y, cw, ch),
                    alignment = SpriteAlignment.Center,
                    pivot     = new Vector2(0.5f, 0.5f),
                    border    = Vector4.zero,
                });
                idx++;
            }

            dp.SetSpriteRects(rects.ToArray());

            // 이름↔fileId 매핑 (Unity 6 직렬화)
            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIdDp != null)
            {
                var pairs = rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList();
                nameIdDp.SetNameFileIdPairs(pairs);
            }

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[Slicer] {s.name}: {s.cols}x{s.rows} = {s.cols * s.rows}프레임 ({cw}x{ch})");
            return true;
        }
    }
}
