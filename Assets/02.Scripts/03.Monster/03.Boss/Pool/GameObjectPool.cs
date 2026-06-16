// ============================================================
// GameObjectPool.cs
// 범용 오브젝트 풀 (프리팹별 재사용 풀)
//  - 보스 투사체/텔레그래프/장판/토템/이펙트 등 비-MonsterController 오브젝트 전용
//    (소환 몬스터: 환영 늑대·분신 등은 기존 MonsterPool 사용)
//  - MonsterPool/GoldDropManager와 동일한 패턴(프리팹 키 → Stack, SetActive 토글)
//  - Get: 비활성 인스턴스 재사용 또는 신규 생성 → 위치/회전 세팅 후 활성화
//  - Return: 비활성화 후 출처 프리팹별 스택에 보관
// ============================================================
using UnityEngine;
using System.Collections.Generic;

namespace BagSurvivor.Monster
{
    public class GameObjectPool : MonoBehaviour
    {
        public static GameObjectPool Instance { get; private set; }

        [Header("풀 설정")]
        [Tooltip("풀링된 오브젝트를 담아둘 부모 (미지정 시 자동 생성)")]
        public Transform poolRoot;

        // 프리팹별 비활성 인스턴스 보관
        private readonly Dictionary<GameObject, Stack<GameObject>> pools =
            new Dictionary<GameObject, Stack<GameObject>>();

        // 인스턴스 → 출처 프리팹 역참조 (Return 시 어느 스택으로 돌릴지 판단)
        private readonly Dictionary<GameObject, GameObject> sourceOf =
            new Dictionary<GameObject, GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (poolRoot == null)
            {
                GameObject rootObj = new GameObject("GameObjectPool_Root");
                rootObj.transform.SetParent(transform);
                poolRoot = rootObj.transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 풀에서 오브젝트를 꺼내 지정 위치/회전으로 활성화합니다. (없으면 생성)
        /// PooledObject가 붙어 있으면 출처 풀을 주입해 자가 반환을 가능하게 합니다.
        /// </summary>
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!pools.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                pools[prefab] = stack;
            }

            GameObject go = null;
            // 파괴된(null) 인스턴스는 건너뛰며 유효한 것을 꺼냄
            while (stack.Count > 0)
            {
                GameObject candidate = stack.Pop();
                if (candidate != null) { go = candidate; break; }
            }

            if (go == null)
            {
                go = Instantiate(prefab, poolRoot);
                go.SetActive(false);
            }

            sourceOf[go] = prefab;
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = prefab.transform.localScale; // 재사용 시 스케일 초기화(LeapBlast 등에서 키운 값 잔존 방지)

            // 자가 반환용 출처 풀 주입 (있을 때만)
            PooledObject po = go.GetComponent<PooledObject>();
            if (po != null) po.SetPool(this);

            go.SetActive(true); // OnEnable에서 초기화하는 컴포넌트가 있다면 여기서 실행됨
            return go;
        }

        /// <summary>위치만 지정해 활성화하는 단축 오버로드 (회전 기본값).</summary>
        public GameObject Get(GameObject prefab, Vector3 position)
            => Get(prefab, position, Quaternion.identity);

        /// <summary>오브젝트를 비활성화하여 풀로 반환합니다.</summary>
        public void Return(GameObject go)
        {
            if (go == null) return;

            go.SetActive(false);
            if (poolRoot != null) go.transform.SetParent(poolRoot);

            if (!sourceOf.TryGetValue(go, out GameObject prefab))
                return; // 출처를 모르면 보관만 (풀에서 나온 게 아닐 수 있음)

            if (!pools.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                pools[prefab] = stack;
            }
            stack.Push(go);
        }

        /// <summary>지정 프리팹을 미리 생성해 풀을 예열합니다(보유량을 count까지 채움). 로딩 중 호출 권장.</summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            if (!pools.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                pools[prefab] = stack;
            }

            int need = count - stack.Count;
            for (int i = 0; i < need; i++)
            {
                GameObject go = Instantiate(prefab, poolRoot);
                go.SetActive(false);
                sourceOf[go] = prefab;
                stack.Push(go);
            }
        }
    }
}
