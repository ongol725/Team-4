using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상 및 설정")]
    public Transform target;       // 따라다닐 플레이어
    public float smoothSpeed = 5f; // 카메라가 따라가는 속도 (클수록 빠름)
    public Vector3 offset = new Vector3(0, 0, -10f); // 카메라와 플레이어의 거리 (Z축 -10 유지 필수)

    void LateUpdate()
    {
        // 타겟(플레이어)이 비어있지 않다면 실행
        if (target != null)
        {
            // 카메라가 가야 할 목표 위치
            Vector3 desiredPosition = target.position + offset;
            
            // 현재 위치에서 목표 위치로 부드럽게(Lerp) 이동
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }
    }
}