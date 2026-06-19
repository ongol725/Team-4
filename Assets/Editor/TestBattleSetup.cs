using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Team4 메뉴 → 순서대로 실행하면 테스트 전투 환경이 완성됩니다.
///   1. 무기 SO 공격 스타일 일괄 설정
///   2. Player 프리팹에 PlayerAttack 추가
///   3. TestBattle 씬 생성
/// </summary>
public static class TestBattleSetup
{
    private const string WEAPON_SO_PATH  = "Assets/Resources/ScriptableObjects/Weapons";
    private const string PLAYER_PREFAB   = "Assets/03.Prefabs/01.Players/Player.prefab";
    private const string GM_PREFAB       = "Assets/03.Prefabs/06.Gimmicks/GameManager.prefab";
    private const string SCENE_PATH      = "Assets/01.Scenes/00.TestBattle.unity";
    private const string PROJECTILE_DATA = "Assets/Resources/ScriptableObjects/Projectile/BasicBullet.asset";

    // 파일명 앞 번호 → 공격 스타일 매핑 (문서 기준)
    private static readonly Dictionary<int, WeaponAttackStyleType> StyleMap = new()
    {
        {  1, WeaponAttackStyleType.SingleTarget    }, // 단검
        {  2, WeaponAttackStyleType.MeleeFan        }, // 장검
        {  3, WeaponAttackStyleType.MeleeSingle     }, // 철퇴
        {  4, WeaponAttackStyleType.MeleeFan        }, // 채찍
        {  5, WeaponAttackStyleType.MeleeSingle     }, // 도끼
        {  6, WeaponAttackStyleType.SingleTarget    }, // 쇠뇌
        {  7, WeaponAttackStyleType.SingleTarget    }, // 권총
        {  8, WeaponAttackStyleType.SpreadShot      }, // 산탄총
        {  9, WeaponAttackStyleType.SingleTarget    }, // 활
        { 10, WeaponAttackStyleType.Boomerang       }, // 부메랑
        { 11, WeaponAttackStyleType.SingleTarget    }, // 지팡이
        { 12, WeaponAttackStyleType.AreaDrop        }, // 마도서
        { 13, WeaponAttackStyleType.AreaDrop        }, // 번개구슬
        { 14, WeaponAttackStyleType.ThrownExplosive }, // 수류탄
        { 15, WeaponAttackStyleType.BurstFire       }, // 라이플
        { 16, WeaponAttackStyleType.SingleTarget    }, // 수리검
        { 17, WeaponAttackStyleType.ThrownExplosive }, // 바주카
        { 18, WeaponAttackStyleType.BurstFire       }, // 레일건
        { 19, WeaponAttackStyleType.MeleeFan        }, // 대검
        { 20, WeaponAttackStyleType.MeleeFan        }, // 카타나
        { 21, WeaponAttackStyleType.PierceLine      }, // 스피어
        { 22, WeaponAttackStyleType.MeleeFan        }, // 플레일 (폴백)
        { 23, WeaponAttackStyleType.MeleeFan        }, // 메이스
        { 24, WeaponAttackStyleType.MeleeFan        }, // 몽둥이
        { 25, WeaponAttackStyleType.MeleeSingle     }, // 너클
        { 26, WeaponAttackStyleType.MeleeFan        }, // 화염방사기 (폴백)
        { 27, WeaponAttackStyleType.MeleeFan        }, // 할버드
        { 28, WeaponAttackStyleType.Sniper          }, // 장궁
        { 29, WeaponAttackStyleType.MeleeFan        }, // 워해머
        { 30, WeaponAttackStyleType.MeleeFan        }, // 사이드/낫
    };

    // ─────────────────────────────────────────────────────────────

    [MenuItem("Team4/1. 무기 SO 공격 스타일 일괄 설정")]
    static void SetWeaponAttackStyles()
    {
        var bullet = AssetDatabase.LoadAssetAtPath<SO_ProjectileData>(PROJECTILE_DATA);

        string[] guids = AssetDatabase.FindAssets("t:SO_WeaponData", new[] { WEAPON_SO_PATH });
        int changed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var wd = AssetDatabase.LoadAssetAtPath<SO_WeaponData>(path);
            if (wd == null) continue;

            // 파일명 앞 번호 파싱 ("001_Dagger" → 1)
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            string[] parts  = fileName.Split('_');
            if (parts.Length < 1 || !int.TryParse(parts[0], out int idx)) continue;

            bool dirty = false;

            if (StyleMap.TryGetValue(idx, out var style) && wd.attackStyleType != style)
            {
                wd.attackStyleType = style;
                dirty = true;
            }

            // projectileData 미설정 무기에 BasicBullet 기본 할당
            if (bullet != null && wd.projectileData == null)
            {
                wd.projectileData = bullet;
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(wd);
                changed++;
                Debug.Log($"[Setup] {wd.itemName}  →  {wd.attackStyleType}");
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("완료",
            $"{guids.Length}개 무기 SO 검사\n{changed}개 수정 완료", "확인");
    }

    // ─────────────────────────────────────────────────────────────

    [MenuItem("Team4/2. Player 프리팹에 PlayerAttack 추가")]
    static void AddPlayerAttackToPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB) == null)
        {
            EditorUtility.DisplayDialog("오류", $"Player.prefab 없음:\n{PLAYER_PREFAB}", "확인");
            return;
        }

        var contents = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB);

        if (contents.GetComponent<PlayerAttack>() != null)
        {
            PrefabUtility.UnloadPrefabContents(contents);
            EditorUtility.DisplayDialog("알림", "PlayerAttack이 이미 추가되어 있습니다.", "확인");
            return;
        }

        contents.AddComponent<PlayerAttack>();
        PrefabUtility.SaveAsPrefabAsset(contents, PLAYER_PREFAB);
        PrefabUtility.UnloadPrefabContents(contents);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("완료",
            "Player.prefab에 PlayerAttack 추가 완료\n\n" +
            "인스펙터에서 Enemy Layer를 설정해주세요.\n" +
            "(몬스터가 속한 레이어 선택)", "확인");
    }

    // ─────────────────────────────────────────────────────────────

    [MenuItem("Team4/3. TestBattle 씬 생성")]
    static void CreateTestScene()
    {
        // 이미 존재하면 열기만
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SCENE_PATH) != null)
        {
            EditorSceneManager.OpenScene(SCENE_PATH);
            EditorUtility.DisplayDialog("알림", "씬이 이미 존재합니다.\n열었습니다.", "확인");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // GameManager 프리팹 배치
        var gmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GM_PREFAB);
        if (gmPrefab != null)
            PrefabUtility.InstantiatePrefab(gmPrefab);
        else
            Debug.LogWarning("[Setup] GameManager.prefab 없음 — GameManager 오브젝트를 수동 배치하세요.");

        // Player 프리팹 배치
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB);
        if (playerPrefab != null)
        {
            var player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            if (player != null) player.transform.position = Vector3.zero;
        }
        else
            Debug.LogWarning("[Setup] Player.prefab 없음 — Player를 수동 배치하세요.");

        AddInventoryLoaderToScene();

        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("완료",
            "Assets/01.Scenes/00.TestBattle.unity 생성 완료\n\n" +
            "남은 수동 설정:\n" +
            "· PlayerAttack 컴포넌트 → Enemy Layer 지정\n" +
            "· 테스트용 더미 몬스터 배치", "확인");
    }

    // ─────────────────────────────────────────────────────────────

    [MenuItem("Team4/4. TestBattle 씬에 인벤토리 로더 추가")]
    static void AddInventoryLoaderMenu()
    {
        // 씬이 열려 있지 않으면 먼저 열기
        var scene = EditorSceneManager.GetSceneByPath(SCENE_PATH);
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        }

        AddInventoryLoaderToScene();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("완료",
            "InventoryAdditiveLoader 추가 완료\n\n" +
            "플레이 시 99.InventoryStore 씬이 자동 로드됩니다.\n" +
            "I 키로 인벤토리를 열 수 있습니다.", "확인");
    }

    static void AddInventoryLoaderToScene()
    {
        // 이미 존재하면 스킵
        var existing = Object.FindFirstObjectByType<InventoryAdditiveLoader>();
        if (existing != null)
        {
            Debug.Log("[Setup] InventoryAdditiveLoader 이미 존재합니다.");
            return;
        }

        var go = new GameObject("InventoryLoader");
        go.AddComponent<InventoryAdditiveLoader>();
        Debug.Log("[Setup] InventoryAdditiveLoader 추가 완료");
    }
}
