// Team4 메뉴 → "Setup InventoryStore Scene" 실행 시
// 99.InventoryStore 씬에 인벤토리·상점 UI를 자동 구성합니다.
// 모든 컴포넌트 참조(SerializeField)까지 자동 연결됩니다.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class InventoryStoreSetupTool
{
    // ══════════════════════════════════════════════════════════════
    // 레이아웃 상수
    // ══════════════════════════════════════════════════════════════
    const int CellSize  = 50;
    const int CellGap   = 2;
    const int GridRows  = 7;   // 전체 행 수
    const int GridCols  = 10;  // 전체 열 수
    const int SlotW     = 162;
    const int SlotH     = 258;
    const int SlotGap   = 8;

    static Font _font;

    // ══════════════════════════════════════════════════════════════
    // 메뉴 진입점
    // ══════════════════════════════════════════════════════════════
    [MenuItem("Team4/Setup InventoryStore Scene", priority = 1)]
    static void Run()
    {
        if (!EditorUtility.DisplayDialog("씬 자동 구성",
            "현재 씬에 InventoryStore UI를 생성합니다.\n" +
            "기존 'InventoryStoreRoot'가 있으면 삭제 후 재생성합니다.\n\n" +
            "99.InventoryStore 씬을 열고 실행해 주세요.",
            "생성", "취소")) return;

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
             ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 기존 루트 삭제
        var existing = GameObject.Find("InventoryStoreRoot");
        if (existing != null) { Undo.DestroyObjectImmediate(existing); }

        Undo.SetCurrentGroupName("Setup InventoryStore UI");

        // ── 1. Canvas ──
        var root   = BuildCanvas();
        var canvas = root.GetComponent<Canvas>();

        // ── 2. 인벤토리 섹션 ──
        var (gridGO, inventoryGrid, inventoryGridUI) = BuildInventorySection(root);

        // ── 3. 임시칸 ──
        var tempSlot = BuildTempSlot(root);

        // ── 4. 상점 섹션 ──
        var (shopGO, shopManager, shopUI,
             slotComponents, rerollBtn, rerollCostTxt) = BuildShopSection(root);

        // ── 5. 직렬화 필드 연결 ──
        WireInventoryGridUI(inventoryGridUI, inventoryGrid, tempSlot);
        WireShopUI(shopUI, shopManager, inventoryGridUI,
                   slotComponents, rerollBtn, rerollCostTxt, shopGO);

        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Selection.activeGameObject = root;
        Debug.Log("[Team4] InventoryStore 씬 구성 완료 ✓");
    }

    // ══════════════════════════════════════════════════════════════
    // 1. Canvas
    // ══════════════════════════════════════════════════════════════
    static GameObject BuildCanvas()
    {
        var go = new GameObject("InventoryStoreRoot");
        Undo.RegisterCreatedObjectUndo(go, "Create Root");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        // EventSystem (없을 때만 생성)
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        return go;
    }

    // ══════════════════════════════════════════════════════════════
    // 2. 인벤토리 섹션 (좌측)
    // ══════════════════════════════════════════════════════════════
    static (GameObject gridGO, InventoryGrid grid, InventoryGridUI gridUI)
        BuildInventorySection(GameObject canvasGO)
    {
        // 컨테이너 (화면 중앙 기준 좌측)
        var container = MakeGO("InventoryContainer", canvasGO);
        SetRect(container, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-480, 0), new Vector2(300, 500));

        // 배경 패널
        var bg = MakeImage(container, "BG",
                           new Color(0.08f, 0.08f, 0.12f, 0.95f));
        SetRect(bg, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

        // 타이틀
        var title = MakeText(container, "Title", "인벤토리",
                             18, FontStyle.Bold, TextAnchor.MiddleCenter,
                             Color.white);
        SetRect(title.gameObject, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(0, 30));

        // 구분선
        var line = MakeImage(container, "Divider",
                             new Color(0.4f, 0.4f, 0.5f, 0.6f));
        SetRect(line, new Vector2(0.05f, 1), new Vector2(0.95f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(0, 1));

        // 그리드 패널 (InventoryGrid + InventoryGridUI 컴포넌트 부착)
        int gridW = GridCols * (CellSize + CellGap) - CellGap;
        int gridH = GridRows * (CellSize + CellGap) - CellGap;

        var gridPad = MakeImage(container, "GridPad",
                                new Color(0.05f, 0.05f, 0.08f, 0.80f));
        SetRect(gridPad, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(16, -64),
                new Vector2(gridW + 32, gridH + 32));

        // 실제 Grid 오브젝트 (InventoryGridUI가 자식 셀을 자동 생성)
        var gridGO = MakeGO("InventoryGrid", gridPad);
        SetRect(gridGO, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(16, -16),
                new Vector2(gridW, gridH));

        var inventoryGrid   = gridGO.AddComponent<InventoryGrid>();
        var inventoryGridUI = gridGO.AddComponent<InventoryGridUI>();

        // 컨테이너 최종 크기 (7×10 그리드 기준)
        SetRect(container, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-480, 0),
                new Vector2(gridW + 64, gridH + 100));

        return (gridGO, inventoryGrid, inventoryGridUI);
    }

    // ══════════════════════════════════════════════════════════════
    // 3. 상점 섹션 (하단)
    // ══════════════════════════════════════════════════════════════
    static (GameObject shopGO, ShopManager manager, ShopUI shopUI,
            ShopSlotUI[] slots, Button rerollBtn, Text rerollTxt)
        BuildShopSection(GameObject canvasGO)
    {
        int totalSlotsW = SlotW * 5 + SlotGap * 4;

        // 컨테이너 (인벤토리 우측, 상단 정렬)
        // gridH = 7*52-2 = 362  →  invContainerH = 462  →  invHalfH = 231
        // shopH = SlotH+110 = 368  →  shopHalfH = 184
        // 상단 정렬 y = 231 - 184 = 47
        var shopGO = MakeGO("ShopContainer", canvasGO);
        SetRect(shopGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(300, 47),
                new Vector2(totalSlotsW + 80, SlotH + 110));

        // 배경
        var bg = MakeImage(shopGO, "BG",
                           new Color(0.06f, 0.06f, 0.10f, 0.97f));
        SetRect(bg, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

        // ShopManager + ShopUI 컴포넌트
        var shopManager = shopGO.AddComponent<ShopManager>();
        var shopUI      = shopGO.AddComponent<ShopUI>();

        // 헤더 행
        var header = MakeGO("Header", shopGO);
        SetRect(header, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(0, 36));

        var shopTitle = MakeText(header, "ShopTitle", "상점",
                                 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                                 Color.white);
        SetRect(shopTitle.gameObject, new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(120, 0));

        // 리롤 버튼
        var (rerollBtn, rerollBtnTxt) = MakeButton(header, "RerollButton",
            "리롤  (2G)", new Color(0.3f, 0.55f, 0.85f));
        SetRect(rerollBtn.gameObject, new Vector2(1, 0), new Vector2(1, 1),
                new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(130, 34));

        // 구분선
        var line = MakeImage(shopGO, "Divider",
                             new Color(0.4f, 0.4f, 0.5f, 0.6f));
        SetRect(line, new Vector2(0.02f, 1), new Vector2(0.98f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -54), new Vector2(0, 1));

        // 슬롯 영역
        var slotsArea = MakeGO("SlotsArea", shopGO);
        SetRect(slotsArea, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0.5f, 0.5f),
                new Vector2(0, -26),
                new Vector2(totalSlotsW, SlotH));

        var hlg = slotsArea.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing             = SlotGap;
        hlg.childAlignment      = TextAnchor.MiddleCenter;
        hlg.childControlWidth   = false;
        hlg.childControlHeight  = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        // 슬롯 5개 생성
        var slotComponents = new ShopSlotUI[5];
        for (int i = 0; i < 5; i++)
            slotComponents[i] = BuildShopSlot(slotsArea, i);

        return (shopGO, shopManager, shopUI,
                slotComponents, rerollBtn, rerollBtnTxt);
    }

    // ══════════════════════════════════════════════════════════════
    // 슬롯 1개 생성
    // ══════════════════════════════════════════════════════════════
    static ShopSlotUI BuildShopSlot(GameObject parent, int idx)
    {
        var slot = MakeImage(parent, $"Slot_{idx}",
                             new Color(0.14f, 0.14f, 0.18f, 1f));
        var slotRT = slot.GetComponent<RectTransform>();
        slotRT.sizeDelta = new Vector2(SlotW, SlotH);

        // 좌상단 컬러 바
        var bar = MakeImage(slot, "ColorBar",
                            new Color(0.25f, 0.50f, 0.90f, 1f)); // 런타임에 rarity 색으로 교체됨
        SetRect(bar, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(0, 5));

        // 희귀도 텍스트
        var rarityTxt = MakeText(slot, "RarityText", "일반",
                                 10, FontStyle.Normal, TextAnchor.MiddleLeft,
                                 new Color(0.7f, 0.7f, 0.7f));
        SetRect(rarityTxt.gameObject, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(-16, 20));

        // 아이템 이름
        var nameTxt = MakeText(slot, "NameText", "아이템 이름",
                               13, FontStyle.Bold, TextAnchor.MiddleCenter,
                               Color.white);
        SetRect(nameTxt.gameObject, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(-8, 26));

        // 미리보기 컨테이너 (ShopSlotUI가 런타임에 셀 타일 자동 생성)
        var preview = MakeGO("PreviewContainer", slot);
        SetRect(preview, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(72, 72));

        // 코스트
        var costTxt = MakeText(slot, "CostText", "0 G",
                               12, FontStyle.Normal, TextAnchor.MiddleCenter,
                               new Color(1f, 0.85f, 0.3f));
        SetRect(costTxt.gameObject, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0.5f, 0), new Vector2(0, 58), new Vector2(-8, 26));

        // 구매 버튼
        var (buyBtn, buyBtnTxt) = MakeButton(slot, "BuyButton",
            "구매", new Color(0.20f, 0.70f, 0.35f));
        SetRect(buyBtn.gameObject, new Vector2(0.1f, 0), new Vector2(0.9f, 0),
                new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(0, 34));

        // ShopSlotUI 컴포넌트 부착 + 직렬화 필드 연결
        var slotUI = slot.AddComponent<ShopSlotUI>();
        var so = new SerializedObject(slotUI);
        so.FindProperty("_nameText").objectReferenceValue        = nameTxt;
        so.FindProperty("_costText").objectReferenceValue        = costTxt;
        so.FindProperty("_rarityText").objectReferenceValue      = rarityTxt;
        so.FindProperty("_buyButton").objectReferenceValue       = buyBtn;
        so.FindProperty("_buyButtonText").objectReferenceValue   = buyBtnTxt;
        so.FindProperty("_previewContainer").objectReferenceValue =
            preview.GetComponent<RectTransform>();
        so.FindProperty("_slotBackground").objectReferenceValue  =
            slot.GetComponent<Image>();
        so.ApplyModifiedPropertiesWithoutUndo();

        return slotUI;
    }

    // ══════════════════════════════════════════════════════════════
    // 임시칸 생성
    // ══════════════════════════════════════════════════════════════
    static TempSlotUI BuildTempSlot(GameObject canvasGO)
    {
        const int SlotSize = 120;

        var go = MakeGO("TempSlot", canvasGO);
        // 인벤토리 좌측 하단 (인벤토리 컨테이너 기준 왼쪽)
        SetRect(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-480 - SlotSize - 16, -100),
                new Vector2(SlotSize, SlotSize));

        var bg = MakeImage(go, "BG", new Color(0.12f, 0.12f, 0.16f, 0.90f));
        SetRect(bg, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

        var outline = go.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor    = new Color(0.6f, 0.4f, 0.1f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        var label = MakeText(go, "Label", "임시 보관",
                             11, FontStyle.Normal, TextAnchor.LowerCenter, new Color(0.7f, 0.6f, 0.3f));
        SetRect(label.gameObject, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(0, 18));

        var tempSlot = go.AddComponent<TempSlotUI>();

        // TempSlotUI._background 연결
        var so = new SerializedObject(tempSlot);
        so.FindProperty("_background").objectReferenceValue = bg.GetComponent<UnityEngine.UI.Image>();
        so.ApplyModifiedPropertiesWithoutUndo();

        return tempSlot;
    }

    // ══════════════════════════════════════════════════════════════
    // SerializedField 연결
    // ══════════════════════════════════════════════════════════════
    static void WireInventoryGridUI(InventoryGridUI gridUI, InventoryGrid grid, TempSlotUI tempSlot)
    {
        var so = new SerializedObject(gridUI);
        so.FindProperty("_grid").objectReferenceValue     = grid;
        so.FindProperty("_tempSlot").objectReferenceValue = tempSlot;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireShopUI(ShopUI shopUI, ShopManager manager,
        InventoryGridUI gridUI, ShopSlotUI[] slots,
        Button rerollBtn, Text rerollCostTxt, GameObject panelRoot)
    {
        var so = new SerializedObject(shopUI);
        so.FindProperty("_shopManager").objectReferenceValue     = manager;
        so.FindProperty("_inventoryGridUI").objectReferenceValue = gridUI;
        so.FindProperty("_rerollButton").objectReferenceValue    = rerollBtn;
        so.FindProperty("_rerollCostText").objectReferenceValue  = rerollCostTxt;
        so.FindProperty("_panelRoot").objectReferenceValue       = panelRoot;

        var slotsProp = so.FindProperty("_slots");
        slotsProp.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ══════════════════════════════════════════════════════════════
    // 유틸 헬퍼
    // ══════════════════════════════════════════════════════════════
    static GameObject MakeGO(string name, GameObject parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static GameObject MakeImage(GameObject parent, string name, Color color)
    {
        var go  = MakeGO(name, parent);
        go.AddComponent<Image>().color = color;
        return go;
    }

    static Text MakeText(GameObject parent, string name, string content,
        int size, FontStyle style, TextAnchor alignment, Color color)
    {
        var go  = MakeGO(name, parent);
        var txt = go.AddComponent<Text>();
        txt.text      = content;
        txt.font      = _font;
        txt.fontSize  = size;
        txt.fontStyle = style;
        txt.alignment = alignment;
        txt.color     = color;
        txt.raycastTarget = false;
        return txt;
    }

    // 버튼 + 레이블 텍스트 반환
    static (Button btn, Text label) MakeButton(GameObject parent,
        string name, string label, Color bgColor)
    {
        var go  = MakeGO(name, parent);
        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn       = go.AddComponent<Button>();
        var colors    = btn.colors;
        colors.highlightedColor = bgColor * 1.25f;
        colors.pressedColor     = bgColor * 0.75f;
        btn.colors = colors;

        var labelTxt = MakeText(go, "Label", label,
            13, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetRect(labelTxt.gameObject, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        btn.targetGraphic = img;

        return (btn, labelTxt);
    }

    static void SetRect(GameObject go,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var rt          = go.GetComponent<RectTransform>();
        rt.anchorMin    = anchorMin;
        rt.anchorMax    = anchorMax;
        rt.pivot        = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta    = sizeDelta;
    }
}
