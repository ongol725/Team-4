// ============================================================
// BossHpBar.cs
// 최종 보스 전용 상단 고정 HP바 (겹/layer 모델)
//  - 보스 고유 최대체력을 baseLayers(기본 5)겹으로 나눔. 1겹 = 한 색.
//  - 데미지로 현재 겹을 다 깎으면 아래 겹 색으로 전환(무지개색 순환).
//  - 런 경과시간(층 누적)에 따라 escalateInterval(1분)마다 +1겹(최대 maxLayers).
//    → 늦게 도착·오래 끌수록 겹이 쌓여(총 HP↑) 압박.
//  - HP바 바로 아래에 겹 상승까지 남은 시간을 보여주는 타이머 바(자동 생성).
// ============================================================
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
        [Tooltip("기본 겹수(보스 고유 최대체력을 이 수로 나눠 1겹 HP 결정)")]
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

        private int _layerHP;        // 1겹당 HP (연결 시 확정)
        private int _appliedLayers;  // 현재까지 반영된 겹수
        private int _lastLayerIdx = -1;

        /// <summary>보스 몬스터 연결.</summary>
        public void SetTarget(MonsterController mc, string bossName)
        {
            target = mc;
            if (nameText != null) nameText.text = bossName;

            _layerHP = Mathf.Max(1, mc.MaxHP / Mathf.Max(1, baseLayers));
            _appliedLayers = baseLayers; // 보스 고유 최대 = 기본 겹수
            _lastLayerIdx = -1;

            EnsureTimerBar();

            float within = CurrentLayerFill(out int idx);
            _lastLayerIdx = idx;
            lastCur = within; displayedDelayed = within; catchFrom = within;
            ApplyLayerColor(idx);
            SetMain(within); SetDelayed(within);
            RefreshHpText(idx);
        }

        private float RunElapsed()
        {
            var rs = RunStatsLogger.Instance;
            return rs != null ? rs.RunElapsedSeconds : Time.time;
        }

        private void Update()
        {
            // 시간에 따른 겹 상승 (연결된 보스 한정)
            if (target != null && !target.IsDead && _layerHP > 0)
            {
                int targetLayers = Mathf.Clamp(
                    baseLayers + Mathf.FloorToInt(RunElapsed() / Mathf.Max(1f, escalateInterval)),
                    baseLayers, maxLayers);
                while (_appliedLayers < targetLayers)
                {
                    target.IncreaseMaxHP(_layerHP); // 최대+현재 +1겹 (덧씌워 채움)
                    _appliedLayers++;
                }
            }

            // 겹 상승 타이머 바
            if (timerFill != null)
            {
                bool maxed = target == null || _appliedLayers >= maxLayers;
                timerFill.fillAmount = maxed ? 0f
                    : Mathf.Repeat(RunElapsed(), escalateInterval) / Mathf.Max(1f, escalateInterval);
            }

            float cur = CurrentLayerFill(out int layerIdx);

            // 겹이 바뀌면(경계 통과/새 겹) 색·지연 게이지 갱신
            if (layerIdx != _lastLayerIdx)
            {
                _lastLayerIdx = layerIdx;
                ApplyLayerColor(layerIdx);
                displayedDelayed = cur; catchFrom = cur; delayTimer = 0f; catchTimer = 0f;
                SetDelayed(cur);
            }

            SetMain(cur);
            RefreshHpText(layerIdx);

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

        private int CurHP() => target != null ? target.CurrentHP : standaloneCurHP;
        private int MaxHP() => target != null ? Mathf.Max(1, target.MaxHP) : Mathf.Max(1, standaloneMaxHP);

        // 현재 HP가 속한 겹 index(1~appliedLayers)와 그 겹 내 채움 비율(0~1)
        private float CurrentLayerFill(out int idx)
        {
            int cur = CurHP();
            if (cur <= 0) { idx = 0; return 0f; }
            if (_layerHP <= 0) { idx = 1; return Mathf.Clamp01((float)cur / MaxHP()); } // 미연결=단일 바
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

        private void RefreshHpText(int idx)
        {
            if (hpText == null) return;
            hpText.text = CurHP() + " / " + MaxHP() + (idx > 0 ? $"  ({idx}/{_appliedLayers}겹)" : "");
        }

        private void SetMain(float v) { if (mainFill != null) mainFill.fillAmount = v; }
        private void SetDelayed(float v) { if (delayedFill != null) delayedFill.fillAmount = v; }

        // HP바 바로 아래에 겹 상승 타이머 바를 자동 생성(인스펙터에서 timerFill을 직접 꽂으면 그걸 사용).
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
            bgRt.anchoredPosition = barRt.anchoredPosition + new Vector2(0f, -(barH * 0.5f) - 10f); // 바 바로 아래
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
    }
}
