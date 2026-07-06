using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using BagSurvivor.Monster; // RoomMonsterSpawner (디스폰/재스폰 연동)

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
    [HideInInspector] public DoorController door;                                   // 특수방: 단일 입구 문
    [HideInInspector] public List<DoorController> doors = new List<DoorController>(); // 일반방: 복도 개구부마다 1개
    [HideInInspector] public GameObject stairs;

    // 복도로 직접 연결된 방들 (DungeonPopulator에서 주입) — 스폰 밴드(near/far) 판정용
    [HideInInspector] public List<RoomController> connectedRooms = new List<RoomController>();

    [Header("문 잠금 안전 여백")]
    [Tooltip("문이 닫힐 때 밀려나지 않도록, 플레이어가 방 가장자리에서 이 거리만큼 안쪽에 들어와야 잠금")]
    public float safeInnerMargin = 2.5f;

    private bool activated = false;     // 방이 실제로 활성화(스폰+잠금)되었는가 (1회만)
    private bool playerInside = false;  // 플레이어가 현재 트리거 안에 있는가
    private Transform playerTf;         // 진입한 플레이어 (안쪽 진입 판정용)
    private Collider2D roomCol;         // 방 트리거 콜라이더 (월드 bounds 판정용)
    private Coroutine confirmCo;        // 특수방 진입 확인 대기 코루틴
    private Coroutine guardCo;          // 잠금 후 플레이어 존재 가드 코루틴
    private bool doorClosed = false;    // 현재 문이 닫혀(잠겨) 있는가
    private bool roomCleared = false;   // 방 클리어(영구 개방)되었는가
    private int monsterCount = 0;

    // 전투 잠금 방: 진입 확인 후 문을 잠그고 킬수 클리어를 진행하는 방 (시작/상점 제외)
    private bool IsCombatRoom =>
        roomType == RoomType.Normal ||
        roomType == RoomType.Elite ||
        roomType == RoomType.MiniBoss ||
        roomType == RoomType.Boss;

    /// <summary>방이 클리어(영구 개방·비전투)되었는가. 스폰러/이벤트 게이트용.</summary>
    public bool IsCleared => roomCleared;

    private bool HasDoors => door != null || doors.Count > 0;

    // 이 방의 모든 문(특수방 단일 + 일반방 다중)을 일괄 개폐
    private void SetDoorsClosed(bool closed)
    {
        if (door != null) { if (closed) door.Close(); else door.Open(); }
        for (int i = 0; i < doors.Count; i++)
        {
            var d = doors[i];
            if (d == null) continue;
            if (closed) d.Close(); else d.Open();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        // 방 진입 시 0.5초 무적 — 바로 앞에 스폰된 몹과 충돌해 즉시 피해받는 것 방지
        var ph = collision.GetComponentInParent<PlayerHealth>();
        if (ph != null) ph.GrantInvincibility(0.5f);

        playerInside = true;
        playerTf = collision.transform;
        if (activated) return; // 이미 활성화(클리어 포함)된 방은 무시 — 클리어 방 재진입은 영구 비전투

        // 비전투 방(시작/상점): 즉시 1회 진입 통지만
        if (!IsCombatRoom)
        {
            activated = true;
            Debug.Log($"[{roomType}] 방에 플레이어가 진입했습니다! (크기: {roomBounds.width}x{roomBounds.height})");
            OnPlayerEnterRoom?.Invoke();
            return;
        }

        // 전투방(일반 포함): 충분히 안쪽으로 들어왔을 때만 스폰+잠금 (잠깐 밟고 나가면 미활성)
        if (confirmCo == null)
            confirmCo = StartCoroutine(ConfirmEntry());
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;
        playerInside = false;

        // 방을 떠날 때 바닥에 남은 골드를 플레이어에게 빨려가듯 날려 획득(확실한 피드백)
        BagSurvivor.Items.GoldDropManager.Instance?.CollectAllDropped(collision.transform);

        // 활성화 전(확인 대기 중)에 나가면 취소 → 잠그지 않고 재진입 가능
        if (!activated && confirmCo != null)
        {
            StopCoroutine(confirmCo);
            confirmCo = null;
        }
    }

    private IEnumerator ConfirmEntry()
    {
        // 플레이어가 방 가장자리(문)에서 충분히 안쪽으로 들어올 때까지 대기.
        //  - 도중에 나가면(playerInside=false) 취소 → 잠그지 않음(재진입 가능).
        //  - 안쪽 깊이 들어온 뒤 닫으므로, 닫히는 문 콜라이더에 밀려나지 않는다.
        while (true)
        {
            if (!playerInside) { confirmCo = null; yield break; }
            if (IsPlayerWellInside()) break;
            yield return null;
        }
        confirmCo = null;

        activated = true;
        Debug.Log($"[{roomType}] 방에 플레이어가 진입했습니다! (크기: {roomBounds.width}x{roomBounds.height})");
        OnPlayerEnterRoom?.Invoke(); // 스폰(일반방은 총량 선등록 포함)

        SetDoorsClosed(true);
        doorClosed = true;
        if (HasDoors) Debug.Log($"[{roomType}] 문 잠금");

        // 몬스터가 등록되지 않은 상태면 (스폰 규칙 없는 층 등) 즉시 클리어
        if (monsterCount <= 0)
            ClearRoom();
        else
            guardCo = StartCoroutine(PresenceGuard()); // 잠금 후 존재 가드 시작
    }

    /// <summary>잠금 후 안전장치(히스테리시스):
    ///  - 닫힘 중 플레이어가 방 콜라이더를 '완전히' 벗어나면(밀려남 등) → 재개방(자가 치유)
    ///  - 다시 '충분히 안쪽'으로 들어오고 몬스터가 남아있으면 → 재잠금
    /// 잠금 기준(well-inside)과 해제 기준(콜라이더 완전 이탈)을 다르게 둬 문턱 떨림을 방지.</summary>
    private IEnumerator PresenceGuard()
    {
        var wait = new WaitForSeconds(0.1f);
        while (!roomCleared)
        {
            bool inside = IsPlayerInsideCollider();

            if (doorClosed && !inside)
            {
                // 잠겼는데 플레이어가 완전히 밖 → 재개방 + 몬스터 디스폰(따라 나오지 못하게)
                SetDoorsClosed(false);
                doorClosed = false;
                DespawnRoomMonsters();
                Debug.Log($"[{roomType}] 플레이어가 밖에 있어 문 임시 개방 + 몬스터 디스폰");
            }
            else if (!doorClosed && IsPlayerWellInside())
            {
                // 다시 깊이 들어옴 → 재잠금 + 재스폰(처음부터, 일반방은 총량 재추첨)
                SetDoorsClosed(true);
                doorClosed = true;
                RespawnRoomMonsters();
                Debug.Log($"[{roomType}] 플레이어 재진입 → 문 재잠금 + 재스폰");
            }

            yield return wait;
        }
        guardCo = null;
    }

    /// <summary>플레이어가 방 트리거 콜라이더 안에 (여백 없이) 있는지. 해제 판정용.</summary>
    private bool IsPlayerInsideCollider()
    {
        if (playerTf == null) return false;
        if (roomCol == null) roomCol = GetComponent<Collider2D>();
        if (roomCol == null) return false;
        return roomCol.OverlapPoint(playerTf.position);
    }

    /// <summary>이 방 몬스터를 풀로 회수(문 밖으로 따라 나오지 못하게). 카운트도 0으로 초기화.</summary>
    private void DespawnRoomMonsters()
    {
        // 특수방에 있을 땐 스폰러의 활성 목록 = 이 방 몬스터뿐(일반방 몬스터는 진입 시 이미 디스폰됨)
        if (RoomMonsterSpawner.Instance != null)
            RoomMonsterSpawner.Instance.DespawnAllMonsters();
        monsterCount = 0; // 디스폰은 사망 콜백을 안 거치므로 직접 초기화
    }

    /// <summary>재진입 시 이 방을 처음부터 다시 스폰(스폰러가 OnPlayerEnterRoom을 듣고 SpawnForRoom 실행).</summary>
    private void RespawnRoomMonsters()
    {
        monsterCount = 0;
        OnPlayerEnterRoom?.Invoke();
    }

    /// <summary>플레이어가 방 가장자리에서 safeInnerMargin 이상 안쪽에 있는지(문에 안 걸침).
    /// 좌표계 불일치 방지를 위해 진입 감지에 쓰는 콜라이더의 '월드 bounds'로 판정.</summary>
    private bool IsPlayerWellInside()
    {
        if (playerTf == null) return false;
        if (roomCol == null) roomCol = GetComponent<Collider2D>();
        if (roomCol == null) return false;

        Bounds b = roomCol.bounds; // 월드 좌표 AABB
        // 작은 방에서도 안쪽 영역이 남도록 여백을 방 크기에 맞춰 보정
        float mx = Mathf.Max(0f, Mathf.Min(safeInnerMargin, b.extents.x - 0.5f));
        float my = Mathf.Max(0f, Mathf.Min(safeInnerMargin, b.extents.y - 0.5f));

        Vector2 p = playerTf.position;
        return p.x > b.min.x + mx && p.x < b.max.x - mx
            && p.y > b.min.y + my && p.y < b.max.y - my;
    }

    // 몬스터 스폰 시 호출
    public void RegisterMonster()
    {
        monsterCount++;
    }

    /// <summary>스폰 예정 총량 선등록(일반방 갇힘 전투용).
    /// 미스폰 몬스터까지 포함해 카운트해 두어, 웨이브 사이 일시 전멸로 문이 일찍 열리지 않게 한다.</summary>
    public void RegisterMonsters(int count)
    {
        if (count > 0) monsterCount += count;
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
        roomCleared = true;          // 존재 가드 종료 신호
        doorClosed = false;
        SetDoorsClosed(false);       // 모든 문 영구 개방
        if (stairs != null) stairs.SetActive(true);

        // 메타 업그레이드(전장의 회복): 방 클리어 시 최대 HP의 1/3/6/10% 회복
        float healPct = MetaUpgrades.ClearHealPct;
        if (healPct > 0f && playerTf != null)
        {
            var ph = playerTf.GetComponentInParent<PlayerHealth>();
            if (ph != null && !ph.IsDead)
            {
                int heal = Mathf.Max(1, Mathf.RoundToInt(ph.MaxHP * healPct));
                ph.Heal(heal);
                Debug.Log($"[Meta] 방 클리어 회복 +{heal} HP");
            }
        }

        Debug.Log($"[{roomType}] 방 클리어!");
        OnRoomCleared?.Invoke();
    }
}
