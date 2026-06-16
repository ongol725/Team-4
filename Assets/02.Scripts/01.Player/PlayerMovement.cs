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

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // 카메라 추적 시 떨림(지터) 방지: 물리 스텝 사이를 부드럽게 보간
        if (rb != null) rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        if (moveAction != null) moveAction.action.Enable();
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
        //스턴시 이속 0으로
        if (isStunned)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = moveInput * moveSpeed;
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