// ============================================================
// PlayerHealth.cs
// 플레이어 체력 시스템
//  - 몬스터 접촉 데미지 등 외부에서 TakeDamage 호출
//  - Player_State HUD(체력바/텍스트) 자동 갱신
//  - 피격 시 스프라이트 빨강 깜빡(0.1초)
//  - 사망 시 결과(실패) 화면 표시 + 일시정지
//  - 인벤토리 방어구 연동: GameManager.onLoadoutReady 구독
//    → maxHP 갱신 + 10초당 HP 재생 코루틴 실행
// ============================================================
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using BagSurvivor.UI;
using BagSurvivor.Monster;

public class PlayerHealth : MonoBehaviour
{
    [Header("체력")]
    public int maxHP = 100;

    [Header("피격 연출")]
    [Tooltip("피격 시 깜빡일 색")]
    public Color hitFlashColor = Color.red;
    [Tooltip("깜빡임 지속 시간(초)")]
    public float hitFlashDuration = 0.1f;

    [Header("참조 (미지정 시 자동 검색)")]
    public PlayerStateHUD hud;
    public ResultPopup resultPopup;

    [Header("이벤트")]
    public UnityEvent onPlayerDeath;

    private int     currentHP;
    private bool    isDead;
    private SpriteRenderer spriteRenderer;
    private Color   baseColor = Color.white;
    private Coroutine flashCoroutine;

    private int         _baseMaxHp;
    private Coroutine   _regenCoroutine;
    private GameManager _gameManager;

    public int  CurrentHP => currentHP;
    public int  MaxHP     => maxHP;
    public bool IsDead    => isDead;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;

        _baseMaxHp = maxHP;
    }

    private void Start()
    {
        _gameManager = GameManager.Instance;
        if (_gameManager != null)
            _gameManager.onLoadoutReady += OnLoadoutReady;

        if (hud == null) hud = FindFirstObjectByType<PlayerStateHUD>();
        if (resultPopup == null) resultPopup = FindFirstObjectByType<ResultPopup>();

        currentHP = maxHP;
        isDead    = false;
        UpdateHud();
    }

    private void OnDestroy()
    {
        if (_gameManager != null)
            _gameManager.onLoadoutReady -= OnLoadoutReady;
    }

    // ─────────────────────────────────────────────────────────────
    // 인벤토리 방어구 연동

    private void OnLoadoutReady(BattleLoadout loadout)
    {
        maxHP     = _baseMaxHp + loadout.TotalHpBonus;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        UpdateHud();

        // 기존 재생 코루틴 교체
        if (_regenCoroutine != null) StopCoroutine(_regenCoroutine);
        if (loadout.TotalHpRegen > 0 && !isDead)
            _regenCoroutine = StartCoroutine(RegenLoop(loadout.TotalHpRegen));
    }

    private IEnumerator RegenLoop(int regenPerTick)
    {
        var wait = new WaitForSeconds(10f);
        while (!isDead)
        {
            yield return wait;
            Heal(regenPerTick);
        }
        _regenCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>데미지를 받습니다. (몬스터 접촉 등에서 호출)</summary>
    public void TakeDamage(int amount)
    {
        if (isDead || amount <= 0) return;

        currentHP = Mathf.Max(0, currentHP - amount);
        UpdateHud();

        if (spriteRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(HitFlash());
        }

        if (currentHP <= 0) Die();
    }

    /// <summary>체력을 회복합니다.</summary>
    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        UpdateHud();
    }

    private void UpdateHud()
    {
        if (hud != null) hud.SetHealth(currentHP, maxHP);
    }

    private IEnumerator HitFlash()
    {
        spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        if (!isDead) spriteRenderer.color = baseColor;
        flashCoroutine = null;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (_regenCoroutine != null)
        {
            StopCoroutine(_regenCoroutine);
            _regenCoroutine = null;
        }

        onPlayerDeath?.Invoke();

        // 결과(실패) 화면 표시
        if (resultPopup != null)
        {
            var stats = new ResultStats();
            stats.playTime = DifficultyScaler.Instance != null
                ? DifficultyScaler.Instance.ElapsedMinutes * 60f
                : Time.timeSinceLevelLoad;
            stats.mainSynergies = "-";
            stats.weaponCount   = 0;
            stats.killCount     = 0;
            stats.goldSpent     = 0;
            resultPopup.Show(false, stats);
        }
        else
        {
            Time.timeScale = 0f;
            Debug.Log("[PlayerHealth] 사망 — ResultPopup 미연결(결과화면 없음)");
        }
    }
}
