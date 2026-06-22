// ============================================================
// ZombieEliteDeathGimmick.cs
// 좀비 엘리트 사망 시 폭발 장판(ZombieEliteExplosion)을 1개 스폰.
//  - MonsterController.OnDeath에 반응(컴포지션 패턴).
//  - 폭발 오브젝트는 GameObjectPool 우선, 없으면 Instantiate 폴백.
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class ZombieEliteDeathGimmick : MonoBehaviour
    {
        [Tooltip("사망 시 생성할 폭발 장판 프리팹 (ZombieEliteExplosion 포함)")]
        public GameObject explosionPrefab;

        private MonsterController controller;

        private void Awake() => controller = GetComponent<MonsterController>();
        private void OnEnable() => controller.OnDeath += HandleDeath;
        private void OnDisable() => controller.OnDeath -= HandleDeath;

        private void HandleDeath(MonsterController mc)
        {
            if (explosionPrefab == null) return;
            Vector3 pos = transform.position;
            if (GameObjectPool.Instance != null)
                GameObjectPool.Instance.Get(explosionPrefab, pos, Quaternion.identity);
            else
                Instantiate(explosionPrefab, pos, Quaternion.identity);
        }
    }
}
