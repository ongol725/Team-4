using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 캐릭터 선택창 전체를 제어한다.
/// 프리팹 구조:
///   CharacterSelectUI (이 스크립트)
///   └── Canvas
///       ├── Panel_Background
///       ├── Text_Title
///       ├── HorizontalGroup_Cards
///       │   ├── Card_전사  (CharacterCardUI)
///       │   ├── Card_마법사 (CharacterCardUI)
///       │   └── Card_도적  (CharacterCardUI)
///       └── Button_Confirm
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    [Header("캐릭터 데이터 (약한→강한 순으로 배치)")]
    public SO_CharacterData[] characters;   // 인스펙터에서 전사/마법사/도적 SO 할당

    [Header("UI 참조")]
    public CharacterCardUI[] cards;         // characters와 동일한 순서로 카드 배열 연결
    public Button            confirmButton;
    public Text              titleText;

    [Header("씬 이동")]
    [Tooltip("확인 버튼 클릭 후 로드할 씬 이름")]
    public string nextSceneName = "02.Ingame";

    private int _selectedIndex = 0;

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (titleText != null) titleText.text = "캐릭터 선택";

        for (int i = 0; i < cards.Length; i++)
        {
            if (i >= characters.Length || characters[i] == null) continue;
            int captured = i;
            cards[i].Setup(characters[i], () => OnCardClicked(captured));
        }

        confirmButton?.onClick.AddListener(OnConfirm);

        // 기본 선택: 이전에 고른 캐릭터 유지, 없으면 0번
        int defaultIdx = GetPreviousSelectionIndex();
        SelectCard(defaultIdx);
    }

    // ─────────────────────────────────────────────────────────────

    private void OnCardClicked(int index) => SelectCard(index);

    private void SelectCard(int index)
    {
        _selectedIndex = index;
        for (int i = 0; i < cards.Length; i++)
            cards[i].SetSelected(i == index);
    }

    private void OnConfirm()
    {
        if (_selectedIndex < characters.Length && characters[_selectedIndex] != null)
        {
            if (CharacterManager.Instance != null)
                CharacterManager.Instance.SelectCharacter(characters[_selectedIndex]);
            else
                Debug.LogWarning("[CharacterSelectUI] CharacterManager 인스턴스 없음 — GameManager 프리팹 옆에 CharacterManager 프리팹을 씬에 배치하세요.");
        }

        SceneManager.LoadScene(nextSceneName);
    }

    // 이전에 선택한 캐릭터가 있으면 해당 인덱스 반환
    private int GetPreviousSelectionIndex()
    {
        SO_CharacterData prev = CharacterManager.Instance?.SelectedCharacter;
        if (prev == null) return 0;
        for (int i = 0; i < characters.Length; i++)
            if (characters[i] == prev) return i;
        return 0;
    }
}
