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
        // CharacterManager에서 선택된 캐릭터를 직접 읽음 (PlayerStats 미연결 / 적용 실패 시 폴백)
        SO_CharacterData charData = CharacterManager.Instance?.SelectedCharacter;

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
            // PlayerStats 미연결 씬에서도 캐릭터 데이터 표시
            Apply(charData.maxHp, charData.maxHp,
                  charData.attackMultiplier, charData.attackSpeedMultiplier,
                  charData.moveSpeed, charData.critChance);
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
