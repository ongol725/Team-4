using UnityEngine;

/// <summary>
/// 인벤토리 배경 이미지 위치/크기를 Inspector에서 실시간 조정합니다.
/// BG 오브젝트(anchor stretch-fill 기준)에 부착합니다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BackpackBGAdjust : MonoBehaviour
{
    [Header("배경 이미지 조정 (인스펙터 실시간 반영)")]
    [Tooltip("중심 위치 오프셋 (픽셀)")]
    [SerializeField] private Vector2 _posOffset  = Vector2.zero;
    [Tooltip("부모 대비 크기 차이 (양수=크게, 음수=작게)")]
    [SerializeField] private Vector2 _sizeOffset = Vector2.zero;

    private RectTransform _rt;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        Apply();
    }

    private void Apply()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        _rt.anchoredPosition = _posOffset;
        _rt.sizeDelta        = _sizeOffset;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Apply();
    }
#endif
}
