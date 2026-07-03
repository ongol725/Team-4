using System.Collections;
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

    [Header("배경 스프라이트 (빨간 판매 배경) — Item_Shop_Grid_red_bg_with_border")]
    [SerializeField] private Sprite _bgSprite;

    [Header("상점 오버레이 위치/크기 (드래그 시 상점 위에 표시) — BG(Item_Shop_Grid)와 동일한 영역")]
    [SerializeField] private Vector2 _shopOverlayPos  = new Vector2(789.05f, -1.25f);
    [SerializeField] private Vector2 _shopOverlaySize = new Vector2(341.07f, 757.65f);

    [Header("트랜지션")]
    [SerializeField] private float _fadeDuration = 0.15f;

    [Header("디버그")]
    [Tooltip("체크하면 플레이 중 패널을 미리 표시해서 위치/크기를 바로 조정할 수 있습니다")]
    [SerializeField] private bool _previewInEditor = false;

    public RectTransform PanelRt { get; private set; }

    private Image          _bg;
    private Canvas         _cachedCanvas;
    private InventoryGridUI _gridUI;
    private bool           _inShopMode;

    private Text        _priceText;
    private CanvasGroup _canvasGroup;
    private Coroutine   _fadeRoutine;

    private static readonly Color PriceActiveColor = new Color(1f, 0.90f, 0.35f, 1f);
    private static readonly Color PriceDimColor    = new Color(1f, 0.90f, 0.35f, 0.45f);

    private static readonly int[] RarityBaseCosts = { 100, 200, 300, 400 };

    // 스프라이트 미지정 시(폴백) 사용하는 단색 빨강
    private static readonly Color IdleColorFlat  = new Color(0.50f, 0.05f, 0.05f, 0.88f);
    private static readonly Color HoverColorFlat = new Color(0.85f, 0.12f, 0.12f, 0.96f);
    // 빨간 배경 스프라이트 사용 시: 스프라이트 색은 유지하고 명암만 조절해 호버 피드백을 준다
    private static readonly Color IdleColorTint  = new Color(0.80f, 0.80f, 0.80f, 1f);
    private static readonly Color HoverColorTint = new Color(1f, 1f, 1f, 1f);

    // BuildUI에서 스프라이트 유무에 따라 선택되는 실제 색
    private Color _idleColor  = IdleColorFlat;
    private Color _hoverColor = HoverColorFlat;

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
        bool isOver     = isDragging && IsMouseOver();
        _bg.color = isOver ? _hoverColor : _idleColor;

        UpdateLivePrice(isDragging, isOver);
    }

    /// <summary>드래그 중인 아이템의 판매가를 골드 아이콘 위에 실시간으로 표시한다.
    /// 완전히 올라와 있을 때(over)만 밝게 강조해 오조작을 방지한다.</summary>
    private void UpdateLivePrice(bool isDragging, bool isOver)
    {
        if (_priceText == null) return;

        var inst = _gridUI != null ? _gridUI.ActiveFollowingBlock?.Instance : null;
        if (isDragging && inst?.data != null)
        {
            _priceText.gameObject.SetActive(true);
            _priceText.text  = $"판매가 {ComputePrice(inst)} G";
            _priceText.color = isOver ? PriceActiveColor : PriceDimColor;
        }
        else
        {
            _priceText.gameObject.SetActive(false);
        }
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

    /// <summary>강제 닫기 전용 (전투 구역 진입 등). 표시는 SetShopMode로만 제어.</summary>
    public void SetPanelActive(bool active)
    {
        if (!active)
        {
            _inShopMode = false;
            PanelRt?.gameObject.SetActive(false);
        }
    }

    /// <summary>상점 오버레이 모드: 드래그 중 상점 위에 판매 패널을 표시/숨깁니다 (부드러운 페이드 전환).</summary>
    public void SetShopMode(bool active)
    {
        if (PanelRt == null || _inShopMode == active) return;
        _inShopMode = active;

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

        if (active)
        {
            PanelRt.anchoredPosition = _shopOverlayPos;
            PanelRt.sizeDelta        = _shopOverlaySize;
            PanelRt.gameObject.SetActive(true);
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            _fadeRoutine = StartCoroutine(FadeTo(1f, null));
        }
        else
        {
            _fadeRoutine = StartCoroutine(FadeTo(0f, () => PanelRt.gameObject.SetActive(false)));
        }
    }

    private IEnumerator FadeTo(float target, System.Action onComplete)
    {
        if (_canvasGroup == null) { onComplete?.Invoke(); yield break; }

        float start = _canvasGroup.alpha;
        float t = 0f;
        while (t < _fadeDuration)
        {
            t += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(start, target, t / _fadeDuration);
            yield return null;
        }
        _canvasGroup.alpha = target;
        onComplete?.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (PanelRt == null) return;
        PanelRt.anchoredPosition = _shopOverlayPos;
        PanelRt.sizeDelta        = _shopOverlaySize;
        if (_previewInEditor)
            PanelRt.gameObject.SetActive(true);
    }
#endif

    /// <summary>아이템을 판매하고 골드를 지급한다</summary>
    public void Sell(ItemInstance inst)
    {
        GameManager.Instance?.AddGold(ComputePrice(inst));
    }

    private static int ComputePrice(ItemInstance inst)
    {
        int rarityIdx = Mathf.Clamp((int)inst.data.rarity, 0, RarityBaseCosts.Length - 1);
        return RarityBaseCosts[rarityIdx] / 2;
    }

    // ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var panel = new GameObject("SellSlot", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_canvas.transform, false);

        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = _shopOverlayPos;
        rt.sizeDelta        = _shopOverlaySize;
        PanelRt = rt;

        _bg = panel.GetComponent<Image>();
        if (_bgSprite != null)
        {
            _bg.sprite  = _bgSprite;
            _bg.type    = Image.Type.Simple;
            _idleColor  = IdleColorTint;
            _hoverColor = HoverColorTint;
        }
        _bg.color = _idleColor;

        _canvasGroup = panel.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        var font = (Resources.Load<Font>("Fonts/Galmuri9") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"))
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

        // ── 안내 텍스트 (하단 골드 아이콘 영역 140px 만큼 여백을 남김) ──────
        var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
        hintGo.transform.SetParent(panel.transform, false);
        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 1f);
        hintRt.offsetMin = new Vector2(8f, 150f);
        hintRt.offsetMax = new Vector2(-8f, -34f);
        var hintTxt = hintGo.GetComponent<Text>();
        hintTxt.font      = font;
        hintTxt.text      = "아이템을 여기에\n올려놓으면 판매됩니다\n(구매가의 50%)";
        hintTxt.fontSize  = 11;
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.color     = new Color(0.85f, 0.65f, 0.65f);
        hintTxt.raycastTarget = false;

        // ── 동전(골드) 아이콘 placeholder ────────────────────────────
        var coinGo = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
        coinGo.transform.SetParent(panel.transform, false);
        var coinRt = coinGo.GetComponent<RectTransform>();
        coinRt.anchorMin        = new Vector2(0.5f, 0f);
        coinRt.anchorMax        = new Vector2(0.5f, 0f);
        coinRt.pivot            = new Vector2(0.5f, 0f);
        coinRt.anchoredPosition = new Vector2(0f, 24f);
        coinRt.sizeDelta        = new Vector2(56f, 56f);
        var coinImg = coinGo.GetComponent<Image>();
        coinImg.color         = new Color(0.95f, 0.75f, 0.15f, 0.95f);
        coinImg.raycastTarget = false;

        var coinLabelGo = new GameObject("CoinLabel", typeof(RectTransform), typeof(Text));
        coinLabelGo.transform.SetParent(coinGo.transform, false);
        var coinLabelRt = coinLabelGo.GetComponent<RectTransform>();
        coinLabelRt.anchorMin = Vector2.zero;
        coinLabelRt.anchorMax = Vector2.one;
        coinLabelRt.offsetMin = Vector2.zero;
        coinLabelRt.offsetMax = Vector2.zero;
        var coinLabelTxt = coinLabelGo.GetComponent<Text>();
        coinLabelTxt.font      = font;
        coinLabelTxt.text      = "G";
        coinLabelTxt.fontSize  = 24;
        coinLabelTxt.fontStyle = FontStyle.Bold;
        coinLabelTxt.alignment = TextAnchor.MiddleCenter;
        coinLabelTxt.color     = new Color(0.35f, 0.20f, 0.02f);
        coinLabelTxt.raycastTarget = false;

        // ── 실시간 판매가 텍스트 (골드 아이콘 위, 드래그 중에만 표시) ─────
        var priceGo = new GameObject("PriceText", typeof(RectTransform), typeof(Text));
        priceGo.transform.SetParent(panel.transform, false);
        var priceRt = priceGo.GetComponent<RectTransform>();
        priceRt.anchorMin        = new Vector2(0.5f, 0f);
        priceRt.anchorMax        = new Vector2(0.5f, 0f);
        priceRt.pivot            = new Vector2(0.5f, 0f);
        priceRt.anchoredPosition = new Vector2(0f, 90f);
        priceRt.sizeDelta        = new Vector2(260f, 26f);
        _priceText = priceGo.GetComponent<Text>();
        _priceText.font      = font;
        _priceText.fontSize  = 15;
        _priceText.fontStyle = FontStyle.Bold;
        _priceText.alignment = TextAnchor.MiddleCenter;
        _priceText.color     = PriceActiveColor;
        _priceText.raycastTarget = false;
        _priceText.gameObject.SetActive(false);

        // 인벤토리가 닫힌 상태에서 시작하므로 초기에는 숨김
        panel.SetActive(false);
    }
}
