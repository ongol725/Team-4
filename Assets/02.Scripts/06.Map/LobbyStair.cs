using UnityEngine;

/// <summary>
/// 로비의 입구 계단. 플레이어가 밟으면 새 런을 시작(GameManager.StartRun)하여
/// 인게임 씬으로 진입한다. 층을 올리는 StairController와 달리 런을 처음부터 시작한다.
/// </summary>
public class LobbyStair : MonoBehaviour
{
    private bool used = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (used) return;
        if (!collision.CompareTag("Player")) return;

        used = true;

        if (GameManager.Instance != null)
            GameManager.Instance.StartRun();
        else
            Debug.LogWarning("[LobbyStair] GameManager를 찾을 수 없습니다. 씬에 GameManager가 있는지 확인하세요.");
    }
}
