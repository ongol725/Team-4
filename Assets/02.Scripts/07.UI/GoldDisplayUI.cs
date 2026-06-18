using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 씬 상단 중앙에 골드 소지량을 표시합니다.
/// GameManager.onGoldChanged 이벤트를 구독해 자동 갱신됩니다.
/// </summary>
public class GoldDisplayUI : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;

    private Text        _goldText;
    private GameManager _gameManager;

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_canvas == null)
        {
            var go = GameObject.Find("Canvas_Inventory");
            if (go != null) _canvas = go.GetComponent<Canvas>();
        }
        if (_canvas == null) return;

        BuildUI();
        BuildControlsPanel();

        _gameManager = GameManager.Instance;
        if (_gameManager != null)
        {
            _gameManager.onGoldChanged += SetGold;
            SetGold(_gameManager.gold);
        }
    }

    private void OnDestroy()
    {
        if (_gameManager != null)
            _gameManager.onGoldChanged -= SetGold;
    }

    // ─────────────────────────────────────────────────────────────

    private void SetGold(int amount)
    {
        if (_goldText != null)
            _goldText.text = $"{amount:N0} G";
    }

    // ─────────────────────────────────────────────────────────────

    private void BuildControlsPanel()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // GoldPanel 바로 아래 (y: -10 패널높이38 -간격6 = -54)
        var panel = new GameObject("ControlsPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_canvas.transform, false);

        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -54f);
        rt.sizeDelta        = new Vector2(200f, 112f);
        panel.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.10f, 0.88f);

        var textGo = new GameObject("ControlsText", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(panel.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 6f);
        textRt.offsetMax = new Vector2(-10f, -6f);

        var txt = textGo.GetComponent<Text>();
        txt.font        = font;
        txt.fontSize    = 11;
        txt.alignment   = TextAnchor.UpperLeft;
        txt.color       = new Color(0.82f, 0.85f, 0.95f, 1f);
        txt.raycastTarget = false;
        txt.text =
            "<b>[ 단축키 ]</b>\n" +
            "<color=#FFDD88>[I]</color>  인벤토리 열기·닫기\n" +
            "<color=#FFDD88>[L]</color>  즉시 합성\n" +
            "<color=#FFDD88>[O]</color>  자동 배치\n" +
            "<color=#FFDD88>[우클릭]</color>  스마트 구매";
        txt.supportRichText = true;
    }

    private void BuildUI()
    {
        var panel = new GameObject("GoldPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_canvas.transform, false);

        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -10f);
        rt.sizeDelta        = new Vector2(180f, 38f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.02f, 0.92f);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        var textGo = new GameObject("GoldText", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(panel.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(6f, 4f);
        textRt.offsetMax = new Vector2(-6f, -4f);

        _goldText = textGo.GetComponent<Text>();
        _goldText.font      = font;
        _goldText.fontSize  = 18;
        _goldText.fontStyle = FontStyle.Bold;
        _goldText.alignment = TextAnchor.MiddleCenter;
        _goldText.color     = new Color(1f, 0.88f, 0.25f);
        _goldText.raycastTarget = false;
        _goldText.text      = "0 G";
    }
}
