// ============================================================
// GimmickPickup.cs
// 기믹 아이템 픽업 (돌 파괴 기믹의 '무력화 아이템' 등)
//  - 플레이어가 닿으면(트리거) onCollected 콜백 후 풀 반환.
//  - 돌을 부쉈을 때 드롭되며, 플레이어가 주워야 효과 발동(보스 그로기).
// 프리팹 요구: Collider2D(IsTrigger), PooledObject(권장)
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(Collider2D))]
    public class GimmickPickup : MonoBehaviour
    {
        /// <summary>플레이어가 획득 시 호출(1회).</summary>
        public System.Action onCollected;

        private PooledObject pooled;

        private void Awake()
        {
            pooled = GetComponent<PooledObject>();
        }

        private void OnEnable()
        {
            onCollected = null; // 풀 재사용 시 이전 콜백 잔존 방지(스폰 직후 패턴이 다시 설정)
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var cb = onCollected;
            onCollected = null;
            cb?.Invoke();
            Return();
        }

        private void Return()
        {
            if (pooled != null) pooled.ReturnToPool();
            else gameObject.SetActive(false);
        }
    }
}
