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

    private bool[,]          _occupied;
    private ItemInstance[,]  _itemAt;
    private readonly Dictionary<ItemInstance, Vector2Int> _origins = new();

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _occupied = new bool[Rows, Cols];
        _itemAt   = new ItemInstance[Rows, Cols];
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
        return true;
    }

    public bool IsOccupied(Vector2Int cell) =>
        InBounds(cell) && _occupied[cell.x, cell.y];

    /// <summary>해당 셀을 차지하는 ItemInstance 반환 (없으면 null)</summary>
    public ItemInstance GetInstanceAt(Vector2Int cell) =>
        InBounds(cell) ? _itemAt[cell.x, cell.y] : null;

    public bool TryGetOrigin(ItemInstance inst, out Vector2Int origin) =>
        _origins.TryGetValue(inst, out origin);

    public bool InActiveArea(Vector2Int cell) =>
        cell.x >= 0 && cell.y >= 0 &&
        cell.x < ActiveRows && cell.y < ActiveCols;

    // ─────────────────────────────────────────────────────────────

    private bool InBounds(Vector2Int cell) =>
        cell.x >= 0 && cell.x < Rows &&
        cell.y >= 0 && cell.y < Cols;

    public static Vector2Int[] GetCells(SO_ItemData data) =>
        (data.cells != null && data.cells.Length > 0)
            ? data.cells
            : new[] { Vector2Int.zero };
}
