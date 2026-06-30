using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace BagSurvivor.UI
{
    /// <summary>
    /// 로비 씬 튜토리얼. 페이지형 팝업 패널을 코드로 자체 생성한다(프리팹 불필요).
    /// - 최초 1회(PlayerPrefs) 자동 표시, 이후엔 Open()/단축키로 수동 열람.
    /// - 페이지 데이터(pages)는 인스펙터에서 편집 가능. 비어 있으면 코드 기본값을 사용한다.
    /// - 이미지는 추후 채울 예정이므로 비워 두면 이미지 영역은 자동으로 숨긴다.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        [System.Serializable]
        public class TutorialPage
        {
            public string title;
            [TextArea(3, 8)] public string body;
            public Sprite image; // 비워 두면 이미지 영역 숨김
        }

        [Header("페이지 (비어 있으면 코드 기본값 사용)")]
        [SerializeField] private TutorialPage[] pages;

        [Header("동작 설정")]
        [Tooltip("켜면 PlayerPrefs를 무시하고 플레이할 때마다 항상 표시 (개발/테스트용 — 출시 전 끌 것)")]
        [SerializeField] private bool alwaysShowOnPlay = false;
        [Tooltip("최초 1회 자동 표시 여부 (PlayerPrefs로 관람 여부 기록)")]
        [SerializeField] private bool autoShowOnFirstVisit = true;
        [Tooltip("자동 표시 여부 판단에 쓰는 PlayerPrefs 키")]
        [SerializeField] private string seenPrefKey = "TutorialSeen";

        private Canvas _canvas;
        private GameObject _root;   // Dimmer 루트 — 딤+패널 전체를 함께 토글
        private GameObject _panel;
        private TMP_FontAsset _font;

        private Image _pageImage;
        private RectTransform _pageImageRT;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _bodyText;
        private TextMeshProUGUI _indicatorText;
        private Button _prevButton;
        private Button _nextButton;
        private TextMeshProUGUI _nextLabel;

        private TutorialPage[] _activePages;
        private int _index;
        private bool _isVisible;

        // ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _font = Resources.Load<TMP_FontAsset>("Fonts/BoldDunggeunmo SDF Damage");
            EnsureEventSystem();
            BuildUI();
            SetVisible(false);
        }

        /// <summary>씬에 EventSystem이 없으면 생성한다(없으면 버튼 클릭이 동작하지 않음).</summary>
        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private void Start()
        {
            // BuildUI는 Awake에서 끝나므로 추가 대기 없이 첫 프레임에 바로 표시한다.
            if (alwaysShowOnPlay)
            {
                Open(); // PlayerPrefs 무시하고 매번 표시
                return;
            }
            if (autoShowOnFirstVisit && PlayerPrefs.GetInt(seenPrefKey, 0) == 0)
            {
                Open();
                PlayerPrefs.SetInt(seenPrefKey, 1);
                PlayerPrefs.Save();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Public API

        public void Open()
        {
            _activePages = (pages != null && pages.Length > 0) ? pages : BuildDefaultPages();
            _index = 0;
            ShowPage(_index);
            SetVisible(true);
        }

        public void Close() => SetVisible(false);

        /// <summary>관람 기록을 지워 다음 진입 시 다시 자동 표시되게 한다(디버그/리셋용).</summary>
        public void ResetSeenFlag() => PlayerPrefs.DeleteKey(seenPrefKey);

        // ─────────────────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_root != null) _root.SetActive(visible);
        }

        private void Prev()
        {
            if (_index <= 0) return;
            ShowPage(--_index);
        }

        private void Next()
        {
            if (_index >= _activePages.Length - 1) { Close(); return; }
            ShowPage(++_index);
        }

        private void ShowPage(int i)
        {
            var page = _activePages[i];
            if (_titleText != null) _titleText.text = page.title;
            if (_bodyText != null) _bodyText.text = page.body;
            if (_indicatorText != null) _indicatorText.text = $"{i + 1} / {_activePages.Length}";

            bool hasImage = page.image != null;
            if (_pageImageRT != null) _pageImageRT.gameObject.SetActive(hasImage);
            if (hasImage) _pageImage.sprite = page.image;

            if (_prevButton != null) _prevButton.interactable = i > 0;
            if (_nextLabel != null)
                _nextLabel.text = (i >= _activePages.Length - 1) ? "닫기" : "다음";
        }

        // ─────────────────────────────────────────────────────────────
        // 기본 페이지 (인스펙터가 비어 있을 때)

        private static TutorialPage[] BuildDefaultPages() => new[]
        {
            new TutorialPage
            {
                title = "이동",
                body  = "WASD 키로 캐릭터를 상하좌우로 이동합니다.",
            },
            new TutorialPage
            {
                title = "복도",
                body  = "복도에서 배낭을 열고 닫을 수 있습니다.",
            },
            new TutorialPage
            {
                title = "엘리트 방 / 계단",
                body  = "문이 있는 방에 들어가면 엘리트를 처치할 때까지 밖으로 나갈 수 없습니다.\n" +
                        "엘리트를 처치하면 다음 층으로 내려가는 계단이 생성됩니다.",
            },
        };

        // ─────────────────────────────────────────────────────────────
        // UI 빌드 (런타임 코드 생성)

        private void BuildUI()
        {
            // ── 자체 Canvas ──
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 110; // 다른 팝업(100)보다 위

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // ── 딤 배경 (클릭 차단) ── 딤+패널 전체를 이 루트로 토글
            var dimmerGO = MakeGO("Dimmer", transform);
            _root = dimmerGO;
            var dimmerRT = dimmerGO.GetComponent<RectTransform>();
            Stretch(dimmerRT);
            var dimmer = dimmerGO.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.7f);

            // ── 패널 ──
            _panel = MakeGO("Panel", dimmerGO.transform);
            var panelRT = _panel.GetComponent<RectTransform>();
            panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(720f, 520f);

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.11f, 0.98f);
            var outline = _panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.6f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            // ── 타이틀 ──
            _titleText = MakeText(_panel.transform, "Title", 40,
                new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, FontStyles.Bold);
            var titleRT = _titleText.rectTransform;
            titleRT.anchorMin = new Vector2(0f, 1f);
            titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.offsetMin = new Vector2(30f, -90f);
            titleRT.offsetMax = new Vector2(-30f, -24f);

            // ── 이미지 영역 (추후 채움, 비면 숨김) ──
            var imageGO = MakeGO("PageImage", _panel.transform);
            _pageImageRT = imageGO.GetComponent<RectTransform>();
            _pageImageRT.anchorMin = new Vector2(0.5f, 1f);
            _pageImageRT.anchorMax = new Vector2(0.5f, 1f);
            _pageImageRT.pivot = new Vector2(0.5f, 1f);
            _pageImageRT.sizeDelta = new Vector2(360f, 200f);
            _pageImageRT.anchoredPosition = new Vector2(0f, -100f);
            _pageImage = imageGO.AddComponent<Image>();
            _pageImage.preserveAspect = true;
            imageGO.SetActive(false);

            // ── 본문 ──
            _bodyText = MakeText(_panel.transform, "Body", 24,
                new Color(0.9f, 0.9f, 0.9f), TextAlignmentOptions.Top, FontStyles.Normal);
            var bodyRT = _bodyText.rectTransform;
            bodyRT.anchorMin = new Vector2(0f, 0f);
            bodyRT.anchorMax = new Vector2(1f, 1f);
            bodyRT.pivot = new Vector2(0.5f, 0.5f);
            bodyRT.offsetMin = new Vector2(40f, 90f);   // 하단 버튼 영역 확보
            bodyRT.offsetMax = new Vector2(-40f, -110f); // 타이틀 영역 확보

            // ── 페이지 표시기 ──
            _indicatorText = MakeText(_panel.transform, "Indicator", 20,
                new Color(0.6f, 0.6f, 0.7f), TextAlignmentOptions.Center, FontStyles.Normal);
            var indRT = _indicatorText.rectTransform;
            indRT.anchorMin = new Vector2(0.5f, 0f);
            indRT.anchorMax = new Vector2(0.5f, 0f);
            indRT.pivot = new Vector2(0.5f, 0f);
            indRT.sizeDelta = new Vector2(200f, 36f);
            indRT.anchoredPosition = new Vector2(0f, 26f);

            // ── 버튼: 이전 / 다음(닫기) / 닫기(X) ──
            _prevButton = MakeButton(_panel.transform, "PrevButton", "이전",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(150f, 56f), new Vector2(110f, 28f), out _);
            _nextButton = MakeButton(_panel.transform, "NextButton", "다음",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(150f, 56f), new Vector2(-110f, 28f), out _nextLabel);

            var closeButton = MakeButton(_panel.transform, "CloseButton", "X",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(48f, 48f), new Vector2(-30f, -30f), out _);

            _prevButton.onClick.AddListener(Prev);
            _nextButton.onClick.AddListener(Next);
            closeButton.onClick.AddListener(Close);
        }

        // ─────────────────────────────────────────────────────────────
        // UI 헬퍼

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private TextMeshProUGUI MakeText(Transform parent, string name, float size,
            Color color, TextAlignmentOptions align, FontStyles style)
        {
            var go = MakeGO(name, parent);
            var txt = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) txt.font = _font;
            txt.fontSize = size;
            txt.color = color;
            txt.alignment = align;
            txt.fontStyle = style;
            txt.enableWordWrapping = true;
            txt.raycastTarget = false;
            return txt;
        }

        private Button MakeButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 size, Vector2 anchoredPos, out TextMeshProUGUI labelText)
        {
            var go = MakeGO(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.32f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            labelText = MakeText(go.transform, "Label", 22, Color.white,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(labelText.rectTransform);
            labelText.text = label;

            return btn;
        }
    }
}
