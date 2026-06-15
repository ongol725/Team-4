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

            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingPopup != null) settingPopup.SetActive(false);
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
            if (pausePanel != null) pausePanel.SetActive(false);
            Time.timeScale = 1f; // 재개
        }

        private void OpenSetting()
        {
            if (settingPopup != null) settingPopup.SetActive(true);
        }

        private void OnLobby()
        {
            // TODO: 탈주/포기 방지 2차 확인 모달 -> 로비(메인) 씬 로드
            Time.timeScale = 1f;
            Debug.Log("[PausePopup] 로비 이동 요청 (2차 확인 모달/씬 로드 미구현)");
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