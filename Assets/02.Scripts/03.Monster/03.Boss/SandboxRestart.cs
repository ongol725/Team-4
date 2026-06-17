// ============================================================
// SandboxRestart.cs
// 보스 샌드박스 검증 전용 - R 키로 현재 씬 재시작(부활)
//  - 죽을 때마다 수동으로 다시 시작하는 번거로움 제거용 테스트 헬퍼.
//  - 사망 시 Time.timeScale=0 / 결과창으로 멈춰도 입력은 실시간이라 R로 즉시 리로드.
//  - 새 Input System 기준(Keyboard.current).
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BagSurvivor.Monster
{
    public class SandboxRestart : MonoBehaviour
    {
        [Tooltip("재시작 키")]
        public Key restartKey = Key.R;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[restartKey].wasPressedThisFrame)
            {
                Time.timeScale = 1f; // 사망 시 0으로 멈춘 경우 복구
                // 현재 씬 리로드(빌드 세팅에 등록돼 있어야 함 — 샌드박스 빌드 도구가 자동 등록)
                SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
            }
        }
    }
}
