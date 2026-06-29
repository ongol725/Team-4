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

    [Header("참조 (인벤토리 씬 — 방어구 HP 반영용. 비우면 자동 탐색)")]
    [SerializeField] private InventoryAnalyzer _analyzer;

    [Header("값 텍스트")]
    [SerializeField] private Text _hpText;
    [SerializeField] private Text _attackPowerText;
    [SerializeField] private Text _attackSpeedText;
    [SerializeField] private Text _moveSpeedText;
    [SerializeField] private Text _dpsText;          // 선택적 — 없어도 동작
    [SerializeField] private Text _critChanceText;
    [SerializeField] private Text _hpRegenText;      // 선택적 — 없어도 동작

    [Header("Mock 데이터 (PlayerStats 미연결 시 표시)")]
    [SerializeField] private bool  _useMock        = true;
    [SerializeField] private int   _mockMaxHp      = 100;
    [SerializeField] private float _mockAtkMul     = 1.0f;   // 캐릭터 공격 배율
    [SerializeField] private float _mockAtkSpdMul  = 1.0f;   // 캐릭터 공속 배율
    [SerializeField] private float _mockMoveSpeed  = 5.0f;
    [SerializeField, Range(0f, 1f)]
    private float _mockCritChance = 0.05f;

    // ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (_playerStats != null)
            _playerStats.OnStatsChanged += Refresh;

        // 인벤토리 씬: 방어구를 놓거나 뺄 때(스냅샷 변경) 패널을 갱신하도록 구독
        if (_analyzer == null) _analyzer = FindFirstObjectByType<InventoryAnalyzer>();
        if (_analyzer != null) _analyzer.OnSnapshotChanged += OnSnapshotChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (_playerStats != null)
            _playerStats.OnStatsChanged -= Refresh;
        if (_analyzer != null)
            _analyzer.OnSnapshotChanged -= OnSnapshotChanged;
    }

    private void OnSnapshotChanged(InventorySnapshot _) => Refresh();

    /// <summary>외부(InventoryAnalyzer.OnSnapshotChanged 등)에서 갱신 요청 시 호출한다.</summary>
    public void Refresh()
    {
        // 선택된 캐릭터(없으면 전사 폴백)를 읽음
        SO_CharacterData charData = CharacterManager.GetSelectedOrDefault();

        if (_playerStats != null)
        {
            // HP·재생은 PlayerStats(방어구 보너스 포함), 배율은 CharacterManager가 있으면 우선 사용
            float atkMul    = charData != null ? charData.attackMultiplier      : _playerStats.attackMultiplier;
            float atkSpdMul = charData != null ? charData.attackSpeedMultiplier : _playerStats.attackSpeedMultiplier;
            float moveSpd   = charData != null ? charData.moveSpeed             : _playerStats.moveSpeed;
            float crit      = charData != null ? charData.critChance            : _playerStats.critChance;

            Apply(_playerStats.CurrentHp, _playerStats.maxHp,
                  atkMul, atkSpdMul, moveSpd, crit, _playerStats.hpRegen);
        }
        else if (charData != null)
        {
            // PlayerStats 미연결 씬(인벤토리/상점): 캐릭터 기본 HP + 방어구 hpBonus를 합산해 표시
            var snap = _analyzer != null ? (_analyzer.LatestSnapshot ?? _analyzer.Analyze()) : null;
            int armorHp = snap != null ? snap.TotalHpBonus : 0;
            int regen   = snap != null ? snap.TotalHpRegen : 0;
            int maxHp   = charData.maxHp + armorHp;

            Apply(maxHp, maxHp,
                  charData.attackMultiplier, charData.attackSpeedMultiplier,
                  charData.moveSpeed, charData.critChance, regen);
        }
        else if (_useMock)
        {
            Apply(_mockMaxHp, _mockMaxHp,
                  _mockAtkMul, _mockAtkSpdMul,
                  _mockMoveSpeed, _mockCritChance);
        }
    }

    private void Apply(int hp, int maxHp, float atk, float atkSpd, float moveSpd, float crit, int hpRegen = 0)
    {
        float dps = atk * atkSpd * 10f;
        if (_hpText          != null) _hpText.text          = $"{hp} / {maxHp}";
        if (_attackPowerText != null) _attackPowerText.text  = $"×{atk:F1}";
        if (_attackSpeedText != null) _attackSpeedText.text  = $"×{atkSpd:F1}";
        if (_moveSpeedText   != null) _moveSpeedText.text    = moveSpd.ToString("F1");
        if (_dpsText         != null) _dpsText.text          = $"{dps:F1}";
        if (_critChanceText  != null) _critChanceText.text   = $"{crit * 100f:F1}%";
        if (_hpRegenText     != null) _hpRegenText.text      = hpRegen > 0 ? $"+{hpRegen}/10s" : "0";
    }
}
