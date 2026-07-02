using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 씬(10.Title)의 시작 버튼에 부착. 클릭 시 로비 씬으로 전환한다.
/// 같은 GameObject의 Button OnClick을 코드로 자동 바인딩하므로 인스펙터 연결이 필요 없다.
/// </summary>
[RequireComponent(typeof(Button))]
public class TitleController : MonoBehaviour
{
    [Tooltip("이동할 로비 씬 이름 (Build Settings에 등록되어 있어야 함)")]
    [SerializeField] private string lobbySceneName = "01.Lobby";

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(GoToLobby);
    }

    /// <summary>시작 버튼 클릭 시 호출. 로비 씬으로 이동한다.</summary>
    public void GoToLobby()
    {
        SceneManager.LoadScene(lobbySceneName);
    }
}
