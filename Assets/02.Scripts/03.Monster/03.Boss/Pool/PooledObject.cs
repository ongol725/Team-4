// ============================================================
// PooledObject.cs
// GameObjectPool에서 꺼낸 오브젝트의 자가 반환 헬퍼
//  - 출처 풀을 주입받아(SetPool), 수명 만료 시 또는 외부 호출 시 풀로 반환
//  - autoReturnAfter > 0 이면 활성화 시점부터 그 시간 뒤 자동 반환 (투사체/이펙트 수명)
//  - 풀이 없으면(샌드박스 단독 테스트 등) 그냥 비활성화로 폴백
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    public class PooledObject : MonoBehaviour
    {
        [Header("자동 반환")]
        [Tooltip("활성화 후 이 시간(초)이 지나면 자동으로 풀에 반환. 0이면 수동 반환만.")]
        public float autoReturnAfter = 0f;

        private GameObjectPool pool;
        private float timer;

        /// <summary>풀에서 꺼낼 때 출처 풀을 주입합니다(GameObjectPool.Get이 호출).</summary>
        public void SetPool(GameObjectPool p) => pool = p;

        private void OnEnable()
        {
            timer = 0f;
        }

        private void Update()
        {
            if (autoReturnAfter <= 0f) return;

            timer += Time.deltaTime;
            if (timer >= autoReturnAfter)
                ReturnToPool();
        }

        /// <summary>이 오브젝트를 풀로 반환합니다. 풀이 없으면 비활성화로 폴백.</summary>
        public void ReturnToPool()
        {
            if (pool != null)
                pool.Return(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
