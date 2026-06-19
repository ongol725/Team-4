using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 상점 전체 패널: 5개 슬롯 + 리롤 버튼 + 상점 등급 관리
/// 리롤 누적 횟수에 따라 상점 등급(1~7)이 상승한다.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private ShopManager     _shopManager;
    [SerializeField] private InventoryGridUI _inventoryGridUI;

    [Header("슬롯 (5개)")]
    [SerializeField] private ShopSlotUI[] _slots;

    [Header("리롤 버튼")]
    [SerializeField] private Button _rerollButton;
    [SerializeField] private Text   _rerollCostText;
    [SerializeField] private int    _rerollCost = 2;

    [Header("상점 등급 UI")]
    [SerializeField] private Text _gradeText;

    [Header("패널 루트")]
    [SerializeField] private GameObject _panelRoot;

    [Header("전투 데이터 전달")]
    [SerializeField] private BattleLoadoutBuilder _loadoutBuilder;

    // 각 등급에서 다음 등급으로 오르는 데 필요한 리롤 횟수 (인덱스 0 = 1등급 → 2등급)
    private static readonly int[] GradeThresholds = { 10, 15, 20, 25, 30, 35 };

    // ShopSlotUI · ShopManager 와 동일한 값 — 스탯 박스 표시용
    private static readonly int[]   Grade2Rates   = { 8,  10, 12, 14, 16, 18, 20 };
    private static readonly int[]   DiscountRates  = { 12, 15, 18, 21, 24, 27, 30 };
    private static readonly int[,]  RarityWeights  =
    {
        { 90, 10,  0,  0 },
        { 80, 10, 10,  0 },
        { 65, 20, 10,  5 },
        { 55, 20, 15, 10 },
        { 40, 30, 15, 15 },
        { 30, 30, 20, 20 },
        { 20, 30, 25, 25 },
    };

    private int _shopGrade             = 1;
    private int _rerollsInCurrentGrade = 0;

    private Text   _statsText;
    private Canvas _rootCanvas;

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _rootCanvas = GetComponentInParent<Canvas>();

        _rerollButton.onClick.AddListener(Reroll);
        if (_rerollCostText != null)
            _rerollCostText.text = $"리롤 ({_rerollCost}G)";

        if (_gradeText != null)
            CreateStatsPanel();

        PopulateSlots();
        UpdateGradeUI();
    }

    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_rootCanvas != null && _rootCanvas.enabled
         && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            Reroll();
    }

    public void Reroll()
    {
        if (GameManager.Instance != null && !GameManager.Instance.SpendGold(_rerollCost))
            return; // 골드 부족 — 리롤 취소
        _rerollsInCurrentGrade++;
        UpdateGrade();
        PopulateSlots();
    }

    public void Open()
    {
        if (_panelRoot != null) _panelRoot.SetActive(true);
        GoldDisplayUI.Instance?.ShowHelp();
    }

    public void Close()
    {
        _loadoutBuilder?.BuildAndDeliver();
        if (_panelRoot != null) _panelRoot.SetActive(false);
        GoldDisplayUI.Instance?.HideHelp();
    }

    // ─────────────────────────────────────────────────────────────

    private void PopulateSlots()
    {
        var items = _shopManager.GenerateShopItems(5, _shopGrade);
        for (int i = 0; i < _slots.Length; i++)
        {
            var item = i < items.Length ? items[i] : null;
            _slots[i].SetItem(item, OnItemBought, _shopGrade);
        }
    }

    private void UpdateGrade()
    {
        int thresholdIdx = _shopGrade - 1;
        if (thresholdIdx >= GradeThresholds.Length) return;

        if (_rerollsInCurrentGrade < GradeThresholds[thresholdIdx]) return;

        _shopGrade++;
        _rerollsInCurrentGrade = 0;
        UpdateGradeUI();
    }

    private void UpdateGradeUI()
    {
        if (_gradeText != null)
            _gradeText.text = $"상점 Lv.{_shopGrade}";

        UpdateStatsPanel();
    }

    // ─────────────────────────────────────────────────────────────
    // 스탯 박스 생성 (런타임, _gradeText 바로 아래)

    private void CreateStatsPanel()
    {
        var gradeRt = _gradeText.GetComponent<RectTransform>();

        // 배경 박스 (드래그 이벤트를 받으려면 Image raycastTarget=true 필요)
        var boxGo = new GameObject("ShopStatsPanel", typeof(RectTransform), typeof(Image));
        boxGo.transform.SetParent(_gradeText.transform.parent, false);

        var boxRt = boxGo.GetComponent<RectTransform>();
        boxRt.anchorMin        = gradeRt.anchorMin;
        boxRt.anchorMax        = gradeRt.anchorMax;
        boxRt.pivot            = new Vector2(1f, 1f);
        boxRt.anchoredPosition = new Vector2(
            gradeRt.anchoredPosition.x - gradeRt.sizeDelta.x * 0.5f - 8f,
            gradeRt.anchoredPosition.y);
        boxRt.sizeDelta = new Vector2(230f, 64f);  // 핸들 영역 포함해 높이 늘림

        var bg = boxGo.GetComponent<Image>();
        bg.color         = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        bg.raycastTarget = true;  // 드래그 이벤트 수신

        // 드래그 컴포넌트
        boxGo.AddComponent<DraggableUI>();

        // ── 핸들 텍스트 (박스 상단) ──────────────────────────────────
        var handleGo = new GameObject("DragHandle", typeof(RectTransform), typeof(Text));
        handleGo.transform.SetParent(boxGo.transform, false);

        var handleRt = handleGo.GetComponent<RectTransform>();
        handleRt.anchorMin        = new Vector2(0f, 1f);
        handleRt.anchorMax        = new Vector2(1f, 1f);
        handleRt.pivot            = new Vector2(0.5f, 1f);
        handleRt.anchoredPosition = Vector2.zero;
        handleRt.sizeDelta        = new Vector2(0f, 14f);

        var handleTxt = handleGo.GetComponent<Text>();
        handleTxt.font               = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        handleTxt.text               = "≡ 상점 확률표";
        handleTxt.fontSize           = 9;
        handleTxt.color              = new Color(0.55f, 0.55f, 0.65f);
        handleTxt.alignment          = TextAnchor.MiddleCenter;
        handleTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        handleTxt.verticalOverflow   = VerticalWrapMode.Overflow;
        handleTxt.raycastTarget      = false;

        // ── 스탯 텍스트 (핸들 아래) ──────────────────────────────────
        var textGo = new GameObject("StatsText", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(boxGo.transform, false);

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.offsetMin = new Vector2(6f, 4f);
        textRt.offsetMax = new Vector2(-6f, -16f);  // 상단 핸들 높이만큼 여백

        _statsText = textGo.GetComponent<Text>();
        _statsText.font               = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                                   ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        _statsText.fontSize           = 10;
        _statsText.color              = new Color(0.85f, 0.85f, 0.85f);
        _statsText.lineSpacing        = 1.3f;
        _statsText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _statsText.verticalOverflow   = VerticalWrapMode.Overflow;
        _statsText.raycastTarget      = false;
    }

    private void UpdateStatsPanel()
    {
        if (_statsText == null) return;

        int idx      = Mathf.Clamp(_shopGrade - 1, 0, 6);
        int normal   = RarityWeights[idx, 0];
        int rare     = RarityWeights[idx, 1];
        int epic     = RarityWeights[idx, 2];
        int legend   = RarityWeights[idx, 3];

        _statsText.text =
            $"할인 확률  : {DiscountRates[idx]}%   2등급 확률 : {Grade2Rates[idx]}%\n" +
            $"일반 {normal}%  희귀 {rare}%  영웅 {epic}%  전설 {legend}%";
    }

    // ─────────────────────────────────────────────────────────────

    private void OnItemBought(ItemInstance inst, ShopSlotUI slot)
    {
        slot.SetSoldOut();
        _inventoryGridUI.BeginPlaceFromShop(inst, slot, slot.FinalCost);
    }
}
