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

    [Header("옵션")]
    [Tooltip("체크 시 슬롯 배경을 항상 투명하게 유지한다 (런타임 배경색 갱신 무시)")]
    [SerializeField] private bool _hideBackground;

    private static readonly Color[] RarityColors =
    {
        new Color(0.75f, 0.75f, 0.75f),  // Common
        new Color(0.30f, 0.55f, 1.00f),  // Rare
        new Color(0.65f, 0.25f, 0.95f),  // Epic
        new Color(1.00f, 0.80f, 0.10f),  // Legendary
    };

    private static readonly string[] RarityLabels = { "일반", "희귀", "영웅", "전설" };

    // 상점 슬롯 배경 기본 톤 (다크 브라운/블랙) — 레어도 색은 이 위에 약하게만 섞인다
    private static readonly Color SlotBaseColor = new Color(0.14f, 0.09f, 0.05f, 0.92f);

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
            { SynergyType.Overload,       "과부하"     },
            { SynergyType.Electro,        "일렉트로"   },
            { SynergyType.Impregnable,    "난공불락"   },
            { SynergyType.Titan,          "티탄"       },
            { SynergyType.Fairy,          "페어리"     },
        };

    private const float MiniCellGap = 1f;

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

    [Header("아이템 아이콘 박스 — 슬롯 전체를 채움 (안쪽 여백만 조정 가능)")]
    [SerializeField] private Vector2 _iconBoxPadding = Vector2.zero;

    [Header("아이콘 이미지(ShopImage) 안쪽 오프셋 — 슬롯별로 다르게 조정 가능")]
    [SerializeField] private Vector2 _shopImageOffsetMin = Vector2.zero;
    [SerializeField] private Vector2 _shopImageOffsetMax = Vector2.zero;

    [Header("시너지 2/3번째 줄 간격 (1번째 줄 SynergiesText 기준)")]
    [SerializeField] private float _synergyLabelSpacing = 18.4f;

    private RectTransform _discountIconRt;
    private RectTransform _grade2IconRt;
    private RectTransform _shopImageRt;

    private readonly System.Collections.Generic.List<Text> _extraSynergyLabels = new();

    private static readonly Color MergeHintBlue = new Color(0.35f, 0.75f, 1f);

    private Color            _rarityNameColor = Color.white;
    private InventoryGrid    _cachedGrid;
    private TempSlotUI       _cachedTempSlot;
    private InventoryGridUI  _cachedGridUI;
    private bool             _gridSubscribed;
    private bool             _tempSubscribed;
    private bool             _isSoldOut;
    private bool             _isHovered;

    // ─────────────────────────────────────────────────────────────

    public void SetItem(SO_ItemData item, Action<ItemInstance, ShopSlotUI> onBuy, int shopGrade = 1)
    {
        _item  = item;
        _onBuy = onBuy;

        if (item == null)
        {
            SetEmpty();
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

        int discountIdx = Mathf.Clamp(shopGrade - 1, 0, DiscountRates.Length - 1);
        _isDiscounted    = UnityEngine.Random.Range(0, 100) < DiscountRates[discountIdx];

        RebuildVisual();
        RefreshSynergies(item);
        EnsureSubscriptions();
        CheckMergeHighlight();
    }

    public int FinalCost => _finalCost;

    /// <summary>2등급/할인 롤 등 이미 확정된 상태를 바탕으로 아이콘·이름·가격·레어도 표시를 다시 그린다 (재롤 없음)</summary>
    private void RebuildVisual()
    {
        var item = _item;

        Color color;
        string rarityLabel;
        if (item is SO_InventoryBlockData)
        {
            color       = new Color(0.25f, 0.80f, 0.35f);
            rarityLabel = "확장";
        }
        else
        {
            var rarityIdx = Mathf.Clamp((int)item.rarity, 0, RarityColors.Length - 1);
            color         = RarityColors[rarityIdx];
            rarityLabel   = RarityLabels[rarityIdx];
        }

        _nameText.text = item is SO_InventoryBlockData ? "인벤토리" : item.itemName;

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
        _finalCost        = _isDiscounted ? Mathf.Max(1, Mathf.FloorToInt(baseCost * 0.5f)) : baseCost;
        _costText.text    = $"{_finalCost}G";
        _rarityText.text  = rarityLabel;
        _rarityText.color = color;

        // 아이템명도 등급 색상으로 노출 (합성 힌트 파란색이 우선)
        _rarityNameColor  = color;
        _nameText.color   = color;

        // 다크 브라운/블랙 톤 배경 + 레어도 색은 약하게만 섞어서 통일된 상점 테마를 유지
        if (_slotBackground != null)
            _slotBackground.color = _hideBackground ? Color.clear : Color.Lerp(SlotBaseColor, color, 0.15f);

        BuildMiniPreview(color);
    }

    /// <summary>구매되어 자리가 비거나, 처음부터 진열된 아이템이 없는 빈 슬롯을 노출한다 (칸 자체는 숨기지 않음)</summary>
    private void SetEmpty()
    {
        gameObject.SetActive(true);
        _isSoldOut = true;
        ApplyEmptySlotVisual();
    }

    private void ApplyEmptySlotVisual()
    {
        ClearPreview();

        foreach (var l in _extraSynergyLabels)
            if (l != null) Destroy(l.gameObject);
        _extraSynergyLabels.Clear();

        if (_nameText != null)     _nameText.text     = string.Empty;
        if (_costText != null)     _costText.text     = string.Empty;
        if (_rarityText != null)   _rarityText.text   = string.Empty;
        if (_synergiesText != null) _synergiesText.text = string.Empty;

        if (_slotBackground != null)
            _slotBackground.color = _hideBackground
                ? Color.clear
                : new Color(SlotBaseColor.r * 0.5f, SlotBaseColor.g * 0.5f, SlotBaseColor.b * 0.5f, 0.55f);
    }

    public void SetSoldOut()
    {
        _isSoldOut = true;
        ApplyEmptySlotVisual();
    }

    public void RestoreFromSoldOut()
    {
        _isSoldOut = false;
        if (_item == null) return;

        RebuildVisual();
        RefreshSynergies(_item);
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>미리보기 컨테이너의 자식(아이콘/미니셀/버튼)을 즉시 제거한다</summary>
    private void ClearPreview()
    {
        // Destroy는 다음 프레임 실행이므로 DestroyImmediate로 즉시 제거 — 리롤 시 Button 중복 방지
        var children = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in _previewContainer)
            children.Add(child.gameObject);
        foreach (var c in children)
            DestroyImmediate(c);

        _shopImageButton = null;
        _shopImageRt     = null;
    }

    /// <summary>슬롯 전체(부모 Rect 전체)를 채운다 — 아이콘 스프라이트가 있으면 그 이미지 하나로,
    /// 없으면 아이템 모양(격자)으로 대체한다. 둘을 겹쳐 그리지 않는다.
    /// ShopImage 크기 == Slot 크기가 되어야 하므로, previewContainer 자체를 부모에 꽉 채운다.</summary>
    private void BuildMiniPreview(Color color)
    {
        ClearPreview();

        // 아이콘/격자가 배경으로 깔리고 이름·가격·시너지 텍스트가 그 위에 보이도록 항상 맨 뒤로 보낸다
        _previewContainer.SetAsFirstSibling();
        _previewContainer.anchorMin = Vector2.zero;
        _previewContainer.anchorMax = Vector2.one;
        _previewContainer.offsetMin = _iconBoxPadding;
        _previewContainer.offsetMax = -_iconBoxPadding;

        Sprite shopSprite = null;
        if (!string.IsNullOrEmpty(_item.itemID))
            shopSprite = Resources.Load<Sprite>("ShopItems/" + _item.itemID + "_Shop");

        if (shopSprite != null)
        {
            var go = new GameObject("ShopImage", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_previewContainer, false);

            _shopImageRt = go.GetComponent<RectTransform>();
            _shopImageRt.anchorMin = Vector2.zero;
            _shopImageRt.anchorMax = Vector2.one;
            _shopImageRt.offsetMin = _shopImageOffsetMin;
            _shopImageRt.offsetMax = _shopImageOffsetMax;

            var img = go.GetComponent<Image>();
            img.sprite         = shopSprite;
            img.preserveAspect = true;

            _shopImageButton = go.GetComponent<Button>();
            _shopImageButton.onClick.AddListener(OnBuyClicked);
        }
        else
        {
            var cells = (_item.cells != null && _item.cells.Length > 0)
                ? _item.cells
                : new[] { Vector2Int.zero };

            int minRow = int.MaxValue, maxRow = int.MinValue, minCol = int.MaxValue, maxCol = int.MinValue;
            foreach (var c in cells)
            {
                minRow = Mathf.Min(minRow, c.x); maxRow = Mathf.Max(maxRow, c.x);
                minCol = Mathf.Min(minCol, c.y); maxCol = Mathf.Max(maxCol, c.y);
            }
            int cols = maxCol - minCol + 1;
            int rows = maxRow - minRow + 1;

            var containerSize = _previewContainer.rect.size;
            float cellW = containerSize.x / cols;
            float cellH = containerSize.y / rows;

            foreach (var cell in cells)
                CreateMiniCell(new Vector2Int(cell.x - minRow, cell.y - minCol), cellW, cellH, color);
        }

        if (_displayGradeIndex > 0)
            AddGrade2Icon();

        if (_isDiscounted)
            AddDiscountIcon();
    }

    private void CreateMiniCell(Vector2Int cell, float cellW, float cellH, Color color)
    {
        var go = new GameObject("mc", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_previewContainer, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(Mathf.Max(1f, cellW - MiniCellGap), Mathf.Max(1f, cellH - MiniCellGap));
        rt.anchoredPosition = new Vector2(cell.y * cellW, -cell.x * cellH);

        go.GetComponent<Image>().color = color;
    }

    private void AddGrade2Icon()
    {
        var sprite = Resources.Load<Sprite>("Icons/icon_grade2");
        if (sprite == null) return;

        var go = new GameObject("grade2_icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_previewContainer, false);

        // 할인(1/2) 아이콘과 동일한 좌하단 기준으로 배치해 두 아이콘의 위치 좌표계를 통일
        _grade2IconRt               = go.GetComponent<RectTransform>();
        _grade2IconRt.anchorMin     = new Vector2(0f, 0f);
        _grade2IconRt.anchorMax     = new Vector2(0f, 0f);
        _grade2IconRt.pivot         = new Vector2(0f, 0f);
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
        if (_previewContainer != null)
        {
            _previewContainer.anchorMin = Vector2.zero;
            _previewContainer.anchorMax = Vector2.one;
            _previewContainer.offsetMin = _iconBoxPadding;
            _previewContainer.offsetMax = -_iconBoxPadding;
        }
        if (_shopImageRt != null)
        {
            _shopImageRt.offsetMin = _shopImageOffsetMin;
            _shopImageRt.offsetMax = _shopImageOffsetMax;
        }
    }

    /// <summary>시너지 슬롯 1/2/3을 항상 표시한다 — 값이 없으면 번호만 남기고 빈 칸으로 둔다.</summary>
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

        var srcRt = _synergiesText.rectTransform;

        for (int i = 0; i < 3; i++)
        {
            string label = i < valid.Count
                ? (SynergyNames.TryGetValue(valid[i], out var n) ? n : valid[i].ToString())
                : string.Empty;
            string slotText = string.IsNullOrEmpty(label) ? $"{i + 1}" : $"{i + 1}. {label}";

            if (i == 0)
                _synergiesText.text = slotText;
            else
                _extraSynergyLabels.Add(CreateSynergyLabel(i, slotText, srcRt, _synergyLabelSpacing));
        }
    }

    private static Text CreateSynergyLabel(int index, string text, RectTransform template, float spacing)
    {
        var go = new GameObject($"SynergyLabel_{index}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(template.parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = template.anchorMin;
        rt.anchorMax        = template.anchorMax;
        rt.pivot            = template.pivot;
        rt.sizeDelta        = template.sizeDelta;
        rt.anchoredPosition = template.anchoredPosition + new Vector2(0f, -index * spacing);

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
        if (!hasGrades) { _nameText.color = _rarityNameColor; return; }

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

        _nameText.color = found ? MergeHintBlue : _rarityNameColor;
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
        if (_item == null || _isSoldOut) return;
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
