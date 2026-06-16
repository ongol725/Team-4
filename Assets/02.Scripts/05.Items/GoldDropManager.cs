// ============================================================
// GoldDropManager.cs
// 골드 픽업 드롭 + 오브젝트 풀 (최적화)
//  - 몬스터 사망 시 Drop(position, amount) 호출
//  - 픽업 획득 시 Return으로 풀에 반환
// ============================================================
using UnityEngine;
using System.Collections.Generic;

namespace BagSurvivor.Items
{
    public class GoldDropManager : MonoBehaviour
    {
        public static GoldDropManager Instance { get; private set; }

        [Header("골드 픽업 프리팹")]
        public GameObject goldPrefab;

        [Header("풀 부모 (미지정 시 자동 생성)")]
        public Transform poolRoot;

        private readonly Stack<GoldPickup> pool = new Stack<GoldPickup>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (poolRoot == null)
            {
                GameObject go = new GameObject("GoldPool_Root");
                go.transform.SetParent(transform);
                poolRoot = go.transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>지정 위치에 골드 픽업을 드롭합니다.</summary>
        public void Drop(Vector3 position, int amount)
        {
            if (goldPrefab == null || amount <= 0) return;

            GoldPickup g = null;
            while (pool.Count > 0)
            {
                GoldPickup c = pool.Pop();
                if (c != null) { g = c; break; }
            }

            if (g == null)
            {
                GameObject inst = Instantiate(goldPrefab, poolRoot);
                inst.SetActive(false); // Init 후 활성화
                g = inst.GetComponent<GoldPickup>();
                if (g == null) { Destroy(inst); return; }
            }

            g.transform.SetParent(poolRoot);
            g.transform.position = position;
            g.Init(amount, this);
            g.gameObject.SetActive(true);
        }

        /// <summary>획득된 픽업을 풀로 반환합니다.</summary>
        public void Return(GoldPickup g)
        {
            if (g == null) return;
            g.gameObject.SetActive(false);
            g.transform.SetParent(poolRoot);
            pool.Push(g);
        }
    }
}
