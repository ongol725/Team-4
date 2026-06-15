// ============================================================
// StringTable.cs
// 전투화면 UI 문자열 테이블
// Resources/Data/StringTable_BattleUI.csv 를 로드하여 코드->문자열 조회
// 코드 체계: [대분류][중분류][소분류3자리] (기획서 스트링 테이블 규칙)
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace BagSurvivor.UI
{
    public static class StringTable
    {
        private const string ResourcePath = "Data/StringTable_BattleUI";
        private static Dictionary<int, string> table;

        private static void EnsureLoaded()
        {
            if (table != null) return;
            table = new Dictionary<int, string>();

            TextAsset csv = Resources.Load<TextAsset>(ResourcePath);
            if (csv == null)
            {
                Debug.LogError($"[StringTable] CSV를 찾을 수 없습니다: Resources/{ResourcePath}");
                return;
            }

            string[] lines = csv.text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // 헤더(code,text) 스킵
                if (i == 0 && line.StartsWith("code")) continue;

                int comma = line.IndexOf(',');
                if (comma < 0) continue;

                string codeStr = line.Substring(0, comma).Trim();
                string text = line.Substring(comma + 1).Trim();

                // 양끝 큰따옴표 제거 (CSV 이스케이프 대응)
                if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
                    text = text.Substring(1, text.Length - 2);

                if (int.TryParse(codeStr, out int code))
                    table[code] = text;
            }

            Debug.Log($"[StringTable] {table.Count}개 문자열 로드 완료.");
        }

        /// <summary>코드에 해당하는 문자열 반환. 없으면 "[code]" 형태(누락 식별용).</summary>
        public static string Get(int code)
        {
            EnsureLoaded();
            return table.TryGetValue(code, out string v) ? v : $"[{code}]";
        }

        public static bool TryGet(int code, out string value)
        {
            EnsureLoaded();
            return table.TryGetValue(code, out value);
        }

        /// <summary>강제 재로드 (런타임 데이터 갱신 시).</summary>
        public static void Reload()
        {
            table = null;
            EnsureLoaded();
        }
    }
}