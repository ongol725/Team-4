using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Q키를 누르면 인벤토리 좌측에 단축키 도움말 팝업을 토글한다.
/// 패널 높이는 텍스트 내용에 맞게 자동 계산된다.
/// RuntimeInitializeOnLoadMethod로 자동 생성되므로 씬에 배치 불필요.
/// </summary>
public class ShortcutHelpUI : MonoBehaviour
{
    private static ShortcutHelpUI _instance;

    private GameObject _panel;
    private RectTransform _panelRt;
    private Vector2 _basePosition;
    private bool _isVisible;

    // 위치 미세조정 오프셋 (x=좌우, y=상하)
    private Vector2 _positionOffset = Vector2.zero;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;
        var go = new GameObject("ShortcutHelpUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ShortcutHelpUI>();
    }

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
            Toggle();

        // Inspector offset 실시간 반영
        if (_panelRt != null)
            _panelRt.anchoredPosition = _basePosition + _positionOffset;
    }

    private IEnumerator RebuildAndShow()
    {
        _isVisible = false;
        _panelRt   = null;
        yield return null; // 씬 초기화 완료 대기
        BuildUI();
        SetVisible(true);
    }

    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (_panel == null) return;

        // 버튼이 패널보다 늦게 생성되는 경우를 대비해 표시 시점에 위치 재계산
        if (visible)
        {
            PositionAboveButton(_panelRt);
            _panel.transform.SetAsLastSibling();
            _panel.SetActive(true);
            EnsureTopSorting(); // 중첩 Canvas 정렬은 활성 상태에서 설정해야 유지된다
        }
        else
        {
            _panel.SetActive(false);
        }
    }

    /// <summary>패널에 전용 Canvas(override sorting)를 부여해 인벤토리 등 다른 UI보다 항상 위에 렌더링한다.
    /// 인벤토리 캔버스(10)·시너지(11)보다 높고 일시정지 팝업(100~120)보다는 낮은 값.</summary>
    private void EnsureTopSorting()
    {
        var canvas = _panel.GetComponent<Canvas>();
        if (canvas == null) canvas = _panel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = 60;
    }

    /// <summary>단축키 정보 버튼(AutoSortButton) 바로 위에 패널을 배치한다. 성공 여부 반환.</summary>
    private bool PositionAboveButton(RectTransform panelRt)
    {
        if (panelRt == null) return false;

        var btn = GameObject.Find("AutoSortButton");
        if (btn == null || btn.transform.parent != panelRt.parent) return false;

        var btnRt = btn.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.pivot     = new Vector2(0f, 0f);
        _basePosition     = btnRt.anchoredPosition + new Vector2(0f, btnRt.sizeDelta.y + Gap);
        panelRt.anchoredPosition = _basePosition + _positionOffset;
        return true;
    }

    /// <summary>버튼 등 외부에서 도움말을 토글한다 — Q 키와 동일 동작</summary>
    public static void Toggle()
    {
        if (_instance == null) return;

        // 씬 전환으로 이전 Canvas가 파괴된 경우 패널 재생성 후 표시
        if (_instance._panel == null)
        {
            _instance.StartCoroutine(_instance.RebuildAndShow());
            return;
        }
        _instance.SetVisible(!_instance._isVisible);
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
        panelRt.pivot     = new Vector2(0f, 1f); // 좌상단 기준 — 시너지 우측에 붙임
        panelRt.sizeDelta = new Vector2(PanelW, 100f); // 높이는 나중에 계산

        var bg = _panel.GetComponent<Image>();
        var frameSprite = Resources.Load<Sprite>("UI/Paus_Frame");
        if (frameSprite != null)
        {
            bg.sprite = frameSprite;
            bg.color  = Color.white; // 프레임 원본 색 그대로
        }
        else
        {
            // 리소스를 못 찾으면 기존 단색 배경 폴백
            bg.color = new Color(0.05f, 0.05f, 0.10f, 0.96f);
            var outlineComp = _panel.AddComponent<Outline>();
            outlineComp.effectColor    = new Color(0.35f, 0.35f, 0.55f, 0.85f);
            outlineComp.effectDistance = new Vector2(1.5f, -1.5f);
        }

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

        // ── 단축키 정보 버튼 위에 배치 (버튼이 없으면 시너지 우측 폴백) ──
        _panelRt = panelRt;
        if (!PositionAboveButton(panelRt))
            PositionNextToSynergy(panelRt, canvas);

        _panel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────

    private void PositionNextToSynergy(RectTransform panelRt, Canvas canvas)
    {
        var canvasRt  = canvas.GetComponent<RectTransform>();
        var cam       = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var synergyRt = FindFirstObjectByType<BagSurvivor.UI.SynergyListUI>()?.GetComponent<RectTransform>();

        float rightX = GetRightEdgeX(synergyRt, canvasRt, cam);
        float topY   = GetTopEdgeY(synergyRt, canvasRt, cam);

        if (float.IsPositiveInfinity(rightX))
        {
            // 시너지를 못 찾으면 화면 왼쪽 끝에 폴백
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0f, 0.5f);
            panelRt.anchoredPosition = new Vector2(Gap, 0f);
            return;
        }

        panelRt.anchorMin = panelRt.anchorMax = canvasRt.pivot;

        // 화면 오른쪽으로 나가지 않도록 clamp
        float canvasHalfW = canvasRt.rect.width * 0.5f;
        rightX = Mathf.Min(rightX, canvasHalfW - PanelW - Gap);

        _basePosition = new Vector2(rightX, topY);
        panelRt.anchoredPosition = _basePosition + _positionOffset;
    }

    /// <summary>RectTransform의 월드 우상단 X를 캔버스 로컬 좌표로 반환</summary>
    private static float GetRightEdgeX(RectTransform rt, RectTransform canvasRt, Camera cam)
    {
        if (rt == null) return float.PositiveInfinity;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners); // 2=우상
        var screenPt = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRt, screenPt, cam, out var local) ? local.x : float.PositiveInfinity;
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
        "  R              리롤\n" +
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
