using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 그리드 UI 레이어.
/// - 셀 이미지 자동 생성 및 색상 관리
/// - 배치·합성·스왑 하이라이트
/// - ItemInstance ↔ ItemBlockUI 매핑 추적
/// - 임시칸(TempSlotUI) 연동
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class InventoryGridUI : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private InventoryGrid _grid;
    [SerializeField] private TempSlotUI    _tempSlot;

    [Header("셀 설정 (설계서: 50×50px)")]
    [SerializeField] private int _cellSize = 50;
    [SerializeField] private int _cellGap  = 2;

    [Header("셀 색상")]
    [SerializeField] private Color _emptyColor     = new Color(0.18f, 0.18f, 0.18f, 0.90f);
    [SerializeField] private Color _occupiedColor  = new Color(0.35f, 0.35f, 0.35f, 0.90f);
    [SerializeField] private Color _lockedColor    = new Color(0.07f, 0.07f, 0.09f, 0.85f);
    [SerializeField] private Color _validColor     = new Color(0.15f, 0.85f, 0.25f, 0.75f);
    [SerializeField] private Color _invalidColor   = new Color(0.90f, 0.15f, 0.15f, 0.75f);
    [SerializeField] private Color _synthesizeColor = new Color(0.20f, 0.60f, 1.00f, 0.80f); // 합성 가능: 파란색
    [SerializeField] private Color _swapColor      = new Color(1.00f, 0.55f, 0.10f, 0.80f); // 스왑 가능: 주황색

    private RectTransform _rt;
    private Image[,]      _cellImages;
    private ItemBlockUI   _pendingBlock;

    // ItemInstance → 배치된 ItemBlockUI 매핑
    private readonly Dictionary<ItemInstance, ItemBlockUI> _instanceToBlock = new();

    public TempSlotUI TempSlot         => _tempSlot;
    public int        CellSize         => _cellSize;
    public bool       IsAnyFollowingMouse => _followingCount > 0;

    private int _followingCount;

    public void OnBlockStartedFollowing()
    {
        _followingCount++;
    }

    public void OnBlockStoppedFollowing()
    {
        if (_followingCount > 0) _followingCount--;
    }

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        BuildGrid();
    }

    // ─────────────────────────────────────────────────────────────
    // 그리드 생성

    private void BuildGrid()
    {
        _cellImages = new Image[_grid.Rows, _grid.Cols];
        int stride  = _cellSize + _cellGap;

        for (int r = 0; r < _grid.Rows; r++)
        for (int c = 0; c < _grid.Cols; c++)
        {
            var go = new GameObject($"Cell_{r}_{c}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot            = new Vector2(0f, 1f);
            rt.sizeDelta        = new Vector2(_cellSize, _cellSize);
            rt.anchoredPosition = new Vector2(c * stride, -r * stride);

            bool locked = !_grid.InActiveArea(new Vector2Int(r, c));
            var img     = go.GetComponent<Image>();
            img.color   = locked ? _lockedColor : _emptyColor;

            var outline = go.AddComponent<Outline>();
            outline.effectColor    = locked
                ? new Color(0.2f, 0.2f, 0.2f, 0.20f)
                : new Color(0.5f, 0.5f, 0.5f, 0.35f);
            outline.effectDistance = new Vector2(1f, -1f);

            _cellImages[r, c] = img;
        }

        int s = _cellSize + _cellGap;
        _rt.sizeDelta = new Vector2(
            _grid.Cols * s - _cellGap,
            _grid.Rows * s - _cellGap);
    }

    // ─────────────────────────────────────────────────────────────
    // 상점 연동

    /// <summary>상점 구매 후 아이템을 마우스에 들고 배치 대기 상태로 만든다</summary>
    public void BeginPlaceFromShop(ItemInstance inst)
    {
        if (_pendingBlock != null)
        {
            Destroy(_pendingBlock.gameObject);
            _pendingBlock = null;
        }

        var go    = new GameObject("ItemBlock_Pending", typeof(RectTransform));
        go.transform.SetParent(transform.root, false);

        var block = go.AddComponent<ItemBlockUI>();
        block.Initialize(inst, this, _grid, _cellSize);

        _pendingBlock = block;
    }

    // ─────────────────────────────────────────────────────────────
    // 하이라이트

    public void HighlightPlacement(ItemInstance inst, Vector2Int origin)
    {
        ClearHighlight();

        var cells = InventoryGrid.GetCells(inst.data);

        // 겹치는 아이템 분석
        ItemInstance overlapping = null;
        bool isMultiple = false;
        bool outOfRange = false;

        foreach (var local in cells)
        {
            var world = origin + local;
            if (!InCellRange(world) || !_grid.InActiveArea(world))
            {
                outOfRange = true; break;
            }
            var existing = _grid.GetInstanceAt(world);
            if (existing == null) continue;
            if (overlapping == null) overlapping = existing;
            else if (overlapping != existing) { isMultiple = true; break; }
        }

        Color color;
        if (outOfRange || isMultiple)
            color = _invalidColor;
        else if (overlapping == null)
            color = _validColor;
        else if (overlapping.data == inst.data)
            color = overlapping.gradeIndex < 4 ? _synthesizeColor : _invalidColor;
        else
            color = _swapColor; // 항상 스왑 가능 (임시칸 또는 마우스로 이동)

        foreach (var local in cells)
        {
            var world = origin + local;
            if (InCellRange(world))
                _cellImages[world.x, world.y].color = color;
        }
    }

    public void ClearHighlight()
    {
        for (int r = 0; r < _grid.Rows; r++)
        for (int c = 0; c < _grid.Cols; c++)
        {
            var cell = new Vector2Int(r, c);
            if (!_grid.InActiveArea(cell))
                _cellImages[r, c].color = _lockedColor;
            else if (_grid.IsOccupied(cell))
                _cellImages[r, c].color = _occupiedColor;
            else
                _cellImages[r, c].color = _emptyColor;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 좌표 변환

    public Vector2Int? ScreenToCell(Vector2 screenPos)
    {
        var canvas = GetComponentInParent<Canvas>();
        var cam    = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rt, screenPos, cam, out var local))
            return null;

        int stride = _cellSize + _cellGap;
        int col    = Mathf.FloorToInt( local.x / stride);
        int row    = Mathf.FloorToInt(-local.y / stride);

        if (!InCellRange(new Vector2Int(row, col))) return null;
        return new Vector2Int(row, col);
    }

    public Vector2 CellToAnchoredPos(Vector2Int cell)
    {
        int stride = _cellSize + _cellGap;
        return new Vector2(cell.y * stride, -cell.x * stride);
    }

    // ─────────────────────────────────────────────────────────────
    // ItemBlockUI 콜백

    public void OnPlacementSuccess(ItemInstance inst, ItemBlockUI block)
    {
        _pendingBlock = null;
        _instanceToBlock[inst] = block;
        ClearHighlight();
    }

    public void OnPlacementCancelled(ItemInstance inst)
    {
        if (_pendingBlock != null)
        {
            _instanceToBlock.Remove(inst);
            Destroy(_pendingBlock.gameObject);
            _pendingBlock = null;
        }
        ClearHighlight();
    }

    /// <summary>재집기 또는 스왑으로 그리드에서 제거됐을 때</summary>
    public void OnItemUnplaced(ItemInstance inst)
    {
        _instanceToBlock.Remove(inst);
        ClearHighlight();
    }

    /// <summary>스왑 실패 시 기존 아이템 블록을 dict에 복원</summary>
    public void OnItemRestored(ItemInstance inst, ItemBlockUI block)
    {
        _instanceToBlock[inst] = block;
    }

    // ─────────────────────────────────────────────────────────────
    // 합성 비주얼 갱신

    public void RefreshItemBlockVisual(ItemInstance inst)
    {
        if (_instanceToBlock.TryGetValue(inst, out var block))
            block.RefreshVisuals();
    }

    public ItemBlockUI FindItemBlock(ItemInstance inst) =>
        _instanceToBlock.TryGetValue(inst, out var block) ? block : null;

    // ─────────────────────────────────────────────────────────────

    private bool InCellRange(Vector2Int cell) =>
        cell.x >= 0 && cell.x < _grid.Rows &&
        cell.y >= 0 && cell.y < _grid.Cols;
}
