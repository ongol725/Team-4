using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// State_shop 오브젝트에 부착.
/// 클릭 시 인벤토리 팝업을 열고 닫는다.
/// — Image / SpriteRenderer 등 종류에 상관없이 동작하려면
///   해당 오브젝트에 Collider2D(or GraphicRaycaster 대상 Image)가 있어야 한다.
/// </summary>
public class ShopStateButton : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        var toggle = FindAnyObjectByType<InventoryPopupToggle>();
        if (toggle != null)
            toggle.Toggle();
        else
            Debug.LogWarning("[ShopStateButton] InventoryPopupToggle을 찾을 수 없습니다.");
    }
}
