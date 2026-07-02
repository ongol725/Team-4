// ============================================================
// BossHpBar.cs
// 최종 보스 전용 상단 고정 HP바 (겹/layer 상승 모델)
//  - 보스 고유 최대체력을 baseLayers(기본 5)겹으로 나눔. 1겹 = 한 색(무지개 순환).
//  - 데미지로 현재 겹을 다 깎으면 아래 겹 색으로 전환.
//  - 런 경과시간(층 누적)에 따라 escalateInterval(1분)마다 +1겹(최대 maxLayers).
//    보스 미연결(던전) 상태에서도 상승 → "던전에서 보스가 힘을 쌓는다".
//  - 겹이 오를 때마다 HP바 왼쪽에 "보스가 힘을 축적하고 있습니다" 팝업 표시.
//  - HP바 바로 아래에 다음 겹까지의 타이머 바(자동 생성).
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BagSurvivor.Monster;

namespace BagSurvivor.UI
{
    public class BossHpBar : MonoBehaviour
    {
        [Header("게이지 (Filled Horizontal)")]
        public Image mainFill;     // 현재 겹 색으로 즉시 채움
        public Image delayedFill;  // 데미지 지연(어두운 현재 겹 색)

        [Header("텍스트")]
        public Text nameText;
        public Text hpText;

        [Header("지연 게이지 타이밍")]
        public float delayBeforeCatch = 0.2f;
        public float catchDuration = 0.4f;

        [Header("겹(layer) 상승 — 시간 압박")]
        [Tooltip("기본 겹수(고유 최대체력을 이 수로 나눠 1겹 HP 결정)")]
        public int baseLayers = 5;
        [Tooltip("최대 겹수 상한")]
        public int maxLayers = 100;
        [Tooltip("이 간격(초)마다 1겹 증가")]
        public float escalateInterval = 60f;
        [Tooltip("겹 상승 타이머 바(비우면 HP바 바로 아래에 자동 생성)")]
        public Image timerFill;

        [Header("보스 미연결 시 표시용 기본 체력")]
        public int standaloneMaxHP = 1000;
        public int standaloneCurHP = 1000;

        private MonsterController target;
        private float displayedDelayed = 1f;
        private float delayTimer, catchTimer, catchFrom = 1f, lastCur = 1f;

        private int  _layerHP;        // 1겹당 HP
        private int  _appliedLayers;  // 현재까지 반영된 겹수
        private int  _lastLayerIdx = -1;
        private bool _inited;

        // 힘 축적 팝업
        private GameObject    _popupGo;
        private CanvasGroup   _popupCg;
        private RectTransform _popupRt;
        private Vector2       _popupHome;
        private Coroutine     _popupCo;

        private void Start() { EnsureInit(); }

        /// <summary>보스 몬스터 연결.</summary>
        public void SetTarget(MonsterController mc, string bossName)
        {
            target = mc;
            if (nameText != null) nameText.text = bossName;

            // 보스 고유 최대체력 기준으로 1겹 HP 재산출, 겹수는 기본값에서 다시 상승 계산.
            _layerHP = Mathf.Max(1, mc.MaxHP / Mathf.Max(1, baseLayers));
            _appliedLayers = baseLayers;
            _inited = true;
            _lastLayerIdx = -1;

            EnsureTimerBar();

            float within = CurrentLayerFill(out int idx);
            _lastLayerIdx = idx;
            lastCur = within; displayedDelayed = within; catchFrom = within;
            ApplyLayerColor(idx);
            SetMain(within); SetDelayed(within);
            RefreshHpText();
        }

        private void EnsureInit()
        {
            if (_inited) return;
            _layerHP = Mathf.Max(1, MaxHP() / Mathf.Max(1, baseLayers));
            _appliedLayers = baseLayers;
            _inited = true;
            EnsureTimerBar();
        }

        private float RunElapsed()
        {
            var rs = RunStatsLogger.Instance;
            return rs != null ? rs.RunElapsedSeconds : Time.time;
        }

        private void Update()
        {
            EnsureInit();

            // 시간에 따른 겹 상승 (보스 연결 여부와 무관 — 던전에서도 축적)
            if (_layerHP > 0 && !(target != null && target.IsDead))
            {
                int targetLayers = Mathf.Clamp(
                    baseLayers + Mathf.FloorToInt(RunElapsed() / Mathf.Max(1f, escalateInterval)),
                    baseLayers, maxLayers);
                while (_appliedLayers < targetLayers)
                {
                    _appliedLayers++;
                    AddOneLayerHp();
                    ShowPowerPopup(); // 1겹 오를 때마다 "힘 축적" 팝업
                }
            }

            // 겹 상승 타이머 바
            if (timerFill != null)
            {
                bool maxed = _appliedLayers >= maxLayers;
                timerFill.fillAmount = maxed ? 1f
                    : Mathf.Repeat(RunElapsed(), escalateInterval) / Mathf.Max(1f, escalateInterval);
            }

            float cur = CurrentLayerFill(out int layerIdx);

            if (layerIdx != _lastLayerIdx)
            {
                _lastLayerIdx = layerIdx;
                ApplyLayerColor(layerIdx);
                displayedDelayed = cur; catchFrom = cur; delayTimer = 0f; catchTimer = 0f;
                SetDelayed(cur);
            }

            SetMain(cur);
            RefreshHpText();

            if (cur < lastCur - 0.0001f) { delayTimer = 0f; catchTimer = 0f; catchFrom = displayedDelayed; }
            lastCur = cur;

            if (displayedDelayed > cur + 0.0001f)
            {
                if (delayTimer < delayBeforeCatch) { delayTimer += Time.deltaTime; catchFrom = displayedDelayed; }
                else
                {
                    catchTimer += Time.deltaTime;
                    float t = catchDuration <= 0f ? 1f : Mathf.Clamp01(catchTimer / catchDuration);
                    displayedDelayed = Mathf.Lerp(catchFrom, cur, t);
                    SetDelayed(displayedDelayed);
                }
            }
            else if (displayedDelayed < cur) { displayedDelayed = cur; SetDelayed(cur); }
        }

        // 1겹만큼 최대+현재 HP 상승 (덧씌워 채움)
        private void AddOneLayerHp()
        {
            if (target != null && !target.IsDead) target.IncreaseMaxHP(_layerHP);
            else
            {
                standaloneMaxHP = Mathf.Max(1, standaloneMaxHP + _layerHP);
                standaloneCurHP = Mathf.Clamp(standaloneCurHP + _layerHP, 0, standaloneMaxHP);
            }
        }

        private int CurHP() => target != null ? target.CurrentHP : standaloneCurHP;
        private int MaxHP() => target != null ? Mathf.Max(1, target.MaxHP) : Mathf.Max(1, standaloneMaxHP);

        // 현재 HP가 속한 겹 index(1~appliedLayers)와 그 겹 내 채움 비율(0~1)
        private float CurrentLayerFill(out int idx)
        {
            int cur = CurHP();
            if (cur <= 0) { idx = 0; return 0f; }
            if (_layerHP <= 0) { idx = 1; return Mathf.Clamp01((float)cur / MaxHP()); }
            idx = Mathf.Clamp(Mathf.CeilToInt((float)cur / _layerHP), 1, Mathf.Max(1, _appliedLayers));
            float within = (cur - (idx - 1) * _layerHP) / (float)_layerHP;
            return Mathf.Clamp01(within);
        }

        private void ApplyLayerColor(int idx)
        {
            Color c = RainbowColor(idx);
            if (mainFill != null) mainFill.color = c;
            if (delayedFill != null) { Color d = c * 0.5f; d.a = 1f; delayedFill.color = d; }
        }

        // 무지개 색 — 겹마다 순환(빨→주→노→초→청→파→보)
        private static Color RainbowColor(int idx)
        {
            if (idx <= 0) return Color.gray;
            float hue = Mathf.Repeat((idx - 1) / 7f, 1f);
            return Color.HSVToRGB(hue, 0.85f, 1f);
        }

        // 체력만 표기 (겹 수는 표시하지 않음)
        private void RefreshHpText()
        {
            if (hpText == null) return;
            hpText.text = CurHP() + " / " + MaxHP();
        }

        private void SetMain(float v) { if (mainFill != null) mainFill.fillAmount = v; }
        private void SetDelayed(float v) { if (delayedFill != null) delayedFill.fillAmount = v; }

        // ─────────────────────────────────────────────────────────────
        // 겹 상승 타이머 바 (HP바 바로 아래 자동 생성)
        // ─────────────────────────────────────────────────────────────
        private void EnsureTimerBar()
        {
            if (timerFill != null || mainFill == null) return;
            var barRt  = mainFill.rectTransform;
            var parent = barRt.parent as RectTransform;
            if (parent == null) return;

            var rootGo = new GameObject("EscalateTimer", typeof(RectTransform), typeof(Image));
            var bgRt = (RectTransform)rootGo.transform;
            bgRt.SetParent(parent, false);
            bgRt.anchorMin = barRt.anchorMin; bgRt.anchorMax = barRt.anchorMax; bgRt.pivot = barRt.pivot;
            bgRt.sizeDelta = new Vector2(barRt.sizeDelta.x, 12f);
            float barH = Mathf.Abs(barRt.rect.height);
            bgRt.anchoredPosition = barRt.anchoredPosition + new Vector2(0f, -(barH * 0.5f) - 10f);
            var bg = rootGo.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f); bg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fRt = (RectTransform)fillGo.transform;
            fRt.SetParent(rootGo.transform, false);
            fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
            timerFill = fillGo.GetComponent<Image>();
            timerFill.color = new Color(1f, 0.95f, 0.4f, 0.9f);
            timerFill.raycastTarget = false;
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Horizontal;
            timerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        // ─────────────────────────────────────────────────────────────
        // "보스가 힘을 축적하고 있습니다" 팝업 (HP바 왼쪽)
        // ─────────────────────────────────────────────────────────────
        private void EnsurePopup()
        {
            if (_popupGo != null || mainFill == null) return;
            var parent = mainFill.rectTransform.parent as RectTransform;
            if (parent == null) return;

            _popupGo = new GameObject("PowerAccumPopup", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _popupRt = (RectTransform)_popupGo.transform;
            _popupRt.SetParent(parent, false);
            _popupRt.anchorMin = new Vector2(0f, 0.5f);
            _popupRt.anchorMax = new Vector2(0f, 0.5f);
            _popupRt.pivot     = new Vector2(1f, 0.5f);       // 오른쪽 끝 기준 → HP바 왼쪽 바깥
            _popupRt.sizeDelta = new Vector2(230f, 52f);
            _popupHome = new Vector2(-18f, 0f);               // HP바 왼쪽 여백
            _popupRt.anchoredPosition = _popupHome;

            var bg = _popupGo.GetComponent<Image>();
            bg.color = new Color(0.5f, 0.05f, 0.05f, 0.85f);  // 진홍 배경
            bg.raycastTarget = false;

            _popupCg = _popupGo.GetComponent<CanvasGroup>();
            _popupCg.alpha = 0f;

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var tRt = (RectTransform)txtGo.transform;
            tRt.SetParent(_popupGo.transform, false);
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(8f, 4f); tRt.offsetMax = new Vector2(-8f, -4f);
            var txt = txtGo.GetComponent<Text>();
            txt.text = "보스가 힘을\n축적하고 있습니다";
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 18;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            Font f = nameText != null ? nameText.font : (hpText != null ? hpText.font : null);
            if (f != null) txt.font = f;

            _popupGo.SetActive(false);
        }

        private void ShowPowerPopup()
        {
            EnsurePopup();
            if (_popupGo == null) return;
            if (_popupCo != null) StopCoroutine(_popupCo);
            _popupGo.SetActive(true);
            _popupCo = StartCoroutine(PopupRoutine());
        }

        private IEnumerator PopupRoutine()
        {
            const float inDur = 0.25f, hold = 1.7f, outDur = 0.6f, rise = 14f;
            float t = 0f;
            while (t < inDur) // 등장: 아래→제자리 + 페이드 인
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / inDur);
                _popupCg.alpha = p;
                _popupRt.anchoredPosition = _popupHome + new Vector2(0f, -rise * (1f - p));
                yield return null;
            }
            _popupCg.alpha = 1f; _popupRt.anchoredPosition = _popupHome;
            yield return new WaitForSeconds(hold);
            t = 0f;
            while (t < outDur) // 사라짐: 페이드 아웃 + 살짝 위로
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / outDur);
                _popupCg.alpha = 1f - p;
                _popupRt.anchoredPosition = _popupHome + new Vector2(0f, rise * p);
                yield return null;
            }
            _popupCg.alpha = 0f;
            _popupGo.SetActive(false);
            _popupCo = null;
        }
    }
}
