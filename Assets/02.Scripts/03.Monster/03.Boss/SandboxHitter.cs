// ============================================================
// SandboxHitter.cs
// 보스 샌드박스 검증 전용 - 마우스 좌클릭으로 HittableObject(토템/돌) 타격
//  - 실제 플레이어 공격 시스템이 없으므로, 커서 아래 오브젝트를 클릭해 Hit().
//  - 새 Input System 기준(Mouse.current).
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace BagSurvivor.Monster
{
    public class SandboxHitter : MonoBehaviour
    {
        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 sp = mouse.position.ReadValue();
            Vector2 wp = cam.ScreenToWorldPoint(sp);

            Collider2D col = Physics2D.OverlapPoint(wp);
            if (col == null) return;

            var hittable = col.GetComponentInParent<HittableObject>();
            if (hittable != null) hittable.Hit();
        }
    }
}
