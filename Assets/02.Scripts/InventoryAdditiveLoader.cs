using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 전투 씬 시작 시 99.InventoryStore 씬을 Additive로 로드한다.
/// 로드 완료 후 InventoryPopupToggle이 InventoryStoreRoot를 찾아 I 키가 동작한다.
///
/// 에디터 플레이 모드: 에셋 경로 직접 사용 (Build Settings 불필요)
/// 실제 빌드:         Build Settings에 99.InventoryStore 추가 필요
/// </summary>
public class InventoryAdditiveLoader : MonoBehaviour
{
    [SerializeField] private string _scenePath = "Assets/01.Scenes/99.InventoryStore.unity";
    [SerializeField] private string _sceneName = "99.InventoryStore";

    private IEnumerator Start()
    {
        if (SceneManager.GetSceneByName(_sceneName).isLoaded)
        {
            Debug.Log("[InventoryLoader] 이미 로드됨");
            yield break;
        }

#if UNITY_EDITOR
        var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            _scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
        while (op != null && !op.isDone) yield return null;
#else
        yield return SceneManager.LoadSceneAsync(_sceneName, LoadSceneMode.Additive);
#endif
        Debug.Log("[InventoryLoader] 인벤토리 씬 로드 완료 — I 키로 인벤토리 열기");
    }
}
