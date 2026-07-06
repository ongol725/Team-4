// ============================================================
// MetaLobbyUI.cs
// 로비 좌측 "이번 런 시작 스탯" 패널 (랜덤 스탯 확인 + 리롤)
//  - 씬 무수정: 로비 씬(01.Lobby) 로드 시 부트스트랩이 자동 생성
//  - 재화(정수) 잔액 / 3종 스탯 배율(등급색) / 리롤 버튼(비용 표시)
//  - 코드 생성 UGUI — 아트/레이아웃 확정 시 프리팹으로 교체 예정
// ============================================================
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>로비 씬 로드 시 MetaLobbyUI를 자동 생성하는 부트스트랩.</summary>
public static class MetaLobbyBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => TrySpawn(scene.name);
        TrySpawn(SceneManager.GetActiveScene().name); // 첫 씬이 로비인 경우
    }

    private static void TrySpawn(string sceneName)
    {
        if (sceneName != "01.Lobby") return;
        if (Object.FindFirstObjectByType<MetaLobbyUI>() != null) return;
        new GameObject("MetaLobbyUI").AddComponent<MetaLobbyUI>();
    }
}

public class MetaLobbyUI : MonoBehaviour
{
    private TextMeshProUGUI _currencyText;
    private TextMeshProUGUI _atkText, _spdText, _movText;
    private TextMeshProUGUI _rerollLabel;
    private static TMP_FontAsset _font;

    // 업그레이드 행 (정의 ↔ UI 텍스트 매핑)
    private class UpgradeRow
    {
        public MetaUpgradeDef def;
        public TextMeshProUGUI label;    // "이름 Lv.n/max"
        public TextMeshProUGUI btnLabel; // "-비용" / "MAX"
    }
    private readonly System.Collections.Generic.List<UpgradeRow> _upgradeRows
        = new System.Collections.Generic.List<UpgradeRow>();

    private void Start()
    {
        RunStartStats.RollFree(); // 로비 진입 시 무료 추첨 + 리롤 비용 초기화
        Build();
        Refresh();

        RunStartStats.onRolled          += Refresh;
        MetaProgression.onCurrencyChanged += OnCurrencyChanged;
    }

    private void OnDestroy()
    {
        RunStartStats.onRolled          -= Refresh;
        MetaProgression.onCurrencyChanged -= OnCurrencyChanged;
    }

    private void OnCurrencyChanged(int _) => Refresh();

    // ── UI 구성 ─────────────────────────────────────────────
    private void Build()
    {
        if (_font == null) _font = Resources.Load<TMP_FontAsset>("Fonts/BoldDunggeunmo SDF Damage");

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // 좌측 상단쪽: 시작 스탯 패널
        var panel = new GameObject("StatPanel", typeof(RectTransform), typeof(Image));
        var prt = (RectTransform)panel.transform;
        prt.SetParent(canvasGo.transform, false);
        prt.anchorMin = new Vector2(0f, 0.5f);
        prt.anchorMax = new Vector2(0f, 0.5f);
        prt.pivot     = new Vector2(0f, 0.5f);
        prt.anchoredPosition = new Vector2(24f, 190f);
        prt.sizeDelta = new Vector2(320f, 300f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.85f);

        BuildUpgradePanel(canvasGo.transform); // 좌측 하단쪽: 영구 업그레이드 패널

        MakeText(prt, "이번 런 시작 스탯", 26, new Vector2(0f, -18f), Color.white, FontStyles.Bold);
        _currencyText = MakeText(prt, "", 20, new Vector2(0f, -56f), new Color(0.85f, 0.75f, 1f), FontStyles.Normal);

        _atkText = MakeText(prt, "", 22, new Vector2(0f, -100f), Color.white, FontStyles.Normal);
        _spdText = MakeText(prt, "", 22, new Vector2(0f, -134f), Color.white, FontStyles.Normal);
        _movText = MakeText(prt, "", 22, new Vector2(0f, -168f), Color.white, FontStyles.Normal);

        // 리롤 버튼
        var btnGo = new GameObject("RerollButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var brt = (RectTransform)btnGo.transform;
        brt.SetParent(prt, false);
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot     = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 20f);
        brt.sizeDelta = new Vector2(240f, 52f);
        btnGo.GetComponent<Image>().color = new Color(0.45f, 0.30f, 0.12f, 1f);
        btnGo.GetComponent<Button>().onClick.AddListener(OnRerollClicked);

        _rerollLabel = MakeText(brt, "", 22, Vector2.zero, Color.white, FontStyles.Bold);
        var lrt = _rerollLabel.rectTransform;   // 버튼 내부 중앙 정렬로 재설정
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.sizeDelta = Vector2.zero;
        lrt.anchoredPosition = Vector2.zero;
        _rerollLabel.alignment = TextAlignmentOptions.Center;
    }

    // 영구 업그레이드 패널 (5종 목록 + 구매 버튼)
    private void BuildUpgradePanel(Transform canvasParent)
    {
        var panel = new GameObject("UpgradePanel", typeof(RectTransform), typeof(Image));
        var prt = (RectTransform)panel.transform;
        prt.SetParent(canvasParent, false);
        prt.anchorMin = new Vector2(0f, 0.5f);
        prt.anchorMax = new Vector2(0f, 0.5f);
        prt.pivot     = new Vector2(0f, 0.5f);
        prt.anchoredPosition = new Vector2(24f, -160f);
        prt.sizeDelta = new Vector2(320f, 340f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.85f);

        MakeText(prt, "영구 업그레이드", 26, new Vector2(0f, -16f), Color.white, FontStyles.Bold);

        float y = -58f;
        foreach (var def in MetaUpgrades.All)
        {
            var row = new UpgradeRow { def = def };

            // 이름 + 레벨 (좌측 정렬)
            row.label = MakeText(prt, "", 19, new Vector2(-30f, y), Color.white, FontStyles.Normal);
            row.label.alignment = TextAlignmentOptions.Left;
            row.label.rectTransform.sizeDelta = new Vector2(230f, 30f);

            // 구매 버튼 (우측)
            var btnGo = new GameObject("Buy_" + def.id, typeof(RectTransform), typeof(Image), typeof(Button));
            var brt = (RectTransform)btnGo.transform;
            brt.SetParent(prt, false);
            brt.anchorMin = new Vector2(1f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot     = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-10f, y);
            brt.sizeDelta = new Vector2(72f, 30f);
            btnGo.GetComponent<Image>().color = new Color(0.45f, 0.30f, 0.12f, 1f);
            var capturedDef = def; // 클로저 캡처
            btnGo.GetComponent<Button>().onClick.AddListener(() => { MetaUpgrades.TryBuy(capturedDef); Refresh(); });

            row.btnLabel = MakeText(brt, "", 16, Vector2.zero, Color.white, FontStyles.Bold);
            var lrt = row.btnLabel.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = Vector2.zero; lrt.anchoredPosition = Vector2.zero;
            row.btnLabel.alignment = TextAlignmentOptions.Center;

            // 효과 설명 (작게)
            var desc = MakeText(prt, def.desc, 14, new Vector2(-30f, y - 22f), new Color(0.7f, 0.7f, 0.72f), FontStyles.Normal);
            desc.alignment = TextAlignmentOptions.Left;
            desc.rectTransform.sizeDelta = new Vector2(230f, 22f);

            _upgradeRows.Add(row);
            y -= 54f;
        }
    }

    private TextMeshProUGUI MakeText(RectTransform parent, string text, float size, Vector2 pos, Color color, FontStyles style)
    {
        var go = new GameObject("Txt", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(300f, 34f);

        var t = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    // ── 동작 ────────────────────────────────────────────────
    private void OnRerollClicked()
    {
        if (!RunStartStats.TryReroll())
        {
            // 잔액 부족 — 버튼 라벨로 짧게 안내
            if (_rerollLabel != null) _rerollLabel.text = $"{MetaProgression.CURRENCY_NAME} 부족!";
            Invoke(nameof(Refresh), 0.8f);
        }
    }

    private void Refresh()
    {
        if (_currencyText != null)
            _currencyText.text = $"보유 {MetaProgression.CURRENCY_NAME}  {MetaProgression.Currency:N0}";

        SetStat(_atkText, "공격력",   RunStartStats.AttackMul);
        SetStat(_spdText, "공격속도", RunStartStats.AttackSpeedMul);
        SetStat(_movText, "이동속도", RunStartStats.MoveSpeedMul);

        if (_rerollLabel != null)
            _rerollLabel.text = $"리롤  (-{RunStartStats.NextRerollCost} {MetaProgression.CURRENCY_NAME})";

        // 업그레이드 행 갱신
        foreach (var row in _upgradeRows)
        {
            int lv = MetaUpgrades.GetLevel(row.def.id);
            if (row.label != null)
                row.label.text = $"{row.def.displayName}  Lv.{lv}/{row.def.maxLevel}";
            if (row.btnLabel != null)
                row.btnLabel.text = MetaUpgrades.IsMaxed(row.def) ? "MAX" : $"-{MetaUpgrades.NextCost(row.def)}";
        }
    }

    private static void SetStat(TextMeshProUGUI t, string label, float mul)
    {
        if (t == null) return;
        t.text  = $"{label}  ×{mul:F2}";
        t.color = RunStartStats.GradeColor(mul);
    }
}
