using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시너지 종류별 설정 데이터 (이름 · 아이콘 · 등급 임계값 · 효과 텍스트).
/// Project 뷰에서 Create → Config → SynergyConfig 로 에셋을 생성한 뒤
/// Inspector에서 13종 시너지를 채워넣는다.
/// </summary>
[CreateAssetMenu(fileName = "SynergyConfig", menuName = "Config/SynergyConfig")]
public class SO_SynergyConfig : ScriptableObject
{
    public SynergyThreshold[] thresholds;

    // SO가 로드·재로드될 때 캐시를 초기화한다.
    private Dictionary<SynergyType, SynergyThreshold> _cache;
    private void OnEnable() => _cache = null;

    /// <summary>SynergyType → SynergyThreshold 빠른 조회. 설정이 없으면 null 반환.</summary>
    public SynergyThreshold GetThreshold(SynergyType type)
    {
        if (_cache == null)
        {
            _cache = new Dictionary<SynergyType, SynergyThreshold>();
            if (thresholds != null)
                foreach (var t in thresholds)
                    if (t != null) _cache[t.type] = t;
        }
        return _cache.TryGetValue(type, out var entry) ? entry : null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>시너지 하나의 설정 데이터.</summary>
[System.Serializable]
public class SynergyThreshold
{
    [Header("식별")]
    public SynergyType type;
    public string      displayName;   // 인게임 표시 이름 (예: "암살단")
    public Sprite      icon;

    [Header("활성화 임계값 (보유 아이템 종류 수)")]
    public int bronzeThreshold = 2;
    public int silverThreshold = 4;
    public int goldThreshold   = 6;
    public int prismThreshold  = 0;

    [Header("효과 텍스트")]
    [TextArea(1, 2)] public string triggerCondition; // 발동 조건 (구성 아이템 목록)
    [TextArea(1, 3)] public string description;   // 시너지 공통 설명
    [TextArea(1, 3)] public string bronzeEffect;  // Bronze 등급 효과
    [TextArea(1, 3)] public string silverEffect;  // Silver 등급 효과
    [TextArea(1, 3)] public string goldEffect;    // Gold   등급 효과
    [TextArea(1, 3)] public string prismEffect;   // Prism  등급 효과
}
