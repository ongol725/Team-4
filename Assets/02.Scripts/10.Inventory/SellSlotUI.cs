using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 임시칸 오른쪽에 표시되는 판매 슬롯.
/// 아이템 블록을 드래그해서 좌클릭하면 구매가의 50%를 골드로 반환한다.
/// </summary>
public class SellSlotUI : MonoBehaviour
{
    public static SellSlotUI Instance { get; private set; }

    [SerializeField] private Canvas _canvas;

    public RectTransform PanelRt { get; private set; }

    private Image          _bg;
    private Canvas         _cachedCanvas;
    private InventoryGridUI _gridUI;

    private static readonly int[] RarityBaseCosts = { 100, 200, 300, 400 };

    private static readonly Color IdleColor  = new Color(0.50f, 0.05f, 0.05f, 0.88f);
    private static readonly Color HoverColor = new Color(0.85f, 0.12f, 0.12f, 0.96f);

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (_canvas == null)
        {
            // TempSlot과 같은 좌표계(InventoryStoreRoot)에 배치하기 위해 해당 Canvas를 우선 사용
            var go = GameObject.Find("InventoryStoreRoot");
            if (go != null) _canvas = go.GetComponent<Canvas>();
        }
        if (_canvas == null)
        {
            var go = GameObject.Find("Canvas_Inventory");
            if (go != null) _canvas = go.GetComponent<Canvas>();
        }
        if (_canvas == null) return;

        _cachedCanvas = _canvas;
        _gridUI       = FindFirstObjectByType<InventoryGridUI>();
        BuildUI();
    }

    private void Update()
    {
        if (_bg == null) return;
        bool isDragging = _gridUI != null && _gridUI.IsAnyFollowingMouse;
        _bg.color = (isDragging && IsMouseOver()) ? HoverColor : IdleColor;
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>현재 마우스 커서가 판매 슬롯 위에 있는지 반환</summary>
    public bool IsMouseOver()
    {
        if (PanelRt == null || Mouse.current == null) return false;
        var cam = (_cachedCanvas != null && _cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _cachedCanvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(
            PanelRt, Mouse.current.position.ReadValue(), cam);
    }

    /// <summary>팝업 토글 시 패널 전체를 표시/숨깁니다.</summary>
    public void SetPanelActive(bool active) =>
        PanelRt?.gameObject.SetActive(active);

    /// <summary>아이템을 판매하고 골드를 지급한다</summary>
    public void Sell(ItemInstance inst)
    {
        int rarityIdx = Mathf.Clamp((int)inst.data.rarity, 0, RarityBaseCosts.Length - 1);
        int price = RarityBaseCosts[rarityIdx] / 2;
        GameManager.Instance?.AddGold(price);
    }

    // ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // TempSlot: anchor=center, pos=(137,-50), size=200×200
        // SellSlot: TempSlot 오른쪽 끝(237) + 8px 여백 + 자신 너비 절반(100) = 345
        var panel = new GameObject("SellSlot", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_canvas.transform, false);

        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(345f, -50f);
        rt.sizeDelta        = new Vector2(200f, 200f);
        PanelRt = rt;

        _bg = panel.GetComponent<Image>();
        _bg.color = IdleColor;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // ── 타이틀 "판매" ──────────────────────────────────────────
        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(panel.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin        = new Vector2(0f, 1f);
        titleRt.anchorMax        = new Vector2(1f, 1f);
        titleRt.pivot            = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = Vector2.zero;
        titleRt.sizeDelta        = new Vector2(0f, 30f);
        var titleTxt = titleGo.GetComponent<Text>();
        titleTxt.font      = font;
        titleTxt.text      = "판매";
        titleTxt.fontSize  = 17;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color     = new Color(1f, 0.55f, 0.55f);
        titleTxt.raycastTarget = false;

        // ── 안내 텍스트 ───────────────────────────────────────────
        var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
        hintGo.transform.SetParent(panel.transform, false);
        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 1f);
        hintRt.offsetMin = new Vector2(8f, 8f);
        hintRt.offsetMax = new Vector2(-8f, -34f);
        var hintTxt = hintGo.GetComponent<Text>();
        hintTxt.font      = font;
        hintTxt.text      = "아이템을 여기에\n올려놓으면 판매됩니다\n(구매가의 50%)";
        hintTxt.fontSize  = 11;
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.color     = new Color(0.85f, 0.65f, 0.65f);
        hintTxt.raycastTarget = false;

        // 인벤토리가 닫힌 상태에서 시작하므로 초기에는 숨김
        panel.SetActive(false);
    }
}
