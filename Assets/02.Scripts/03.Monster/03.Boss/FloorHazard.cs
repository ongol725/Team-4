// ============================================================
// FloorHazard.cs
// 바닥 장판(지속 피해 지대). 2페이즈 진입 시 생성되는 영구 장판 등에 사용.
//  - 플레이어가 장판 위에 있는 동안 tickInterval마다 피해.
//  - Trigger Collider2D 필요. 플레이어(태그 Player)에만 반응.
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(Collider2D))]
    public class FloorHazard : MonoBehaviour
    {
        [Header("피해 설정")]
        [Tooltip("틱당 피해량")]
        public int damagePerTick = 5;

        [Tooltip("피해 간격(초)")]
        public float tickInterval = 0.5f;

        private Coroutine damageCo;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (damageCo == null)
                damageCo = StartCoroutine(DamageWhileInside(other));
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (damageCo != null) { StopCoroutine(damageCo); damageCo = null; }
        }

        private IEnumerator DamageWhileInside(Collider2D playerCol)
        {
            PlayerHealth ph = playerCol != null ? playerCol.GetComponentInParent<PlayerHealth>() : null;
            while (ph != null && !ph.IsDead)
            {
                ph.TakeDamage(damagePerTick);
                yield return new WaitForSeconds(tickInterval);
            }
            damageCo = null;
        }
    }
}
