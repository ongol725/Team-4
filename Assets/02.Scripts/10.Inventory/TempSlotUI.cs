using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 스왑/이동 시 아이템을 임시로 보관하는 슬롯.
/// 아이템은 슬롯 내부에서 가장자리/모서리 우선으로 분산 배치된다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TempSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _background;
    [SerializeField] private Color _emptyColor    = new Color(0.15f, 0.15f, 0.20f, 0.80f);
    [SerializeField] private Color _occupiedColor = new Color(0.35f, 0.25f, 0.10f, 0.90f);

    private readonly List<ItemBlockUI> _heldBlocks = new();
    private RectTransform _rt;

    // 배치 우선순위: 좌상 → 우상 → 좌하 → 우하 → 상중 → 좌중 → 우중 → 하중 → 중앙
    // Vector2(normalizedX, normalizedY)  X: 0=left 1=right, Y: 0=bottom 1=top
    private static readonly Vector2[] SpreadAnchors =
    {
        new Vector2(0f,   1f),   // 좌상
        new Vector2(1f,   1f),   // 우상
        new Vector2(0f,   0f),   // 좌하
        new Vector2(1f,   0f),   // 우하
        new Vector2(0.5f, 1f),   // 상중
        new Vector2(0f,   0.5f), // 좌중
        new Vector2(1f,   0.5f), // 우중
        new Vector2(0.5f, 0f),   // 하중
        new Vector2(0.5f, 0.5f), // 중앙
    };

    public bool IsOccupied => _heldBlocks.Count > 0;
    public IReadOnlyList<ItemBlockUI> HeldBlocks => _heldBlocks;

    /// <summary>임시칸 내에서 inst와 합성 가능한 블록을 반환한다</summary>
    public ItemBlockUI FindMergeTarget(ItemInstance inst)
    {
        foreach (var block in _heldBlocks)
            if (block.Instance != inst
             && block.Instance.data       == inst.data
             && block.Instance.gradeIndex == inst.gradeIndex)
                return block;
        return null;
    }

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
                -(pad + (1f - anchor.y) * usableY));

            // 배경(sibling 0) 다음 순서로 쌓기 — 뒤에 추가된 아이템이 위에 렌더링
            rt.SetSiblingIndex(i + 1);
        }
    }

    // 과적 색 단계: 연한 노랑 → 주황 → 빨강 → 진한 빨강 (불투명·선명 — 배경 비침 방지)
    private static readonly Color[] OverloadStops =
    {
        new Color(1.00f, 0.92f, 0.30f, 1f), // 연노랑
        new Color(1.00f, 0.55f, 0.10f, 1f), // 주황
        new Color(0.95f, 0.12f, 0.08f, 1f), // 빨강
        new Color(0.55f, 0.00f, 0.00f, 1f), // 진한 빨강
    };

    private void UpdateBackground()
    {
        if (_background == null) return;

        if (!IsOccupied) { _background.color = _emptyColor; return; }

        // 반지 제외 아이템 수로 과적 심각도(0~1) 산출 → 색으로 표시
        int cnt = CountNonRingItems();
        float sev = BattleLoadoutBuilder.GetTempSeverity(cnt);
        _background.color = sev <= 0f ? _occupiedColor : OverloadColor(sev);
    }

    // ── 호버 시 과적 디메리트 간단 표기(작은 팝업) ──
    private GameObject      _tooltip;
    private TextMeshProUGUI _tooltipText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnsureTooltip();
        int cnt = CountNonRingItems();
        BattleLoadoutBuilder.GetTempPenaltyPercents(cnt, out int m, out int a);
        _tooltipText.text = (m == 0 && a == 0)
            ? $"임시칸 여유 {cnt}/{BattleLoadoutBuilder.TempFreeCount}"
            : $"과적 {cnt}개\n이속 -{m}%  공속 -{a}%";
        _tooltip.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltip != null) _tooltip.SetActive(false);
    }

    private void EnsureTooltip()
    {
        if (_tooltip != null) return;

        _tooltip = new GameObject("TempTooltip", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)_tooltip.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); // 슬롯 상단 중앙
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 8f);
        rt.sizeDelta        = new Vector2(190f, 46f);

        var bg = _tooltip.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);
        bg.raycastTarget = false;
        rt.SetAsLastSibling();

        var textGO = new GameObject("Text", typeof(RectTransform));
        var trt = (RectTransform)textGO.transform;
        trt.SetParent(_tooltip.transform, false);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(6f, 4f); trt.offsetMax = new Vector2(-6f, -4f);

        _tooltipText = textGO.AddComponent<TextMeshProUGUI>();
        _tooltipText.fontSize      = 15;
        _tooltipText.alignment     = TextAlignmentOptions.Center;
        _tooltipText.color         = Color.white;
        _tooltipText.raycastTarget = false;

        _tooltip.SetActive(false);
    }

    private int CountNonRingItems()
    {
        int n = 0;
        foreach (var b in _heldBlocks)
        {
            var data = b != null && b.Instance != null ? b.Instance.data : null;
            if (data == null || data is SO_AccessoryData) continue; // 반지 노카운트
            n++;
        }
        return n;
    }

    // severity(0~1)를 OverloadStops 구간에 매핑해 색 보간
    private static Color OverloadColor(float s)
    {
        s = Mathf.Clamp01(s);
        int seg = OverloadStops.Length - 1;          // 구간 수
        float scaled = s * seg;
        int i = Mathf.Min((int)scaled, seg - 1);
        float f = scaled - i;
        return Color.Lerp(OverloadStops[i], OverloadStops[i + 1], f);
    }
}
