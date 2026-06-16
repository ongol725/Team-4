using UnityEngine;
using UnityEngine.Events;

public class StairController : MonoBehaviour
{
    [Tooltip("다음 층으로 넘어갈 때 호출됩니다.")]
    public UnityEvent OnStairUsed;

    private bool used = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (used) return;
        if (!collision.CompareTag("Player")) return;

        used = true;
        OnStairUsed?.Invoke();

        if (GameManager.Instance != null)
            GameManager.Instance.GoToNextFloor();
        else
            Debug.LogWarning("[Stair] GameManager를 찾을 수 없습니다. 씬에 GameManager 오브젝트가 있는지 확인하세요.");
    }
}
