using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 2D 효과음 원샷 재생 유틸.
///  - 설정 팝업의 sfxOn/sfxVol(PlayerPrefs) 반영.
///  - Resources 경로 로드는 캐시(반복 재생 비용 제거).
///  - 같은 클립이 같은 프레임에 여러 번 겹치는 스팸 방지(짧은 쿨다운).
/// 사용: AudioUtil.PlaySfx("01.SFX/03.UI/DefaultClick");
/// </summary>
public static class AudioUtil
{
    private static readonly Dictionary<string, AudioClip> _cache = new();
    private static readonly Dictionary<AudioClip, float> _lastPlay = new();
    private const float MinInterval = 0.06f; // 같은 클립 최소 재생 간격(중첩 폭음 방지)

    /// <summary>현재 효과음 볼륨(설정 반영). 꺼져 있으면 0.</summary>
    public static float SfxVolume =>
        PlayerPrefs.GetInt("sfxOn", 1) == 1 ? PlayerPrefs.GetFloat("sfxVol", 0.8f) : 0f;

    /// <summary>Resources 경로(확장자 없이)로 효과음 재생.</summary>
    public static void PlaySfx(string resourcesPath, float volumeMul = 1f)
    {
        if (string.IsNullOrEmpty(resourcesPath)) return;
        if (!_cache.TryGetValue(resourcesPath, out var clip))
        {
            clip = Resources.Load<AudioClip>(resourcesPath);
            _cache[resourcesPath] = clip; // null도 캐시해 매번 로드 시도 방지
            if (clip == null) Debug.LogWarning("[AudioUtil] 클립 없음: " + resourcesPath);
        }
        PlaySfx(clip, volumeMul);
    }

    /// <summary>클립 직접 재생 (2D, 몬스터 사망음 등 직렬화 참조용).</summary>
    public static void PlaySfx(AudioClip clip, float volumeMul = 1f)
    {
        if (clip == null) return;
        float vol = SfxVolume * volumeMul;
        if (vol <= 0f) return;

        if (_lastPlay.TryGetValue(clip, out float t) && Time.unscaledTime - t < MinInterval) return;
        _lastPlay[clip] = Time.unscaledTime;

        var go = new GameObject("Sfx_" + clip.name);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.spatialBlend = 0f; // 2D
        src.volume = vol;
        src.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }
}
