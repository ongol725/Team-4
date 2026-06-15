using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
public class RoomController : MonoBehaviour
{
    [Header("방 설정")]
    public RoomType roomType;
    public RectInt roomBounds;

    [Header("이벤트 (외부 연결용)")]
    [Tooltip("플레이어가 방에 처음 진입했을 때 호출됩니다.")]
    public UnityEvent OnPlayerEnterRoom;
    
    [Tooltip("방의 몬스터가 모두 죽거나 클리어되었을 때 호출됩니다.")]
    public UnityEvent OnRoomCleared;

    private bool hasEntered = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 이미 한 번 진입했으면 무시 (필요시 제거 가능)
        if (hasEntered) return;

        // Player 태그를 가진 오브젝트가 닿았는지 확인
        if (collision.CompareTag("Player"))
        {
            hasEntered = true;
            Debug.Log($"[{roomType}] 방에 플레이어가 진입했습니다! (크기: {roomBounds.width}x{roomBounds.height})");
            
            // 다른 개발자분이 만들어둔 몬스터 스폰 스크립트를 이 이벤트에 연결하여 사용하시면 됩니다.
            OnPlayerEnterRoom?.Invoke();
        }
    }
}
