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
    private bool _invincible;            // true면 데미지 무시 (방 진입 무적 등)
    private Coroutine _invincibleCo;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private Coroutine flashCoroutine;

    // 방어구 HP/재생 자체 적용 (전투 씬에 PlayerStats가 없을 때 PlayerHealth가 직접 브릿지)
    private int         _baseMaxHP;       // 캐릭터 기본 최대체력(방어구 보너스 제외)
    private bool        _selfBridge;      // PlayerStats가 없으면 true → 직접 로드아웃 반영
    private GameManager _gm;
    private Coroutine   _regenCoroutine;
    private int         _currentRegen = 0;   // 현재 적용 중인 10초당 재생량
    private bool        _inCombat     = false; // 전투 구역 내 여부 — 비전투 시 재생 정지

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public bool IsDead => isDead;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;

        // 발밑 그림자 자동 부착 (플레이어 전용 폭 배수 적용)
        var shadow = GetComponent<BlobShadow>();
        if (shadow == null) shadow = gameObject.AddComponent<BlobShadow>();
        shadow.MarkAsPlayer();

        // PlayerStats 없이도 캐릭터 maxHp 반영 (미선택/씬에 CharacterManager 없으면 전사 폴백)
        var charData = CharacterManager.GetSelectedOrDefault();
        if (charData != null) maxHP = charData.maxHp;

        _baseMaxHP  = maxHP;
        // PlayerStats가 같은 GameObject에 있으면 그쪽이 방어구 HP를 처리 → 중복 방지
        _selfBridge = GetComponent<PlayerStats>() == null;
    }

    private void Start()
    {
        if (hud == null) hud = FindFirstObjectByType<PlayerStateHUD>();
        if (resultPopup == null) resultPopup = FindFirstObjectByType<ResultPopup>();

        currentHP = maxHP;
        isDead = false;

        // 전투 구역 진입/이탈 추적 — 비전투 상태에서는 체력 재생을 멈춘다.
        CombatZone.onCombatStateChanged += OnCombatStateChanged;

        // PlayerStats가 없으면 PlayerHealth가 직접 방어구 HP/재생을 반영한다.
        if (_selfBridge)
        {
            _gm = GameManager.Instance;
            if (_gm != null)
            {
                _gm.onLoadoutReady += OnLoadoutReady;
                if (_gm.CurrentLoadout != null) OnLoadoutReady(_gm.CurrentLoadout);
            }
        }

        // 모든 Start() 완료 후 갱신 — PlayerStateHUD.Start()의 mock 값보다 늦게 실행 보장
        StartCoroutine(InitHudLate());
    }

    private void OnDestroy()
    {
        if (_gm != null) _gm.onLoadoutReady -= OnLoadoutReady;
        CombatZone.onCombatStateChanged -= OnCombatStateChanged;
        if (_regenCoroutine != null) StopCoroutine(_regenCoroutine);
    }

    private void OnCombatStateChanged(bool inCombat) => _inCombat = inCombat;

    /// <summary>전투 로드아웃의 방어구 HP 보너스/재생을 체력에 반영한다(PlayerStats 부재 시).</summary>
    private void OnLoadoutReady(BattleLoadout loadout)
    {
        if (loadout == null) return;

        int newMax = Mathf.Max(1, _baseMaxHP + loadout.TotalHpBonus);
        // 현재 체력 "비율"을 유지하며 최대체력을 변경한다.
        // (장착/해제를 반복해도 풀피로 회복되지 않게 — 재장착 익스플로잇 방지)
        float ratio = maxHP > 0 ? (float)currentHP / maxHP : 1f;
        maxHP = newMax;
        int newCur = Mathf.RoundToInt(newMax * ratio);
        if (currentHP > 0 && newCur < 1) newCur = 1; // 살아있으면 최소 1 보장
        currentHP = Mathf.Clamp(newCur, 0, maxHP);
        UpdateHud();

        // 재생량이 바뀐 경우에만 루프를 재시작한다.
        // (실시간 전달로 OnLoadoutReady가 자주 호출돼도 10초 타이머가 리셋되지 않도록)
        if (loadout.TotalHpRegen != _currentRegen)
        {
            _currentRegen = loadout.TotalHpRegen;
            if (_regenCoroutine != null) { StopCoroutine(_regenCoroutine); _regenCoroutine = null; }
            if (_currentRegen > 0)
                _regenCoroutine = StartCoroutine(RegenLoop());
        }
    }

    // 10초마다 _currentRegen만큼 회복 (재생량은 OnLoadoutReady에서 갱신)
    private IEnumerator RegenLoop()
    {
        var wait = new WaitForSeconds(10f);
        while (!isDead && _currentRegen > 0)
        {
            yield return wait;
            if (_inCombat) Heal(_currentRegen); // 전투 상태에서만 재생
        }
        _regenCoroutine = null;
    }

    private System.Collections.IEnumerator InitHudLate()
    {
        yield return null;
        if (hud == null) hud = FindFirstObjectByType<PlayerStateHUD>();
        UpdateHud();
    }

    /// <summary>지정 시간 동안 무적 부여 (방 진입 시 바로 앞 스폰 몹 접촉 방지용).</summary>
    public void GrantInvincibility(float seconds)
    {
        if (seconds <= 0f) return;
        if (_invincibleCo != null) StopCoroutine(_invincibleCo);
        _invincibleCo = StartCoroutine(InvincibilityRoutine(seconds));
    }

    private IEnumerator InvincibilityRoutine(float seconds)
    {
        _invincible = true;
        yield return new WaitForSeconds(seconds);
        _invincible = false;
        _invincibleCo = null;
    }

    /// <summary>데미지를 받습니다. (몬스터 접촉 등에서 호출)</summary>
    public void TakeDamage(int amount)
    {
        if (isDead || _invincible || amount <= 0) return;

        if (DamageReductionPct > 0f)
        {
            float clampedReduction = Mathf.Clamp01(DamageReductionPct);
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - clampedReduction)));
        }

        currentHP = Mathf.Max(0, currentHP - amount);

        // 플레이어 머리 위에 받은 데미지 표시 (연한 빨강으로 구분, 몬스터와 동일 풀 재사용)
        BagSurvivor.DamagePopup.Show(transform.position, amount, false, new Color(1f, 0.55f, 0.55f));

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
