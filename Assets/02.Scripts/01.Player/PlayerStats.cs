using UnityEngine;

/// <summary>
/// 캐릭터 기본 스탯 데이터.
/// Player GameObject에 부착하며, 전투 시스템·UI가 이 컴포넌트를 참조한다.
///
/// ▶ 인벤토리 무기와의 관계
///   FinalAttackPower  = attackPower  × weapon.attackPower
///   FinalAttackSpeed  = attackSpeed  × weapon.attackSpeed
///
/// ▶ 인벤토리 방어구 연동
///   GameManager.onLoadoutReady 구독 → hpBonus/hpRegen 자동 갱신
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("기본 스탯")]
    public int   maxHp        = 100;
    public int   attackPower  = 10;
    public float attackSpeed  = 1.0f;
    public float moveSpeed    = 5.0f;
    [Range(0f, 1f)]
    public float critChance   = 0.05f;

    [Header("방어구 보너스 (인벤토리 연동, 런타임 갱신)")]
    [HideInInspector] public int hpBonus;   // 방어구 합산 체력 보너스
    [HideInInspector] public int hpRegen;   // 10초당 체력 재생

    /// <summary>런타임 현재 HP</summary>
    public int CurrentHp { get; private set; }

    /// <summary>스탯(HP 포함)이 바뀔 때마다 발행 — StatInfoPanelUI 등이 구독한다.</summary>
    public event System.Action OnStatsChanged;

    private int         _baseMaxHp;
    private GameManager _gameManager;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _baseMaxHp = maxHp;
        CurrentHp  = maxHp;
    }

    private void Start()
    {
        _gameManager = GameManager.Instance;
        if (_gameManager != null)
            _gameManager.onLoadoutReady += OnLoadoutReady;
    }

    private void OnDestroy()
    {
        if (_gameManager != null)
            _gameManager.onLoadoutReady -= OnLoadoutReady;
    }

    // ─────────────────────────────────────────────────────────────

    private void OnLoadoutReady(BattleLoadout loadout)
    {
        hpBonus = loadout.TotalHpBonus;
        hpRegen = loadout.TotalHpRegen;
        maxHp   = _baseMaxHp + hpBonus;
        CurrentHp = Mathf.Clamp(CurrentHp, 0, maxHp);
        OnStatsChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────

    public void TakeDamage(int amount)
    {
        CurrentHp = Mathf.Max(0, CurrentHp - amount);
        OnStatsChanged?.Invoke();
    }

    public void Heal(int amount)
    {
        CurrentHp = Mathf.Min(maxHp, CurrentHp + amount);
        OnStatsChanged?.Invoke();
    }
}
