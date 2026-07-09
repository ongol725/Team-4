using UnityEngine;
using BagSurvivor.Monster;

/// <summary>
/// 플레이어 현재 위치가 속한 방의 밴드(near/mid/far)를 조회한다(사망존 통계용).
/// 방 콜라이더 OverlapPoint로 역조회 후 RoomMonsterSpawner의 밴드 판정을 사용.
/// </summary>
public static class RunZoneTracker
{
    public static string CurrentZone()
    {
        var sp = RoomMonsterSpawner.Instance;
        if (sp == null) return "-";

        var player = GameObject.FindWithTag("Player");
        if (player == null) return "-";

        Vector2 p = player.transform.position;
        foreach (var rc in Object.FindObjectsByType<RoomController>(FindObjectsSortMode.None))
        {
            if (rc == null) continue;
            var col = rc.GetComponent<Collider2D>();
            if (col != null && col.OverlapPoint(p)) return sp.GetZoneName(rc);
        }
        return "-";
    }
}
