using UnityEngine;
using UnityEngine.InputSystem; 
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 5f;

    // 인스펙터에서 직접 액션을 꽂아넣을 구멍을 뚫어줍니다.
    public InputActionReference moveAction; 

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isStunned = false;
    private bool _inventoryOpen = false;
    private Vector2 _prevPosition;

    /// <summary>이동 거리(m)를 인자로 발행. SynergyManager 대부호 트리거 구독용.</summary>
    public event System.Action<float> onDistanceMoved;

    /// <summary>이동속도 배율. 과부화 패널티 등에서 일시 변경.</summary>
    public float speedMultiplier = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // 카메라 추적 시 떨림(지터) 방지: 물리 스텝 사이를 부드럽게 보간
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _prevPosition = rb.position;
        }
    }

    void Start()
    {
        if (moveAction != null) moveAction.action.Enable();

        InventoryPopupToggle.onPopupToggled += OnInventoryToggled;
    }

    void OnDestroy()
    {
        InventoryPopupToggle.onPopupToggled -= OnInventoryToggled;
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
    }

    void FixedUpdate()
    {
        if (isStunned || _inventoryOpen)
        {
            rb.linearVelocity = Vector2.zero;
            _prevPosition = rb.position;
            return;
        }

        rb.linearVelocity = moveInput * moveSpeed * speedMultiplier;

        float dist = ((Vector2)rb.position - _prevPosition).magnitude;
        // 한 물리 스텝의 정상 보행 한계(여유 4배). 이를 넘는 변위는 층 전환 등 순간이동으로 간주하여
        // 이동 거리 이벤트에서 제외한다(대부호 코인이 한꺼번에 쏟아지는 버그 방지).
        float maxWalkStep = moveSpeed * speedMultiplier * Time.fixedDeltaTime * 4f;
        if (dist > 0f && dist <= maxWalkStep) onDistanceMoved?.Invoke(dist);
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