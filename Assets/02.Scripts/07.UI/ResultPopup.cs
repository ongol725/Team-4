// ============================================================
// ResultPopup.cs
// 전투화면 L7 - 결과 화면 (클리어/실패 공용)
//  - 클리어/실패는 제목·색만 다르게 (기획서: 디자인만 다르게)
//  - 통계: 플레이 시간 / 사용 시너지 / 배치 무기 / 처치 몬스터 / 소모 골드
//  - 통계 시스템 연결 전: Show(isClear, ResultStats) 로 외부에서 주입
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace BagSurvivor.UI
{
    [System.Serializable]
    public class ResultStats
    {
        public float playTime;        // 초
        public string mainSynergies;  // 사용한 메인 시너지(표시용 문자열)
        public int weaponCount;       // 배치한 무기 수
        public int killCount;         // 잡은 몬스터 수
        public int goldSpent;         // 소모한 골드
    }

    public class ResultPopup : MonoBehaviour
    {
        [Header("참조")]
        public GameObject panel;
        public Image frameImage;
        public Text titleText;
        public Text playTimeText;
        public Text synergyText;
        public Text weaponText;
        public Text killText;
        public Text goldText;
        public Button lobbyButton;   // 로비 이동(153003)
        public Button retryButton;   // 다시하기

        [Header("클리어/실패 색")]
        public Color clearColor = new Color(0.16f, 0.38f, 0.62f, 0.98f);
        public Color failColor = new Color(0.45f, 0.14f, 0.14f, 0.98f);

        [Header("씬 이름 (Build Settings 등록 필요)")]
        [Tooltip("다시하기 시 로드할 인게임 시작 씬 (층마다 다른 씬이라 항상 첫 인게임 씬으로 재시작)")]
        public string ingameSceneName = "02.Ingame";
        [Tooltip("로비 이동 시 로드할 씬")]
        public string lobbySceneName = "01.Lobby";

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (lobbyButton != null) lobbyButton.onClick.AddListener(OnLobby);
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
        }

        /// <summary>결과 화면 표시. isClear=true 클리어, false 실패.</summary>
        public void Show(bool isClear, ResultStats s)
        {
            if (panel != null) panel.SetActive(true);
            Time.timeScale = 0f;

            if (titleText != null) titleText.text = isClear ? "클리어!" : "실패...";
            if (frameImage != null) frameImage.color = isClear ? clearColor : failColor;

            if (s != null)
            {
                if (playTimeText != null) playTimeText.text = "플레이 시간    " + FormatTime(s.playTime);
                if (synergyText != null) synergyText.text = "사용 시너지    " + s.mainSynergies;
                if (weaponText != null) weaponText.text = "배치 무기    " + s.weaponCount + " 개";
                if (killText != null) killText.text = "처치 몬스터    " + s.killCount + " 마리";
                if (goldText != null) goldText.text = "소모 골드    " + s.goldSpent.ToString("N0");
            }
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
            Time.timeScale = 1f;
        }

        private string FormatTime(float t)
        {
            int m = (int)(t / 60f);
            int sec = (int)(t % 60f);
            return string.Format("{0:00}:{1:00}", m, sec);
        }

        private void OnLobby()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(lobbySceneName);
        }

        private void OnRetry()
        {
            // 층마다 씬이 다르므로, 다시하기는 항상 첫 인게임 씬(02.Ingame)부터 = 1층 새 런으로 재시작
            Time.timeScale = 1f;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.currentFloor = 1;
                GameManager.Instance.ResetGold(); // 새 런이므로 골드 초기화
            }

            // 시간 비례 난이도 타이머 초기화 (DontDestroyOnLoad로 유지되므로 명시적 리셋)
            if (BagSurvivor.Monster.DifficultyScaler.Instance != null)
                BagSurvivor.Monster.DifficultyScaler.Instance.ResetTimer();

            SceneManager.LoadScene(ingameSceneName);
        }
    }
}
