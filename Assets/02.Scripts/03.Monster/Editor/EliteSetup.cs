// ============================================================
// EliteSetup.cs  (Editor 전용)
// 엘리트 보스 에셋/프리팹 일괄 생성:
//  - MonsterData_1020(슬라임 엘리트), 1021(좀비 엘리트)  ← 베이스(1003/1009) 복제 + 조정
//  - 경고 원형 스프라이트(EliteWarning) 생성(지름 1유닛)
//  - Prefab_EliteExplosion (좀비 사망 폭발 장판: ZombieEliteExplosion + PooledObject)
//  - Prefab_EliteSlime  (슬라임 2.5배 + EliteMonster + EliteSlimeSplitGimmick(자기참조))
//  - Prefab_EliteZombie (좀비 1.6배 + EliteMonster + ZombieEliteDeathGimmick)
// 메뉴: Team4/엘리트 셋업
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using BagSurvivor.Monster;

public static class EliteSetup
{
    private const string MonsterDir = "Assets/03.Prefabs/02.Monsters/";
    private const string DataDir = "Assets/Resources/ScriptableObjects/Monsters/";
    private const string ImgPath = "Assets/04.Images/02.Monsters/EliteWarning.png";

    private const string SlimeData = DataDir + "MonsterData_1003_Slime.asset";
    private const string ZombieData = DataDir + "MonsterData_1009_Zombie.asset";
    private const string SlimePrefab = MonsterDir + "Prefab_Slime.prefab";
    private const string ZombiePrefab = MonsterDir + "Prefab_Zombie.prefab";

    private const string ESlimeData = DataDir + "MonsterData_1020_EliteSlime.asset";
    private const string EZombieData = DataDir + "MonsterData_1021_EliteZombie.asset";
    private const string ESlimePrefab = MonsterDir + "Prefab_EliteSlime.prefab";
    private const string EZombiePrefab = MonsterDir + "Prefab_EliteZombie.prefab";
    private const string ExplosionPrefab = MonsterDir + "Prefab_EliteExplosion.prefab";

    [MenuItem("Team4/엘리트 셋업 (슬라임/좀비 엘리트 + 폭발)")]
    public static void Setup()
    {
        var baseSlime = AssetDatabase.LoadAssetAtPath<MonsterData>(SlimeData);
        var baseZombie = AssetDatabase.LoadAssetAtPath<MonsterData>(ZombieData);
        if (baseSlime == null || baseZombie == null)
        { Debug.LogError("[EliteSetup] 베이스 데이터(1003/1009)를 찾지 못함"); return; }

        // 1) 엘리트 데이터 (HP는 EliteMonster 배율로 ×5, 데이터는 베이스 유지 / 이속·드롭만 조정)
        var eSlime = MakeEliteData(baseSlime, ESlimeData, 1020, "슬라임 엘리트", "EliteSlime", 2.5f, 35, ESlimePrefab);
        var eZombie = MakeEliteData(baseZombie, EZombieData, 1021, "좀비 엘리트", "EliteZombie", 2.0f, 500, EZombiePrefab);

        // 2) 경고 원형 스프라이트
        Sprite circle = MakeCircleSprite();

        // 3) 폭발 장판 프리팹
        GameObject explosion = MakeExplosionPrefab(circle);

        // 4) 엘리트 슬라임 프리팹 (자기참조 selfPrefab은 2차 패스에서 연결)
        MakeEliteSlimePrefab(eSlime);

        // 5) 엘리트 좀비 프리팹
        MakeEliteZombiePrefab(eZombie, explosion);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EliteSetup] 완료 — Prefab_EliteSlime / Prefab_EliteZombie / Prefab_EliteExplosion + 데이터(1020/1021) 생성.\n" +
                  "이어서 'BagSurvivor/Setup/Create or Update MonsterSpawnSystem'을 실행하면 Elite 방(1·3층)에 자동 배선됩니다.");
    }

    // ── 데이터 ────────────────────────────────────────────────
    private static MonsterData MakeEliteData(MonsterData baseData, string path, int index,
        string krName, string enName, float moveSpeed, int drop, string prefabPath)
    {
        var d = AssetDatabase.LoadAssetAtPath<MonsterData>(path);
        bool isNew = d == null;
        if (isNew) d = ScriptableObject.CreateInstance<MonsterData>();

        d.index = index;
        d.monsterName = krName;
        d.englishName = enName;
        d.grade = MonsterGrade.Elite;
        d.maxHP = baseData.maxHP;       // ×5는 EliteMonster.hpMultiplier로 적용
        d.attack = baseData.attack;
        d.defense = baseData.defense;
        d.attackStyle = baseData.attackStyle;
        d.attackPattern = baseData.attackPattern;
        d.moveSpeed = moveSpeed;        // 엘리트는 느리게
        d.movePattern = baseData.movePattern;
        d.kbResist = 0.8f;              // 엘리트 보스는 넉백 강저항
        d.kbCooldown = baseData.kbCooldown;
        d.spawnType = baseData.spawnType;
        d.spawnInterval = baseData.spawnInterval;
        d.spawnCount = 1;
        d.dropItemID = "Item_001";
        d.dropItemValue = drop;
        d.prefabPath = prefabPath;

        if (isNew) AssetDatabase.CreateAsset(d, path);
        EditorUtility.SetDirty(d);
        return d;
    }

    // ── 경고 원형 스프라이트 (지름 1유닛) ──────────────────────
    private static Sprite MakeCircleSprite()
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(N / 2f, N / 2f);
        float rOuter = N / 2f - 2f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), c);
                // 가장자리에 링 강조 + 내부 옅은 채움
                float a = dist > rOuter ? 0f : (dist > rOuter - 10f ? 1f : 0.35f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();

        System.IO.File.WriteAllBytes(ImgPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ImgPath, ImportAssetOptions.ForceUpdate);

        var imp = (TextureImporter)AssetImporter.GetAtPath(ImgPath);
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = N;        // 256px / 256ppu = 1유닛 지름
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ImgPath);
    }

    // ── 폭발 장판 프리팹 ──────────────────────────────────────
    private static GameObject MakeExplosionPrefab(Sprite circle)
    {
        var go = new GameObject("EliteExplosion");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circle;
        sr.color = new Color(0.8f, 0.1f, 0.1f, 0.25f);
        sr.sortingOrder = 1; // 바닥 위, 몬스터 아래 정도
        go.AddComponent<PooledObject>(); // autoReturnAfter=0 → 스크립트가 반환
        var ex = go.AddComponent<ZombieEliteExplosion>();
        ex.warningRenderer = sr;
        ex.radius = 10f;
        ex.chargeTime = 5f;
        ex.dotPercent = 0.5f;
        ex.dotDuration = 5f;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ExplosionPrefab);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ── 엘리트 슬라임 프리팹 ──────────────────────────────────
    private static void MakeEliteSlimePrefab(MonsterData data)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SlimePrefab) == null)
        { Debug.LogWarning("[EliteSetup] Prefab_Slime 없음"); return; }

        AssetDatabase.DeleteAsset(ESlimePrefab);
        AssetDatabase.CopyAsset(SlimePrefab, ESlimePrefab);

        // 1차 패스: 데이터/스케일/기믹 구성
        var contents = PrefabUtility.LoadPrefabContents(ESlimePrefab);
        var mc = contents.GetComponentInChildren<MonsterController>();
        if (mc != null) mc.monsterData = data;
        contents.transform.localScale = Vector3.one * 2.5f;

        var oldSplit = contents.GetComponentInChildren<SlimeSplitGimmick>();
        if (oldSplit != null) Object.DestroyImmediate(oldSplit, true); // 일반 분열 제거

        var host = mc != null ? mc.gameObject : contents;
        if (host.GetComponent<EliteMonster>() == null) host.AddComponent<EliteMonster>().hpMultiplier = 5f;
        var split = host.GetComponent<EliteSlimeSplitGimmick>();
        if (split == null) split = host.AddComponent<EliteSlimeSplitGimmick>();
        split.rootGeneration = 2;
        split.splitCount = 3;

        PrefabUtility.SaveAsPrefabAsset(contents, ESlimePrefab);
        PrefabUtility.UnloadPrefabContents(contents);

        // 2차 패스: selfPrefab을 자기 자신(저장된 에셋)으로 연결
        var self = AssetDatabase.LoadAssetAtPath<GameObject>(ESlimePrefab);
        var contents2 = PrefabUtility.LoadPrefabContents(ESlimePrefab);
        var split2 = contents2.GetComponentInChildren<EliteSlimeSplitGimmick>();
        if (split2 != null) split2.selfPrefab = self;
        PrefabUtility.SaveAsPrefabAsset(contents2, ESlimePrefab);
        PrefabUtility.UnloadPrefabContents(contents2);
    }

    // ── 엘리트 좀비 프리팹 ──────────────────────────────────
    private static void MakeEliteZombiePrefab(MonsterData data, GameObject explosion)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefab) == null)
        { Debug.LogWarning("[EliteSetup] Prefab_Zombie 없음"); return; }

        AssetDatabase.DeleteAsset(EZombiePrefab);
        AssetDatabase.CopyAsset(ZombiePrefab, EZombiePrefab);

        var contents = PrefabUtility.LoadPrefabContents(EZombiePrefab);
        var mc = contents.GetComponentInChildren<MonsterController>();
        if (mc != null) mc.monsterData = data;
        contents.transform.localScale = Vector3.one * 1.6f;

        var host = mc != null ? mc.gameObject : contents;
        if (host.GetComponent<EliteMonster>() == null) host.AddComponent<EliteMonster>().hpMultiplier = 5f;
        var death = host.GetComponent<ZombieEliteDeathGimmick>();
        if (death == null) death = host.AddComponent<ZombieEliteDeathGimmick>();
        death.explosionPrefab = explosion;

        PrefabUtility.SaveAsPrefabAsset(contents, EZombiePrefab);
        PrefabUtility.UnloadPrefabContents(contents);
    }
}
#endif
