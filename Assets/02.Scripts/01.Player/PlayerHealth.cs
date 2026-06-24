// ============================================================
// PlayerHealth.cs
// 플레이어 체력 시스템
//  - 몬스터 접촉 데미지 등 외부에서 TakeDamage 호출
//  - Player_State HUD(체력바/텍스트) 자동 갱신
//  - 피격 시 스프라이트 빨강 깜빡(0.1초)
//  - 사망 시 결과(실패) 화면 표시 + 일시정지
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

    /// <summary>피해를 받을 때마다 발행 — 난공불락 시너지가 구독한다. 인자: 실제 받은 피해량</summary>
    public event System.Action<int> onDamageTaken;

    /// <summary>받는 피해 감소율 (0~1). 난공불락 시너지가 설정한다.</summary>
    [HideInInspector] public float DamageReductionPct = 0f;

    private int currentHP;
    private bool isDead;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private Coroutine flashCoroutine;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public bool IsDead => isDead;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;

        // PlayerStats 없이도 캐릭터 maxHp 반영
        var charData = CharacterManager.Instance?.SelectedCharacter;
        if (charData != null) maxHP = charData.maxHp;
    }

    private void Start()
    {
        if (hud == null) hud = FindFirstObjectByType<PlayerStateHUD>();
        if (resultPopup == null) resultPopup = FindFirstObjectByType<ResultPopup>();

        currentHP = maxHP;
        isDead = false;
        // 모든 Start() 완료 후 갱신 — PlayerStateHUD.Start()의 mock 값보다 늦게 실행 보장
        StartCoroutine(InitHudLate());
    }

    private System.Collections.IEnumerator InitHudLate()
    {
        yield return null;
        if (hud == null) hud = FindFirstObjectByType<PlayerStateHUD>();
        UpdateHud();
    }

    /// <summary>데미지를 받습니다. (몬스터 접촉 등에서 호출)</summary>
    public void TakeDamage(int amount)
    {
        if (isDead || amount <= 0) return;

        if (DamageReductionPct > 0f)
        {
            float clampedReduction = Mathf.Clamp01(DamageReductionPct);
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - clampedReduction)));
        }

        currentHP = Mathf.Max(0, currentHP - amount);
        onDamageTaken?.Invoke(amount);
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

        onPlayerDeath?.Invoke();

        // 결과(실패) 화면 표시
        if (resultPopup != null)
        {
            var stats = new ResultStats();
            stats.playTime = DifficultyScaler.Instance != null
                ? DifficultyScaler.Instance.ElapsedMinutes * 60f
                : Time.timeSinceLevelLoad;
            stats.mainSynergies = "-";
            stats.weaponCount = 0;
            stats.killCount = 0;
            stats.goldSpent = 0;
            resultPopup.Show(false, stats); // Show 내부에서 timeScale=0 처리
        }
        else
        {
            Time.timeScale = 0f;
            Debug.Log("[PlayerHealth] 사망 — ResultPopup 미연결(결과화면 없음)");
        }
    }
}
