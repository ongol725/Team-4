// ============================================================
// SandboxPlayerMove.cs
// 보스 샌드박스 검증 전용 - WASD/화살표 플레이어 이동
//  - 실제 플레이어 컨트롤러가 아니라, 패턴 회피 테스트용 임시 이동 스크립트.
//  - 프로젝트가 새 Input System 전용이므로 Keyboard.current로 입력을 읽는다.
//  - 보스 패턴(추적·텔레그래프·회피) 동작 확인 후 제거 가능.
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace BagSurvivor.Monster
{
    public class SandboxPlayerMove : MonoBehaviour
    {
        [Tooltip("이동 속도(m/s)")]
        public float moveSpeed = 7f;

        private Rigidbody2D rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                    - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);

            Vector2 v = new Vector2(x, y).normalized * moveSpeed;

            if (rb != null) rb.linearVelocity = v;
            else transform.position += (Vector3)(v * Time.deltaTime);
        }
    }
}
