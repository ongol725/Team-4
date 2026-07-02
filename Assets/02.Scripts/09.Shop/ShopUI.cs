using System.Collections;
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
    [SerializeField] private int    _rerollCost = 20;

    [Header("상점 등급 UI")]
    [SerializeField] private Text _gradeText;

    [Header("리롤 진행 게이지 (다음 등급까지 잔여 횟수)")]
    [SerializeField] private Vector2 _gaugeOffset = new Vector2(0f, -30f);
    [SerializeField] private Vector2 _gaugeSize   = new Vector2(150f, 14f);
    [SerializeField] private Vector2 _gaugeTextOffsetMin = new Vector2(71.9f, 31.8f);
    [SerializeField] private Vector2 _gaugeTextOffsetMax = new Vector2(71.9f, 31.8f);

    [Header("확률표 (진열대 하단 고정)")]
    [SerializeField] private Vector2 _statsPanelOffset = new Vector2(0f, 8f);
    [SerializeField] private float   _statsPanelHeight = 42f;
    [SerializeField] private Vector2 _statsTextOffsetMin = new Vector2(93f, -196f);
    [SerializeField] private Vector2 _statsTextOffsetMax = new Vector2(77f, -200f);

    [Header("패널 루트")]
    [SerializeField] private GameObject _panelRoot;

    [Header("전투 데이터 전달")]
    [SerializeField] private BattleLoadoutBuilder _loadoutBuilder;

    // 각 등급에서 다음 등급으로 오르는 데 필요한 리롤 횟수 (인덱스 0 = 1등급 → 2등급)
    private static readonly int[] GradeThresholds = { 10, 15, 20, 25, 30, 35 };

    // ShopSlotUI · ShopManager 와 동일한 값 — 스탯 박스 표시용
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

    private bool   _inSellMode;
    private bool[] _slotWasActive;

    private Text   _statsText;
    private Canvas _rootCanvas;

    private RectTransform _gaugeFillRt;
    private Text          _gaugeText;

    private Image     _statsPanelBg;
    private Coroutine  _statsFlashRoutine;
    private static readonly Color StatsPanelBaseColor  = new Color(0.08f, 0.08f, 0.12f, 0.92f);
    private static readonly Color StatsPanelFlashColor = new Color(1.00f, 0.85f, 0.30f, 0.92f);

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _rootCanvas = GetComponentInParent<Canvas>();

        _rerollButton.onClick.AddListener(Reroll);
        if (_rerollCostText != null)
            _rerollCostText.text = $"리롤 ({_rerollCost}G)";

        if (_gradeText != null)
        {
            CreateStatsPanel();
            CreateRerollGaugePanel();
        }

        ArrangeSlotsVertically();
        PopulateSlots();
        UpdateGradeUI();
        UpdateRerollButtonState();
    }

    /// <summary>Slot_0의 위치·스케일을 기준으로, 각 슬롯의 실제(스케일 반영) 높이를 재서
    /// 다음 슬롯을 간격 0으로 바로 아래에 배치한다. VerticalLayoutGroup 없이도 겹치거나
    /// 빈틈이 생기지 않는다 — 아이콘 박스 크기를 나중에 또 바꿔도 자동으로 따라온다.</summary>
    private void ArrangeSlotsVertically()
    {
        if (_slots == null || _slots.Length == 0) return;

        var baseRt = _slots[0].GetComponent<RectTransform>();

        for (int i = 1; i < _slots.Length; i++)
        {
            var prevRt = _slots[i - 1].GetComponent<RectTransform>();
            var rt     = _slots[i].GetComponent<RectTransform>();

            rt.localScale = baseRt.localScale;

            float prevHalfHeight = prevRt.rect.height * prevRt.localScale.y * 0.5f;
            float curHalfHeight  = rt.rect.height      * rt.localScale.y    * 0.5f;

            rt.anchoredPosition = new Vector2(
                prevRt.anchoredPosition.x,
                prevRt.anchoredPosition.y - prevHalfHeight - curHalfHeight);
        }
    }

    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_rootCanvas != null && _rootCanvas.enabled
         && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            Reroll();

        UpdateRerollButtonState();

        if (_panelRoot == null || !_panelRoot.activeSelf) return;
        bool shouldSell = _inventoryGridUI != null
            && _inventoryGridUI.IsAnyFollowingMouse
            && IsMouseOverPanel();
        if (shouldSell != _inSellMode)
            SetSellMode(shouldSell);
    }

    private bool IsMouseOverPanel()
    {
        if (Mouse.current == null) return false;
        var rt  = _panelRoot.GetComponent<RectTransform>();
        var cam = (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _rootCanvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(
            rt, Mouse.current.position.ReadValue(), cam);
    }

    private void SetSellMode(bool active)
    {
        _inSellMode = active;
        if (active)
        {
            _slotWasActive = new bool[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                _slotWasActive[i] = _slots[i].gameObject.activeSelf;
                _slots[i].gameObject.SetActive(false);
            }
        }
        else if (_slotWasActive != null)
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i].gameObject.SetActive(_slotWasActive[i]);
            _slotWasActive = null;
        }
        SellSlotUI.Instance?.SetShopMode(active);
    }

    public void Reroll()
    {
        if (GameManager.Instance != null && !GameManager.Instance.SpendGold(_rerollCost))
            return; // 골드 부족 — 리롤 취소
        _rerollsInCurrentGrade++;
        RunStatsLogger.Instance?.Reroll();
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
        SetSellMode(false);
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
        bool leveledUp = thresholdIdx < GradeThresholds.Length
                       && _rerollsInCurrentGrade >= GradeThresholds[thresholdIdx];

        if (!leveledUp)
        {
            UpdateRerollGauge(); // 레벨업 전이라도 잔여 횟수 게이지는 갱신
            return;
        }

        _shopGrade++;
        _rerollsInCurrentGrade = 0;
        UpdateGradeUI();
        FlashStatsPanel();
    }

    private void UpdateGradeUI()
    {
        if (_gradeText != null)
            _gradeText.text = $"상점 Lv.{_shopGrade}";

        UpdateStatsPanel();
        UpdateRerollGauge();
    }

    /// <summary>소지 골드가 리롤 비용보다 적으면 버튼을 비활성화(Dimmed)한다.</summary>
    private void UpdateRerollButtonState()
    {
        if (_rerollButton == null) return;
        bool canAfford = GameManager.Instance != null && GameManager.Instance.gold >= _rerollCost;
        if (_rerollButton.interactable != canAfford)
            _rerollButton.interactable = canAfford;
    }

    // ─────────────────────────────────────────────────────────────
    // 리롤 진행 게이지 (다음 등급까지 잔여 횟수)

    private void CreateRerollGaugePanel()
    {
        var gradeRt = _gradeText.GetComponent<RectTransform>();

        var bgGo = new GameObject("RerollGaugeBg", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(_gradeText.transform.parent, false);

        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin        = gradeRt.anchorMin;
        bgRt.anchorMax        = gradeRt.anchorMax;
        bgRt.pivot            = new Vector2(0f, 1f);
        bgRt.anchoredPosition = gradeRt.anchoredPosition + _gaugeOffset;
        bgRt.sizeDelta        = _gaugeSize;

        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color         = new Color(0.05f, 0.05f, 0.05f, 0.85f);
        bgImg.raycastTarget = false;

        var fillGo = new GameObject("RerollGaugeFill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(bgGo.transform, false);

        _gaugeFillRt               = fillGo.GetComponent<RectTransform>();
        _gaugeFillRt.anchorMin     = new Vector2(0f, 0f);
        _gaugeFillRt.anchorMax     = new Vector2(0f, 1f);
        _gaugeFillRt.pivot         = new Vector2(0f, 0.5f);
        _gaugeFillRt.anchoredPosition = Vector2.zero;
        _gaugeFillRt.sizeDelta     = Vector2.zero;

        var fillImg = fillGo.GetComponent<Image>();
        fillImg.color         = new Color(0.95f, 0.65f, 0.15f, 0.95f);
        fillImg.raycastTarget = false;

        var textGo = new GameObject("RerollGaugeText", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(bgGo.transform, false);

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = _gaugeTextOffsetMin;
        textRt.offsetMax = _gaugeTextOffsetMax;

        _gaugeText = textGo.GetComponent<Text>();
        _gaugeText.font              = (Resources.Load<Font>("Fonts/Galmuri9") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"))
                                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        _gaugeText.fontSize          = 10;
        _gaugeText.alignment         = TextAnchor.MiddleCenter;
        _gaugeText.color             = Color.white;
        _gaugeText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _gaugeText.verticalOverflow   = VerticalWrapMode.Truncate;
        _gaugeText.raycastTarget     = false;
    }

    private void UpdateRerollGauge()
    {
        if (_gaugeFillRt == null) return;

        int  thresholdIdx = _shopGrade - 1;
        bool isMaxGrade   = thresholdIdx >= GradeThresholds.Length;
        int  threshold    = isMaxGrade ? 1 : GradeThresholds[thresholdIdx];
        float progress    = isMaxGrade ? 1f : Mathf.Clamp01((float)_rerollsInCurrentGrade / threshold);

        _gaugeFillRt.sizeDelta = new Vector2(_gaugeSize.x * progress, 0f);

        if (_gaugeText != null)
            _gaugeText.text = isMaxGrade ? "MAX" : $"{_rerollsInCurrentGrade} / {threshold}";
    }

    // ─────────────────────────────────────────────────────────────
    // 확률표 (런타임, 진열대 하단에 고정 배치 — 더 이상 드래그되지 않음)

    private void CreateStatsPanel()
    {
        var parent = _panelRoot != null ? _panelRoot.transform : _gradeText.transform.parent;

        var boxGo = new GameObject("ShopStatsPanel", typeof(RectTransform), typeof(Image));
        boxGo.transform.SetParent(parent, false);

        var boxRt = boxGo.GetComponent<RectTransform>();
        boxRt.anchorMin        = new Vector2(0f, 0f);
        boxRt.anchorMax        = new Vector2(1f, 0f);
        boxRt.pivot            = new Vector2(0.5f, 0f);
        boxRt.anchoredPosition = _statsPanelOffset;
        boxRt.sizeDelta        = new Vector2(0f, _statsPanelHeight);

        _statsPanelBg               = boxGo.GetComponent<Image>();
        _statsPanelBg.color         = StatsPanelBaseColor;
        _statsPanelBg.raycastTarget = false;

        var textGo = new GameObject("StatsText", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(boxGo.transform, false);

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = _statsTextOffsetMin;
        textRt.offsetMax = _statsTextOffsetMax;

        _statsText = textGo.GetComponent<Text>();
        _statsText.font               = (Resources.Load<Font>("Fonts/Galmuri9") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"))
                                   ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        _statsText.fontSize           = 10;
        _statsText.color              = new Color(0.85f, 0.85f, 0.85f);
        _statsText.lineSpacing        = 1.3f;
        _statsText.alignment          = TextAnchor.MiddleCenter;
        _statsText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _statsText.verticalOverflow   = VerticalWrapMode.Overflow;
        _statsText.raycastTarget      = false;
    }

    /// <summary>상점 레벨업 시 확률표 배경을 잠깐 반짝여 확률이 갱신되었음을 알린다</summary>
    private void FlashStatsPanel()
    {
        if (_statsPanelBg == null) return;
        if (_statsFlashRoutine != null) StopCoroutine(_statsFlashRoutine);
        _statsFlashRoutine = StartCoroutine(FlashStatsPanelRoutine());
    }

    private IEnumerator FlashStatsPanelRoutine()
    {
        const float duration = 0.35f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            _statsPanelBg.color = Color.Lerp(StatsPanelFlashColor, StatsPanelBaseColor, t / duration);
            yield return null;
        }
        _statsPanelBg.color = StatsPanelBaseColor;
    }

    private void UpdateStatsPanel()
    {
        if (_statsText == null) return;

        int idx      = Mathf.Clamp(_shopGrade - 1, 0, 6);
        int normal   = RarityWeights[idx, 0];
        int rare     = RarityWeights[idx, 1];
        int epic     = RarityWeights[idx, 2];
        int legend   = RarityWeights[idx, 3];

        _statsText.text = $"일반 {normal}%  희귀 {rare}%  영웅 {epic}%  전설 {legend}%";
    }

    // ─────────────────────────────────────────────────────────────

    private void OnItemBought(ItemInstance inst, ShopSlotUI slot)
    {
        slot.SetSoldOut();
        _inventoryGridUI.BeginPlaceFromShop(inst, slot, slot.FinalCost);
    }
}
