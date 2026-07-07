// ============================================================
// SettingsPopup.cs
// 전투화면 L7 - 설정 팝업 (battle_settingPopup)
//  - 화면 모드 좌우 순환: 전체화면(143002)/창모드(143003)/테두리없는창모드(143004)
//  - 배경음(143020)/효과음(143021): 토글 + 볼륨 슬라이더 (PlayerPrefs 저장, AudioMixer 연결 시 적용)
//  - 닫기(X) / 확인(143022)
//  ※ 키 변경(리바인딩)은 다음 단계에서 추가
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

namespace BagSurvivor.UI
{
    public class SettingsPopup : MonoBehaviour
    {
        [Header("닫기 / 확인")]
        public Button closeButton;
        public Button okButton;

        [Header("화면 모드")]
        public Text screenModeLabel;  // "화면 모드"
        public Text screenModeValue;  // 현재 모드 표시
        public Button screenModeLeft;
        public Button screenModeRight;

        [Header("배경음")]
        public Text bgmLabel;
        public Toggle bgmToggle;
        public Slider bgmSlider;

        [Header("효과음")]
        public Text sfxLabel;
        public Toggle sfxToggle;
        public Slider sfxSlider;

        [Header("오디오 믹서 (선택 - 연결 시 실제 볼륨 적용)")]
        public AudioMixer mixer;
        public string bgmParam = "BGMVol";
        public string sfxParam = "SFXVol";

        private static readonly FullScreenMode[] modes =
        {
            FullScreenMode.ExclusiveFullScreen, // 전체화면
            FullScreenMode.Windowed,            // 창모드
            FullScreenMode.FullScreenWindow     // 테두리 없는 창 모드
        };

        // 저장된 화면 모드를 게임 시작 시 '1회만' 적용한다.
        // 화면 모드는 씬이 바뀌어도 유지되므로, 씬마다(SettingsPopup.Awake) 재적용하면
        // 스테이지 진입 때마다 해상도가 재설정되어 화면이 번쩍이고 모드가 바뀌던 문제가 생긴다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedScreenModeOnce()
        {
            if (!PlayerPrefs.HasKey("screenModeIdx")) return; // 저장값 없으면 빌드 기본값 유지
            ApplyScreenMode(PlayerPrefs.GetInt("screenModeIdx", 1));
        }
        private readonly int[] modeCodes = { 143002, 143003, 143004 };
        private int modeIndex;

        private void Awake()
        {
            if (screenModeLabel != null) screenModeLabel.text = StringTable.Get(143001);
            if (bgmLabel != null) bgmLabel.text = StringTable.Get(143020);
            if (sfxLabel != null) sfxLabel.text = StringTable.Get(143021);
            SetOkLabel();

            // 화면 모드 — 현재 상태를 UI에 표시만 한다(실제 적용은 시작 시 1회 + 사용자가 바꿀 때만).
            // 씬 로드마다 재적용하면 화면이 번쩍이므로 여기선 SetResolution을 호출하지 않는다.
            modeIndex = PlayerPrefs.GetInt("screenModeIdx", CurrentModeIndex());
            UpdateScreenModeText();
            if (screenModeLeft != null) screenModeLeft.onClick.AddListener(delegate { CycleMode(-1); });
            if (screenModeRight != null) screenModeRight.onClick.AddListener(delegate { CycleMode(1); });

            // 볼륨
            if (bgmToggle != null) { bgmToggle.isOn = PlayerPrefs.GetInt("bgmOn", 1) == 1; bgmToggle.onValueChanged.AddListener(delegate { ApplyAudio(); Save(); }); }
            if (sfxToggle != null) { sfxToggle.isOn = PlayerPrefs.GetInt("sfxOn", 1) == 1; sfxToggle.onValueChanged.AddListener(delegate { ApplyAudio(); Save(); }); }
            if (bgmSlider != null) { bgmSlider.minValue = 0f; bgmSlider.maxValue = 1f; bgmSlider.value = PlayerPrefs.GetFloat("bgmVol", 0.8f); bgmSlider.onValueChanged.AddListener(delegate { ApplyAudio(); Save(); }); }
            if (sfxSlider != null) { sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f; sfxSlider.value = PlayerPrefs.GetFloat("sfxVol", 0.8f); sfxSlider.onValueChanged.AddListener(delegate { ApplyAudio(); Save(); }); }

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (okButton != null) okButton.onClick.AddListener(Close);

            ApplyAudio();
        }

        private void SetOkLabel()
        {
            if (okButton == null) return;
            var t = okButton.GetComponentInChildren<Text>(true);
            if (t != null) t.text = StringTable.Get(143022); // 확인
        }

        // ===== 화면 모드 =====
        private int CurrentModeIndex()
        {
            for (int i = 0; i < modes.Length; i++)
                if (modes[i] == Screen.fullScreenMode) return i;
            return 1; // 기본 창모드
        }

        private void CycleMode(int dir)
        {
            modeIndex = (modeIndex + dir + modes.Length) % modes.Length;
            ApplyScreenMode(modeIndex);
            UpdateScreenModeText();
            Save();
        }

        /// <summary>화면 모드 실제 적용. 창모드=1280x720, 전체/테두리없음=1920x1080 고정.
        /// (노트북마다 네이티브 해상도가 달라 화면이 깨지던 문제 방지 — 백버퍼를 1920x1080으로 고정하고
        ///  GPU가 모니터에 맞춰 스케일링)</summary>
        private static void ApplyScreenMode(int idx)
        {
            FullScreenMode mode = modes[Mathf.Clamp(idx, 0, modes.Length - 1)];
            if (mode == FullScreenMode.Windowed)
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            else
                Screen.SetResolution(1920, 1080, mode);
        }

        private void UpdateScreenModeText()
        {
            if (screenModeValue != null) screenModeValue.text = StringTable.Get(modeCodes[modeIndex]);
        }

        // ===== 볼륨 =====
        private void ApplyAudio()
        {
            bool bgmOn = bgmToggle == null || bgmToggle.isOn;
            bool sfxOn = sfxToggle == null || sfxToggle.isOn;
            float bgmV = bgmSlider != null ? bgmSlider.value : 0.8f;
            float sfxV = sfxSlider != null ? sfxSlider.value : 0.8f;

            if (mixer != null)
            {
                mixer.SetFloat(bgmParam, ToDb(bgmOn ? bgmV : 0f));
                mixer.SetFloat(sfxParam, ToDb(sfxOn ? sfxV : 0f));
            }
            // BGM은 BgmManager가 PlayerPrefs를 읽어 재생 — 저장 후 즉시 반영
            Save();
            BgmManager.Instance?.ApplyVolume();
        }

        private float ToDb(float linear)
        {
            return linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
        }

        private void Save()
        {
            PlayerPrefs.SetInt("screenModeIdx", modeIndex);
            if (bgmToggle != null) PlayerPrefs.SetInt("bgmOn", bgmToggle.isOn ? 1 : 0);
            if (sfxToggle != null) PlayerPrefs.SetInt("sfxOn", sfxToggle.isOn ? 1 : 0);
            if (bgmSlider != null) PlayerPrefs.SetFloat("bgmVol", bgmSlider.value);
            if (sfxSlider != null) PlayerPrefs.SetFloat("sfxVol", sfxSlider.value);
            PlayerPrefs.Save();
        }

        public void Close()
        {
            Save();
            gameObject.SetActive(false);
        }
    }
}