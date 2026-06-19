// ============================================================
// GoldDropManager.cs
// 골드 픽업 드롭 + 오브젝트 풀 (최적화)
//  - 몬스터 사망 시 Drop(position, dropItemID, count) 호출
//    → 돈 ID(액면가)에 맞는 동전을 count개 떨어뜨림 (CSV: Drop_ItemID / Drop_Item_Value)
//  - 픽업 획득 시 Return으로 풀에 반환
// ============================================================
using UnityEngine;
using System.Collections.Generic;

namespace BagSurvivor.Items
{
    public class GoldDropManager : MonoBehaviour
    {
        public static GoldDropManager Instance { get; private set; }

        /// <summary>돈 ID → 액면가(골드값) + 스프라이트 매핑. CSV의 Drop_ItemID에 대응.</summary>
        [System.Serializable]
        public class Denomination
        {
            public string id;          // 예: Item_001
            public int goldValue;      // 액면가(획득 시 더해지는 골드)
            public Sprite sprite;      // 해당 동전 스프라이트(Gold_1~4)
        }

        [Header("골드 픽업 프리팹")]
        public GameObject goldPrefab;

        [Header("돈 액면가 테이블 (Drop_ItemID → 골드값/스프라이트)")]
        public Denomination[] denominations = new Denomination[]
        {
            new Denomination { id = "Item_001", goldValue = 1 },
            new Denomination { id = "Item_002", goldValue = 10 },
            new Denomination { id = "Item_003", goldValue = 100 },
            new Denomination { id = "Item_004", goldValue = 500 },
        };

        [Header("드롭 분산 반경 (동전 여러 개 흩뿌리기, m)")]
        public float scatterRadius = 0.5f;

        [Header("풀 부모 (미지정 시 자동 생성)")]
        public Transform poolRoot;

        [Header("풀 예열")]
        [Tooltip("시작 시 미리 만들어 둘 골드 픽업 수 (0이면 끄기)")]
        public int prewarmCount = 12;

        private readonly Stack<GoldPickup> pool = new Stack<GoldPickup>();
        private readonly Dictionary<string, Denomination> denomLookup = new Dictionary<string, Denomination>();

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

            // 기존 씬 인스턴스에서 새 필드가 비어 있을 수 있으니, 비면 기본 액면가로 채움(스프라이트는 인스펙터 지정)
            if (denominations == null || denominations.Length == 0)
            {
                denominations = new Denomination[]
                {
                    new Denomination { id = "Item_001", goldValue = 1 },
                    new Denomination { id = "Item_002", goldValue = 10 },
                    new Denomination { id = "Item_003", goldValue = 100 },
                    new Denomination { id = "Item_004", goldValue = 500 },
                };
            }

            denomLookup.Clear();
            foreach (var d in denominations)
                if (d != null && !string.IsNullOrEmpty(d.id)) denomLookup[d.id] = d;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // 첫 드롭 끊김 방지를 위해 미리 생성해 풀에 적재
            if (goldPrefab == null || prewarmCount <= 0) return;
            for (int i = 0; i < prewarmCount; i++)
            {
                GameObject inst = Instantiate(goldPrefab, poolRoot);
                inst.SetActive(false);
                GoldPickup g = inst.GetComponent<GoldPickup>();
                if (g != null) pool.Push(g);
                else Destroy(inst);
            }
        }

        /// <summary>
        /// 돈 ID(액면가)에 맞는 동전을 count개 드롭합니다. (CSV: Drop_ItemID / Drop_Item_Value)
        /// 예: itemID=Item_002(10골드), count=3 → 10골드 동전 3개(=30골드) 흩뿌림.
        /// </summary>
        public void Drop(Vector3 position, string itemID, int count)
        {
            if (goldPrefab == null || count <= 0) return;

            if (!denomLookup.TryGetValue(itemID, out Denomination d) || d == null)
            {
                Debug.LogWarning($"[GoldDrop] 알 수 없는 돈 ID '{itemID}' — 액면가 테이블에 없음. 드롭 생략.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = position;
                if (scatterRadius > 0f) pos += (Vector3)(Random.insideUnitCircle * scatterRadius);
                SpawnPickup(pos, d.goldValue, d.sprite);
            }
        }

        /// <summary>지정 위치에 골드 픽업 1개를 드롭합니다(액면가/스프라이트 지정).</summary>
        public void Drop(Vector3 position, int amount) => SpawnPickup(position, amount, null);

        private void SpawnPickup(Vector3 position, int goldValue, Sprite sprite)
        {
            if (goldPrefab == null || goldValue <= 0) return;

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
            g.Init(goldValue, this, sprite);
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
