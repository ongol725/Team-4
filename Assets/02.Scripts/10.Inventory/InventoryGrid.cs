using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리 그리드 데이터 레이어.
/// ItemInstance 단위로 점유·배치·제거를 관리한다.
/// </summary>
public class InventoryGrid : MonoBehaviour
{
    [Header("그리드 전체 크기 (최대: 7행 × 10열)")]
    [SerializeField] public int Rows = 7;
    [SerializeField] public int Cols = 10;

    [Header("활성화 영역 (기본: 3행 × 4열)")]
    [SerializeField] public int ActiveRows = 3;
    [SerializeField] public int ActiveCols = 4;

    /// <summary>아이템 배치·제거가 발생할 때마다 발행된다. InventoryAnalyzer가 구독한다.</summary>
    public event System.Action OnGridChanged;

    private bool[,]          _occupied;
    private ItemInstance[,]  _itemAt;
    private readonly Dictionary<ItemInstance, Vector2Int> _origins = new();

    // 인벤토리 블록으로 개별 활성화된 추가 셀 (초기 직사각형 외부)
    private HashSet<Vector2Int> _extraActiveCells;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _occupied         = new bool[Rows, Cols];
        _itemAt           = new ItemInstance[Rows, Cols];
        _extraActiveCells = new HashSet<Vector2Int>();
    }

    // ─────────────────────────────────────────────────────────────

    public bool IsValidPlacement(ItemInstance inst, Vector2Int origin)
    {
        foreach (var local in GetCells(inst.data))
        {
            var world = origin + local;
            if (!InBounds(world))            return false;
            if (!InActiveArea(world))        return false;
            if (_occupied[world.x, world.y]) return false;
        }
        return true;
    }

    public bool TryPlace(ItemInstance inst, Vector2Int origin)
    {
        if (!IsValidPlacement(inst, origin)) return false;

        foreach (var local in GetCells(inst.data))
        {
            var world = origin + local;
            _occupied[world.x, world.y] = true;
            _itemAt[world.x, world.y]   = inst;
        }
        _origins[inst] = origin;
        OnGridChanged?.Invoke();
        return true;
    }

    public bool Remove(ItemInstance inst)
    {
        if (!_origins.TryGetValue(inst, out var origin)) return false;

        foreach (var local in GetCells(inst.data))
        {
            var world = origin + local;
            if (InBounds(world))
            {
                _occupied[world.x, world.y] = false;
                _itemAt[world.x, world.y]   = null;
            }
        }
        _origins.Remove(inst);
        OnGridChanged?.Invoke();
        return true;
    }

    public bool IsOccupied(Vector2Int cell) =>
        InBounds(cell) && _occupied[cell.x, cell.y];

    /// <summary>해당 셀을 차지하는 ItemInstance 반환 (없으면 null)</summary>
    public ItemInstance GetInstanceAt(Vector2Int cell) =>
        InBounds(cell) ? _itemAt[cell.x, cell.y] : null;

    public bool TryGetOrigin(ItemInstance inst, out Vector2Int origin) =>
        _origins.TryGetValue(inst, out origin);

    /// <summary>현재 그리드에 배치된 모든 ItemInstance를 열거한다. (인벤토리 블록은 배치 즉시 소멸하므로 포함되지 않음)</summary>
    public IEnumerable<ItemInstance> GetAllPlacedInstances() => _origins.Keys;

    /// <summary>
    /// 초기 직사각형(ActiveRows × ActiveCols, 그리드 중앙 배치) 또는
    /// 인벤토리 블록으로 개별 활성화된 셀이면 true.
    /// </summary>
    public bool InActiveArea(Vector2Int cell)
    {
        int startRow = (Rows - ActiveRows) / 2;
        int startCol = (Cols - ActiveCols) / 2;
        if (cell.x >= startRow && cell.x < startRow + ActiveRows &&
            cell.y >= startCol && cell.y < startCol + ActiveCols)
            return true;
        return _extraActiveCells != null && _extraActiveCells.Contains(cell);
    }

    /// <summary>인벤토리 블록 배치 가능 여부: 모든 셀이 잠긴 영역(비활성 + 미점유)이어야 한다.</summary>
    public bool IsValidBlockExpansion(ItemInstance inst, Vector2Int origin)
    {
        foreach (var local in GetCells(inst.data))
        {
            var world = origin + local;
            if (!InBounds(world))            return false;
            if (InActiveArea(world))         return false;
            if (_occupied[world.x, world.y]) return false;
        }
        return true;
    }

    /// <summary>배치된 셀을 추가 활성 집합에 등록한다.</summary>
    public void ExpandWithBlock(ItemInstance inst, Vector2Int origin)
    {
        if (_extraActiveCells == null) _extraActiveCells = new HashSet<Vector2Int>();
        foreach (var local in GetCells(inst.data))
        {
            var world = origin + local;
            if (InBounds(world))
                _extraActiveCells.Add(world);
        }
    }

    // ─────────────────────────────────────────────────────────────

    private bool InBounds(Vector2Int cell) =>
        cell.x >= 0 && cell.x < Rows &&
        cell.y >= 0 && cell.y < Cols;

    public static Vector2Int[] GetCells(SO_ItemData data) =>
        (data.cells != null && data.cells.Length > 0)
            ? data.cells
            : new[] { Vector2Int.zero };
}
