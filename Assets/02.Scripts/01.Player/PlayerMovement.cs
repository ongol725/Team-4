using UnityEngine;
using UnityEngine.InputSystem; 
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 6f;

    // 인스펙터에서 직접 액션을 꽂아넣을 구멍을 뚫어줍니다.
    public InputActionReference moveAction; 

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isStunned = false;

    /// <summary>현재 이동 입력(-1~1). 방향 애니메이션 등에서 참조.</summary>
    public Vector2 MoveInput => moveInput;

    /// <summary>대시 스태미너 충전률(0~1). 1이면 대시 가능. DashStaminaUI 표시용.</summary>
    public float DashCharge01 => dashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(_dashCd / dashCooldown);
    private bool _inventoryOpen = false;
    private Vector2 _prevPosition;

    /// <summary>이동 거리(m)를 인자로 발행. SynergyManager 대부호 트리거 구독용.</summary>
    public event System.Action<float> onDistanceMoved;

    /// <summary>이동속도 배율. 과부하 패널티 등에서 일시 변경.</summary>
    public float speedMultiplier = 1f;

    /// <summary>임시칸 과적 이동속도 배율(로드아웃 확정 시 반영, 1=무패널티).</summary>
    private float _loadoutMoveMult = 1f;
    private GameManager _gm;

    [Header("대시 (스페이스)")]
    public float dashSpeed        = 22f;   // 대시 속도
    public float dashDuration     = 0.15f; // 대시 지속(초)
    public float dashCooldown     = 2.0f;  // 재사용 대기(초)
    public float afterimageInterval = 0.03f; // 잔상 생성 간격
    public Color afterimageColor  = new Color(0.6f, 0.9f, 1f, 0.5f); // 잔상 색

    private Vector2 _lastDir = Vector2.right; // 마지막 바라본 방향(정지 시 대시 방향)
    private Vector2 _dashDir;
    private float _dashTimer, _dashCd, _afterimgTimer;
    private bool  _isDashing;
    public float dashInvincibleAfter = 0.5f; // 대시 종료 후 추가 무적(초)
    private SpriteRenderer _sr; // 잔상 복제용 캐릭터 스프라이트
    private PlayerHealth   _health;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        _sr = GetComponentInChildren<SpriteRenderer>();
        _health = GetComponent<PlayerHealth>();
        // 카메라 추적 시 떨림(지터) 방지: 물리 스텝 사이를 부드럽게 보간
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            // 대시 등 고속 이동 시 얇은 벽을 통과(터널링)하지 않도록 스윕 충돌 감지 사용
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _prevPosition = rb.position;
        }
    }

    void Start()
    {
        if (moveAction != null) moveAction.action.Enable();

        // 런 시작 랜덤 이동속도 배율(로비 미경유 시 1). base moveSpeed와 곱연산이라 이중 적용 없음.
        speedMultiplier *= RunStartStats.MoveSpeedMul;

        // 머리 위 대시 스태미너 원형 게이지 자동 부착(씬/프리팹 수정 없음 — 작업 충돌 방지)
        if (GetComponent<DashStaminaUI>() == null) gameObject.AddComponent<DashStaminaUI>();

        InventoryPopupToggle.onPopupToggled += OnInventoryToggled;

        // 임시칸 과적 이동속도 페널티: 로드아웃 확정 시 반영
        _gm = GameManager.Instance;
        if (_gm != null)
        {
            _gm.onLoadoutReady += OnLoadoutReady;
            if (_gm.CurrentLoadout != null) OnLoadoutReady(_gm.CurrentLoadout);
        }
    }

    void OnDestroy()
    {
        InventoryPopupToggle.onPopupToggled -= OnInventoryToggled;
        if (_gm != null) _gm.onLoadoutReady -= OnLoadoutReady;
    }

    private void OnLoadoutReady(BattleLoadout loadout)
    {
        _loadoutMoveMult = loadout != null ? loadout.tempMoveMult : 1f;
    }

    private void OnInventoryToggled(bool isOpen)
    {
        _inventoryOpen = isOpen;
        if (isOpen) rb.linearVelocity = Vector2.zero;
    }

    void Update()
    {
        // OnMove 함수를 지우고, 매 프레임 직접 키보드 값을 빼옵니다.
        if (moveAction != null)
        {
            moveInput = moveAction.action.ReadValue<Vector2>();
        }

        // 튜토리얼 팝업 표시 중에는 이동 입력 무시
        if (BagSurvivor.UI.TutorialController.IsBlocking) moveInput = Vector2.zero;

        // 바라보는 방향 추적(정지 시 마지막 방향 유지)
        if (moveInput.sqrMagnitude > 0.01f) _lastDir = moveInput.normalized;

        // 대시 쿨다운
        if (_dashCd > 0f) _dashCd -= Time.deltaTime;

        // 스페이스 → 대시 (인벤 열림·튜토리얼·스턴·쿨다운 중 제외)
        if (!_inventoryOpen && !BagSurvivor.UI.TutorialController.IsBlocking
            && !isStunned && !_isDashing && _dashCd <= 0f
            && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StartDash();
        }

        // 대시 중 잔상 생성
        if (_isDashing)
        {
            _afterimgTimer -= Time.deltaTime;
            if (_afterimgTimer <= 0f) { SpawnAfterimage(); _afterimgTimer = afterimageInterval; }
        }
    }

    private void StartDash()
    {
        _isDashing     = true;
        _dashTimer     = dashDuration;
        _dashCd        = dashCooldown;
        _dashDir       = _lastDir.sqrMagnitude > 0.01f ? _lastDir.normalized : Vector2.right;
        _afterimgTimer = 0f;

        // 대시 중 + 종료 후 0.5초 무적(적과 부딪혀도 피해 없음)
        if (_health != null) _health.GrantInvincibility(dashDuration + dashInvincibleAfter);

        SpawnAfterimage(); // 시작 즉시 하나
    }

    // 현재 캐릭터 스프라이트를 반투명 복제해 잔상 생성 → 서서히 사라지고 소멸
    private void SpawnAfterimage()
    {
        if (_sr == null || _sr.sprite == null) return;

        var go = new GameObject("Afterimage");
        go.transform.position   = _sr.transform.position;
        go.transform.rotation   = _sr.transform.rotation;
        go.transform.localScale = _sr.transform.lossyScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite          = _sr.sprite;
        sr.flipX           = _sr.flipX;
        sr.color           = afterimageColor;
        sr.sortingLayerID  = _sr.sortingLayerID;
        sr.sortingOrder    = _sr.sortingOrder - 1; // 캐릭터 뒤에 깔림

        StartCoroutine(FadeGhost(sr));
    }

    private IEnumerator FadeGhost(SpriteRenderer sr)
    {
        const float life = 0.3f;
        float t = 0f;
        Color c0 = sr.color;
        while (t < life && sr != null)
        {
            t += Time.deltaTime;
            var c = c0; c.a = Mathf.Lerp(c0.a, 0f, t / life); sr.color = c;
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    void FixedUpdate()
    {
        if (isStunned || _inventoryOpen || BagSurvivor.UI.TutorialController.IsBlocking)
        {
            rb.linearVelocity = Vector2.zero;
            _prevPosition = rb.position;
            return;
        }

        // 대시 중: 바라본 방향으로 고속 이동(배율 무시), 거리 이벤트 제외
        if (_isDashing)
        {
            rb.linearVelocity = _dashDir * dashSpeed;
            _dashTimer -= Time.fixedDeltaTime;
            if (_dashTimer <= 0f) _isDashing = false;
            _prevPosition = rb.position;
            return;
        }

        rb.linearVelocity = moveInput * moveSpeed * speedMultiplier * _loadoutMoveMult;

        float dist = ((Vector2)rb.position - _prevPosition).magnitude;
        // 한 물리 스텝의 정상 보행 한계(여유 4배). 이를 넘는 변위는 층 전환 등 순간이동으로 간주하여
        // 이동 거리 이벤트에서 제외한다(대부호 코인이 한꺼번에 쏟아지는 버그 방지).
        float maxWalkStep = moveSpeed * speedMultiplier * _loadoutMoveMult * Time.fixedDeltaTime * 4f;
        if (dist > 0f && dist <= maxWalkStep)
        {
            onDistanceMoved?.Invoke(dist);
            RunStatsLogger.Instance?.AddDistance(dist);   // 런 통계 이동거리 누적
        }
        _prevPosition = rb.position;
    }

        // 스턴시 얼마동안 이동불가 / 시간이 끝나면 다시
    public IEnumerator ApplyStun(float duration)
    {
        isStunned = true; // 이동 불가 상태로 변경
        
        // duration(초) 만큼 시간 흐름을 대기
        yield return new WaitForSeconds(duration); 
        
        isStunned = false; // 대기 시간이 끝나면 다시 이동 가능 상태로 복구
    }
}