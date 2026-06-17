using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 슬롯 하나를 담당: 아이템 미리보기, 이름, 코스트, 구매 버튼
/// </summary>
public class ShopSlotUI : MonoBehaviour
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

    private Vector2 _origAnchorMin, _origAnchorMax, _origOffsetMin, _origOffsetMax;
    private int     _origSiblingIndex;
    private Image   _colorBar;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
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
        int baseCost     = item.cost * (_displayGradeIndex > 0 ? 2 : 1);
        _finalCost       = _isDiscounted ? Mathf.Max(1, Mathf.FloorToInt(baseCost * 0.5f)) : baseCost;
        _costText.text   = $"{_finalCost} G";
        _rarityText.text  = rarityLabel;
        _rarityText.color = color;

        RefreshSynergies(item);

        if (_slotBackground != null)
            _slotBackground.color = new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.85f);

        if (_colorBar != null)
            _colorBar.color = color;

        BuildMiniPreview(color);
    }

    public void SetSoldOut()
    {
        if (_shopImageButton != null)
            _shopImageButton.interactable = false;
    }

    // ─────────────────────────────────────────────────────────────

    private void BuildMiniPreview(Color color)
    {
        foreach (Transform child in _previewContainer)
            Destroy(child.gameObject);

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
            AddGrade2Arrow();

        if (_isDiscounted)
            AddDiscountArrow();
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

    private void AddGrade2Arrow()
    {
        var go = new GameObject("grade2_arrow", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(_previewContainer, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta        = new Vector2(14f, 14f);

        var txt = go.GetComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.text      = "↑";
        txt.fontSize  = 12;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.UpperRight;
        txt.color     = new Color(0.25f, 0.90f, 0.35f);
        txt.raycastTarget = false;
    }

    private void AddDiscountArrow()
    {
        var go = new GameObject("discount_arrow", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(_previewContainer, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 0f);
        rt.pivot            = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta        = new Vector2(14f, 14f);

        var txt = go.GetComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.text      = "↓";
        txt.fontSize  = 12;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.LowerLeft;
        txt.color     = new Color(1f, 0.25f, 0.25f);
        txt.raycastTarget = false;
    }

    private void RefreshSynergies(SO_ItemData item)
    {
        if (_synergiesText == null) return;

        if (item.synergies == null || item.synergies.Length == 0)
        {
            _synergiesText.text = string.Empty;
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var syn in item.synergies)
        {
            if (syn == SynergyType.None) continue;
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(SynergyNames.TryGetValue(syn, out var name) ? name : syn.ToString());
        }
        _synergiesText.text = sb.ToString();
    }

    private void OnBuyClicked()
    {
        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(_finalCost))
            return; // 골드 부족 — 구매 취소
        _onBuy?.Invoke(new ItemInstance { data = _item, gradeIndex = _displayGradeIndex }, this);
    }
}
