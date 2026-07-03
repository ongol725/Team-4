using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬별 BGM 자동 재생 + 게임오버 징글.
///  - 타이틀/로비 계열 씬 = Title_BGM, 전투 계열(인게임/보스룸/테스트) = Ingame_BGM (루프).
///  - 사망 시 PlayGameOver()로 BGM 중단 후 게임오버 트랙 1회.
///  - 볼륨은 설정 팝업의 bgmOn/bgmVol(PlayerPrefs) 사용, ApplyVolume()으로 즉시 반영.
/// 부트스트랩으로 자동 생성(배치 불필요), 씬 전환에도 유지.
/// </summary>
public class BgmManager : MonoBehaviour
{
    public static BgmManager Instance { get; private set; }

    private const string TitlePath    = "02.BGM/Title_BGM";
    private const string IngamePath   = "02.BGM/Ingame_BGM";
    private const string GameOverPath = "02.BGM/GameOver_Fireball_Doomed";

    private AudioSource _src;
    private string _currentPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("BgmManager").AddComponent<BgmManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _src = gameObject.AddComponent<AudioSource>();
        _src.loop = true;
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;

        SceneManager.activeSceneChanged += OnSceneChanged;
        PlayForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.activeSceneChanged -= OnSceneChanged;
        Instance = null;
    }

    private void OnSceneChanged(Scene _, Scene next) => PlayForScene(next.name);

    private static float BgmVolume =>
        PlayerPrefs.GetInt("bgmOn", 1) == 1 ? PlayerPrefs.GetFloat("bgmVol", 0.8f) : 0f;

    private void PlayForScene(string sceneName)
    {
        string path = null;
        if (sceneName.Contains("Title") || sceneName.Contains("Lobby"))
            path = TitlePath;
        else if (sceneName.Contains("Ingame") || sceneName.Contains("BossRoom") || sceneName.Contains("TestBattle"))
            path = IngamePath;

        if (path == null || path == _currentPath) { ApplyVolume(); return; } // 매핑 없는 씬/같은 곡 = 유지
        var clip = Resources.Load<AudioClip>(path);
        if (clip == null) { Debug.LogWarning("[BgmManager] BGM 없음: " + path); return; }

        _currentPath = path;
        _src.loop = true;
        _src.clip = clip;
        _src.volume = BgmVolume;
        _src.Play();
    }

    /// <summary>게임오버: BGM 중단 후 게임오버 트랙 1회 재생(사망 결과창).</summary>
    public void PlayGameOver()
    {
        _currentPath = null; // 다음 씬 진입 시 BGM 다시 시작되게
        var clip = Resources.Load<AudioClip>(GameOverPath);
        if (clip == null) { _src.Stop(); return; }
        _src.loop = false;
        _src.clip = clip;
        _src.volume = BgmVolume;
        _src.Play();
    }

    /// <summary>설정 팝업에서 BGM 볼륨/토글 변경 시 즉시 반영.</summary>
    public void ApplyVolume()
    {
        if (_src != null) _src.volume = BgmVolume;
    }
}
