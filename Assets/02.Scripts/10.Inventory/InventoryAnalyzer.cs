using UnityEngine;

/// <summary>
/// 인벤토리 배치 아이템 분석기.
/// LateUpdate에서 그리드 상태를 폴링해 변화가 생기면 OnSnapshotChanged를 발행한다.
/// InventoryGridUI 등 배치 코드에 일절 손대지 않는다.
/// </summary>
public class InventoryAnalyzer : MonoBehaviour
{
    [SerializeField] private InventoryGrid _grid;

    /// <summary>인벤토리가 변경될 때마다 새 스냅샷과 함께 발행된다.</summary>
    public event System.Action<InventorySnapshot> OnSnapshotChanged;

    private int _lastItemCount = -1;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_grid == null) _grid = GetComponent<InventoryGrid>();
    }

    private void Start()
    {
        // 씬 시작 시 초기 스냅샷 전파
        ForceRefresh();
    }

    private void LateUpdate()
    {
        if (_grid == null) return;

        int count = CountPlaced();
        if (count == _lastItemCount) return;

        _lastItemCount = count;
        NotifyInventoryChanged();
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>현재 그리드 배치 상태를 분석해 스냅샷을 반환한다.</summary>
    public InventorySnapshot Analyze()
    {
        var snapshot = new InventorySnapshot();
        if (_grid == null) return snapshot;
        foreach (var inst in _grid.GetAllPlacedInstances())
            snapshot.Add(inst);
        return snapshot;
    }

    /// <summary>외부에서 강제로 갱신이 필요할 때 호출한다.</summary>
    public void ForceRefresh()
    {
        _lastItemCount = CountPlaced();
        NotifyInventoryChanged();
    }

    public void NotifyInventoryChanged() =>
        OnSnapshotChanged?.Invoke(Analyze());

    private int CountPlaced()
    {
        if (_grid == null) return 0;
        int n = 0;
        foreach (var _ in _grid.GetAllPlacedInstances()) n++;
        return n;
    }
}
