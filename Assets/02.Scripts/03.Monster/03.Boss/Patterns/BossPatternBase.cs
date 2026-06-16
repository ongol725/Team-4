// ============================================================
// BossPatternBase.cs
// 보스/중간보스 공격 패턴의 공통 베이스 (컴포지션 패턴)
//  - MonsterController에 컴포넌트로 부착하여 사용
//  - 패턴 하나 = 컴포넌트 하나. 원하는 패턴만 골라 붙이면 중간 보스가 나눠 가질 수 있다.
//  - BossPatternDriver가 부착된 패턴들을 자동 수집해 쿨다운/사거리/확률로 선택·실행한다.
//
// 파생 클래스는 ExecuteRoutine()만 구현하면 된다.
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public abstract class BossPatternBase : MonoBehaviour
    {
        [Header("패턴 공통 설정")]
        [Tooltip("패턴 표시 이름 (디버그용)")]
        public string patternName = "Pattern";

        [Tooltip("특수 패턴 여부 (false=기본 패턴, true=특수 패턴). 드라이버의 기본/특수 분기에 사용")]
        public bool isSpecial = false;

        [Tooltip("이 패턴을 사용할 수 있는 최대 거리(m). 플레이어가 이 안에 있어야 발동 후보가 된다")]
        public float useRange = 10f;

        [Tooltip("선행 동작(텔레그래프) 시간(초)")]
        public float telegraphTime = 1f;

        [Tooltip("패턴 사용 후 재사용까지 쿨다운(초). 패턴 시작 시점부터 카운트")]
        public float cooldown = 10f;

        [Tooltip("후보 중 선택될 상대 가중치(확률). 클수록 자주 뽑힘")]
        [Range(0f, 100f)]
        public float chance = 25f;

        protected MonsterController controller;
        private float cooldownTimer;

        /// <summary>쿨다운이 끝나 사용 가능한 상태인지</summary>
        public bool IsReady => cooldownTimer <= 0f;

        /// <summary>선택 가중치(확률)</summary>
        public float Chance => chance;

        /// <summary>특수 패턴 여부</summary>
        public bool IsSpecial => isSpecial;

        protected virtual void Awake()
        {
            controller = GetComponent<MonsterController>();
        }

        protected virtual void OnEnable()
        {
            // 풀 재사용/재활성화 시 즉시 사용 가능 상태로 초기화
            cooldownTimer = 0f;
        }

        protected virtual void Update()
        {
            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;
        }

        /// <summary>플레이어가 사용 사거리 안에 있는지</summary>
        public bool InRange()
        {
            if (controller == null) return false;
            return controller.GetDistanceToPlayer() <= useRange;
        }

        /// <summary>지금 이 패턴을 실행할 수 있는지(쿨다운 + 사거리 + 생존)</summary>
        public virtual bool CanExecute()
        {
            if (controller == null || controller.IsDead) return false;
            return IsReady && InRange();
        }

        /// <summary>
        /// 패턴을 실행합니다(드라이버가 호출). 시작 시점에 쿨다운을 건다.
        /// 이동을 직접 제어하는 패턴은 ExecuteRoutine 안에서 Begin/EndExternalMovement를 사용한다.
        /// </summary>
        public IEnumerator Execute()
        {
            cooldownTimer = cooldown;
            yield return ExecuteRoutine();
        }

        /// <summary>실제 패턴 동작. 파생 클래스에서 구현.</summary>
        protected abstract IEnumerator ExecuteRoutine();

        // ==========================================
        // 파생 클래스 공용 헬퍼
        // ==========================================

        protected GameObjectPool Pool => GameObjectPool.Instance;

        /// <summary>풀에서 이펙트/투사체를 꺼냅니다. 풀이 없으면 null.</summary>
        protected GameObject SpawnFromPool(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return null;
            if (Pool != null) return Pool.Get(prefab, pos, rot);
            return Instantiate(prefab, pos, rot); // 폴백(샌드박스 단독 테스트)
        }

        /// <summary>텔레그래프(예고) 표식을 dir 방향으로 띄웁니다. 반환값은 ReturnPooled로 정리.</summary>
        protected GameObject ShowTelegraph(GameObject prefab, Vector3 pos, Vector2 dir)
        {
            if (prefab == null) return null;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            return SpawnFromPool(prefab, pos, Quaternion.Euler(0f, 0f, ang));
        }

        /// <summary>풀에서 꺼낸 오브젝트를 반환(또는 비활성화)합니다.</summary>
        protected void ReturnPooled(GameObject go)
        {
            if (go == null) return;
            PooledObject po = go.GetComponent<PooledObject>();
            if (po != null) po.ReturnToPool();
            else go.SetActive(false);
        }

        /// <summary>플레이어를 향하는 정규화 방향(없으면 오른쪽).</summary>
        protected Vector2 DirToPlayer()
        {
            Vector2 d = controller != null ? controller.GetDirectionToPlayer() : Vector2.zero;
            return d.sqrMagnitude < 0.0001f ? Vector2.right : d;
        }

        /// <summary>
        /// 지정 원(부채꼴) 범위 안에 플레이어가 있으면 보스 공격력으로 피해를 줍니다.
        /// coneAngleDeg가 0보다 크면 dir 기준 부채꼴(전체 각도) 판정, 아니면 360° 원형.
        /// </summary>
        protected bool TryHitPlayer(Vector2 center, float radius, float coneAngleDeg = 0f, Vector2 dir = default)
        {
            if (controller == null || controller.PlayerTransform == null) return false;

            Vector2 toPlayer = (Vector2)controller.PlayerTransform.position - center;
            if (toPlayer.magnitude > radius) return false;

            if (coneAngleDeg > 0f && toPlayer.sqrMagnitude > 0.0001f)
            {
                Vector2 facing = dir.sqrMagnitude < 0.0001f ? DirToPlayer() : dir.normalized;
                float angle = Vector2.Angle(facing, toPlayer.normalized);
                if (angle > coneAngleDeg * 0.5f) return false;
            }

            PlayerHealth ph = controller.PlayerTransform.GetComponentInParent<PlayerHealth>();
            if (ph != null && !ph.IsDead)
            {
                ph.TakeDamage(controller.Attack);
                return true;
            }
            return false;
        }
    }
}
