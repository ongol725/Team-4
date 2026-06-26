// ============================================================
// MonsterDataImporter.cs
// CSV(MonsterDataBase.csv)를 읽어서 ScriptableObject 에셋을 자동 생성하는 에디터 유틸리티
// Unity 에디터 메뉴: BagSurvivor > 몬스터 데이터 가져오기
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace BagSurvivor.Monster
{
    public class MonsterDataImporter : EditorWindow
    {
        // CSV 파일 기본 경로
        private string csvPath = "Docs/MonsterDataBase.csv";
        // SO 에셋 저장 경로
        private string outputPath = "Assets/Resources/ScriptableObjects/Monsters";

        [MenuItem("BagSurvivor/몬스터 데이터 가져오기")]
        public static void ShowWindow()
        {
            GetWindow<MonsterDataImporter>("몬스터 데이터 가져오기");
        }

        private void OnGUI()
        {
            GUILayout.Label("MonsterDataBase CSV → ScriptableObject 변환", EditorStyles.boldLabel);
            GUILayout.Space(10);

            csvPath = EditorGUILayout.TextField("CSV 경로", csvPath);
            outputPath = EditorGUILayout.TextField("저장 경로", outputPath);

            GUILayout.Space(10);

            if (GUILayout.Button("CSV에서 몬스터 데이터 생성/갱신", GUILayout.Height(40)))
            {
                ImportFromCSV();
            }

            GUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "CSV 파일의 데이터를 읽어서 ScriptableObject 에셋을 생성합니다.\n" +
                "이미 존재하는 에셋은 수치만 갱신됩니다.",
                MessageType.Info
            );
        }

        /// <summary>
        /// CSV 파일을 파싱하여 MonsterData ScriptableObject 에셋을 생성/갱신합니다.
        /// </summary>
        private void ImportFromCSV()
        {
            // 프로젝트 루트 기준 CSV 경로
            string fullCsvPath = Path.Combine(Application.dataPath, "..", csvPath);

            if (!File.Exists(fullCsvPath))
            {
                EditorUtility.DisplayDialog("오류", $"CSV 파일을 찾을 수 없습니다:\n{fullCsvPath}", "확인");
                return;
            }

            // 출력 폴더 생성
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                string[] folders = outputPath.Split('/');
                string currentPath = folders[0]; // "Assets"
                for (int i = 1; i < folders.Length; i++)
                {
                    string nextPath = currentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = nextPath;
                }
            }

            string[] lines = File.ReadAllLines(fullCsvPath);
            int createdCount = 0;
            int updatedCount = 0;

            // 첫 두 줄은 헤더와 타입 정보이므로 스킵 (3번째 줄부터 데이터)
            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] fields = ParseCSVLine(line);
                if (fields.Length < 19) continue;

                // CSV 필드 파싱
                int index = int.Parse(fields[0]);
                string monsterName = fields[1];
                string englishName = fields[2];

                // 에셋 파일 경로
                string assetFileName = $"MonsterData_{index}_{englishName}.asset";
                string assetPath = $"{outputPath}/{assetFileName}";

                // 기존 에셋이 있으면 로드, 없으면 새로 생성
                MonsterData data = AssetDatabase.LoadAssetAtPath<MonsterData>(assetPath);
                bool isNew = (data == null);

                if (isNew)
                {
                    data = ScriptableObject.CreateInstance<MonsterData>();
                }

                // 데이터 채우기
                data.index = index;
                data.monsterName = monsterName;
                data.englishName = englishName;
                data.grade = ParseEnum<MonsterGrade>(fields[3]);
                data.attackStyle = ParseEnum<AttackStyle>(fields[4]);
                data.spawnType = ParseEnum<SpawnType>(fields[5]);
                data.spawnInterval = float.Parse(fields[6]);
                data.spawnCount = int.Parse(fields[7]);
                data.maxHP = int.Parse(fields[8]);
                data.attack = int.Parse(fields[9]);
                data.defense = int.Parse(fields[10]);
                data.moveSpeed = float.Parse(fields[11]);
                data.kbResist = float.Parse(fields[12]);
                data.kbCooldown = float.Parse(fields[13]);
                data.movePattern = ParseEnum<MovePattern>(fields[14]);
                data.attackPattern = ParseEnum<AttackPattern>(fields[15]);
                data.dropItemID = fields[16];
                data.dropItemValue = int.Parse(fields[17]);
                data.prefabPath = fields[18];

                if (isNew)
                {
                    AssetDatabase.CreateAsset(data, assetPath);
                    createdCount++;
                }
                else
                {
                    EditorUtility.SetDirty(data);
                    updatedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "완료",
                $"몬스터 데이터 가져오기 완료!\n새로 생성: {createdCount}개\n갱신: {updatedCount}개",
                "확인"
            );

            Debug.Log($"[MonsterDataImporter] 완료 - 생성: {createdCount}, 갱신: {updatedCount}");
        }

        /// <summary>
        /// CSV 한 줄을 쉼표로 분리합니다. 큰따옴표 안의 쉼표는 무시합니다.
        /// </summary>
        private string[] ParseCSVLine(string line)
        {
            var fields = new System.Collections.Generic.List<string>();
            bool inQuotes = false;
            string currentField = "";

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.Trim());
                    currentField = "";
                }
                else
                {
                    currentField += c;
                }
            }
            fields.Add(currentField.Trim());

            return fields.ToArray();
        }

        /// <summary>
        /// 문자열을 Enum 값으로 파싱합니다.
        /// </summary>
        private T ParseEnum<T>(string value) where T : struct
        {
            if (System.Enum.TryParse<T>(value.Trim(), true, out T result))
            {
                return result;
            }

            Debug.LogWarning($"[MonsterDataImporter] Enum 파싱 실패: '{value}' → {typeof(T).Name}, 기본값 사용");
            return default;
        }
    }
}
#endif
