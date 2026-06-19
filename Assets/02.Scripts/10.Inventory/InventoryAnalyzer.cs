using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리 배치 아이템 분석기.
/// InventoryGrid.OnGridChanged 이벤트를 구독해 더티 플래그를 세우고,
/// LateUpdate에서 한 프레임에 1회만 Analyze를 실행한다.
/// (한 프레임에 배치·제거가 여러 번 일어나도 Analyze는 1회만 호출됨)
/// </summary>
public class InventoryAnalyzer : MonoBehaviour
{
    [SerializeField] private InventoryGrid _grid;

    /// <summary>인벤토리가 변경될 때마다 새 스냅샷과 함께 발행된다.</summary>
    public event System.Action<InventorySnapshot> OnSnapshotChanged;

    /// <summary>마지막으로 계산된 스냅샷. BattleLoadoutBuilder 등 외부에서 참조한다.</summary>
    public InventorySnapshot LatestSnapshot { get; private set; }

    // 더티 플래그: 한 프레임에 여러 번 그리드 변화가 생겨도 Analyze는 LateUpdate에서 1회만 실행
    private bool _dirty;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_grid == null) _grid = GetComponent<InventoryGrid>();
    }

    private void Start()
    {
        if (_grid != null) _grid.OnGridChanged += OnGridChanged;
        ForceRefresh();
    }

    private void OnDestroy()
    {
        if (_grid != null) _grid.OnGridChanged -= OnGridChanged;
    }

    private void OnGridChanged() => _dirty = true;

    private void LateUpdate()
    {
        if (!_dirty) return;
        _dirty = false;
        NotifyInventoryChanged();
    }

    // ─────────────────────────────────────────────────────────────

    // 4방향 오프셋 (상·하·좌·우)
    private static readonly Vector2Int[] Directions =
    {
        new(-1,  0),
        new( 1,  0),
        new( 0, -1),
        new( 0,  1),
    };

    /// <summary>현재 그리드 배치 상태를 분석해 스냅샷을 반환한다.</summary>
    public InventorySnapshot Analyze()
    {
        var snapshot = new InventorySnapshot();
        if (_grid == null) return snapshot;

        // 인접 버프 초기화 후 스냅샷 집계
        foreach (var inst in _grid.GetAllPlacedInstances())
        {
            inst.RingGradeBonus = 0;
            snapshot.Add(inst);
        }

        ApplyRingBuffs(snapshot);
        LatestSnapshot = snapshot;
        return snapshot;
    }

    /// <summary>
    /// 반지(SO_AccessoryData) 인접 버프 패스.
    /// 각 반지의 4방향 셀에 무기가 있으면 해당 무기의 RingGradeBonus += 1.
    /// </summary>
    private void ApplyRingBuffs(InventorySnapshot snapshot)
    {
        foreach (var acc in snapshot.Accessories)
        {
            if (acc.data is not SO_AccessoryData) continue;
            if (!_grid.TryGetOrigin(acc, out var origin)) continue;

            // 반지가 차지하는 월드 셀 집합
            var ringCells = new HashSet<Vector2Int>();
            foreach (var local in InventoryGrid.GetCells(acc.data))
                ringCells.Add(origin + local);

            // 인접 무기 아이템 중복 없이 수집
            var adjacentWeapons = new HashSet<ItemInstance>();
            foreach (var cell in ringCells)
            {
                foreach (var dir in Directions)
                {
                    var neighbor = cell + dir;
                    if (ringCells.Contains(neighbor)) continue;

                    var inst = _grid.GetInstanceAt(neighbor);
                    if (inst != null && inst.data is SO_WeaponData)
                        adjacentWeapons.Add(inst);
                }
            }

            foreach (var weapon in adjacentWeapons)
            {
                weapon.RingGradeBonus += 1;
                snapshot.RecordWeaponRingBuff(weapon, acc);
            }
        }
    }

    /// <summary>외부에서 강제로 즉시 갱신이 필요할 때 호출한다.</summary>
    public void ForceRefresh() => NotifyInventoryChanged();

    public void NotifyInventoryChanged() =>
        OnSnapshotChanged?.Invoke(Analyze());
}
