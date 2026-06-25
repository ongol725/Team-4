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
    [SerializeField] private Color _expandColor    = new Color(1.00f, 0.85f, 0.20f, 0.85f); // 잠금 해제 예정: 금색

    private RectTransform _rt;
    private Image[,]      _cellImages;
    private ItemBlockUI   _pendingBlock;

    // ItemInstance → 배치된 ItemBlockUI 매핑
    private readonly Dictionary<ItemInstance, ItemBlockUI> _instanceToBlock = new();

    public TempSlotUI    TempSlot            => _tempSlot;
    public int           CellSize            => _cellSize;
    public bool          IsAnyFollowingMouse  => _followingCount > 0;
    public ItemBlockUI   ActiveFollowingBlock => _activeFollowingBlock;
    public InventoryGrid Grid               => _grid;

    private int          _followingCount;
    private ItemBlockUI  _activeFollowingBlock;

    public void OnBlockStartedFollowing(ItemBlockUI block)
    {
        _followingCount++;
        _activeFollowingBlock = block;
    }

    public void OnBlockStoppedFollowing(ItemBlockUI block)
    {
        if (_followingCount > 0) _followingCount--;
        if (_activeFollowingBlock == block) _activeFollowingBlock = null;
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
    public void BeginPlaceFromShop(ItemInstance inst, ShopSlotUI shopSlot = null, int refundCost = 0)
    {
        // 이미 마우스에 들린 아이템이 있으면 임시칸으로 보냄
        if (_activeFollowingBlock != null)
            _activeFollowingBlock.ForceSendToTempSlot();

        var go    = new GameObject("ItemBlock_Pending", typeof(RectTransform));
        go.transform.SetParent(transform.root, false);

        var block = go.AddComponent<ItemBlockUI>();
        block.Initialize(inst, this, _grid, _cellSize);
        block.SetShopSource(shopSlot, refundCost);

        _pendingBlock = block;
    }

    // ─────────────────────────────────────────────────────────────
    // 하이라이트

    public void HighlightPlacement(ItemInstance inst, Vector2Int origin)
    {
        ClearHighlight();

        var cells = InventoryGrid.GetCells(inst.data);

        // ── 인벤토리 블록: 잠긴 셀에 배치 → 금색 / 불가 → 빨간색 ──
        if (inst.data is SO_InventoryBlockData)
        {
            bool valid = _grid.IsValidBlockExpansion(inst, origin);
            var blockColor = valid ? _expandColor : _invalidColor;
            foreach (var local in cells)
            {
                var world = origin + local;
                if (InCellRange(world))
                    _cellImages[world.x, world.y].color = blockColor;
            }
            return;
        }

        // ── 일반 아이템 배치 ──
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
        if (outOfRange)
            color = _invalidColor;
        else if (isMultiple)
            color = _swapColor;   // 여러 아이템 → 전부 밀어내기 가능
        else if (overlapping == null)
            color = _validColor;
        else if (overlapping.data == inst.data)
            color = overlapping.gradeIndex < 4 ? _synthesizeColor : _invalidColor;
        else
            color = _swapColor;

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

    public void OnItemSentToTempSlot(ItemBlockUI block)
    {
        if (_pendingBlock == block) _pendingBlock = null;
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
    // 인벤토리 확장 연동

    /// <summary>활성 영역 변경 후 호출하여 잠긴 셀 / 열린 셀 색상을 갱신한다.</summary>
    public void RefreshCellColors()
    {
        for (int r = 0; r < _grid.Rows; r++)
        for (int c = 0; c < _grid.Cols; c++)
        {
            var cell   = new Vector2Int(r, c);
            bool locked = !_grid.InActiveArea(cell);

            var outline = _cellImages[r, c].GetComponent<Outline>();
            if (locked)
            {
                _cellImages[r, c].color = _lockedColor;
                if (outline != null) outline.effectColor = new Color(0.2f, 0.2f, 0.2f, 0.20f);
            }
            else
            {
                _cellImages[r, c].color = _grid.IsOccupied(cell) ? _occupiedColor : _emptyColor;
                if (outline != null) outline.effectColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────

    private bool InCellRange(Vector2Int cell) =>
        cell.x >= 0 && cell.x < _grid.Rows &&
        cell.y >= 0 && cell.y < _grid.Cols;

    // ─────────────────────────────────────────────────────────────
    // 자동 최적화 정렬 (O 키)

    /// <summary>
    /// 인벤토리 그리드 + 임시칸의 아이템을 전부 모아
    /// 합성등급(내림) → 희귀도(내림) → 셀 크기(내림) 순으로 정렬 후 재배치.
    /// 그리드가 꽉 차도 임시칸 아이템까지 포함해 최적 위치를 찾는다.
    /// 그리드에 들어가지 못한 아이템은 임시칸으로 이동.
    /// </summary>
    public void AutoSortInventory()
    {
        // ── 1. 전체 수집 ──
        var gridInstances = new List<ItemInstance>(_grid.GetAllPlacedInstances());
        var tempBlockSnap = _tempSlot != null
            ? new List<ItemBlockUI>(_tempSlot.HeldBlocks)
            : new List<ItemBlockUI>();

        var allItems = new List<ItemInstance>(gridInstances);
        var blockMap = new Dictionary<ItemInstance, ItemBlockUI>();

        foreach (var inst in gridInstances)
        {
            var b = FindItemBlock(inst);
            if (b != null) blockMap[inst] = b;
        }
        foreach (var block in tempBlockSnap)
        {
            if (block?.Instance == null) continue;
            allItems.Add(block.Instance);
            blockMap[block.Instance] = block;
        }

        Debug.Log($"[AutoSort] grid={gridInstances.Count}개, temp={tempBlockSnap.Count}개, 합계={allItems.Count}개");
        if (allItems.Count == 0) return;

        // ── 2. 현재 위치에서 전부 제거 ──
        foreach (var inst in gridInstances)
        {
            _grid.Remove(inst);
            OnItemUnplaced(inst);
        }
        foreach (var block in tempBlockSnap)
        {
            if (block?.Instance != null)
                _tempSlot.OnItemPickedUp(block);
        }

        // ── 3. 블록 상태 플래그 초기화 (그리드/임시칸 모두) ──
        foreach (var block in blockMap.Values)
            block.ResetForAutoSort();

        // ── 4. 정렬: 합성등급↓ → 희귀도↓ → 셀 크기↓ ──
        allItems.Sort((a, b) =>
        {
            int g = b.gradeIndex.CompareTo(a.gradeIndex);
            if (g != 0) return g;
            int r = ((int)b.data.rarity).CompareTo((int)a.data.rarity);
            if (r != 0) return r;
            return InventoryGrid.GetCells(b.data).Length
                  .CompareTo(InventoryGrid.GetCells(a.data).Length);
        });

        // ── 5. 재배치: 그리드 먼저, 공간 없으면 임시칸 ──
        foreach (var inst in allItems)
        {
            if (!blockMap.TryGetValue(inst, out var block)) continue;

            var origin = FindFirstValidPlacement(inst);
            if (origin.HasValue)
            {
                _grid.TryPlace(inst, origin.Value);
                block.SnapDirectly(origin.Value);
            }
            else if (_tempSlot != null)
            {
                _tempSlot.ReceiveBlock(block);
                OnItemSentToTempSlot(block);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 우클릭 스마트 구매 (합성 > 자동배치 > 임시칸)

    /// <summary>
    /// 우클릭 구매 아이템을 우선순위에 따라 처리한다.
    /// 1순위: 인벤/임시칸의 같은 종류+등급 아이템과 합성
    /// 2순위: 인벤토리 빈 공간에 자동 배치
    /// 3순위: 임시칸으로 이동
    /// </summary>
    public void SmartReceiveFromShop(ItemInstance inst)
    {
        // 이미 드래그 중인 아이템이 있으면 먼저 임시칸으로 보냄
        if (_activeFollowingBlock != null)
            _activeFollowingBlock.ForceSendToTempSlot();

        if (TryMergeWithExisting(inst)) return;

        var origin = FindFirstValidPlacement(inst);
        if (origin.HasValue) { DirectPlaceFromShop(inst, origin.Value); return; }

        SendToTempSlotFromShop(inst);
    }

    /// <summary>인벤/임시칸에서 합성 가능한 아이템을 찾아 합성. 성공 시 true.</summary>
    private bool TryMergeWithExisting(ItemInstance newInst)
    {
        if (!newInst.HasGrades || newInst.gradeIndex >= 4) return false;

        // 그리드 탐색
        foreach (var existing in _grid.GetAllPlacedInstances())
        {
            if (existing.data != newInst.data || existing.gradeIndex != newInst.gradeIndex) continue;
            existing.TryUpgrade();
            RefreshItemBlockVisual(existing);
            // TryUpgrade는 이벤트를 발행하지 않으므로 InventoryAnalyzer 갱신을 위해 명시적 통지
            _grid.NotifyChanged();
            return true;
        }

        // 임시칸 탐색
        if (_tempSlot != null)
            foreach (var block in _tempSlot.HeldBlocks)
            {
                if (block?.Instance == null) continue;
                if (block.Instance.data != newInst.data || block.Instance.gradeIndex != newInst.gradeIndex) continue;
                block.Instance.TryUpgrade();
                block.RefreshVisuals();
                _grid.NotifyChanged();
                return true;
            }

        return false;
    }

    /// <summary>아이템 모양을 수용할 수 있는 첫 번째 유효 셀을 반환.</summary>
    public Vector2Int? FindFirstValidPlacement(ItemInstance inst)
    {
        for (int r = 0; r < _grid.Rows; r++)
        for (int c = 0; c < _grid.Cols; c++)
        {
            var cell = new Vector2Int(r, c);
            if (_grid.IsValidPlacement(inst, cell)) return cell;
        }
        return null;
    }

    /// <summary>지정 셀에 아이템 블록을 마우스 없이 즉시 배치.</summary>
    private void DirectPlaceFromShop(ItemInstance inst, Vector2Int origin)
    {
        if (!_grid.TryPlace(inst, origin)) return;

        var go = new GameObject("ItemBlock", typeof(RectTransform));
        go.transform.SetParent(transform.root, false);
        go.transform.SetAsLastSibling();

        var block = go.AddComponent<ItemBlockUI>();
        block.Initialize(inst, this, _grid, _cellSize);
        block.SnapDirectly(origin);
    }

    /// <summary>아이템 블록을 생성해 임시칸으로 즉시 전달.</summary>
    private void SendToTempSlotFromShop(ItemInstance inst)
    {
        var go = new GameObject("ItemBlock", typeof(RectTransform));
        go.transform.SetParent(transform.root, false);
        go.transform.SetAsLastSibling();

        var block = go.AddComponent<ItemBlockUI>();
        block.Initialize(inst, this, _grid, _cellSize);
        block.ForceSendToTempSlot();
    }
}
