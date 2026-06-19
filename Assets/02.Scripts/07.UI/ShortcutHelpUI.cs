using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Q키를 누르면 인벤토리 좌측에 단축키 도움말 팝업을 토글한다.
/// 패널 높이는 텍스트 내용에 맞게 자동 계산된다.
/// </summary>
public class ShortcutHelpUI : MonoBehaviour
{
    private GameObject _panel;
    private bool _isVisible;

    private const float PanelW  = 340f;
    private const float PadX    = 20f;
    private const float PadY    = 14f;
    private const float TitleH  = 28f;
    private const float Gap     = 10f; // 인벤토리와의 간격

    // ─────────────────────────────────────────────────────────────

    private IEnumerator Start()
    {
        yield return null; // 씬 초기화 완료 대기
        BuildUI();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            SetVisible(!_isVisible);
    }

    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (_panel != null) _panel.SetActive(visible);
    }

    // ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var canvas = FindCanvas();
        if (canvas == null) return;

        // ── 패널 루트 ──────────────────────────────────────────────
        _panel = new GameObject("ShortcutHelpPanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(canvas.transform, false);
        _panel.transform.SetAsLastSibling();

        var panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot     = new Vector2(1f, 1f);
        panelRt.sizeDelta = new Vector2(PanelW, 100f); // 높이는 나중에 계산

        var bg = _panel.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.10f, 0.96f);
        var outlineComp = _panel.AddComponent<Outline>();
        outlineComp.effectColor    = new Color(0.35f, 0.35f, 0.55f, 0.85f);
        outlineComp.effectDistance = new Vector2(1.5f, -1.5f);

        // ── 타이틀 ──────────────────────────────────────────────────
        var titleGo = MakeText(_panel, "[ 단축키 도움말 ]", 13, FontStyle.Bold,
                               new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin        = new Vector2(0f, 1f);
        titleRt.anchorMax        = new Vector2(1f, 1f);
        titleRt.pivot            = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -PadY);
        titleRt.sizeDelta        = new Vector2(0f, TitleH);

        // ── 본문 ────────────────────────────────────────────────────
        var bodyGo = MakeText(_panel, BuildBody(), 11, FontStyle.Normal,
                              new Color(0.88f, 0.88f, 0.88f), TextAnchor.UpperLeft);
        var bodyRt  = bodyGo.GetComponent<RectTransform>();
        var bodyTxt = bodyGo.GetComponent<Text>();
        bodyTxt.horizontalOverflow = HorizontalWrapMode.Wrap;    // 너비 안에서 줄바꿈
        bodyTxt.verticalOverflow   = VerticalWrapMode.Overflow;

        bodyRt.anchorMin        = new Vector2(0f, 1f);
        bodyRt.anchorMax        = new Vector2(1f, 1f);
        bodyRt.pivot            = new Vector2(0f, 1f);
        bodyRt.anchoredPosition = new Vector2(PadX, -(PadY + TitleH + 6f));
        bodyRt.sizeDelta        = new Vector2(-PadX * 2f, 0f); // 너비 고정, 높이 무제한

        // ── 텍스트 높이 측정 후 패널 크기 확정 ──────────────────────
        Canvas.ForceUpdateCanvases();
        float bodyH   = bodyTxt.preferredHeight;
        float panelH  = PadY + TitleH + 6f + bodyH + PadY;
        panelRt.sizeDelta = new Vector2(PanelW, panelH);

        // ── 인벤토리 좌측에 배치 ────────────────────────────────────
        PositionNextToInventory(panelRt, canvas, panelH);

        _panel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────

    private static void PositionNextToInventory(RectTransform panelRt, Canvas canvas, float panelH)
    {
        var canvasRt  = canvas.GetComponent<RectTransform>();
        var cam       = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var invGridRt = FindObjectOfType<InventoryGridUI>()?.GetComponent<RectTransform>();

        // 인벤토리 좌측 X, 상단 Y
        float invLeftX = GetLeftEdgeX(invGridRt, canvasRt, cam);
        float topY     = GetTopEdgeY(invGridRt, canvasRt, cam);

        if (float.IsPositiveInfinity(invLeftX))
        {
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = new Vector2(-PanelW * 0.5f - 20f, panelH * 0.5f);
            return;
        }

        // 팝업 우측 기준점: 기본값은 인벤토리 좌측
        float anchorX = invLeftX - Gap;

        // 시너지 UI가 인벤토리와 겹치는 위치에 있으면 시너지 좌측을 기준으로
        var synergyRt = FindObjectOfType<BagSurvivor.UI.SynergyListUI>()?.GetComponent<RectTransform>();
        if (synergyRt != null)
        {
            float synergyLeft = GetLeftEdgeX(synergyRt, canvasRt, cam);
            if (synergyLeft < invLeftX)
                anchorX = synergyLeft - Gap;
        }

        // 화면 왼쪽 경계를 벗어나지 않도록 clamp
        float canvasHalfW = canvasRt.rect.width * 0.5f;
        anchorX = Mathf.Max(anchorX, -canvasHalfW + PanelW + Gap);

        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.anchoredPosition = new Vector2(anchorX - 1000f, topY);
    }

    /// <summary>RectTransform의 월드 좌상단 X를 캔버스 로컬 좌표로 반환</summary>
    private static float GetLeftEdgeX(RectTransform rt, RectTransform canvasRt, Camera cam)
    {
        if (rt == null) return float.PositiveInfinity;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners); // 0=좌하, 1=좌상
        var screenPt = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRt, screenPt, cam, out var local) ? local.x : float.PositiveInfinity;
    }

    /// <summary>RectTransform의 월드 좌상단 Y를 캔버스 로컬 좌표로 반환</summary>
    private static float GetTopEdgeY(RectTransform rt, RectTransform canvasRt, Camera cam)
    {
        if (rt == null) return 0f;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners); // 1=좌상
        var screenPt = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRt, screenPt, cam, out var local) ? local.y : 0f;
    }

    // ─────────────────────────────────────────────────────────────

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
        "<color=#555566>Q — 열기 / 닫기</color>";

    // ─────────────────────────────────────────────────────────────

    private static Canvas FindCanvas()
    {
        var go = GameObject.Find("InventoryStoreRoot");
        if (go != null)
        {
            var c = go.GetComponent<Canvas>();
            if (c != null) return c;
        }
        return FindObjectOfType<Canvas>();
    }

    private static GameObject MakeText(GameObject parent, string content,
        int fontSize, FontStyle style, Color color, TextAnchor align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent.transform, false);

        var txt = go.GetComponent<Text>();
        txt.font            = GetFont();
        txt.text            = content;
        txt.fontSize        = fontSize;
        txt.fontStyle       = style;
        txt.color           = color;
        txt.alignment       = align;
        txt.lineSpacing     = 1.45f;
        txt.supportRichText = true;
        txt.raycastTarget   = false;
        return go;
    }

    private static Font GetFont() =>
        Resources.Load<Font>("Fonts/Galmuri9")
        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
}
