using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BattleLoadout 임시 디버그 패널.
/// - UGUI 패널: LateUpdate마다 SetAsLastSibling으로 항상 최상단 유지
/// - OnGUI: Canvas 없이도 항상 렌더링되는 폴백
/// </summary>
public class BattleLoadoutDebugUI : MonoBehaviour
{
    [SerializeField] private BattleLoadoutBuilder _builder;
    [SerializeField] private InventoryAnalyzer    _analyzer;
    [SerializeField] private Canvas               _canvas;

    private Text      _contentText;
    private Transform _panelTransform;
    private float     _pollTimer;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_builder  == null) _builder  = GetComponent<BattleLoadoutBuilder>();
        if (_analyzer == null) _analyzer = GetComponent<InventoryAnalyzer>();
    }

    private void Start()
    {
        // Canvas 취득
        if (_canvas == null)
        {
            var go = GameObject.Find("Canvas_Inventory");
            if (go != null) _canvas = go.GetComponent<Canvas>();
        }

        if (_canvas != null)
            BuildPanel(_canvas.transform);

        // 이벤트 구독
        if (_analyzer != null)
        {
            _analyzer.OnSnapshotChanged += Refresh;
            if (_analyzer.LatestSnapshot != null)
                Refresh(_analyzer.LatestSnapshot);
        }
    }

    private void OnDestroy()
    {
        if (_analyzer != null) _analyzer.OnSnapshotChanged -= Refresh;
    }

    /// <summary>팝업 토글 시 패널 전체를 표시/숨깁니다.</summary>
    public void SetPanelActive(bool active) =>
        _panelTransform?.gameObject.SetActive(active);

    // 매 프레임 패널을 최상단으로 — 나중에 추가되는 UI에 덮이지 않도록
    private void LateUpdate()
    {
        if (_panelTransform != null)
            _panelTransform.SetAsLastSibling();

        // 3초 폴링
        _pollTimer += Time.deltaTime;
        if (_pollTimer >= 3f)
        {
            _pollTimer = 0f;
            if (_analyzer?.LatestSnapshot != null)
                Refresh(_analyzer.LatestSnapshot);
        }
    }

    // ─────────────────────────────────────────────────────────────

    private void Refresh(InventorySnapshot snapshot)
    {
        if (_builder == null) return;
        var loadout = _builder.Build();
        if (loadout == null) return;

        string text = BuildText(loadout);
        if (_contentText != null)
            _contentText.text = text;
    }

    // ─────────────────────────────────────────────────────────────
    // 패널 생성 (UGUI)

    private void BuildPanel(Transform canvasRoot)
    {
        var panel = new GameObject("DebugLoadoutPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasRoot, false);
        _panelTransform = panel.transform;

        // SynergyPanel: anchorMin=(0,0), anchorMax=(0,1), pivot=(0,0.5), width=240
        // → 시너지 오른쪽 끝 x=240, 여기서 8px 여백 후 배치
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(248f, 0f);
        rt.sizeDelta        = new Vector2(255f, 0f);
        panel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.12f, 0.93f);

        var font = (Resources.Load<Font>("Fonts/Galmuri9") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"))
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(panel.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = Vector2.zero; titleRt.sizeDelta = new Vector2(0f, 24f);
        var tt = titleGo.GetComponent<Text>();
        tt.font = font; tt.text = "[ 배치 종합정보 ]"; tt.fontSize = 14; tt.fontStyle = FontStyle.Bold;
        tt.alignment = TextAnchor.MiddleCenter;
        tt.color = new Color(0.95f, 0.85f, 0.35f);
        tt.supportRichText = true; tt.verticalOverflow = VerticalWrapMode.Overflow;
        tt.raycastTarget = false;

        var textGo = new GameObject("Content", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(panel.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 6f); textRt.offsetMax = new Vector2(-6f, -28f);
        _contentText = textGo.GetComponent<Text>();
        _contentText.font = font;
        _contentText.fontSize = 12; _contentText.color = new Color(0.88f, 0.88f, 0.88f);
        _contentText.lineSpacing = 1.4f; _contentText.supportRichText = true;
        _contentText.verticalOverflow = VerticalWrapMode.Overflow;
        _contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _contentText.raycastTarget = false;
        _contentText.text = "대기 중...";
    }

    // ─────────────────────────────────────────────────────────────

    private static string BuildText(BattleLoadout loadout)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("■ 무기");
        if (loadout.Weapons.Count == 0) sb.AppendLine("  없음");
        else foreach (var w in loadout.Weapons)
        {
            sb.AppendLine($"  {w.data.itemName} [{w.effectiveGrade + 1}등급]");
            sb.AppendLine($"   공{w.attackPower}  속{w.attackSpeed:F2}");
            if (w.data.projectileData != null) sb.AppendLine($"   {w.data.projectileData.name}");
            foreach (var buff in w.RingBuffs)
                sb.AppendLine($"   ▷ {buff.ringName}: {buff.effectDesc}");
        }
        sb.AppendLine();
        sb.AppendLine("■ 방어구");
        sb.AppendLine($"  HP+{loadout.TotalHpBonus}  재생{loadout.TotalHpRegen}");
        sb.AppendLine();
        sb.AppendLine("■ 시너지");
        if (loadout.ActiveSynergies.Count == 0) sb.AppendLine("  없음");
        else foreach (var s in loadout.ActiveSynergies)
            sb.AppendLine($"  {s.type} {s.grade}({s.count})");
        return sb.ToString();
    }
}
