using UnityEngine;

public class Trap_Blind : MonoBehaviour, ITrapEffect
{
    [Header("시야 차단 설정")]
    [Tooltip("전체 유지 시간 (초)")]
    public float blindDuration = 3f;
    
    [Tooltip("서서히 어두워지고 밝아지는 데 걸리는 시간 (초)")]
    public float fadeTime = 0.5f;
    
    [Tooltip("화면이 어두워지는 강도 (0 ~ 1)")]
    [Range(0f, 1f)]
    public float blindIntensity = 0.8f;

    public float ExecuteTrap(GameObject player)
    {
        // 미니맵 컨트롤러를 찾아서 시야 차단 효과 발동
        MinimapController minimap = FindFirstObjectByType<MinimapController>();
        if (minimap != null)
        {
            minimap.SetBlind(blindDuration);
        }
        
        // 실제 화면 시야 좁아짐 효과 발동 (강도와 페이드 타임 적용)
        VisionController.Instance.ApplyBlind(blindDuration, fadeTime, blindIntensity);
        
        // 함정이 유지되어야 하는 시간(차단 시간) 반환
        return blindDuration;
    }
}
