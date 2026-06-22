// ============================================================
// RangedMonsterSetup.cs  (Editor 전용)
// 원거리 몬스터 셋업 자동화 (몬스터별 전용 투사체):
//  - 고블린 궁수: GoblinBow 화살, 플레이어 위치 조준
//  - 스켈레톤 궁수: SkeletonBow 화살, 플레이어 위치 조준
//  - 코볼트: KoboltSpear 창, 플레이어 이동방향으로 투척
//  각 궁수 프리팹에 RangedAttackGimmick + 전용 투사체 프리팹 + stopDistance(사거리) 설정.
// 메뉴: Team4/원거리 몬스터 셋업
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using BagSurvivor.Monster;

public static class RangedMonsterSetup
{
    private const float Range = 7f, Cooldown = 1.5f, Speed = 8f, MaxRange = 12f;

    private struct Cfg
    {
        public string archerPrefab;   // 궁수 프리팹
        public string spritePath;     // 투사체 스프라이트
        public string projPrefab;     // 생성할 투사체 프리팹 경로
        public AimMode aim;           // 조준 방식
    }

    private static readonly Cfg[] Configs =
    {
        new Cfg { archerPrefab = "Assets/03.Prefabs/02.Monsters/Prefab_GoblinArcher.prefab",
                  spritePath = "Assets/04.Images/02.Monsters/Attack/GoblinBow.png",
                  projPrefab = "Assets/03.Prefabs/02.Monsters/Goblin_Arrow.prefab",
                  aim = AimMode.AtPlayerPosition },
        new Cfg { archerPrefab = "Assets/03.Prefabs/02.Monsters/Prefab_SkeletonArcher.prefab",
                  spritePath = "Assets/04.Images/02.Monsters/Attack/SkeletonBow.png",
                  projPrefab = "Assets/03.Prefabs/02.Monsters/Skeleton_Arrow.prefab",
                  aim = AimMode.AtPlayerPosition },
        new Cfg { archerPrefab = "Assets/03.Prefabs/02.Monsters/Prefab_Kobold.prefab",
                  spritePath = "Assets/04.Images/02.Monsters/Attack/KoboltSpear.png",
                  projPrefab = "Assets/03.Prefabs/02.Monsters/Kobold_Spear.prefab",
                  aim = AimMode.PlayerMoveDirection },
    };

    [MenuItem("Team4/원거리 몬스터 셋업")]
    public static void Setup()
    {
        int done = 0;
        foreach (var c in Configs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(c.archerPrefab) == null)
            { Debug.LogWarning("[RangedMonster] 궁수 프리팹 없음: " + c.archerPrefab); continue; }

            GameObject proj = MakeProjectilePrefab(c.spritePath, c.projPrefab);
            if (proj == null) { Debug.LogWarning("[RangedMonster] 투사체 생성 실패: " + c.projPrefab); continue; }

            var contents = PrefabUtility.LoadPrefabContents(c.archerPrefab);
            var mc = contents.GetComponentInChildren<MonsterController>();
            var host = mc != null ? mc.gameObject : contents;
            if (mc != null) mc.stopDistance = Range;

            var g = host.GetComponent<RangedAttackGimmick>();
            if (g == null) g = host.AddComponent<RangedAttackGimmick>();
            g.aimMode = c.aim;
            g.attackRange = Range;
            g.fireCooldown = Cooldown;
            g.projectileSpeed = Speed;
            g.projectileMaxRange = MaxRange;
            g.projectilePrefab = proj;

            PrefabUtility.SaveAsPrefabAsset(contents, c.archerPrefab);
            PrefabUtility.UnloadPrefabContents(contents);
            done++;
        }

        // 임시 placeholder 정리(이전 셋업에서 만든 Monster_Arrow)
        AssetDatabase.DeleteAsset("Assets/03.Prefabs/02.Monsters/Monster_Arrow.prefab");
        AssetDatabase.DeleteAsset("Assets/04.Images/02.Monsters/Monster_Arrow.png");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[RangedMonster] 셋업 완료 — {done}종 전용 투사체 연결(고블린/스켈레톤=위치조준, 코볼트=이동방향)");
    }

    private static GameObject MakeProjectilePrefab(string spritePath, string prefabPath)
    {
        Sprite spr = LoadSprite(spritePath);
        if (spr == null) { Debug.LogWarning("[RangedMonster] 스프라이트 없음: " + spritePath); return null; }

        var go = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = spr; sr.sortingOrder = 8;
        var col = go.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = 0.25f;
        var rb = go.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0f;
        var po = go.AddComponent<PooledObject>(); po.autoReturnAfter = 5f;
        var proj = go.AddComponent<BossProjectile>(); proj.lifeTime = 5f;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        // Multiple 모드 등: 서브에셋에서 첫 Sprite
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
            if (a is Sprite sp) return sp;
        return null;
    }
}
#endif
