// ============================================================
// PlayerStateHUD.cs
// 전투화면 L3 - Player_State HUD 컨트롤러
//  - 골드(111002), 강화석(111003), 현재 체력바(111004) 표시
//  - 백엔드(재화/체력 시스템) 연동 전까지 Mock 데이터로 동작
//  - 외부에서 SetGold / SetEnhanceStone / SetHealth 호출로 갱신
// ============================================================
using UnityEngine;
using UnityEngine.UI;

namespace BagSurvivor.UI
{
    public class PlayerStateHUD : MonoBehaviour
    {
        [Header("재화")]
        public Text goldText;
        // 강화석 표시는 제거됨(해당 슬롯은 "체력" 라벨로 대체). enhanceStoneText 필드 삭제.

        [Header("체력")]
        [Tooltip("Filled(Horizontal) 타입 Image")]
        public Image healthFill;
        public Text healthText;

        [Header("아이콘 / 버튼")]
        public Image charIcon;
        public Button shopButton;

        [Header("Mock 데이터 (백엔드 연동 전 임시)")]
        public bool useMockData = true;
        public int mockGold = 1234;
        public int mockMaxHP = 100;
        public int mockCurrentHP = 100;

        private GameManager _gameManager;

        private void Start()
        {
            // 골드: GameManager가 단일 소스 (씬/층을 넘어 유지). 변경 시 자동 갱신.
            _gameManager = GameManager.Instance;
            if (_gameManager != null)
            {
                _gameManager.onGoldChanged += SetGold;
                SetGold(_gameManager.gold);
            }
            else if (useMockData)
            {
                SetGold(mockGold);
            }

            // 체력 초기값(곧 PlayerHealth가 실제 값으로 덮어씀)
            if (useMockData) SetHealth(mockCurrentHP, mockMaxHP);
        }

        private void OnDestroy()
        {
            if (_gameManager != null) _gameManager.onGoldChanged -= SetGold;
        }

        public void SetGold(int value)
        {
            if (goldText != null) goldText.text = value.ToString("N0");
        }

        public void SetHealth(int current, int max)
        {
            current = Mathf.Clamp(current, 0, max);
            if (healthFill != null)
                healthFill.fillAmount = max > 0 ? (float)current / max : 0f;
            if (healthText != null)
                healthText.text = current + " / " + max;
        }
    }
}