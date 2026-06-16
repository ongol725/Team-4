// ============================================================
// MonsterPool.cs
// 몬스터 오브젝트 풀 (프리팹별 재사용 풀)
//  - 대량 스폰/디스폰 시 Instantiate/Destroy 비용 제거 (최적화: 핵심 가치)
//  - Get: 비활성 인스턴스 재사용 또는 신규 생성 → 활성화(OnEnable에서 몬스터 초기화)
//  - Return: 비활성화(OnDisable에서 정리) 후 출처 프리팹별 스택에 보관
// ============================================================
using UnityEngine;
using System.Collections.Generic;

namespace BagSurvivor.Monster
{
    public class MonsterPool : MonoBehaviour
    {
        public static MonsterPool Instance { get; private set; }

        [Header("풀 설정")]
        [Tooltip("풀링된 몬스터를 담아둘 부모 (미지정 시 자동 생성)")]
        public Transform poolRoot;

        // 프리팹별 비활성 인스턴스 보관
        private readonly Dictionary<GameObject, Stack<MonsterController>> pools =
            new Dictionary<GameObject, Stack<MonsterController>>();

        // 인스턴스 → 출처 프리팹 역참조 (Return 시 어느 스택으로 돌릴지 판단)
        private readonly Dictionary<MonsterController, GameObject> sourceOf =
            new Dictionary<MonsterController, GameObject>();

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
                GameObject rootObj = new GameObject("MonsterPool_Root");
                rootObj.transform.SetParent(transform);
                poolRoot = rootObj.transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 풀에서 몬스터를 꺼내 지정 위치에 활성화합니다. (없으면 생성)
        /// 스탯 배율은 OnEnable 초기화 전에 적용되도록 활성화 직전에 주입합니다.
        /// </summary>
        public MonsterController Get(GameObject prefab, Vector3 position, float hpMultiplier = 1f, float attackMultiplier = 1f)
        {
            if (prefab == null) return null;

            if (!pools.TryGetValue(prefab, out Stack<MonsterController> stack))
            {
                stack = new Stack<MonsterController>();
                pools[prefab] = stack;
            }

            MonsterController mc = null;
            // 파괴된(null) 인스턴스는 건너뛰며 유효한 것을 꺼냄
            while (stack.Count > 0)
            {
                MonsterController candidate = stack.Pop();
                if (candidate != null) { mc = candidate; break; }
            }

            if (mc == null)
            {
                GameObject go = Instantiate(prefab, poolRoot);
                mc = go.GetComponent<MonsterController>();
                if (mc == null)
                {
                    Debug.LogError($"[MonsterPool] 프리팹 '{prefab.name}'에 MonsterController가 없습니다.");
                    Destroy(go);
                    return null;
                }
                // 신규 인스턴스는 비활성화하여, 배율 주입 후 활성화 시점에 초기화되도록 함
                go.SetActive(false);
            }

            sourceOf[mc] = prefab;
            mc.transform.position = position;
            mc.SetStatMultiplier(hpMultiplier, attackMultiplier); // 활성화 전 주입
            mc.gameObject.SetActive(true); // OnEnable에서 스탯/HP바 초기화
            return mc;
        }

        /// <summary>몬스터를 비활성화하여 풀로 반환합니다.</summary>
        public void Return(MonsterController mc)
        {
            if (mc == null) return;

            mc.gameObject.SetActive(false); // OnDisable에서 코루틴 정지·상태 정리
            if (poolRoot != null) mc.transform.SetParent(poolRoot);

            if (!sourceOf.TryGetValue(mc, out GameObject prefab))
                return; // 출처를 모르면 보관만 (이론상 발생 안 함)

            if (!pools.TryGetValue(prefab, out Stack<MonsterController> stack))
            {
                stack = new Stack<MonsterController>();
                pools[prefab] = stack;
            }
            stack.Push(mc);
        }

        /// <summary>지정 프리팹을 미리 생성해 풀을 예열합니다. (선택: 로딩 중 호출 권장)</summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                MonsterController mc = Get(prefab, poolRoot != null ? poolRoot.position : Vector3.zero);
                if (mc != null) Return(mc);
            }
        }
    }
}
