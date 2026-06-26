// ============================================================
// MiniBossPatternSetup.cs  (Editor 전용)
// 4층 미니보스 후보(Tiger/Bear/Ogre) 프리팹에 보스 패턴 1개를 일괄 부착.
//  - BossPatternDriver(비활성) + Pattern_PhantomDash(멧돼지 소환)
//  - 드라이버는 꺼진 채로 추가 → 평소(2층 포함) 일반 몬스터. 스폰러가 4층 미니보스일 때만 enable.
//  - 소환물(wolfPrefab)=Prefab_Boar. 멧돼지는 PhantomWolfDash가 없어 강제 돌진 없이
//    자기 AI(BoarGimmick)대로 자연스럽게 행동하다 죽으면 풀 반환.
// 메뉴: Team4/4층 미니보스 패턴 셋업
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BagSurvivor.Monster
{
    public static class MiniBossPatternSetup
    {
        private static readonly string[] BossPrefabs =
        {
            "Assets/03.Prefabs/02.Monsters/Prefab_Tiger.prefab",
            "Assets/03.Prefabs/02.Monsters/Prefab_Bear.prefab",
            "Assets/03.Prefabs/02.Monsters/Prefab_Ogre.prefab",
        };
        private const string BoarPrefab = "Assets/03.Prefabs/02.Monsters/Prefab_Boar.prefab";

        [MenuItem("Team4/4층 미니보스 패턴 셋업")]
        public static void Apply()
        {
            var boar = AssetDatabase.LoadAssetAtPath<GameObject>(BoarPrefab);
            if (boar == null) { Debug.LogError("[MiniBoss] Prefab_Boar 없음"); return; }

            int done = 0;
            foreach (string path in BossPrefabs)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null
                    ? PrefabUtility.LoadPrefabContents(path) : null;
                if (root == null) { Debug.LogWarning($"[MiniBoss] 프리팹 없음: {path}"); continue; }

                // 패턴 1개: 멧돼지 소환(PhantomDash). 없으면 추가하고 필드 세팅(AddComponent는 Reset 미호출).
                var pat = root.GetComponent<Pattern_PhantomDash>();
                if (pat == null) pat = root.AddComponent<Pattern_PhantomDash>();
                pat.patternName = "PhantomDash";
                pat.isSpecial = true;
                pat.useRange = 15f;
                pat.telegraphTime = 2f;
                pat.cooldown = 20f;
                pat.chance = 7f;
                pat.wolfPrefab = boar;   // 소환물 = 멧돼지(언제든 인스펙터에서 교체 가능)

                // 구동기: 비활성으로 추가 → 4층 미니보스일 때만 스폰러가 enable
                var drv = root.GetComponent<BossPatternDriver>();
                if (drv == null) drv = root.AddComponent<BossPatternDriver>();
                drv.enabled = false;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                done++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[MiniBoss] {done}개 프리팹에 멧돼지 소환 패턴 부착 완료(드라이버는 비활성 — 4층에서만 작동).");
        }
    }
}
#endif
