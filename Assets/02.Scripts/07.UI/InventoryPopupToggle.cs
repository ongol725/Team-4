using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// I 키를 누를 때마다 인벤토리/상점 팝업 전체를 토글한다.
///
/// 씬 구조:
///   Canvas_Inventory (항상 활성) — InventoryGrid + 로직 컴포넌트들
///   InventoryStoreRoot (토글 대상) — InventoryContainer, ShopContainer, SynergyPanel, StatInfoPanel
///
/// Canvas_Inventory 안의 MonoBehaviour는 InventoryStoreRoot가 비활성화되어도
/// 계속 실행되므로 팝업이 닫혀 있어도 I 키가 동작한다.
/// </summary>
public class InventoryPopupToggle : MonoBehaviour
{
    [SerializeField] private GameObject           _popupRoot;     // InventoryStoreRoot
    [SerializeField] private BattleLoadoutBuilder _loadoutBuilder;

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_popupRoot == null)
            _popupRoot = GameObject.Find("InventoryStoreRoot");

        if (_loadoutBuilder == null)
            _loadoutBuilder = GetComponent<BattleLoadoutBuilder>();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return;
        if (Keyboard.current.iKey.wasPressedThisFrame)
            Toggle();
#endif
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>외부(버튼 등)에서도 호출 가능한 토글 진입점</summary>
    public void Toggle()
    {
        if (_popupRoot == null) return;

        bool willOpen = !_popupRoot.activeSelf;
        _popupRoot.SetActive(willOpen);

        // 닫을 때 로드아웃 확정 (ShopUI.Close의 panelRoot 조작 없이 직접 호출)
        if (!willOpen)
            _loadoutBuilder?.BuildAndDeliver();
    }

    /// <summary>팝업 현재 열림 상태</summary>
    public bool IsOpen => _popupRoot != null && _popupRoot.activeSelf;
}
