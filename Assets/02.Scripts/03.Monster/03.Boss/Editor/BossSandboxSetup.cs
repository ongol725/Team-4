// ============================================================
// BossSandboxSetup.cs  (Editor 전용)
// 메뉴 한 번으로 보스 패턴 검증용 "샌드박스 씬 + 더미 프리팹/에셋"을 자동 생성.
//  - 글로벌 룰: 신규 제작물은 임시 씬에서 검증 → 프리팹화. 본 도구는 그 임시 환경을 만든다.
//  - 더미 산출물은 모두 Assets/_Sandbox/Boss 아래에 모여, 실제 씬/프리팹을 건드리지 않는다.
//  - 메뉴: Tools/Boss/① 샌드박스 빌드 , Tools/Boss/② 샌드박스 삭제
//
// 생성물:
//   _Sandbox/Boss/Art/*.png         : 더미 스프라이트(색 사각형/점)
//   _Sandbox/Boss/Data/*.asset      : 보스/늑대 MonsterData
//   _Sandbox/Boss/Prefabs/*.prefab  : 투사체/텔레그래프/충격파/환영늑대 프리팹
//   01.Scenes/Sandbox/BossSandbox.unity : 매니저+플레이어+보스가 배치된 검증 씬
// ============================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BagSurvivor.Monster.EditorTools
{
    public static class BossSandboxSetup
    {
        private const string SandboxRoot = "Assets/_Sandbox/Boss";
        private const string ArtDir      = SandboxRoot + "/Art";
        private const string DataDir     = SandboxRoot + "/Data";
        private const string PrefabDir   = SandboxRoot + "/Prefabs";
        private const string SceneDir    = "Assets/01.Scenes/Sandbox";
        private const string ScenePath   = SceneDir + "/BossSandbox.unity";

        // ------------------------------------------------------------
        [MenuItem("Tools/Boss/① 샌드박스 빌드", priority = 0)]
        public static void Build()
        {
            EnsureFolders();

            // 1) 더미 스프라이트
            Sprite bossSpr   = MakeSprite(ArtDir + "/boss.png",      new Color(0.6f, 0.2f, 0.2f), 64);
            Sprite playerSpr = MakeSprite(ArtDir + "/player.png",    new Color(0.2f, 0.6f, 1.0f), 32);
            Sprite wolfSpr   = MakeSprite(ArtDir + "/wolf.png",      new Color(0.7f, 0.4f, 0.9f), 32);
            Sprite projSpr   = MakeSprite(ArtDir + "/projectile.png",new Color(1.0f, 0.8f, 0.2f), 16);
            Sprite teleSpr   = MakeSprite(ArtDir + "/telegraph.png", new Color(1.0f, 0.2f, 0.2f, 0.4f), 32);
            Sprite waveSpr   = MakeSprite(ArtDir + "/wave.png",      new Color(1.0f, 0.5f, 0.1f, 0.4f), 32);

            // 2) MonsterData (보스/늑대)
            MonsterData bossData = MakeMonsterData(
                DataDir + "/MonsterData_WolfBoss.asset",
                index: 1019, name: "달빛의 도살자 늑대인간", eng: "MoonlightButcherWerewolf",
                grade: MonsterGrade.Boss, hp: 16787, atk: 300, def: 250, speed: 5f, kbResist: 1f);

            MonsterData wolfData = MakeMonsterData(
                DataDir + "/MonsterData_PhantomWolf.asset",
                index: 10191, name: "환영 늑대", eng: "PhantomWolf",
                grade: MonsterGrade.Normal, hp: 100, atk: 200, def: 0, speed: 6f, kbResist: 1f);

            // 3) 더미 프리팹
            GameObject projPrefab = MakeProjectilePrefab(projSpr);
            GameObject telePrefab = MakeSimplePooledPrefab(PrefabDir + "/Telegraph.prefab", "Telegraph", teleSpr, 0f, sortingOrder: 5);
            GameObject wavePrefab = MakeSimplePooledPrefab(PrefabDir + "/WaveEffect.prefab", "WaveEffect", waveSpr, 0.6f, sortingOrder: 6);
            GameObject wolfPrefab = MakeWolfPrefab(wolfSpr, wolfData, telePrefab);

            // 4) 씬 구성
            BuildScene(bossSpr, playerSpr, bossData, projPrefab, telePrefab, wavePrefab, wolfPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BossSandbox] 빌드 완료 → " + ScenePath + " 를 열어 Play 하세요.");
        }

        [MenuItem("Tools/Boss/② 샌드박스 삭제", priority = 1)]
        public static void Clear()
        {
            if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
            if (AssetDatabase.IsValidFolder(SandboxRoot)) AssetDatabase.DeleteAsset(SandboxRoot);
            AssetDatabase.Refresh();
            Debug.Log("[BossSandbox] 샌드박스 산출물 삭제 완료.");
        }

        // ------------------------------------------------------------
        private static void EnsureFolders()
        {
            CreateFolderChain(ArtDir);
            CreateFolderChain(DataDir);
            CreateFolderChain(PrefabDir);
            CreateFolderChain(SceneDir);
        }

        private static void CreateFolderChain(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) CreateFolderChain(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // 색 사각형 PNG → Sprite 에셋
        private static Sprite MakeSprite(string assetPath, Color color, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = color;
            tex.SetPixels(px);
            tex.Apply();

            string full = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            File.WriteAllBytes(full, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = size; // 스프라이트 1장 = 월드 1유닛
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static MonsterData MakeMonsterData(string path, int index, string name, string eng,
            MonsterGrade grade, int hp, int atk, int def, float speed, float kbResist)
        {
            var d = AssetDatabase.LoadAssetAtPath<MonsterData>(path);
            if (d == null) { d = ScriptableObject.CreateInstance<MonsterData>(); AssetDatabase.CreateAsset(d, path); }
            d.index = index; d.monsterName = name; d.englishName = eng; d.grade = grade;
            d.maxHP = hp; d.attack = atk; d.defense = def; d.moveSpeed = speed; d.kbResist = kbResist;
            d.movePattern = MovePattern.StraightChase;
            EditorUtility.SetDirty(d);
            return d;
        }

        private static GameObject MakeProjectilePrefab(Sprite spr)
        {
            var go = new GameObject("Boss_Projectile");
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = spr; sr.sortingOrder = 8;
            var col = go.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = 0.4f;
            var rb = go.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0f;
            var po = go.AddComponent<PooledObject>(); po.autoReturnAfter = 5f;
            var proj = go.AddComponent<BossProjectile>(); proj.lifeTime = 5f;
            return SaveAndDestroy(go, PrefabDir + "/Boss_Projectile.prefab");
        }

        // 스프라이트 + (선택)PooledObject 만 가진 단순 연출 프리팹
        private static GameObject MakeSimplePooledPrefab(string path, string name, Sprite spr, float autoReturn, int sortingOrder)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = spr; sr.sortingOrder = sortingOrder;
            var po = go.AddComponent<PooledObject>(); po.autoReturnAfter = autoReturn;
            return SaveAndDestroy(go, path);
        }

        private static GameObject MakeWolfPrefab(Sprite spr, MonsterData data, GameObject dirTelegraph)
        {
            var go = new GameObject("Phantom_Wolf");
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = spr; sr.sortingOrder = 9;
            var rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 0f; rb.freezeRotation = true;
            var col = go.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = 0.45f;
            var mc = go.AddComponent<MonsterController>(); mc.monsterData = data; mc.sortingOrder = 9;
            var dash = go.AddComponent<PhantomWolfDash>();
            dash.dirTelegraphPrefab = dirTelegraph; // 돌진 방향 예고
            return SaveAndDestroy(go, PrefabDir + "/Phantom_Wolf.prefab");
        }

        private static GameObject SaveAndDestroy(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------
        private static void BuildScene(Sprite bossSpr, Sprite playerSpr, MonsterData bossData,
            GameObject projPrefab, GameObject telePrefab, GameObject wavePrefab, GameObject wolfPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 카메라 정사영/2D 보기 좋게
            var cam = Camera.main;
            if (cam != null) { cam.orthographic = true; cam.orthographicSize = 12f; cam.transform.position = new Vector3(0, 0, -10); }

            // 매니저: 풀들
            var managers = new GameObject("Managers");
            managers.AddComponent<GameObjectPool>();
            managers.AddComponent<MonsterPool>();

            // 플레이어 (태그 Player)
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(4, 0, 0);
            var psr = player.AddComponent<SpriteRenderer>(); psr.sprite = playerSpr; psr.sortingOrder = 10;
            var prb = player.AddComponent<Rigidbody2D>(); prb.gravityScale = 0f; prb.freezeRotation = true;
            var pcol = player.AddComponent<CircleCollider2D>(); pcol.radius = 0.4f;
            var ph = player.AddComponent<PlayerHealth>(); ph.maxHP = 1000;
            player.AddComponent<SandboxPlayerMove>(); // WASD 이동(테스트용)

            // 보스
            var boss = new GameObject("WolfBoss");
            boss.transform.position = Vector3.zero;
            var bsr = boss.AddComponent<SpriteRenderer>(); bsr.sprite = bossSpr; bsr.sortingOrder = 10;
            // MonsterController의 RequireComponent(Collider2D)는 추상 타입이라 자동 추가가 안 되므로
            // 구체 콜라이더/리지드바디를 먼저 붙인다.
            var brb = boss.AddComponent<Rigidbody2D>(); brb.gravityScale = 0f; brb.freezeRotation = true;
            var bcol = boss.AddComponent<CircleCollider2D>(); bcol.isTrigger = true; bcol.radius = 1f;
            var mc = boss.AddComponent<MonsterController>(); mc.monsterData = bossData; mc.sortingOrder = 10;

            // 패턴 컴포넌트 (Reset 기본값이 안 들어오므로 직접 기획서 수치 세팅)
            var melee = boss.AddComponent<Pattern_MeleeCombo>();
            melee.patternName = "MeleeCombo"; melee.isSpecial = false; melee.useRange = 2f; melee.telegraphTime = 2.5f; melee.cooldown = 10f; melee.chance = 30f;
            melee.telegraphPrefab = telePrefab; melee.hitEffectPrefab = wavePrefab;

            var charge = boss.AddComponent<Pattern_Charge>();
            charge.patternName = "Charge"; charge.isSpecial = false; charge.useRange = 8f; charge.telegraphTime = 2f; charge.cooldown = 10f; charge.chance = 25f;
            charge.telegraphPrefab = telePrefab; charge.impactEffectPrefab = wavePrefab;

            var blast = boss.AddComponent<Pattern_EnergyBlast>();
            blast.patternName = "EnergyBlast"; blast.isSpecial = false; blast.useRange = 10f; blast.telegraphTime = 1f; blast.cooldown = 12f; blast.chance = 25f;
            blast.projectilePrefab = projPrefab; blast.telegraphPrefab = telePrefab; blast.projectilesPerVolley = 6;

            var roar = boss.AddComponent<Pattern_RoarWave>();
            roar.patternName = "RoarWave"; roar.isSpecial = true; roar.useRange = 15f; roar.telegraphTime = 1.5f; roar.cooldown = 15f; roar.chance = 7f;
            roar.telegraphPrefab = telePrefab; roar.projectilePrefab = projPrefab;

            var phantom = boss.AddComponent<Pattern_PhantomDash>();
            phantom.patternName = "PhantomDash"; phantom.isSpecial = true; phantom.useRange = 15f; phantom.telegraphTime = 2f; phantom.cooldown = 20f; phantom.chance = 7f;
            phantom.wolfPrefab = wolfPrefab; phantom.telegraphPrefab = telePrefab;

            var leap = boss.AddComponent<Pattern_LeapBlast>();
            leap.patternName = "LeapBlast"; leap.isSpecial = true; leap.useRange = 30f; leap.telegraphTime = 2.5f; leap.cooldown = 25f; leap.chance = 6f;
            leap.telegraphPrefab = telePrefab; leap.ringWarningPrefab = telePrefab; leap.ringEffectPrefab = wavePrefab;

            var driver = boss.AddComponent<BossPatternDriver>(); // 부착된 패턴 자동 수집
            driver.debugManualMode = true; // 검증 편의: 숫자키로 패턴 직접 발동 (해제하면 자동 80/20)

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
#endif
