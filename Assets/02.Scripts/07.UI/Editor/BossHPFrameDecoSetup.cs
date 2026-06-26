// ============================================================
// BossHPFrameDecoSetup.cs  (Editor 전용)
// 보스 HP 프레임 데코(MHP_Boss_Frame_Decorating)를 SpriteRenderer → UI Image로 전환.
//  - 문제: 데코가 SpriteRenderer라 ScreenSpaceOverlay 캔버스 안에서 화면 밖 월드에 그려져 안 보임.
//  - 해결: UI Image로 만든 컨테이너 프리팹(BossHP_FrameDeco)을 같은 부모에 배치하고
//          기존 SpriteRenderer 데코는 제거. (위치/크기는 이후 수동 조정)
// 메뉴: Team4/보스 HP 프레임 데코 UI화 + 배치
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BossHPFrameDecoSetup
{
    private const string SpritePath = "Assets/04.Images/01.UI/03.Ingames/MHP_Boss_Frame_Decorating.png";
    private const string PrefabPath = "Assets/03.Prefabs/05.UI/BossHP_FrameDeco.prefab";
    private const int    UILayer    = 5; // UI

    // 기록해 둔 원본 시작값(수동 재조정 전 임시 위치). scale은 UI 좌표계라 1로 두고 SetNativeSize.
    private static readonly Vector2 LeftPos  = new Vector2(-132f, -75f);
    private static readonly Vector2 RightPos = new Vector2(134f, -75f);

    [MenuItem("Team4/보스 HP 프레임 데코 UI화 + 배치")]
    public static void Setup()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null) { Debug.LogError("[BossDeco] 스프라이트 없음: " + SpritePath); return; }

        // 1) UI Image 컨테이너 프리팹 생성 (이미 있으면 재생성)
        GameObject prefab = BuildPrefab(sprite);

        // 2) 씬에서 Boss_HpBar 와 기존 SpriteRenderer 데코 찾기
        var bossBar = GameObject.Find("Boss_HpBar");
        var old0 = GameObject.Find("MHP_Boss_Frame_Decorating_0");
        var old1 = GameObject.Find("MHP_Boss_Frame_Decorating_1");

        // 부모: Boss_HpBar 와 같은 부모(L4_HUD2). 없으면 기존 데코 부모로 폴백.
        Transform parent = bossBar != null ? bossBar.transform.parent
                         : old0 != null ? old0.transform.parent
                         : old1 != null ? old1.transform.parent : null;

        if (parent == null)
        {
            Debug.LogWarning("[BossDeco] 씬에서 Boss_HpBar / 기존 데코를 못 찾음. " +
                             "프리팹만 생성했으니 L4_HUD2의 Boss_HpBar 위(앞 형제)로 직접 드래그해 주세요: " + PrefabPath);
            Selection.activeObject = prefab;
            return;
        }

        // 3) 프리팹 인스턴스를 Boss_HpBar 와 같은 부모에 배치
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(inst, "Place BossHP_FrameDeco");
        var rt = inst.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;

        // 보스바 위치에 프레임이 겹치도록 RectTransform 레이아웃을 Boss_HpBar 와 동일하게
        if (bossBar != null)
        {
            var barRt = bossBar.GetComponent<RectTransform>();
            if (barRt != null)
            {
                rt.anchorMin = barRt.anchorMin;
                rt.anchorMax = barRt.anchorMax;
                rt.pivot = barRt.pivot;
                rt.anchoredPosition = barRt.anchoredPosition;
            }
            // 레이어(렌더 순서): Boss_HpBar 보다 앞 형제로 → 프레임이 체력바 뒤에 깔림
            rt.SetSiblingIndex(bossBar.transform.GetSiblingIndex());
        }
        else
        {
            rt.anchoredPosition = Vector2.zero;
        }

        // 4) 기존 SpriteRenderer 데코 제거 (Undo 가능)
        if (old0 != null) Undo.DestroyObjectImmediate(old0);
        if (old1 != null) Undo.DestroyObjectImmediate(old1);

        EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
        Selection.activeGameObject = inst;
        Debug.Log("[BossDeco] 완료 — BossHP_FrameDeco(UI Image)를 Boss_HpBar 뒤(앞 형제) 레이어로 배치, " +
                  "기존 SpriteRenderer 데코 제거.\n위치·크기는 인스펙터에서 다시 잡아 주세요. (저장 잊지 마세요)");
    }

    private static GameObject BuildPrefab(Sprite sprite)
    {
        // 컨테이너 (RectTransform만)
        var root = new GameObject("BossHP_FrameDeco", typeof(RectTransform));
        root.layer = UILayer;
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 1f); // 상단 중앙 기준(보스바 위치 가정)
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = Vector2.zero;

        MakeImageChild(rootRt, "Deco_L", sprite, LeftPos);
        MakeImageChild(rootRt, "Deco_R", sprite, RightPos);

        var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return saved;
    }

    private static void MakeImageChild(RectTransform parent, string name, Sprite sprite, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = UILayer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false; // 데코는 클릭 막지 않게
        img.SetNativeSize();
    }
}
#endif
