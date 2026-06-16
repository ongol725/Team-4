using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스왑/이동 시 아이템을 임시로 보관하는 슬롯.
/// 여러 ItemBlockUI를 겹쳐 쌓을 수 있다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TempSlotUI : MonoBehaviour
{
    [SerializeField] private Image _background;
    [SerializeField] private Color _emptyColor    = new Color(0.15f, 0.15f, 0.20f, 0.80f);
    [SerializeField] private Color _occupiedColor = new Color(0.35f, 0.25f, 0.10f, 0.90f);

    private readonly List<ItemBlockUI> _heldBlocks = new();
    private const float StackOffset = 5f;

    public bool IsOccupied => _heldBlocks.Count > 0;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        UpdateBackground();
    }

    /// <summary>블록을 임시칸에 쌓는다</summary>
    public void ReceiveBlock(ItemBlockUI block)
    {
        int index = _heldBlocks.Count;
        _heldBlocks.Add(block);

        var rt = block.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin        = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(4f + index * StackOffset, -4f - index * StackOffset);

        block.OnSentToTempSlot(this);
        UpdateBackground();
    }

    /// <summary>특정 블록이 꺼내졌을 때 호출</summary>
    public void OnItemPickedUp(ItemBlockUI block)
    {
        _heldBlocks.Remove(block);
        UpdateBackground();
    }

    // ─────────────────────────────────────────────────────────────

    private void UpdateBackground()
    {
        if (_background != null)
            _background.color = IsOccupied ? _occupiedColor : _emptyColor;
    }
}
