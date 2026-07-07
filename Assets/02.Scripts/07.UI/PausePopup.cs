// ============================================================
// PausePopup.cs
// 전투화면 L7 - 일시정지 팝업 (Temporary_Popup)
//  - 우상단 일시정지 버튼(Main_Paus) 또는 ESC로 열기/닫기
//  - 열리면 Time.timeScale=0 으로 게임 일시정지, Punch Scale 연출
//  - 이어하기(153001) / 설정(153002) / 로비 이동(153003)
//  버튼 라벨은 StringTable에서 가져옴
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BagSurvivor.UI
{
    public class PausePopup : MonoBehaviour
    {
        [Header("참조")]
        public Button mainPauseButton;   // 우상단 일시정지 버튼
        public GameObject pausePanel;    // 팝업 패널(프레임+버튼+딤)
        public RectTransform frame;      // Punch Scale 연출 대상
        public Button continueButton;    // 이어하기
        public Button settingButton;     // 설정
        public Button lobbyButton;       // 로비 이동
        public Button quitButton;        // 게임 종료

        [Header("설정 팝업 (선택)")]
        public GameObject settingPopup;

        [Header("로비 이동 확인 모달")]
        public GameObject lobbyConfirmPanel;       // 확인 모달 패널(딤+박스)
        public Button lobbyConfirmYesButton;       // 나가기(확인)
        public Button lobbyConfirmNoButton;        // 취소
        [Tooltip("로비(메인) 씬 이름 — Build Settings에 등록돼 있어야 함")]
        public string lobbySceneName = "01.Lobby";

        [Header("연출")]
        public float punchTime = 0.18f;

        private bool isPaused;
        private bool _bagOpen;   // 가방(인벤토리) 열림 상태 — ESC/일시정지 버튼 게이트

        private void OnDestroy()
        {
            // 씬 전환 등 외부 경로로 파괴될 때 timeScale 복구
            if (isPaused) Time.timeScale = 1f;
            InventoryPopupToggle.onPopupToggled -= OnBagToggled;
        }

        private void Awake()
        {
            // 버튼 라벨 (스트링 테이블)
            SetLabel(continueButton, 153001);
            SetLabel(settingButton, 153002);
            SetLabel(lobbyButton, 153003);

            if (mainPauseButton != null) mainPauseButton.onClick.AddListener(Open);
            if (continueButton != null) continueButton.onClick.AddListener(Close);
            if (settingButton != null) settingButton.onClick.AddListener(OpenSetting);
            if (lobbyButton != null) lobbyButton.onClick.AddListener(OnLobby);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
            if (lobbyConfirmYesButton != null) lobbyConfirmYesButton.onClick.AddListener(OnLobbyConfirm);
            if (lobbyConfirmNoButton != null) lobbyConfirmNoButton.onClick.AddListener(OnLobbyCancel);

            // 가방(인벤토리) 열림/닫힘 구독 — 열리면 일시정지 버튼 UI·기능 끔, 닫히면 복구
            InventoryPopupToggle.onPopupToggled += OnBagToggled;

            // 인벤토리 등 다른 Canvas(sortingOrder 10)보다 위에 렌더링되도록 Canvas override 설정.
            // 모달(설정/로비확인)은 일시정지 메뉴보다 더 위 → 클릭 가능하도록 정렬값 분리.
            // 보스룸 등 다른 씬에 있던 sortingOrder 120대 캔버스에 밀리지 않도록 확인 모달을 크게 올린다.
            EnsureTopCanvas(pausePanel, 500);
            EnsureTopCanvas(settingPopup, 510);
            EnsureTopCanvas(lobbyConfirmPanel, 520);

            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingPopup != null) settingPopup.SetActive(false);
            if (lobbyConfirmPanel != null) lobbyConfirmPanel.SetActive(false);
        }

        private static void EnsureTopCanvas(GameObject panel, int order = 100)
        {
            if (panel == null) return;
            var canvas = panel.GetComponent<Canvas>();
            if (canvas == null) canvas = panel.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
            if (panel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                panel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        private void SetLabel(Button b, int code)
        {
            if (b == null) return;
            var t = b.GetComponentInChildren<Text>(true);
            if (t != null) t.text = StringTable.Get(code);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // 튜토리얼이 열려 있거나 이번 프레임에 튜토리얼이 ESC로 닫혔으면 일시정지는 열지 않는다
                // (실행 순서와 무관하게 안전: 열림 중이면 IsOpen, 이미 닫혔으면 LastEscCloseFrame로 감지)
                if (TutorialController.IsOpen || TutorialController.LastEscCloseFrame == Time.frameCount)
                {
                    // 튜토리얼이 ESC를 소비 — 일시정지 토글 안 함
                }
                else if (_bagOpen) InventoryPopupToggle.Instance?.CloseBag(); // 가방 열림 중엔 ESC = 가방 닫기
                else Toggle();                                               // 그 외엔 일시정지 토글
            }
#endif
        }

        // 가방 열림 시 일시정지 버튼 UI/기능 비활성, 닫히면 복구
        private void OnBagToggled(bool open)
        {
            _bagOpen = open;
            if (mainPauseButton != null) mainPauseButton.gameObject.SetActive(!open);
        }

        public void Toggle() { if (isPaused) Close(); else Open(); }

        public void Open()
        {
            if (isPaused) return;
            isPaused = true;
            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
                EnsureTopCanvas(pausePanel, 500); // 활성화 후 재보장 (overrideSorting은 활성 상태에서만 적용됨)
            }
            Time.timeScale = 0f; // 게임 일시정지
            // TODO: 인게임 효과음 비활성화 (BGM은 유지)
            if (frame != null) StartCoroutine(PunchScale());
        }

        public void Close()
        {
            isPaused = false;
            if (settingPopup != null) settingPopup.SetActive(false);
            if (lobbyConfirmPanel != null) lobbyConfirmPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            Time.timeScale = 1f; // 재개
        }

        private void OpenSetting()
        {
            if (settingPopup != null)
            {
                settingPopup.SetActive(true);
                EnsureTopCanvas(settingPopup, 510); // 활성화 후 재보장
            }
        }

        private void OnLobby()
        {
            // 2차 확인 모달 표시 (게임은 계속 일시정지 유지)
            if (lobbyConfirmPanel != null)
            {
                // overrideSorting은 활성 상태에서만 적용되므로 반드시 SetActive(true) 후 호출한다.
                // (비활성 pausePanel의 자식이라 Awake 시점엔 override가 걸리지 않아 뒤로 밀렸음)
                lobbyConfirmPanel.SetActive(true);
                EnsureTopCanvas(lobbyConfirmPanel, 520);
                lobbyConfirmPanel.transform.SetAsLastSibling(); // 형제 중 맨 위로 → 일시정지 메뉴 앞에 렌더+클릭
            }
            else OnLobbyConfirm(); // 모달이 없으면 바로 이동
        }

        private void OnLobbyCancel()
        {
            // 모달만 닫고 일시정지 화면으로 복귀 (게임 재개 안 함)
            if (lobbyConfirmPanel != null) lobbyConfirmPanel.SetActive(false);
        }

        private void OnLobbyConfirm()
        {
            Time.timeScale = 1f; // 다음 씬이 멈춘 채 시작하지 않도록 복구
            GameManager.Instance?.ClearLoadout();
            SceneManager.LoadScene(lobbySceneName);
        }

        /// <summary>게임 종료. 빌드에서는 앱 종료, 에디터에서는 플레이 중지.</summary>
        private void OnQuit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // timeScale=0 상태이므로 unscaled 시간 사용
        private IEnumerator PunchScale()
        {
            if (frame == null) yield break;

            float t = 0f;
            frame.localScale = Vector3.one * 0.8f;
            while (t < punchTime)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / punchTime);
                frame.localScale = Vector3.one * Mathf.SmoothStep(0.8f, 1.05f, p);
                yield return null;
            }

            t = 0f; float settle = 0.08f;
            while (t < settle)
            {
                t += Time.unscaledDeltaTime;
                frame.localScale = Vector3.one * Mathf.Lerp(1.05f, 1f, t / settle);
                yield return null;
            }
            frame.localScale = Vector3.one;
        }
    }
}