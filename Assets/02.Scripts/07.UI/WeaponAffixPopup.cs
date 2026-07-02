using System.Text;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// WeaponAffixPopup.cs
// 무기 랜덤 강화 옵션 전용 호버 팝업.
//  - 아이템 소개 팝업(ItemInfoPopup)과 별개의 팝업으로, 겹치지 않도록 커서 '위'에 표시.
//  - 씬 배치 없이 첫 호출 시 자체 Canvas를 생성(런타임).
// ============================================================
public static class WeaponAffixPopup
{
    private static Canvas        _canvas;
    private static RectTransform _panel;
    private static Text          _text;
    private static bool          _built;

    private static void Build()
    {
        if (_built && _canvas != null) return;
        _built = true;

        var canvasGo = new GameObject("WeaponAffixPopupCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Object.DontDestroyOnLoad(canvasGo);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32760; // 최상위(소개 팝업 위)

        var panelGo = new GameObject("Panel",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _panel = (RectTransform)panelGo.transform;
        _panel.SetParent(canvasGo.transform, false);
        _panel.pivot = new Vector2(0.5f, 0f); // 하단 기준 → 커서 위에 배치
        var img = panelGo.GetComponent<Image>();
        img.color = new Color(0.05f, 0.05f, 0.08f, 0.94f);
        img.raycastTarget = false;

        var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 8, 8);
        vlg.childControlWidth  = true;  vlg.childControlHeight  = true;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
        var csf = panelGo.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(panelGo.transform, false);
        _text = textGo.GetComponent<Text>();
        _text.font = Resources.Load<Font>("Fonts/Galmuri9")
                     ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _text.fontSize          = 15;
        _text.color             = Color.white;
        _text.supportRichText   = true;
        _text.raycastTarget     = false;
        _text.horizontalOverflow = HorizontalWrapMode.Overflow;
        _text.verticalOverflow   = VerticalWrapMode.Overflow;

        Hide();
    }

    /// <summary>무기 + 옵션이 있을 때만 표시. 그 외에는 숨긴다.</summary>
    public static void Show(ItemInstance inst, Vector2 screenPos)
    {
        if (inst == null || inst.data is not SO_WeaponData
            || inst.affixes == null || inst.affixes.Count == 0)
        {
            Hide();
            return;
        }

        Build();

        var sb = new StringBuilder();
        sb.Append("<b>강화 옵션</b>");
        foreach (var af in inst.affixes)
        {
            string hex = ColorUtility.ToHtmlStringRGB(WeaponAffixTable.TierColor(af.tier));
            sb.Append($"\n<color=#{hex}>[{WeaponAffixTable.TierLabel(af.tier)}] {WeaponAffixTable.EffectText(af)}</color>");
        }
        _text.text = sb.ToString();

        _panel.gameObject.SetActive(true);
        Reposition(screenPos);
    }

    public static void Hide()
    {
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    private static void Reposition(Vector2 screenPos)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
        float w = _panel.rect.width;
        float h = _panel.rect.height;
        const float margin = 18f;
        float x = Mathf.Clamp(screenPos.x, w * 0.5f, Mathf.Max(w * 0.5f, Screen.width - w * 0.5f));
        float y = screenPos.y + margin;                 // 커서 위(소개 팝업은 오른쪽 → 분리)
        if (y + h > Screen.height) y = Mathf.Max(0f, Screen.height - h);
        _panel.position = new Vector3(x, y, 0f);
    }
}
