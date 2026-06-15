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
    [SerializeField] private Button         _buyButton;
    [SerializeField] private Text           _buyButtonText;
    [SerializeField] private RectTransform  _previewContainer;  // 셀 형태 미리보기 부모
    [SerializeField] private Image          _slotBackground;

    private static readonly Color[] RarityColors =
    {
        new Color(0.75f, 0.75f, 0.75f),  // Common   - 회색
        new Color(0.30f, 0.55f, 1.00f),  // Rare     - 파랑
        new Color(0.65f, 0.25f, 0.95f),  // Epic     - 보라
        new Color(1.00f, 0.80f, 0.10f),  // Legendary- 금색
    };

    private static readonly string[] RarityLabels = { "일반", "희귀", "영웅", "전설" };

    private const float MiniCellSize = 11f;
    private const float MiniCellGap  = 1f;

    private SO_ItemData _item;
    private Action<SO_ItemData, ShopSlotUI> _onBuy;

    // ─────────────────────────────────────────────────────────────

    public void SetItem(SO_ItemData item, Action<SO_ItemData, ShopSlotUI> onBuy)
    {
        _item  = item;
        _onBuy = onBuy;

        if (item == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        var rarityIdx = Mathf.Clamp((int)item.rarity, 0, RarityColors.Length - 1);
        var color     = RarityColors[rarityIdx];

        _nameText.text  = item.itemName;
        _costText.text  = $"{item.cost} G";
        _rarityText.text = RarityLabels[rarityIdx];
        _rarityText.color = color;

        if (_slotBackground != null)
            _slotBackground.color = new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.85f);

        BuildMiniPreview(color);

        _buyButton.interactable = true;
        if (_buyButtonText != null) _buyButtonText.text = "구매";

        _buyButton.onClick.RemoveAllListeners();
        _buyButton.onClick.AddListener(OnBuyClicked);
    }

    public void SetSoldOut()
    {
        _buyButton.interactable = false;
        if (_buyButtonText != null) _buyButtonText.text = "구매됨";
    }

    // ─────────────────────────────────────────────────────────────

    private void BuildMiniPreview(Color color)
    {
        foreach (Transform child in _previewContainer)
            Destroy(child.gameObject);

        var cells = (_item.cells != null && _item.cells.Length > 0)
            ? _item.cells
            : new[] { Vector2Int.zero };

        foreach (var cell in cells)
            CreateMiniCell(cell, color);
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

    private void OnBuyClicked() => _onBuy?.Invoke(_item, this);
}
