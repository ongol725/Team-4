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
    }

    public void SelectCharacter(SO_CharacterData data)
    {
        SelectedCharacter = data;
    }
}
