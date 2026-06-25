// ============================================================
// SynergyVFXSetup.cs  (Editor 전용)
//
// 메뉴: BagSurvivor > Setup > Create Synergy VFX Prefabs
//
// Assets/03.Prefabs/08.Synergy/ 의 *.prefab.png 파일들을 읽어
// 행(Row) 단위로 애니메이션을 분리한 뒤 VFX 프리팹을 자동 생성한다.
//
// ※ 스프라이트 슬라이싱이 이미 Sprite Editor에서 완료된 파일만 처리.
// ============================================================
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BagSurvivor.SynergyEditor
{
    public static class SynergyVFXSetup
    {
        private const string SourceFolder = "Assets/03.Prefabs/08.Synergy";
        private const string OutputFolder = "Assets/03.Prefabs/08.Synergy/Generated";

        // 이 크기 이하 스프라이트는 행 구분용 구분자로 간주해 무시
        private const float SeparatorThreshold = 32f;

        // 행 구분: 스프라이트 중심 y좌표 차이가 이 값 초과이면 다른 행
        private const float RowGapThreshold = 100f;

        private const float FrameRate = 12f;

        // ─────────────────────────────────────────────────────────────

        [MenuItem("BagSurvivor/Setup/Create Synergy VFX Prefabs")]
        public static void Run()
        {
            EnsureFolder(OutputFolder);

            string[] pngPaths = Directory.GetFiles(
                Path.Combine(Application.dataPath, "../", SourceFolder),
                "*.prefab.png",
                SearchOption.TopDirectoryOnly
            );

            if (pngPaths.Length == 0)
            {
                Debug.LogWarning($"[SynergyVFXSetup] {SourceFolder} 에서 *.prefab.png 파일을 찾지 못했습니다.");
                return;
            }

            int created = 0;
            foreach (string fullPath in pngPaths)
            {
                string assetPath = "Assets" + Path.GetFullPath(fullPath)
                    .Replace(Path.GetFullPath(Application.dataPath + "/.."), "")
                    .Replace("\\", "/");

                created += ProcessSpriteSheet(assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SynergyVFXSetup] 완료! 총 {created}개 VFX 프리팹 생성 → {OutputFolder}/");
        }

        // ─────────────────────────────────────────────────────────────

        static int ProcessSpriteSheet(string assetPath)
        {
            // 슬라이싱된 스프라이트 전체 로드
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            List<Sprite> sprites = assets
                .OfType<Sprite>()
                .Where(s => s.rect.width > SeparatorThreshold && s.rect.height > SeparatorThreshold)
                .ToList();

            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[SynergyVFXSetup] 유효 스프라이트 없음 (슬라이싱 확인 필요): {assetPath}");
                return 0;
            }

            // 파일명에서 기본 이름 추출 ("Fairy.prefab.png" → "Fairy")
            string baseName = Path.GetFileName(assetPath).Replace(".prefab.png", "");

            // 행(Row) 단위로 스프라이트 분리
            List<List<Sprite>> rows = GroupByRow(sprites);

            int count = 0;
            for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
            {
                List<Sprite> rowSprites = rows[rowIdx];
                if (rowSprites.Count == 0) continue;

                // 행이 1개뿐이면 접미사 없이, 여러 행이면 _R0 _R1… 붙임
                string effectName = rows.Count == 1 ? baseName : $"{baseName}_R{rowIdx}";

                CreateVFXPrefab(effectName, rowSprites);
                count++;
            }

            return count;
        }

        // ── 행 그룹화 ──────────────────────────────────────────────────

        static List<List<Sprite>> GroupByRow(List<Sprite> sprites)
        {
            // 중심 y 기준 내림차순 정렬 (y=0이 아래쪽이므로 높은 y = 위 행)
            var sorted = sprites
                .OrderByDescending(s => s.rect.y + s.rect.height / 2f)
                .ToList();

            var rows = new List<List<Sprite>>();
            var currentRow = new List<Sprite> { sorted[0] };
            float prevCenterY = sorted[0].rect.y + sorted[0].rect.height / 2f;

            for (int i = 1; i < sorted.Count; i++)
            {
                float centerY = sorted[i].rect.y + sorted[i].rect.height / 2f;

                if (Mathf.Abs(centerY - prevCenterY) > RowGapThreshold)
                {
                    // 현재 행 정렬 후 저장 (x 오름차순 = 왼쪽→오른쪽 프레임 순)
                    rows.Add(currentRow.OrderBy(s => s.rect.x).ToList());
                    currentRow = new List<Sprite>();
                }

                currentRow.Add(sorted[i]);
                prevCenterY = centerY;
            }

            if (currentRow.Count > 0)
                rows.Add(currentRow.OrderBy(s => s.rect.x).ToList());

            return rows;
        }

        // ── VFX 프리팹 생성 ────────────────────────────────────────────

        static void CreateVFXPrefab(string effectName, List<Sprite> frames)
        {
            // ─ 1. AnimationClip ─
            var clip = new AnimationClip
            {
                name      = effectName,
                frameRate = FrameRate,
                wrapMode  = WrapMode.Once
            };

            var keyframes = new ObjectReferenceKeyframe[frames.Count + 1];
            for (int i = 0; i < frames.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time  = i / FrameRate,
                    value = frames[i]
                };
            }
            // 마지막 프레임을 한 프레임 더 유지 (끝에서 깜박임 방지)
            keyframes[frames.Count] = new ObjectReferenceKeyframe
            {
                time  = frames.Count / FrameRate,
                value = frames[frames.Count - 1]
            };

            var binding = new EditorCurveBinding
            {
                type         = typeof(SpriteRenderer),
                path         = "",
                propertyName = "m_Sprite"
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            // Loop 설정: Once
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string clipPath = $"{OutputFolder}/{effectName}.anim";
            AssetDatabase.CreateAsset(clip, clipPath);

            // ─ 2. AnimatorController ─
            string ctrlPath = $"{OutputFolder}/{effectName}_Ctrl.controller";
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddMotion(clip);

            // ─ 3. 프리팹 ─
            var go  = new GameObject(effectName);
            var sr  = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];

            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;

            go.AddComponent<SynergyVFXAutoDestroy>();

            string prefabPath = $"{OutputFolder}/{effectName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            Debug.Log($"[SynergyVFXSetup] 생성: {prefabPath}  ({frames.Count}프레임)");
        }

        // ── 폴더 유틸 ──────────────────────────────────────────────────

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent ?? "Assets", folder);
        }
    }
}
