// ============================================================
// HittableObject.cs
// 플레이어가 N번 때리면 파괴되는 오브젝트 (힐링 토템 / 파괴 가능한 돌 공용)
//  - Hit()을 N회 호출하면 파괴 → onDestroyed 콜백 후 풀 반환.
//  - 데미지 절대 수치와 무관하게 '타격 횟수'로 판정(기획서 기준).
//  - 실제 플레이어 공격 시스템이 생기면 그쪽에서 Hit()을 호출하면 됨.
//    (현재는 SandboxHitter 디버그로 타격)
// 프리팹 요구: Collider2D(OverlapPoint 감지용), PooledObject(권장)
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(Collider2D))]
    public class HittableObject : MonoBehaviour
    {
        [Tooltip("파괴까지 필요한 타격 횟수")]
        public int hitsToDestroy = 3;

        /// <summary>파괴 시 호출(인자: 자신). 풀 반환 직전 1회.</summary>
        public System.Action<HittableObject> onDestroyed;

        private int remaining;
        private PooledObject pooled;

        public bool IsAlive => remaining > 0;

        private void Awake()
        {
            pooled = GetComponent<PooledObject>();
        }

        private void OnEnable()
        {
            remaining = Mathf.Max(1, hitsToDestroy);
            onDestroyed = null; // 풀 재사용 시 이전 콜백 잔존 방지
        }

        /// <summary>타격 1회(또는 hits회). 누적이 한도에 도달하면 파괴.</summary>
        public void Hit(int hits = 1)
        {
            if (remaining <= 0) return;
            remaining -= Mathf.Max(1, hits);
            if (remaining <= 0) Destroyed();
        }

        private void Destroyed()
        {
            var cb = onDestroyed;
            onDestroyed = null;
            cb?.Invoke(this);
            ReturnToPool();
        }

        /// <summary>외부에서 강제 정리(패턴 종료 시 남은 오브젝트 회수). 콜백 미호출.</summary>
        public void ForceReturn()
        {
            onDestroyed = null;
            remaining = 0;
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (pooled != null) pooled.ReturnToPool();
            else gameObject.SetActive(false);
        }
    }
}
