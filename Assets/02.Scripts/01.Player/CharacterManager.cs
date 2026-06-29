using UnityEngine;

/// <summary>
/// 캐릭터 선택창에서 고른 캐릭터를 씬 전환 후에도 유지한다.
/// GameManager 프리팹처럼 씬 시작 전에 존재해야 한다.
/// 프리팹 위치: Assets/03.Prefabs/06.Gimmicks/CharacterManager.prefab
/// </summary>
public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    /// <summary>현재 선택된 캐릭터 데이터. CharacterSelectUI에서 설정.</summary>
    public SO_CharacterData SelectedCharacter { get; private set; }

    [Tooltip("캐릭터를 선택하지 않고 인게임으로 진입할 때 기본으로 사용할 캐릭터")]
    public SO_CharacterData defaultCharacter;

    /// <summary>씬에 CharacterManager가 없거나 미선택일 때 로드할 기본 캐릭터(전사) Resources 경로.</summary>
    private const string DefaultResourcePath = "ScriptableObjects/Characters/SO_Warrior";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (SelectedCharacter == null)
            SelectedCharacter = defaultCharacter;
        if (SelectedCharacter == null)
            SelectedCharacter = Resources.Load<SO_CharacterData>(DefaultResourcePath);
    }

    public void SelectCharacter(SO_CharacterData data)
    {
        SelectedCharacter = data;
    }

    /// <summary>
    /// 선택된 캐릭터를 반환한다. CharacterManager 인스턴스가 씬에 없거나(캐릭터 선택을 거치지 않고
    /// 인게임/인벤토리 씬에 바로 진입) 미선택이면 기본 캐릭터(전사)를 Resources에서 로드해 폴백한다.
    /// 어떤 씬에서든 항상 유효한 캐릭터 데이터를 보장한다.
    /// </summary>
    public static SO_CharacterData GetSelectedOrDefault()
    {
        var c = Instance != null ? Instance.SelectedCharacter : null;
        if (c == null) c = Resources.Load<SO_CharacterData>(DefaultResourcePath);
        return c;
    }
}
