using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이 컴포넌트가 붙은 오브젝트의 Trigger Collider2D 안에 플레이어가 들어오면
/// 전투 상태(inCombat = true)로, 나가면 비전투 상태(false)로 전환한다.
///
/// 여러 CombatZone이 겹쳐도 마지막 구역을 완전히 벗어날 때만 비전투로 전환된다.
///
/// 씬 설정:
///   1. 빈 GameObject 생성 → Collider2D(BoxCollider2D 등) 추가 → Is Trigger 체크
///   2. 이 컴포넌트 추가
///   3. Player 오브젝트에 "Player" 태그가 설정되어 있어야 한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CombatZone : MonoBehaviour
{
    /// <summary>true = 전투 구역 진입, false = 전투 구역 이탈</summary>
    public static event System.Action<bool> onCombatStateChanged;

    public static bool IsInCombat => _overlapCount > 0;

    private static int _overlapCount = 0;   // 현재 플레이어가 밟고 있는 전투 구역 수

    // 에디터에서 Play를 반복해도 static이 초기화되도록 보장
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _overlapCount = 0;

    private bool _playerInside = false;

    // 한 프레임 대기 후 체크: Start() 시 모든 구독자(PlayerAttack 등)가 등록된 뒤 실행
    private IEnumerator Start()
    {
        yield return null;
        CheckPlayerAlreadyInside();
    }

    private void CheckPlayerAlreadyInside()
    {
        if (_playerInside) return;
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        var results = new List<Collider2D>();
        col.Overlap(new ContactFilter2D().NoFilter(), results);
        foreach (var other in results)
        {
            if (!other.CompareTag("Player")) continue;
            _playerInside = true;
            _overlapCount++;
            if (_overlapCount == 1)
                onCombatStateChanged?.Invoke(true);
            break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_playerInside || !other.CompareTag("Player")) return;
        _playerInside = true;
        _overlapCount++;
        if (_overlapCount == 1)
            onCombatStateChanged?.Invoke(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!_playerInside || !other.CompareTag("Player")) return;
        _playerInside = false;
        _overlapCount = Mathf.Max(0, _overlapCount - 1);
        if (_overlapCount == 0)
            onCombatStateChanged?.Invoke(false);
    }

    // 씬 전환 / 오브젝트 비활성화 시 카운트 정리
    private void OnDisable()
    {
        if (!_playerInside) return;
        _playerInside = false;
        _overlapCount = Mathf.Max(0, _overlapCount - 1);
        if (_overlapCount == 0)
            onCombatStateChanged?.Invoke(false);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif
}
