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
            if (lobbyConfirmYesButton != null) lobbyConfirmYesButton.onClick.AddListener(OnLobbyConfirm);
            if (lobbyConfirmNoButton != null) lobbyConfirmNoButton.onClick.AddListener(OnLobbyCancel);

            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingPopup != null) settingPopup.SetActive(false);
            if (lobbyConfirmPanel != null) lobbyConfirmPanel.SetActive(false);
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
                Toggle();
#endif
        }

        public void Toggle() { if (isPaused) Close(); else Open(); }

        public void Open()
        {
            if (isPaused) return;
            isPaused = true;
            if (pausePanel != null) pausePanel.SetActive(true);
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
            if (settingPopup != null) settingPopup.SetActive(true);
        }

        private void OnLobby()
        {
            // 2차 확인 모달 표시 (게임은 계속 일시정지 유지)
            if (lobbyConfirmPanel != null) lobbyConfirmPanel.SetActive(true);
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
            SceneManager.LoadScene(lobbySceneName);
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