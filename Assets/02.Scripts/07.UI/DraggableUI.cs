using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 부착된 RectTransform을 마우스로 드래그해 이동시킨다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggableUI : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform _rt;
    private Canvas        _canvas;
    private Vector2       _dragOffset;

    private void Awake()
    {
        _rt     = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var parentRt = _rt.parent as RectTransform;
        if (parentRt == null) return;

        var cam = GetCam();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt, eventData.position, cam, out var localPos);
        _dragOffset = _rt.anchoredPosition - localPos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        var parentRt = _rt.parent as RectTransform;
        if (parentRt == null || _canvas == null) return;

        var cam = GetCam();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRt, eventData.position, cam, out var localPos))
            return;

        _rt.anchoredPosition = localPos + _dragOffset;
    }

    private Camera GetCam() =>
        _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
}
