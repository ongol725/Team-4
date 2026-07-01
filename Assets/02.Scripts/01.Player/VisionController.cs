using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class VisionController : MonoBehaviour
{
    private Volume volume;
    private Vignette vignette;
    
    // 싱글톤 패턴으로 어디서든 쉽게 접근 가능하도록 설정
    private static VisionController instance;
    public static VisionController Instance 
    {
        get 
        {
            if (instance == null) 
            {
                GameObject go = new GameObject("VisionController");
                instance = go.AddComponent<VisionController>();
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject); // 씬이 넘어가도 유지
        
        // 메인 카메라에 포스트 프로세싱 켜기 (없으면 켬)
        EnsurePostProcessing();

        // 글로벌 볼륨 추가
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100; // 높은 우선순위로 덮어씌움
        
        // 동적 프로필 생성 및 비네팅(시야 좁아짐 효과) 추가
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vignette = profile.Add<Vignette>(false);
        
        vignette.active = true;
        vignette.intensity.Override(0f); // 처음엔 0 (정상 시야)
        vignette.color.Override(Color.black);
        vignette.smoothness.Override(1f);
        
        volume.profile = profile;
    }

    // 현재 Camera.main에 포스트프로세싱을 보장한다. (층 전환·카메라 변경에도 암전이 렌더되도록 매 발동 시 재확인)
    private void EnsurePostProcessing()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var camData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
    }

    // 외부에서 시야 차단을 요청할 때 호출
    public void ApplyBlind(float duration, float fadeTime = 0.5f, float targetIntensity = 0.8f)
    {
        EnsurePostProcessing(); // 현재 카메라에 post-processing 재보장 (2~4층 암전 누락 방지)
        StopAllCoroutines();
        StartCoroutine(BlindRoutine(duration, fadeTime, targetIntensity));
    }

    private IEnumerator BlindRoutine(float duration, float fadeTime, float targetIntensity)
    {
        if (vignette == null) yield break;

        // 1. 서서히 시야가 좁아짐
        float t = 0;
        float startIntensity = vignette.intensity.value;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            vignette.intensity.value = Mathf.Lerp(startIntensity, targetIntensity, t / fadeTime);
            yield return null;
        }
        vignette.intensity.value = targetIntensity;

        // 2. 좁아진 상태 유지 (페이드 인/아웃 시간을 뺀 만큼)
        yield return new WaitForSeconds(Mathf.Max(0, duration - fadeTime * 2));

        // 3. 서서히 시야가 다시 넓어짐
        t = 0;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            vignette.intensity.value = Mathf.Lerp(targetIntensity, 0f, t / fadeTime);
            yield return null;
        }
        vignette.intensity.value = 0f;
    }
}
