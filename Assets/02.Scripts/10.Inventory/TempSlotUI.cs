using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스왑 시 밀려난 아이템을 임시로 보관하는 슬롯.
/// 한 번에 하나의 ItemBlockUI만 보관한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TempSlotUI : MonoBehaviour
{
    [SerializeField] private Image _background;
    [SerializeField] private Color _emptyColor    = new Color(0.15f, 0.15f, 0.20f, 0.80f);
    [SerializeField] private Color _occupiedColor = new Color(0.35f, 0.25f, 0.10f, 0.90f);

    private ItemBlockUI _heldBlock;

    public bool IsOccupied => _heldBlock != null;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        UpdateBackground();
    }

    /// <summary>밀려난 블록을 임시칸에 배치한다</summary>
    public void ReceiveBlock(ItemBlockUI block)
    {
        _heldBlock = block;

        var rt = block.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin        = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(4f, -4f);

        block.OnSentToTempSlot(this);
        UpdateBackground();
    }

    /// <summary>블록이 플레이어에 의해 집어들어졌을 때 호출</summary>
    public void OnItemPickedUp()
    {
        _heldBlock = null;
        UpdateBackground();
    }

    // ─────────────────────────────────────────────────────────────

    private void UpdateBackground()
    {
        if (_background != null)
            _background.color = IsOccupied ? _occupiedColor : _emptyColor;
    }
}
