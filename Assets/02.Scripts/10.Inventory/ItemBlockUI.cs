using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 위에 올라간 아이템 블록 하나를 담당한다.
///
/// ▶ 마우스 추적 상태 (_isFollowingMouse):
///   - 좌클릭 → 셀 상태에 따라 배치 / 합성 / 스왑 처리
///   - 우클릭 → 취소(파괴)
///
/// ▶ 배치 완료 상태 (_isPlaced):
///   - IPointerDownHandler → 다시 집어서 재배치
///
/// ▶ 임시칸 상태 (_isInTempSlot):
///   - IPointerDownHandler → 임시칸에서 꺼내어 재배치
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ItemBlockUI : MonoBehaviour, IPointerDownHandler
{
    private ItemInstance    _instance;
    private InventoryGridUI _gridUI;
    private InventoryGrid   _grid;
    private int             _cellSize;

    private RectTransform _rt;
    private bool _isFollowingMouse;
    private bool _isPlaced;
    private bool _isInTempSlot;
    private TempSlotUI _tempSlot;
    private bool _placementInputGuard;

    // 하이라이트 색상 (InventoryGridUI와 별도로 상태 전달용으로 사용)
    private static readonly Color[] RarityColors =
    {
        new Color(0.70f, 0.70f, 0.70f),  // Common
        new Color(0.30f, 0.55f, 1.00f),  // Rare
        new Color(0.65f, 0.25f, 0.95f),  // Epic
        new Color(1.00f, 0.80f, 0.10f),  // Legendary
    };

    // ─────────────────────────────────────────────────────────────

    public void Initialize(ItemInstance instance, InventoryGridUI gridUI,
                           InventoryGrid grid, int cellSize)
    {
        _instance = instance;
        _gridUI   = gridUI;
        _grid     = grid;
        _cellSize = cellSize;
        _rt       = GetComponent<RectTransform>();

        _rt.anchorMin = _rt.anchorMax = new Vector2(0f, 1f);
        _rt.pivot     = new Vector2(0f, 1f);

        BuildVisuals();

        _placementInputGuard = false;
        SetFollowing(true);
    }

    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isFollowingMouse) return;

        var mouse    = Mouse.current;
        var mousePos = (Vector2)mouse.position.ReadValue();

        _rt.position = mousePos;

        var cell = _gridUI.ScreenToCell(mousePos);
        if (cell.HasValue)
            _gridUI.HighlightPlacement(_instance, cell.Value);
        else
            _gridUI.ClearHighlight();

        // 구매 직후 클릭과 구분하기 위한 1프레임 딜레이
        if (!_placementInputGuard) { _placementInputGuard = true; return; }

        if (mouse.leftButton.wasPressedThisFrame && cell.HasValue)
            HandlePlacement(cell.Value);

        if (mouse.rightButton.wasPressedThisFrame)
        {
            SetFollowing(false);
            _gridUI.OnPlacementCancelled(_instance);
            Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 배치 분기

    private void HandlePlacement(Vector2Int origin)
    {
        var (overlapping, isMultiple) = FindOverlappingInstance(origin);

        if (isMultiple) return; // 여러 종류 겹침 → 불가

        if (overlapping == null)
        {
            // 빈 칸 → 일반 배치
            if (_grid.TryPlace(_instance, origin))
                SnapToGrid(origin);
        }
        else if (overlapping.data == _instance.data)
        {
            // 같은 아이템 → 합성
            TrySynthesize(overlapping);
        }
        else
        {
            // 다른 아이템 → 스왑
            TrySwap(overlapping, origin);
        }
    }

    /// <summary>origin 기준으로 이 아이템 셀들이 겹치는 단일 ItemInstance 탐색</summary>
    private (ItemInstance inst, bool isMultiple) FindOverlappingInstance(Vector2Int origin)
    {
        ItemInstance found = null;
        foreach (var local in InventoryGrid.GetCells(_instance.data))
        {
            var world = origin + local;
            var inst  = _grid.GetInstanceAt(world);
            if (inst == null) continue;
            if (found == null) found = inst;
            else if (found != inst) return (null, true);
        }
        return (found, false);
    }

    // ─────────────────────────────────────────────────────────────
    // 합성

    private void TrySynthesize(ItemInstance target)
    {
        if (!target.TryUpgrade()) return; // 5등급이면 합성 불가

        SetFollowing(false);
        _gridUI.RefreshItemBlockVisual(target);
        _gridUI.OnPlacementCancelled(_instance);
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────
    // 스왑

    private void TrySwap(ItemInstance displaced, Vector2Int origin)
    {
        var displacedBlock = _gridUI.FindItemBlock(displaced);

        // 원래 위치를 미리 저장 (배치 실패 시 복원용)
        _grid.TryGetOrigin(displaced, out var savedOrigin);

        _grid.Remove(displaced);
        _gridUI.OnItemUnplaced(displaced);

        if (_grid.TryPlace(_instance, origin))
        {
            // 배치 성공: 임시칸이 있으면 임시칸으로, 없으면 마우스로
            var tempSlot = _gridUI.TempSlot;
            if (tempSlot != null && !tempSlot.IsOccupied && displacedBlock != null)
                tempSlot.ReceiveBlock(displacedBlock);
            else if (displacedBlock != null)
                displacedBlock.ResumeFollowing();

            SnapToGrid(origin);
        }
        else
        {
            // 배치 실패: 밀려난 아이템 원위치 복원, 현재 아이템은 계속 들고 있음
            _grid.TryPlace(displaced, savedOrigin);
            _gridUI.OnItemRestored(displaced, displacedBlock);
        }
    }

    /// <summary>스왑으로 밀려났을 때 마우스를 다시 따라다니게 한다</summary>
    public void ResumeFollowing()
    {
        _isPlaced     = false;
        _isInTempSlot = false;
        _tempSlot     = null;
        _placementInputGuard = true; // 이번 프레임 클릭 무시
        transform.SetParent(transform.root, false);
        transform.SetAsLastSibling();
        SetFollowing(true);
    }

    // ─────────────────────────────────────────────────────────────
    // 배치 완료 후 재집기 (배치 상태 + 임시칸 공통)

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_isFollowingMouse) return;
        if (_gridUI.IsAnyFollowingMouse) return; // 다른 블록 드래그 중 → 클릭 무시

        if (_isPlaced)
        {
            _grid.Remove(_instance);
            _gridUI.OnItemUnplaced(_instance);
            _isPlaced = false;
        }
        else if (_isInTempSlot)
        {
            _tempSlot.OnItemPickedUp();
            _isInTempSlot = false;
            _tempSlot     = null;
        }
        else return;

        _placementInputGuard = false;
        transform.SetParent(transform.root, false);
        transform.SetAsLastSibling();
        _gridUI.ClearHighlight();
        SetFollowing(true);
    }

    // ─────────────────────────────────────────────────────────────
    // following 상태 변경 (GridUI 카운트 동기화)

    private void SetFollowing(bool value)
    {
        if (_isFollowingMouse == value) return;
        _isFollowingMouse = value;
        if (value) _gridUI.OnBlockStartedFollowing();
        else       _gridUI.OnBlockStoppedFollowing();
    }

    // ─────────────────────────────────────────────────────────────
    // 임시칸 수신 콜백

    public void OnSentToTempSlot(TempSlotUI slot)
    {
        _isPlaced     = false;
        _isInTempSlot = true;
        _tempSlot     = slot;
    }

    // ─────────────────────────────────────────────────────────────
    // 그리드에 스냅

    private void SnapToGrid(Vector2Int cell)
    {
        SetFollowing(false);
        _isPlaced = true;

        transform.SetParent(_gridUI.transform, false);
        _rt.anchoredPosition = _gridUI.CellToAnchoredPos(cell);

        _gridUI.OnPlacementSuccess(_instance, this);
    }

    private void OnDestroy()
    {
        // 예외적으로 파괴될 때 카운트 보정
        if (_isFollowingMouse && _gridUI != null)
            _gridUI.OnBlockStoppedFollowing();
    }

    // ─────────────────────────────────────────────────────────────
    // 비주얼

    public void RefreshVisuals()
    {
        foreach (Transform child in _rt)
            Destroy(child.gameObject);
        BuildVisuals();
    }

    private void BuildVisuals()
    {
        var cells = InventoryGrid.GetCells(_instance.data);
        int rarityIdx  = Mathf.Clamp((int)_instance.data.rarity, 0, RarityColors.Length - 1);
        float bright   = 1f + _instance.gradeIndex * 0.12f; // 등급이 높을수록 밝아짐
        var baseColor  = new Color(
            Mathf.Clamp01(RarityColors[rarityIdx].r * bright),
            Mathf.Clamp01(RarityColors[rarityIdx].g * bright),
            Mathf.Clamp01(RarityColors[rarityIdx].b * bright));

        bool labelPlaced = false;
        foreach (var cell in cells)
        {
            var go  = new GameObject("tile", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_rt, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(_cellSize - 2, _cellSize - 2);
            rt.anchoredPosition = new Vector2(
                 cell.y * _cellSize + 1f,
                -cell.x * _cellSize - 1f);

            go.GetComponent<Image>().color = baseColor;

            if (!labelPlaced)
            {
                AddLabel(go, _instance.data.itemName);
                if (_instance.HasGrades)
                    AddGradeBadge(go, _instance.gradeIndex + 1);
                labelPlaced = true;
            }
        }
    }

    private static void AddLabel(GameObject parent, string text)
    {
        var go = new GameObject("label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var txt = go.GetComponent<Text>();
        txt.text      = text;
        txt.fontSize  = 9;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = Color.white;
        txt.raycastTarget = false;
    }

    private static void AddGradeBadge(GameObject parent, int grade)
    {
        var go = new GameObject("grade", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-2f, 2f);
        rt.sizeDelta        = new Vector2(20f, 14f);

        var txt = go.GetComponent<Text>();
        txt.text      = $"+{grade}";
        txt.fontSize  = 8;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.LowerRight;
        txt.color     = new Color(1f, 0.95f, 0.4f);
        txt.raycastTarget = false;
    }
}
