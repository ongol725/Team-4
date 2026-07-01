using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 상점 슬롯 하나를 담당: 아이템 미리보기, 이름, 코스트, 구매 버튼
/// </summary>
public class ShopSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 참조")]
    [SerializeField] private Text           _nameText;
    [SerializeField] private Text           _costText;
    [SerializeField] private Text           _rarityText;
    [SerializeField] private Text           _synergiesText;
    [SerializeField] private RectTransform  _previewContainer;
    [SerializeField] private Image          _slotBackground;

    private static readonly Color[] RarityColors =
    {
        new Color(0.75f, 0.75f, 0.75f),  // Common
        new Color(0.30f, 0.55f, 1.00f),  // Rare
        new Color(0.65f, 0.25f, 0.95f),  // Epic
        new Color(1.00f, 0.80f, 0.10f),  // Legendary
    };

    private static readonly string[] RarityLabels = { "일반", "희귀", "영웅", "전설" };

    // 희귀도(Common/Rare/Epic/Legendary)별 기본 구매가
    private static readonly int[] RarityBaseCosts = { 100, 200, 300, 400 };

    // 인벤토리 확장 블록(바닥) 칸 수별 정가
    private static int InventoryBlockCost(int cells)
    {
        switch (Mathf.Clamp(cells, 1, 4))
        {
            case 1:  return 75;
            case 2:  return 120;
            case 3:  return 150;
            default: return 200; // 4칸 이상
        }
    }

    // 상점 등급(1~7)별 2등급 아이템 등장 확률(%)
    private static readonly int[] Grade2Rates    = { 8, 10, 12, 14, 16, 18, 20 };

    // 상점 등급(1~7)별 할인율(%)
    private static readonly int[] DiscountRates  = { 12, 15, 18, 21, 24, 27, 30 };

    private static readonly System.Collections.Generic.Dictionary<SynergyType, string> SynergyNames =
        new System.Collections.Generic.Dictionary<SynergyType, string>
        {
            { SynergyType.Assassin,       "암살단"     },
            { SynergyType.SwordMaster,    "소드마스터" },
            { SynergyType.HolyKnight,     "성기사단"   },
            { SynergyType.DemonLord,      "마왕"       },
            { SynergyType.BloodBerserker, "피의광전사" },
            { SynergyType.Tycoon,         "대부호"     },
            { SynergyType.Executioner,    "처형자"     },
            { SynergyType.SpiritMage,     "정령술사"   },
            { SynergyType.GearShift,      "기어시프트" },
            { SynergyType.Pinball,        "핀볼"       },
            { SynergyType.Overload,       "과부화"     },
            { SynergyType.Electro,        "일렉트로"   },
            { SynergyType.Impregnable,    "난공불락"   },
        };

    private const float MiniCellSize = 11f;
    private const float MiniCellGap  = 1f;

    private SO_ItemData                      _item;
    private int                              _displayGradeIndex;
    private bool                             _isDiscounted;
    private int                              _finalCost;
    private Action<ItemInstance, ShopSlotUI> _onBuy;
    private Button                           _shopImageButton;

    [Header("아이콘 위치/크기 (인스펙터에서 실시간 조정)")]
    [SerializeField] private Vector2 _discountIconOffset = new Vector2(0f, 95f);
    [SerializeField] private Vector2 _discountIconSize   = new Vector2(44f, 20f);
    [SerializeField] private Vector2 _grade2IconOffset   = new Vector2(-243f, -12f);
    [SerializeField] private Vector2 _grade2IconSize     = new Vector2(25f, 20f);

    private RectTransform _discountIconRt;
    private RectTransform _grade2IconRt;

    private Vector2 _origAnchorMin, _origAnchorMax, _origOffsetMin, _origOffsetMax;
    private int     _origSiblingIndex;
    private Image   _colorBar;

    private readonly System.Collections.Generic.List<Text> _extraSynergyLabels = new();

    private static readonly Color MergeHintBlue = new Color(0.35f, 0.75f, 1f);

    private Color            _defaultNameColor;
    private InventoryGrid    _cachedGrid;
    private TempSlotUI       _cachedTempSlot;
    private InventoryGridUI  _cachedGridUI;
    private bool             _gridSubscribed;
    private bool             _tempSubscribed;
    private bool             _isSoldOut;
    private bool             _isHovered;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _defaultNameColor = _nameText != null ? _nameText.color : Color.white;
        _origAnchorMin    = _previewContainer.anchorMin;
        _origAnchorMax    = _previewContainer.anchorMax;
        _origOffsetMin    = _previewContainer.offsetMin;
        _origOffsetMax    = _previewContainer.offsetMax;
        _origSiblingIndex = _previewContainer.GetSiblingIndex();

        var colorBarTr = transform.Find("ColorBar");
        if (colorBarTr != null)
            _colorBar = colorBarTr.GetComponent<Image>();
    }

    // ─────────────────────────────────────────────────────────────

    public void SetItem(SO_ItemData item, Action<ItemInstance, ShopSlotUI> onBuy, int shopGrade = 1)
    {
        _item  = item;
        _onBuy = onBuy;

        if (item == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        _isSoldOut = false;

        // 2등급 롤: 등급 있는 아이템(무기/방어구)에만 적용
        _displayGradeIndex = 0;
        bool hasGrades = item is SO_WeaponData || item is SO_ArmorData;
        if (hasGrades)
        {
            int rateIdx = Mathf.Clamp(shopGrade - 1, 0, Grade2Rates.Length - 1);
            if (UnityEngine.Random.Range(0, 100) < Grade2Rates[rateIdx])
                _displayGradeIndex = 1;
        }

        // 색상 결정
        Color color;
        string rarityLabel;
        if (item is SO_InventoryBlockData)
        {
            color      = new Color(0.25f, 0.80f, 0.35f);
            rarityLabel = "확장";
        }
        else
        {
            var rarityIdx = Mathf.Clamp((int)item.rarity, 0, RarityColors.Length - 1);
            color         = RarityColors[rarityIdx];
            rarityLabel   = RarityLabels[rarityIdx];
        }

        _nameText.text    = item is SO_InventoryBlockData ? "인벤토리" : item.itemName;
        int discountIdx  = Mathf.Clamp(shopGrade - 1, 0, DiscountRates.Length - 1);
        _isDiscounted    = UnityEngine.Random.Range(0, 100) < DiscountRates[discountIdx];
        int baseCost;
        if (item is SO_InventoryBlockData)
        {
            // 인벤토리 확장 블록(바닥): 차지 칸 수로 정가 책정 (희귀도/등급 배수 미적용)
            int cellCount = (item.cells != null && item.cells.Length > 0) ? item.cells.Length : 1;
            baseCost = InventoryBlockCost(cellCount);
        }
        else
        {
            int priceIdx = Mathf.Clamp((int)item.rarity, 0, RarityBaseCosts.Length - 1);
            baseCost     = RarityBaseCosts[priceIdx] * (_displayGradeIndex > 0 ? 2 : 1);
        }
        _finalCost       = _isDiscounted ? Mathf.Max(1, Mathf.FloorToInt(baseCost * 0.5f)) : baseCost;
        _costText.text   = $"{_finalCost} G";
        _rarityText.text  = rarityLabel;
        _rarityText.color = color;

        RefreshSynergies(item);
        EnsureSubscriptions();
        CheckMergeHighlight();

        if (_slotBackground != null)
            _slotBackground.color = new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.85f);

        if (_colorBar != null)
            _colorBar.color = color;

        BuildMiniPreview(color);
    }

    public int FinalCost => _finalCost;

    public void SetSoldOut()
    {
        _isSoldOut = true;
        if (_shopImageButton != null)
            _shopImageButton.interactable = false;
        if (_nameText != null)
            _nameText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
    }

    public void RestoreFromSoldOut()
    {
        _isSoldOut = false;
        if (_shopImageButton != null)
            _shopImageButton.interactable = true;
        if (_nameText != null)
            _nameText.color = _defaultNameColor;
    }

    // ─────────────────────────────────────────────────────────────

    private void BuildMiniPreview(Color color)
    {
        // Destroy는 다음 프레임 실행이므로 DestroyImmediate로 즉시 제거 — 리롤 시 Button 중복 방지
        var children = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in _previewContainer)
            children.Add(child.gameObject);
        foreach (var c in children)
            DestroyImmediate(c);

        _shopImageButton = null;

        Sprite shopSprite = null;
        if (!string.IsNullOrEmpty(_item.itemID))
            shopSprite = Resources.Load<Sprite>("ShopItems/" + _item.itemID + "_Shop");

        if (shopSprite != null)
        {
            _previewContainer.SetAsFirstSibling();
            _previewContainer.anchorMin = Vector2.zero;
            _previewContainer.anchorMax = Vector2.one;
            _previewContainer.offsetMin = Vector2.zero;
            _previewContainer.offsetMax = Vector2.zero;

            var go = new GameObject("ShopImage", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_previewContainer, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.sprite         = shopSprite;
            img.preserveAspect = true;

            _shopImageButton = go.GetComponent<Button>();
            _shopImageButton.onClick.AddListener(OnBuyClicked);
        }
        else
        {
            _previewContainer.SetSiblingIndex(_origSiblingIndex);
            _previewContainer.anchorMin = _origAnchorMin;
            _previewContainer.anchorMax = _origAnchorMax;
            _previewContainer.offsetMin = _origOffsetMin;
            _previewContainer.offsetMax = _origOffsetMax;

            var cells = (_item.cells != null && _item.cells.Length > 0)
                ? _item.cells
                : new[] { Vector2Int.zero };

            foreach (var cell in cells)
                CreateMiniCell(cell, color);
        }

        if (_displayGradeIndex > 0)
            AddGrade2Icon();

        if (_isDiscounted)
            AddDiscountIcon();
    }

    private void CreateMiniCell(Vector2Int cell, Color color)
    {
        var go = new GameObject("mc", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_previewContainer, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(MiniCellSize, MiniCellSize);
        rt.anchoredPosition = new Vector2(
             cell.y * (MiniCellSize + MiniCellGap),
            -cell.x * (MiniCellSize + MiniCellGap));

        go.GetComponent<Image>().color = color;
    }

    private void AddGrade2Icon()
    {
        var sprite = Resources.Load<Sprite>("Icons/icon_grade2");
        if (sprite == null) return;

        var go = new GameObject("grade2_icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_previewContainer, false);

        _grade2IconRt               = go.GetComponent<RectTransform>();
        _grade2IconRt.anchorMin     = new Vector2(1f, 1f);
        _grade2IconRt.anchorMax     = new Vector2(1f, 1f);
        _grade2IconRt.pivot         = new Vector2(1f, 1f);
        _grade2IconRt.anchoredPosition = _grade2IconOffset;
        _grade2IconRt.sizeDelta     = _grade2IconSize;

        var img = go.GetComponent<Image>();
        img.sprite         = sprite;
        img.preserveAspect = true;
        img.raycastTarget  = false;
    }

    private void AddDiscountIcon()
    {
        var sprite = Resources.Load<Sprite>("Icons/icon_discount");
        if (sprite == null) return;

        var go = new GameObject("discount_icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_previewContainer, false);

        _discountIconRt               = go.GetComponent<RectTransform>();
        _discountIconRt.anchorMin     = new Vector2(0f, 0f);
        _discountIconRt.anchorMax     = new Vector2(0f, 0f);
        _discountIconRt.pivot         = new Vector2(0f, 0f);
        _discountIconRt.anchoredPosition = _discountIconOffset;
        _discountIconRt.sizeDelta     = _discountIconSize;

        var img = go.GetComponent<Image>();
        img.sprite         = sprite;
        img.preserveAspect = true;
        img.raycastTarget  = false;
    }

    private void OnValidate()
    {
        if (_discountIconRt != null)
        {
            _discountIconRt.anchoredPosition = _discountIconOffset;
            _discountIconRt.sizeDelta        = _discountIconSize;
        }
        if (_grade2IconRt != null)
        {
            _grade2IconRt.anchoredPosition = _grade2IconOffset;
            _grade2IconRt.sizeDelta        = _grade2IconSize;
        }
    }

    private void RefreshSynergies(SO_ItemData item)
    {
        foreach (var l in _extraSynergyLabels)
            if (l != null) Destroy(l.gameObject);
        _extraSynergyLabels.Clear();

        if (_synergiesText == null) return;

        var valid = new System.Collections.Generic.List<SynergyType>();
        if (item.synergies != null)
            foreach (var s in item.synergies)
                if (s != SynergyType.None) valid.Add(s);

        if (valid.Count == 0)
        {
            _synergiesText.text = string.Empty;
            return;
        }

        _synergiesText.text = SynergyNames.TryGetValue(valid[0], out var n0) ? n0 : valid[0].ToString();

        var srcRt = _synergiesText.rectTransform;
        for (int i = 1; i < Mathf.Min(valid.Count, 3); i++)
        {
            string label = SynergyNames.TryGetValue(valid[i], out var n) ? n : valid[i].ToString();
            _extraSynergyLabels.Add(CreateSynergyLabel(i, label, srcRt));
        }
    }

    private static Text CreateSynergyLabel(int index, string text, RectTransform template)
    {
        var go = new GameObject($"SynergyLabel_{index}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(template.parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = template.anchorMin;
        rt.anchorMax        = template.anchorMax;
        rt.pivot            = template.pivot;
        rt.sizeDelta        = template.sizeDelta;
        rt.anchoredPosition = template.anchoredPosition + new Vector2(0f, -index * 24f);

        var src = template.GetComponent<Text>();
        var txt = go.GetComponent<Text>();
        txt.font               = src.font;
        txt.fontSize           = src.fontSize;
        txt.fontStyle          = src.fontStyle;
        txt.color              = src.color;
        txt.alignment          = src.alignment;
        txt.horizontalOverflow = src.horizontalOverflow;
        txt.verticalOverflow   = src.verticalOverflow;
        txt.raycastTarget      = false;
        txt.text               = text;

        return txt;
    }

    // ─────────────────────────────────────────────────────────────
    // 합성 가능 힌트

    /// <summary>이벤트 구독 - Grid는 null이면 재시도, TempSlot은 static이라 한 번만</summary>
    private void EnsureSubscriptions()
    {
        if (!_tempSubscribed)
        {
            _tempSubscribed = true;
            TempSlotUI.onTempSlotChanged += CheckMergeHighlight;
        }

        if (!_gridSubscribed)
        {
            _cachedGrid = FindAnyObjectByType<InventoryGrid>();
            if (_cachedGrid != null)
            {
                _gridSubscribed = true;
                _cachedGrid.OnGridChanged += CheckMergeHighlight;
            }
        }

        if (_cachedTempSlot == null)
            _cachedTempSlot = FindAnyObjectByType<TempSlotUI>();
    }

    private void OnDisable()
    {
        // 슬롯이 비활성화될 때 OnPointerExit이 보장되지 않으므로 수동 초기화
        _isHovered = false;
        ItemInfoPopup.Hide();
    }

    private void OnDestroy()
    {
        if (_cachedGrid != null)
            _cachedGrid.OnGridChanged -= CheckMergeHighlight;
        TempSlotUI.onTempSlotChanged -= CheckMergeHighlight;
    }

    private void CheckMergeHighlight()
    {
        if (_nameText == null || _item == null) return;
        if (_isSoldOut) { _nameText.color = new Color(0.5f, 0.5f, 0.5f, 1f); return; }

        // 등급 없는 아이템(악세서리, 인벤 확장 블록)은 합성 불가 → 힌트 없음
        bool hasGrades = _item is SO_WeaponData || _item is SO_ArmorData;
        if (!hasGrades) { _nameText.color = _defaultNameColor; return; }

        // null이면 재탐색 (씬 로드 타이밍 방어)
        if (_cachedGrid == null)
        {
            _cachedGrid = FindAnyObjectByType<InventoryGrid>();
            if (_cachedGrid != null && !_gridSubscribed)
            {
                _gridSubscribed = true;
                _cachedGrid.OnGridChanged += CheckMergeHighlight;
            }
        }
        if (_cachedTempSlot == null)
            _cachedTempSlot = FindAnyObjectByType<TempSlotUI>();

        bool found = false;

        // 인벤토리 그리드 탐색
        if (_cachedGrid != null)
            foreach (var inst in _cachedGrid.GetAllPlacedInstances())
                if (inst.data == _item && inst.gradeIndex == _displayGradeIndex)
                { found = true; break; }

        // 임시칸 탐색
        if (!found && _cachedTempSlot != null)
            foreach (var block in _cachedTempSlot.HeldBlocks)
                if (block?.Instance?.data == _item && block.Instance.gradeIndex == _displayGradeIndex)
                { found = true; break; }

        _nameText.color = found ? MergeHintBlue : _defaultNameColor;
    }

    // ─────────────────────────────────────────────────────────────

    private void OnBuyClicked()
    {
        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(_finalCost))
            return; // 골드 부족 — 구매 취소
        _onBuy?.Invoke(new ItemInstance { data = _item, gradeIndex = _displayGradeIndex }, this);
    }

    // ─────────────────────────────────────────────────────────────
    // 아이템 정보 팝업

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        if (_item == null) return;
        ItemInfoPopup.Show(_item, _displayGradeIndex, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        ItemInfoPopup.Hide();
    }

    // ─────────────────────────────────────────────────────────────
    // 우클릭 스마트 구매

    private void Update()
    {
        if (!_isHovered || !Input.GetMouseButtonDown(1)) return;
        TrySmartBuy();
    }

    private void TrySmartBuy()
    {
        if (_item == null || _isSoldOut) return;
        if (_item is SO_InventoryBlockData) return;
        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(_finalCost)) return;

        if (_cachedGridUI == null) _cachedGridUI = FindAnyObjectByType<InventoryGridUI>();
        if (_cachedGridUI == null) return;

        var inst = new ItemInstance { data = _item, gradeIndex = _displayGradeIndex };
        _cachedGridUI.SmartReceiveFromShop(inst);
        SetSoldOut();
    }
}
