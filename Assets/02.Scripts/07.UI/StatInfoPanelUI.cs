using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가방 무기 스탯 패널.
///  - 가방 공격력  : 가방 안 무기 공격력 총합 (WPN_ATK_SUM)
///  - 무기 평균 공격력 : 가방 안 무기 공격력 평균 (WPN_ATK_AVG)
///  - 배치 무기 개수 : 가방에 배치된 무기 수
/// 값 텍스트 3개는 기존 씬 연결(_attackPowerText/_attackSpeedText/_moveSpeedText)을 재활용한다.
/// </summary>
public class StatInfoPanelUI : MonoBehaviour
{
    [Header("데이터 소스 (비우면 자동 탐색)")]
    [SerializeField] private BattleLoadoutBuilder _loadoutBuilder;
    [SerializeField] private InventoryAnalyzer _analyzer;

    [Header("값 텍스트 (기존 행 재활용)")]
    [SerializeField] private Text _attackPowerText;  // 가방 공격력(합)
    [SerializeField] private Text _attackSpeedText;  // 무기 평균 공격력
    [SerializeField] private Text _moveSpeedText;    // 배치 무기 개수

    // 씬 연결 유지용(미사용) — 제거 시 인스펙터 참조가 끊기므로 남겨둔다.
    [SerializeField] private Text _hpText;
    [SerializeField] private Text _dpsText;
    [SerializeField] private Text _critChanceText;
    [SerializeField] private Text _hpRegenText;
    [SerializeField] private PlayerStats _playerStats;

    private void OnEnable()
    {
        if (_loadoutBuilder == null) _loadoutBuilder = FindFirstObjectByType<BattleLoadoutBuilder>();
        if (_loadoutBuilder != null) _loadoutBuilder.OnLoadoutReady += OnLoadout;

        if (_analyzer == null) _analyzer = FindFirstObjectByType<InventoryAnalyzer>();
        if (_analyzer != null) _analyzer.OnSnapshotChanged += OnSnapshot;

        Refresh();
    }

    private void OnDisable()
    {
        if (_loadoutBuilder != null) _loadoutBuilder.OnLoadoutReady -= OnLoadout;
        if (_analyzer != null) _analyzer.OnSnapshotChanged -= OnSnapshot;
    }

    private void OnLoadout(BattleLoadout _) => Refresh();
    private void OnSnapshot(InventorySnapshot _) => Refresh();

    /// <summary>가방 무기 스탯을 다시 계산해 표시한다.</summary>
    public void Refresh()
    {
        var lo  = _loadoutBuilder != null ? _loadoutBuilder.Build() : null;
        int sum = lo != null ? lo.GetScaledBase(ScalingStatType.WPN_ATK_SUM) : 0;
        int avg = lo != null ? lo.GetScaledBase(ScalingStatType.WPN_ATK_AVG) : 0;
        int cnt = lo != null ? lo.Weapons.Count : 0;

        if (_attackPowerText != null) _attackPowerText.text = sum.ToString();
        if (_attackSpeedText != null) _attackSpeedText.text = avg.ToString();
        if (_moveSpeedText   != null) _moveSpeedText.text   = cnt.ToString();
    }
}
