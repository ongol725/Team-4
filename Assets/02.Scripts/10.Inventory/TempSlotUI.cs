using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스왑/이동 시 아이템을 임시로 보관하는 슬롯.
/// 아이템은 슬롯 내부에서 가장자리/모서리 우선으로 분산 배치된다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TempSlotUI : MonoBehaviour
{
    [SerializeField] private Image _background;
    [SerializeField] private Color _emptyColor    = new Color(0.15f, 0.15f, 0.20f, 0.80f);
    [SerializeField] private Color _occupiedColor = new Color(0.35f, 0.25f, 0.10f, 0.90f);

    private readonly List<ItemBlockUI> _heldBlocks = new();
    private RectTransform _rt;

    // 배치 우선순위: 좌상 → 우하 → 우상 → 좌하 → 중앙 → 상중 → 하중 → 좌중 → 우중
    // Vector2(normalizedX, normalizedY)  X: 0=left 1=right, Y: 0=bottom 1=top
    private static readonly Vector2[] SpreadAnchors =
    {
        new Vector2(0f,   1f),   // 좌상
        new Vector2(1f,   0f),   // 우하
        new Vector2(1f,   1f),   // 우상
        new Vector2(0f,   0f),   // 좌하
        new Vector2(0.5f, 1f),   // 상중
        new Vector2(0.5f, 0f),   // 하중
        new Vector2(0f,   0.5f), // 좌중
        new Vector2(1f,   0.5f), // 우중
        new Vector2(0.5f, 0.5f), // 중앙
    };

    public bool IsOccupied => _heldBlocks.Count > 0;
    public IReadOnlyList<ItemBlockUI> HeldBlocks => _heldBlocks;

    public static event System.Action onTempSlotChanged;


    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        UpdateBackground();
    }

    /// <summary>블록을 임시칸에 쌓는다</summary>
    public void ReceiveBlock(ItemBlockUI block)
    {
        _heldBlocks.Add(block);

        var rt = block.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);

        block.OnSentToTempSlot(this);
        RefreshPositions();
        UpdateBackground();
        onTempSlotChanged?.Invoke();
    }

    /// <summary>특정 블록이 꺼내졌을 때 호출</summary>
    public void OnItemPickedUp(ItemBlockUI block)
    {
        _heldBlocks.Remove(block);
        // 남은 아이템 위치 유지 — RefreshPositions 호출 안 함
        UpdateBackground();
        onTempSlotChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────

    private void RefreshPositions()
    {
        int n = _heldBlocks.Count;
        if (n == 0) return;

        if (_rt == null) _rt = GetComponent<RectTransform>();

        float slotW = _rt.rect.width;
        float slotH = _rt.rect.height;
        if (slotW <= 0 || slotH <= 0) { slotW = 120f; slotH = 120f; }

        const float pad = 4f;

        // 배경을 항상 sibling 0에 고정 → 아이템이 배경 위에 렌더링됨
        if (_background != null)
            _background.transform.SetSiblingIndex(0);

        for (int i = 0; i < n; i++)
        {
            var rt = _heldBlocks[i].GetComponent<RectTransform>();

            // 아이템 실제 크기 (sizeDelta 기준)
            float iw = Mathf.Max(rt.sizeDelta.x, 1f);
            float ih = Mathf.Max(rt.sizeDelta.y, 1f);

            // 아이템 좌상단 기준으로 이동 가능한 범위
            float usableX = Mathf.Max(0f, slotW - iw - pad * 2f);
            float usableY = Mathf.Max(0f, slotH - ih - pad * 2f);

            var anchor = SpreadAnchors[i % SpreadAnchors.Length];

            rt.anchoredPosition = new Vector2(
                 pad + anchor.x * usableX,
                -(pad + anchor.y * usableY));

            // 배경(sibling 0) 다음 순서로 쌓기 — 뒤에 추가된 아이템이 위에 렌더링
            rt.SetSiblingIndex(i + 1);
        }
    }

    private void UpdateBackground()
    {
        if (_background != null)
            _background.color = IsOccupied ? _occupiedColor : _emptyColor;
    }
}
