using UnityEngine;
using UnityEngine.Events;
using System.Collections;

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

    // 문/계단 레퍼런스 (DungeonPopulator에서 주입)
    [HideInInspector] public DoorController door;
    [HideInInspector] public GameObject stairs;

    private bool hasEntered = false;
    private int monsterCount = 0;

    // 특수 방 여부: 문/계단 시스템이 작동하는 방
    private bool IsSpecialRoom =>
        roomType == RoomType.Elite ||
        roomType == RoomType.MiniBoss ||
        roomType == RoomType.Boss;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasEntered) return;
        if (!collision.CompareTag("Player")) return;

        hasEntered = true;
        Debug.Log($"[{roomType}] 방에 플레이어가 진입했습니다! (크기: {roomBounds.width}x{roomBounds.height})");

        OnPlayerEnterRoom?.Invoke();

        // 특수 방: 플레이어가 방 안쪽으로 이동한 뒤 문 잠금
        if (IsSpecialRoom && door != null)
            StartCoroutine(CloseDoorAfterEntry());
    }

    private IEnumerator CloseDoorAfterEntry()
    {
        // 플레이어 이동속도 5 기준: 0.5초면 2.5칸 이동 → 복도폭(2칸)을 벗어남
        yield return new WaitForSeconds(0.5f);
        door.Close();
        Debug.Log($"[{roomType}] 문 잠금");

        // 몬스터가 등록되지 않은 상태면 (아직 몬스터 시스템 미구현 등) 즉시 클리어
        if (monsterCount <= 0)
            ClearRoom();
    }

    // 몬스터 스폰 시 호출
    public void RegisterMonster()
    {
        monsterCount++;
    }

    // 몬스터 사망 시 호출
    public void NotifyMonsterDead()
    {
        if (monsterCount <= 0) return;
        monsterCount--;
        if (monsterCount <= 0)
            ClearRoom();
    }

    private void ClearRoom()
    {
        if (door != null) door.Open();
        if (stairs != null) stairs.SetActive(true);
        Debug.Log($"[{roomType}] 방 클리어!");
        OnRoomCleared?.Invoke();
    }
}
