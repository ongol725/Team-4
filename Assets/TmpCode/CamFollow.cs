using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상 및 설정")]
    public Transform target;       // 따라다닐 플레이어
    public float smoothSpeed = 5f; // 카메라가 따라가는 속도 (클수록 빠름)
    public Vector3 offset = new Vector3(0, 0, -10f); // 카메라와 플레이어의 거리 (Z축 -10 유지 필수)

    void Start()
    {
        // 타겟이 설정되지 않은 경우 자동으로 검색하여 할당
        if (target == null)
        {
            FindTargetPlayer();
        }

        // 2D 카메라의 Z축 오프셋 안전 검사 (Z가 0이면 화면이 보이지 않음)
        if (Mathf.Approximately(offset.z, 0f))
        {
            offset.z = -10f;
            Debug.LogWarning("[CameraFollow] Z축 오프셋이 0으로 설정되어 있어 자동으로 -10f로 조정했습니다.");
        }
    }

    void LateUpdate()
    {
        // 타겟이 아직도 비어있다면 다시 시도
        if (target == null)
        {
            FindTargetPlayer();
        }

        // 타겟(플레이어)이 비어있지 않다면 실행
        if (target != null)
        {
            // 카메라가 가야 할 목표 위치
            Vector3 desiredPosition = target.position + offset;
            
            // 현재 위치에서 목표 위치로 부드럽게(Lerp) 이동
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }
    }

    private void FindTargetPlayer()
    {
        // 1. "Player" 태그로 검색
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
            return;
        }

        // 2. PlayerMovement 컴포넌트로 검색 (태그가 지정되지 않은 경우 대비)
        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            target = playerMovement.transform;
        }
    }
}