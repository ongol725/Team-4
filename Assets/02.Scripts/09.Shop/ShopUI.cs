using UnityEngine;
using UnityEngine.UI;

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

    // 각 등급에서 다음 등급으로 오르는 데 필요한 리롤 횟수 (인덱스 0 = 1등급 → 2등급)
    private static readonly int[] GradeThresholds = { 10, 15, 20, 25, 30, 35 };

    private int _shopGrade              = 1;
    private int _rerollsInCurrentGrade  = 0;

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _rerollButton.onClick.AddListener(Reroll);
        if (_rerollCostText != null)
            _rerollCostText.text = $"리롤 ({_rerollCost}G)";

        PopulateSlots(); // 초기 상품 생성 (리롤 카운트 미포함)
        UpdateGradeUI();
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>리롤 버튼 클릭 시 호출. 현재 등급 내 리롤 횟수를 누적하고 등급을 갱신한다.</summary>
    public void Reroll()
    {
        _rerollsInCurrentGrade++;
        UpdateGrade();
        PopulateSlots();
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

    private void PopulateSlots()
    {
        var items = _shopManager.GenerateShopItems(5, _shopGrade);
        for (int i = 0; i < _slots.Length; i++)
        {
            var item = i < items.Length ? items[i] : null;
            _slots[i].SetItem(item, OnItemBought);
        }
    }

    private void UpdateGrade()
    {
        int thresholdIdx = _shopGrade - 1;
        if (thresholdIdx >= GradeThresholds.Length) return; // 최고 등급 도달

        if (_rerollsInCurrentGrade < GradeThresholds[thresholdIdx]) return;

        _shopGrade++;
        _rerollsInCurrentGrade = 0;
        UpdateGradeUI();
    }

    private void UpdateGradeUI()
    {
        if (_gradeText != null)
            _gradeText.text = $"상점 Lv.{_shopGrade}";
    }

    // ─────────────────────────────────────────────────────────────

    private void OnItemBought(SO_ItemData item, ShopSlotUI slot)
    {
        slot.SetSoldOut();
        _inventoryGridUI.BeginPlaceFromShop(new ItemInstance { data = item, gradeIndex = 0 });
    }
}
