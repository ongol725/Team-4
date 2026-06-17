using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 스탯 정보 패널.
/// PlayerStats 컴포넌트를 참조해 기본 스탯 + 방어구 HP 재생을 표시한다.
/// PlayerStats가 연결되지 않은 씬(인벤토리 상점 등)에서는 Mock 데이터로 동작한다.
/// </summary>
public class StatInfoPanelUI : MonoBehaviour
{
    [Header("참조 (전투 씬에서 Player에 연결)")]
    [SerializeField] private PlayerStats _playerStats;

    [Header("값 텍스트")]
    [SerializeField] private Text _hpText;
    [SerializeField] private Text _attackPowerText;
    [SerializeField] private Text _attackSpeedText;
    [SerializeField] private Text _moveSpeedText;
    [SerializeField] private Text _critChanceText;
    [SerializeField] private Text _hpRegenText;     // 선택적 — 없어도 동작

    [Header("Mock 데이터 (PlayerStats 미연결 시 표시)")]
    [SerializeField] private bool  _useMock        = true;
    [SerializeField] private int   _mockMaxHp      = 100;
    [SerializeField] private int   _mockAttackPow  = 10;
    [SerializeField] private float _mockAtkSpeed   = 1.0f;
    [SerializeField] private float _mockMoveSpeed  = 5.0f;
    [SerializeField, Range(0f, 1f)]
    private float _mockCritChance = 0.05f;

    // ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (_playerStats != null)
            _playerStats.OnStatsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (_playerStats != null)
            _playerStats.OnStatsChanged -= Refresh;
    }

    /// <summary>외부(InventoryAnalyzer.OnSnapshotChanged 등)에서 갱신 요청 시 호출한다.</summary>
    public void Refresh()
    {
        if (_playerStats != null)
        {
            Apply(_playerStats.CurrentHp, _playerStats.maxHp,
                  _playerStats.attackPower, _playerStats.attackSpeed,
                  _playerStats.moveSpeed,   _playerStats.critChance,
                  _playerStats.hpRegen);
        }
        else if (_useMock)
        {
            Apply(_mockMaxHp, _mockMaxHp,
                  _mockAttackPow, _mockAtkSpeed,
                  _mockMoveSpeed, _mockCritChance);
        }
    }

    private void Apply(int hp, int maxHp, int atk, float atkSpd, float moveSpd, float crit, int hpRegen = 0)
    {
        if (_hpText          != null) _hpText.text          = $"{hp} / {maxHp}";
        if (_attackPowerText != null) _attackPowerText.text  = atk.ToString();
        if (_attackSpeedText != null) _attackSpeedText.text  = atkSpd.ToString("F2");
        if (_moveSpeedText   != null) _moveSpeedText.text    = moveSpd.ToString("F2");
        if (_critChanceText  != null) _critChanceText.text   = $"{crit * 100f:F1}%";
        if (_hpRegenText     != null) _hpRegenText.text      = hpRegen > 0 ? $"+{hpRegen}/10s" : "0";
    }
}
