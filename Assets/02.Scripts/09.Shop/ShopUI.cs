using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 전체 패널: 5개 슬롯 + 리롤 버튼 관리
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

    [Header("패널 루트")]
    [SerializeField] private GameObject _panelRoot;

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _rerollButton.onClick.AddListener(Reroll);
        if (_rerollCostText != null)
            _rerollCostText.text = $"리롤 ({_rerollCost}G)";

        Reroll();  // 시작 시 첫 상품 생성
    }

    // ─────────────────────────────────────────────────────────────

    public void Reroll()
    {
        var items = _shopManager.GenerateShopItems(5);
        for (int i = 0; i < _slots.Length; i++)
        {
            var item = i < items.Length ? items[i] : null;
            _slots[i].SetItem(item, OnItemBought);
        }
    }

    public void Open()
    {
        if (_panelRoot != null) _panelRoot.SetActive(true);
    }

    public void Close()
    {
        if (_panelRoot != null) _panelRoot.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────

    private void OnItemBought(SO_ItemData item, ShopSlotUI slot)
    {
        slot.SetSoldOut();
        _inventoryGridUI.BeginPlaceFromShop(new ItemInstance { data = item, gradeIndex = 0 });
    }
}
