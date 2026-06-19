using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Q키를 누르면 인게임 단축키 도움말 팝업을 토글한다.
/// </summary>
public class ShortcutHelpUI : MonoBehaviour
{
    private GameObject _panel;
    private bool _isVisible;

    private void Start() => BuildUI();

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            SetVisible(!_isVisible);
    }

    private void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (_panel != null) _panel.SetActive(visible);
    }

    // ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var canvas = FindCanvas();
        if (canvas == null) return;

        // ── 배경 패널 ──────────────────────────────────────────────
        _panel = new GameObject("ShortcutHelpPanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(canvas.transform, false);
        _panel.transform.SetAsLastSibling();

        var rt = _panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(400f, 320f);
        rt.anchoredPosition = Vector2.zero;

        var bg = _panel.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.10f, 0.96f);

        AddOutline(_panel);

        // ── 타이틀 ──────────────────────────────────────────────────
        AddText(_panel, "[ 단축키 도움말 ]",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -16f), new Vector2(0f, -42f),
            14, FontStyle.Bold, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

        // ── 본문 ────────────────────────────────────────────────────
        AddText(_panel, BuildBody(),
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(24f, 12f), new Vector2(-24f, -50f),
            12, FontStyle.Normal, new Color(0.88f, 0.88f, 0.88f), TextAnchor.UpperLeft);

        _panel.SetActive(false);
    }

    private static string BuildBody() =>
        "<color=#99aaff>■ 인벤토리</color>\n" +
        "  L              자동 합성\n" +
        "  O              자동 배치\n" +
        "  T + 좌클릭     아이템 즉시 판매\n" +
        "\n" +
        "<color=#99aaff>■ 아이템 드래그 중</color>\n" +
        "  우클릭         원위치 복원 / 구매 취소\n" +
        "\n" +
        "<color=#99aaff>■ 임시칸</color>\n" +
        "  드래그 놓기    합성 가능 시 합성, 아니면 보관\n" +
        "  우클릭         그리드 자동 배치 (합성 우선)\n" +
        "\n" +
        "<color=#99aaff>■ 상점</color>\n" +
        "  좌클릭         구매 후 배치\n" +
        "  우클릭         스마트 구매\n" +
        "\n" +
        "<color=#666666>Q — 열기 / 닫기</color>";

    // ─────────────────────────────────────────────────────────────

    private static Canvas FindCanvas()
    {
        var go = GameObject.Find("InventoryStoreRoot");
        if (go != null)
        {
            var c = go.GetComponent<Canvas>();
            if (c != null) return c;
        }
        return Object.FindObjectOfType<Canvas>();
    }

    private static void AddText(GameObject parent,
        string content,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        int fontSize, FontStyle style, Color color, TextAnchor align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var txt = go.GetComponent<Text>();
        txt.font               = GetFont();
        txt.text               = content;
        txt.fontSize           = fontSize;
        txt.fontStyle          = style;
        txt.color              = color;
        txt.alignment          = align;
        txt.lineSpacing        = 1.45f;
        txt.supportRichText    = true;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow   = VerticalWrapMode.Overflow;
        txt.raycastTarget      = false;
    }

    private static void AddOutline(GameObject parent)
    {
        const float thick = 1.5f;
        var rt = parent.GetComponent<RectTransform>();
        float w = rt.sizeDelta.x, h = rt.sizeDelta.y;

        void Strip(Vector2 pos, Vector2 size)
        {
            var go = new GameObject("border", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot     = new Vector2(0f, 1f);
            r.sizeDelta        = size;
            r.anchoredPosition = pos;
            go.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.55f, 0.8f);
        }

        Strip(new Vector2(0,         0        ), new Vector2(w,     thick));
        Strip(new Vector2(0,         -(h-thick)), new Vector2(w,     thick));
        Strip(new Vector2(0,         -thick   ), new Vector2(thick,  h - thick * 2f));
        Strip(new Vector2(w - thick, -thick   ), new Vector2(thick,  h - thick * 2f));
    }

    private static Font GetFont() =>
        Resources.Load<Font>("Fonts/Galmuri9")
        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
}
