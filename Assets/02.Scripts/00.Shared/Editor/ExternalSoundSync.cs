// ============================================================
// ExternalSoundSync.cs (Editor 전용)
// 에셋스토어 사운드(라이선스상 커밋 불가, gitignore)를 프로젝트 표준 위치
// (06.Sounds/Resources/99.External — 역시 gitignore)로 자동 복사한다.
//  - 팀원이 에셋스토어에서 무료로 받아 임포트만 하면 다음 컴파일 때 자동 세팅.
//  - 에셋이 없으면 조용히 건너뜀(코드는 무음 폴백이라 게임 동작에 지장 없음).
// 대상 에셋: "Bow and Hammer Sound Effects", "Demo Ancient Magic Pack FREE"
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class ExternalSoundSync
{
    private const string ExtDir = "Assets/06.Sounds/Resources/99.External";

    // (검색할 원본 클립 이름, 우리 쪽 파일명)
    private static readonly (string source, string target)[] Map =
    {
        ("Battleaxe1",             "Mace_Swing"),          // 철퇴 휘두름
        ("AMP_Lightning_Impact_06","LightningOrb_Attack"), // 번개구슬 공격
        ("AMP_Earth_Impact_01",    "Titan_RockImpact"),    // 티탄 돌 떨구기 착탄
    };

    static ExternalSoundSync()
    {
        // 도메인 리로드마다 가벼운 존재 확인 → 없을 때만 복사
        EditorApplication.delayCall += Sync;
    }

    [MenuItem("Team4/외부 사운드 동기화")]
    public static void SyncMenu() { Sync(); Debug.Log("[ExternalSoundSync] 수동 동기화 완료"); }

    private static void Sync()
    {
        bool any = false;
        foreach (var (source, target) in Map)
        {
            // 이미 복사돼 있으면 스킵
            string targetNoExt = ExtDir + "/" + target;
            if (AssetDatabase.FindAssets("t:AudioClip " + target, new[] { "Assets/06.Sounds" }).Length > 0)
                continue;

            // 원본 검색 (에셋 미임포트면 없음 → 조용히 스킵)
            string srcPath = null;
            foreach (var g in AssetDatabase.FindAssets("t:AudioClip " + source))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == source) { srcPath = p; break; }
            }
            if (srcPath == null) continue;

            if (!AssetDatabase.IsValidFolder(ExtDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/06.Sounds/Resources")) continue; // 구조 예외 시 안전 탈출
                AssetDatabase.CreateFolder("Assets/06.Sounds/Resources", "99.External");
            }

            string dst = targetNoExt + Path.GetExtension(srcPath);
            if (AssetDatabase.CopyAsset(srcPath, dst))
            {
                Debug.Log($"[ExternalSoundSync] 복사: {source} → {dst}");
                any = true;
            }
        }
        if (any) AssetDatabase.Refresh();
    }
}
#endif
