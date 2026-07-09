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
        [Tooltip("0보다 크면 '플레이어 최대체력 비율'로 피해(예: 0.025 = 최대체력의 2.5%). 이 값이 우선 적용됨")]
        [Range(0f, 1f)] public float maxHpPercentPerTick = 0.025f;

        [Tooltip("최대체력 비율(maxHpPercentPerTick)이 0일 때 사용하는 고정 피해량")]
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
                int dmg = maxHpPercentPerTick > 0f
                    ? Mathf.Max(1, Mathf.CeilToInt(ph.MaxHP * maxHpPercentPerTick)) // 최대체력 비율 피해
                    : damagePerTick;                                               // 고정 피해
                ph.TakeDamage(dmg);
                yield return new WaitForSeconds(tickInterval);
            }
            damageCo = null;
        }
    }
}
