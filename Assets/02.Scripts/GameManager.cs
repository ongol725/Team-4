using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("GameManager");
                _instance = go.AddComponent<GameManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [HideInInspector] public int currentFloor = 1;

    private SO_GameSettings _settings;

    // 골드 (씬/층을 넘어 유지). 변경 시 onGoldChanged로 HUD 등에 통지.
    [HideInInspector] public int gold;
    public event System.Action<int> onGoldChanged;

    // 전투 로드아웃 브릿지 — 인벤토리 팝업 닫힐 때 BattleLoadoutBuilder가 호출,
    // PlayerStats/PlayerHealth 등 전투 씬 컴포넌트가 구독한다.
    public BattleLoadout CurrentLoadout { get; private set; }
    public event System.Action<BattleLoadout> onLoadoutReady;

    public void ApplyLoadout(BattleLoadout loadout)
    {
        if (loadout == null) return;
        CurrentLoadout = loadout;
        onLoadoutReady?.Invoke(loadout);
    }

    /// <summary>
    /// 새 런 시작 또는 씬 전환 전 호출. 이전 로드아웃이 새 씬에서 즉시 적용되는 것을 방지한다.
    /// </summary>
    public void ClearLoadout()
    {
        CurrentLoadout = null;
    }

    /// <summary>골드를 추가합니다.</summary>
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        gold += amount;
        onGoldChanged?.Invoke(gold);
    }

    /// <summary>골드를 차감합니다. 잔액이 부족하면 false를 반환하고 차감하지 않습니다.</summary>
    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (gold < amount) return false;
        gold -= amount;
        onGoldChanged?.Invoke(gold);
        return true;
    }

    /// <summary>골드를 시작 기본값으로 초기화합니다. (새 런 시작 시)</summary>
    public void ResetGold()
    {
        gold = _settings != null ? _settings.startingGold : 200;
        onGoldChanged?.Invoke(gold);
    }

    [Header("층 전환 방식")]
    [Tooltip("false = 같은 씬에서 맵 재생성 (프로토타입)\ntrue  = 층별 씬 전환 (정식 버전)")]
    public bool useSceneTransition = false;

    [Header("씬 전환 설정 (useSceneTransition = true 일 때 사용)")]
    [Tooltip("1층부터 순서대로 씬 이름 입력 (마지막이 보스 씬)")]
    public string[] floorSceneNames = { "02.Floor1", "03.Floor2", "04.Floor3", "05.Floor4", "06.Floor5_Boss" };

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        _settings = Resources.Load<SO_GameSettings>("GameSettings");
        gold = _settings != null ? _settings.startingGold : 200;
    }

    public void GoToNextFloor()
    {
        currentFloor++;
        Debug.Log($"[GameManager] {currentFloor}층으로 이동합니다.");

        if (!useSceneTransition)
        {
            // 같은 씬에서 맵 재생성
            DungeonGenerator generator = FindFirstObjectByType<DungeonGenerator>();
            if (generator != null)
                generator.RegenerateDungeon();
            else
                Debug.LogError("[GameManager] DungeonGenerator를 찾을 수 없습니다.");
        }
        else
        {
            // 층 번호에 맞는 씬으로 전환
            int idx = currentFloor - 1;
            if (idx >= 0 && idx < floorSceneNames.Length)
                SceneManager.LoadScene(floorSceneNames[idx]);
            else
                Debug.LogError($"[GameManager] {currentFloor}층에 해당하는 씬 이름이 없습니다. floorSceneNames 배열을 확인하세요.");
        }
    }
}
