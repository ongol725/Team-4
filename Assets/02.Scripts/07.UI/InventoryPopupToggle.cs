using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// I 키를 누를 때마다 인벤토리/상점 팝업 전체를 토글한다.
///
/// InventoryGrid는 InventoryStoreRoot의 자식이므로 SetActive(false)를 쓰면
/// 이 스크립트 자신도 비활성화되어 두 번째 I 키를 감지하지 못한다.
/// Canvas.enabled = false는 렌더링만 끄고 MonoBehaviour는 계속 실행되므로
/// 팝업이 닫혀 있어도 I 키 입력을 항상 감지할 수 있다.
/// </summary>
public class InventoryPopupToggle : MonoBehaviour
{
    [SerializeField] private BattleLoadoutBuilder _loadoutBuilder;

    private Canvas _popupCanvas;   // InventoryStoreRoot 의 Canvas 컴포넌트

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        var root = GameObject.Find("InventoryStoreRoot");
        if (root != null) _popupCanvas = root.GetComponent<Canvas>();

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
        if (_popupCanvas == null) return;

        bool willOpen = !_popupCanvas.enabled;
        _popupCanvas.enabled = willOpen;

        // 닫을 때 로드아웃 확정 (ShopUI.Close의 panelRoot 조작 없이 직접 호출)
        if (!willOpen)
            _loadoutBuilder?.BuildAndDeliver();
    }

    /// <summary>팝업 현재 열림 상태</summary>
    public bool IsOpen => _popupCanvas != null && _popupCanvas.enabled;
}
