using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던전 씬에서 RoomController 타입을 기반으로 전투/비전투 구역을 자동 전환한다.
///
/// 비전투 구역: 복도(어느 방에도 속하지 않음), 시작방(Start), 상점(Shop)
/// 전투  구역: 일반방(Normal), 엘리트(Elite), 미니보스(MiniBoss), 보스(Boss)
///
/// 사용법: 씬의 아무 오브젝트에 부착하면 자동 동작.
/// CombatZone.ForceSetCombat()으로 PlayerAttack, InventoryPopupToggle과 연동된다.
/// </summary>
public class DungeonCombatZone : MonoBehaviour
{
    private Transform              _player;
    private List<RoomController>   _cachedRooms = new List<RoomController>();
    private GameObject             _lastRoomsContainer;
    private bool                   _currentCombat;

    private float _checkTimer;
    private const float CHECK_INTERVAL = 0.1f;   // 초당 10회 체크 (매 프레임 불필요)

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        var go = GameObject.FindWithTag("Player");
        if (go != null) _player = go.transform;

        RefreshRooms();
    }

    private void Update()
    {
        _checkTimer += Time.deltaTime;
        if (_checkTimer < CHECK_INTERVAL) return;
        _checkTimer = 0f;

        // 던전 재생성 감지 → 방 목록 갱신
        var container = GameObject.Find("RoomControllers");
        if (container != _lastRoomsContainer)
        {
            _lastRoomsContainer = container;
            RefreshRooms();
        }

        UpdateCombatState();
    }

    // ─────────────────────────────────────────────────────────────

    private void RefreshRooms()
    {
        _cachedRooms.Clear();
        _cachedRooms.AddRange(
            FindObjectsByType<RoomController>(FindObjectsSortMode.None));
        _lastRoomsContainer = GameObject.Find("RoomControllers");
    }

    private void UpdateCombatState()
    {
        if (_player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go == null) return;
            _player = go.transform;
        }

        bool shouldCombat = IsInCombatRoom(_player.position);
        if (shouldCombat == _currentCombat) return;

        _currentCombat = shouldCombat;
        CombatZone.ForceSetCombat(_currentCombat);
    }

    private void OnDisable()
    {
        if (_currentCombat)
        {
            _currentCombat = false;
            CombatZone.ForceSetCombat(false);
        }
    }

    private bool IsInCombatRoom(Vector2 pos)
    {
        foreach (var rc in _cachedRooms)
        {
            if (rc == null) continue;
            var col = rc.GetComponent<Collider2D>();
            if (col == null || !col.OverlapPoint(pos)) continue;

            // 이 방 안에 있음 → 타입으로 전투 여부 판단
            return rc.roomType != RoomType.Start && rc.roomType != RoomType.Shop;
        }

        // 어느 방에도 속하지 않음 = 복도 = 비전투
        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_player == null) return;
        bool inCombat = IsInCombatRoom(_player.position);
        Gizmos.color = inCombat
            ? new Color(1f, 0.2f, 0.2f, 0.5f)
            : new Color(0.2f, 1f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(_player.position, 0.5f);
    }
#endif
}
