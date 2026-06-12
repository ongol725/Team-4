using UnityEngine;

public class TrapController : MonoBehaviour
{
    private bool isTriggered = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isTriggered || !collision.CompareTag("Player")) return;

        isTriggered = true; // 🌟 일단 밟았다고 체크! (효과 진행 중에 또 밟히는 것 방지)

        ITrapEffect[] trapEffects = GetComponents<ITrapEffect>();
        
        float maxDuration = 0f; // 함정이 파괴되기 전까지 기다려야 할 최대 시간

        foreach (ITrapEffect effect in trapEffects)
        {
            // 각 효과를 실행하고, 걸리는 시간을 받아와
            float duration = effect.ExecuteTrap(collision.gameObject);
            
            // 기존 최대 시간보다 더 오래 걸리는 효과라면 갱신해
            if (duration > maxDuration)
            {
                maxDuration = duration;
            }
        }

        // 모든 함정은 일회용이므로, 계산된 최대 시간(maxDuration) 뒤에 파괴
        Destroy(gameObject, maxDuration);
    }
}