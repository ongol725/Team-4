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
    /// <summary>인벤토리가 열리거나 닫힐 때 발행. true = 열림, false = 닫힘</summary>
    public static event System.Action<bool> onPopupToggled;

    [SerializeField] private BattleLoadoutBuilder _loadoutBuilder;

    private Canvas               _popupCanvas;   // InventoryStoreRoot 의 Canvas 컴포넌트
    private BattleLoadoutDebugUI _debugUI;        // 배치 종합정보 패널 (Canvas_Inventory 직접 자식)
    private InventoryGridUI      _gridUI;         // 현재 드래그 중인 블록 참조용
    private bool                 _inCombatZone;
    // SellSlotUI 는 싱글톤으로 접근

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        var root = GameObject.Find("InventoryStoreRoot");
        if (root != null) _popupCanvas = root.GetComponent<Canvas>();

        if (_loadoutBuilder == null)
            _loadoutBuilder = GetComponent<BattleLoadoutBuilder>();

        _debugUI = GetComponent<BattleLoadoutDebugUI>();
        _gridUI  = GetComponent<InventoryGridUI>();

        CombatZone.onCombatStateChanged += OnCombatStateChanged;

        // Additive 씬 로드 타이밍 문제 대응:
        // InventoryStore 씬이 늦게 로드되어 CombatZone 이벤트를 놓친 경우 즉시 로드아웃 적용
        if (CombatZone.IsInCombat)
        {
            _inCombatZone = true;
            _loadoutBuilder?.BuildAndDeliver();
        }
    }

    private void OnDestroy()
    {
        CombatZone.onCombatStateChanged -= OnCombatStateChanged;
    }

    private void OnCombatStateChanged(bool inCombat)
    {
        _inCombatZone = inCombat;
        if (inCombat)
        {
            if (IsOpen) ForceClose();
            else _loadoutBuilder?.BuildAndDeliver();
        }
    }

    // 강제 닫기 — 전투 구역 진입 시 인벤토리가 열려 있으면 자동 닫음
    private void ForceClose()
    {
        if (_popupCanvas == null || !_popupCanvas.enabled) return;
        _gridUI?.ActiveFollowingBlock?.ForceSendToTempSlot();
        _popupCanvas.enabled = false;
        _debugUI?.SetPanelActive(false);
        SellSlotUI.Instance?.SetPanelActive(false);
        _loadoutBuilder?.BuildAndDeliver();
        onPopupToggled?.Invoke(false);
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
        if (_inCombatZone) return; // 전투 구역에서는 인벤토리 열기 불가

        bool willOpen = !_popupCanvas.enabled;

        // 닫는 순간 들고 있는 아이템이 있으면 임시칸으로 먼저 이동
        if (!willOpen)
            _gridUI?.ActiveFollowingBlock?.ForceSendToTempSlot();

        _popupCanvas.enabled = willOpen;

        // Canvas_Inventory 직접 자식인 패널도 함께 토글
        _debugUI?.SetPanelActive(willOpen);
        SellSlotUI.Instance?.SetPanelActive(willOpen);

        // 닫을 때 로드아웃 확정 (ShopUI.Close의 panelRoot 조작 없이 직접 호출)
        if (!willOpen)
            _loadoutBuilder?.BuildAndDeliver();

        onPopupToggled?.Invoke(willOpen);
    }

    /// <summary>팝업 현재 열림 상태</summary>
    public bool IsOpen => _popupCanvas != null && _popupCanvas.enabled;
}
