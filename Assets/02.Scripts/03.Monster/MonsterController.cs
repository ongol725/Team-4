// ============================================================
// MonsterController.cs
// FSM 기반 몬스터 메인 컨트롤러
// MonsterData(ScriptableObject)를 참조하여 런타임 스탯 관리
// 오브젝트 풀링 대응 (OnDisable에서 초기화)
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class MonsterController : MonoBehaviour
    {
        // ==========================================
        // 인스펙터 설정
        // ==========================================
        [Header("몬스터 데이터")]
        [Tooltip("이 몬스터의 ScriptableObject 데이터")]
        public MonsterData monsterData;

        [Header("충돌 데미지 설정")]
        [Tooltip("접촉 데미지 판정 간격 (초)")]
        private const float CONTACT_DAMAGE_INTERVAL = 0.5f;

        [Header("넉백 설정")]
        [Tooltip("넉백 모션 지속 시간 (초)")]
        private const float KNOCKBACK_DURATION = 0.3f;

        [Header("사망 설정")]
        [Tooltip("사망 이펙트 후 풀 반환까지 대기 시간 (초)")]
        private const float DEATH_DELAY = 0.1f;

        // ==========================================
        // 런타임 변수
        // ==========================================
        private int currentHP;
        private MonsterState currentState = MonsterState.Tracking;
        private Transform playerTransform;
        private Rigidbody2D rb;
        private Collider2D col;
        private SpriteRenderer spriteRenderer;

        // 넉백 관련
        private float kbCooldownTimer = 0f;
        private Coroutine knockbackCoroutine;

        // 접촉 데미지 관련
        private Coroutine contactDamageCoroutine;
        private bool isPlayerInContact = false;

        // 사망 처리 중 플래그
        private bool isDying = false;

        // 특수 기믹에서 이동을 제어하기 위한 플래그
        private bool isMovementPaused = false;

        // ==========================================
        // 프로퍼티 (외부 접근용)
        // ==========================================

        /// <summary>
        /// 현재 HP (읽기 전용)
        /// </summary>
        public int CurrentHP => currentHP;

        /// <summary>
        /// 현재 FSM 상태 (읽기 전용)
        /// </summary>
        public MonsterState CurrentState => currentState;

        /// <summary>
        /// 사망 여부 (읽기 전용)
        /// </summary>
        public bool IsDead => isDying;

        /// <summary>
        /// 플레이어 Transform 참조 (기믹 스크립트에서 사용)
        /// </summary>
        public Transform PlayerTransform => playerTransform;

        // ==========================================
        // 초기화
        // ==========================================
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            // 오브젝트 풀에서 재활성화될 때마다 초기화
            InitializeMonster();
        }

        private void OnDisable()
        {
            // 오브젝트 풀 반환 시 모든 코루틴 정지 및 상태 초기화
            StopAllCoroutines();
            knockbackCoroutine = null;
            contactDamageCoroutine = null;
            isPlayerInContact = false;
            isDying = false;
            isMovementPaused = false;
        }

        /// <summary>
        /// 몬스터 초기 상태로 리셋합니다.
        /// 오브젝트 풀에서 꺼낼 때 자동 호출됩니다.
        /// </summary>
        private void InitializeMonster()
        {
            if (monsterData == null) return;

            currentHP = monsterData.maxHP;
            currentState = MonsterState.Tracking;
            kbCooldownTimer = 0f;
            isDying = false;
            isMovementPaused = false;

            // 콜라이더 활성화
            if (col != null) col.enabled = true;

            // 플레이어 찾기 (태그 기반)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        // ==========================================
        // FSM 업데이트
        // ==========================================
        private void FixedUpdate()
        {
            if (monsterData == null || isDying) return;

            // 넉백 쿨다운 타이머 감소
            if (kbCooldownTimer > 0f)
            {
                kbCooldownTimer -= Time.fixedDeltaTime;
            }

            // Tracking 상태에서만 이동 처리
            if (currentState == MonsterState.Tracking)
            {
                HandleMovement();
            }
        }

        // ==========================================
        // 이동 처리
        // ==========================================
        private void HandleMovement()
        {
            // 기믹에서 이동을 일시정지한 경우
            if (isMovementPaused)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            if (playerTransform == null) return;

            switch (monsterData.movePattern)
            {
                case MovePattern.StraightChase:
                    ChasePlayer();
                    break;

                case MovePattern.StopOnCondition:
                    // 기본 추적 (특수 조건은 기믹 스크립트에서 PauseMovement로 제어)
                    ChasePlayer();
                    break;

                case MovePattern.Stationary:
                    // 제자리 고정
                    rb.linearVelocity = Vector2.zero;
                    break;
            }
        }

        /// <summary>
        /// 플레이어를 향해 최단 거리로 추적합니다.
        /// </summary>
        private void ChasePlayer()
        {
            if (playerTransform == null) return;

            Vector2 direction = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = direction * monsterData.moveSpeed;

            // 이동 방향에 따른 스프라이트 좌우 반전
            if (spriteRenderer != null && direction.x != 0)
            {
                spriteRenderer.flipX = direction.x < 0;
            }
        }

        // ==========================================
        // 피격 처리
        // ==========================================

        /// <summary>
        /// 외부에서 몬스터에게 데미지를 줄 때 호출합니다.
        /// 방어력 공식: MAX(1, 무기데미지 - Defense)
        /// </summary>
        /// <param name="rawDamage">무기의 기본 데미지</param>
        /// <param name="knockbackForce">넉백 거리 (0이면 넉백 없음)</param>
        /// <param name="knockbackDirection">넉백 방향 (정규화된 벡터)</param>
        public void TakeDamage(int rawDamage, float knockbackForce = 0f, Vector2 knockbackDirection = default)
        {
            if (isDying) return;

            // 방어력 적용
            int finalDamage = monsterData.CalculateDamageTaken(rawDamage);
            currentHP -= finalDamage;

            // 피격 이펙트 (Hit 상태 - 이동을 방해하지 않음)
            StartCoroutine(HitEffectCoroutine());

            // HP 확인
            if (currentHP <= 0)
            {
                StartCoroutine(DieCoroutine());
                return;
            }

            // 넉백 처리
            if (knockbackForce > 0f && kbCooldownTimer <= 0f && monsterData.kbResist < 1f)
            {
                ApplyKnockback(knockbackForce, knockbackDirection);
            }
        }

        /// <summary>
        /// 피격 이펙트를 출력합니다.
        /// 이동을 방해하지 않고 시각적 피드백만 제공합니다.
        /// </summary>
        private IEnumerator HitEffectCoroutine()
        {
            // 피격 시 깜빡임 효과
            if (spriteRenderer != null)
            {
                Color originalColor = spriteRenderer.color;
                spriteRenderer.color = Color.red;
                yield return new WaitForSeconds(0.1f);

                // 사망하지 않았으면 색상 복구
                if (!isDying && spriteRenderer != null)
                {
                    spriteRenderer.color = originalColor;
                }
            }
        }

        // ==========================================
        // 넉백 처리
        // ==========================================

        /// <summary>
        /// 넉백을 적용합니다.
        /// 넉백 모션(0.3초) 도중 추가 넉백 발생 시 모션을 초기화하고 다시 실행합니다.
        /// </summary>
        /// <param name="baseKnockbackDistance">무기 기본 넉백 거리</param>
        /// <param name="direction">넉백 방향</param>
        private void ApplyKnockback(float baseKnockbackDistance, Vector2 direction)
        {
            // 넉백 저항력 적용: 최종 거리 = 기본 거리 * (1.0 - KB_Resist)
            float finalDistance = baseKnockbackDistance * (1f - monsterData.kbResist);

            if (finalDistance <= 0f) return;

            // 기존 넉백 코루틴이 있으면 즉시 중단하고 새로 시작 (기획서: 추가 넉백 시 초기화)
            if (knockbackCoroutine != null)
            {
                StopCoroutine(knockbackCoroutine);
            }

            knockbackCoroutine = StartCoroutine(KnockbackCoroutine(finalDistance, direction));
        }

        private IEnumerator KnockbackCoroutine(float distance, Vector2 direction)
        {
            currentState = MonsterState.Knockback;

            // 넉백 방향으로 밀어내기
            if (direction == Vector2.zero && playerTransform != null)
            {
                direction = ((Vector2)transform.position - (Vector2)playerTransform.position).normalized;
            }

            // 0.3초 동안 거리만큼 이동
            float speed = distance / KNOCKBACK_DURATION;
            float timer = 0f;

            while (timer < KNOCKBACK_DURATION)
            {
                rb.linearVelocity = direction * speed;
                timer += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            rb.linearVelocity = Vector2.zero;

            // 넉백 쿨다운 시작
            kbCooldownTimer = monsterData.kbCooldown;

            // Tracking 상태로 복귀
            currentState = MonsterState.Tracking;
            knockbackCoroutine = null;
        }

        // ==========================================
        // 사망 처리
        // ==========================================
        private IEnumerator DieCoroutine()
        {
            isDying = true;
            currentState = MonsterState.Die;

            // 1. 즉시 충돌체 비활성화
            if (col != null) col.enabled = false;
            rb.linearVelocity = Vector2.zero;

            // 2. 사망 이펙트 (0.1초)
            // TODO: 폭발 파티클 등 사망 이펙트 추가
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, 0.5f);
            }

            yield return new WaitForSeconds(DEATH_DELAY);

            // 3. 드롭 아이템 스폰
            SpawnDropItem();

            // 4. 오브젝트 풀 반환 (현재는 비활성화로 대체)
            // TODO: ObjectPool.Return(gameObject) 로 교체
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 사망 시 드롭 아이템을 스폰합니다.
        /// </summary>
        private void SpawnDropItem()
        {
            if (monsterData == null) return;
            if (string.IsNullOrEmpty(monsterData.dropItemID) || monsterData.dropItemValue <= 0) return;

            // TODO: 드롭 아이템 시스템과 연동
            // 예시: DropManager.Instance.SpawnDrop(monsterData.dropItemID, monsterData.dropItemValue, transform.position);
            Debug.Log($"[Monster] {monsterData.monsterName} 사망 - 드롭: {monsterData.dropItemID} x{monsterData.dropItemValue}");
        }

        // ==========================================
        // 접촉 데미지 처리
        // ==========================================
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDying) return;

            if (other.CompareTag("Player") && !isPlayerInContact)
            {
                isPlayerInContact = true;
                contactDamageCoroutine = StartCoroutine(ContactDamageCoroutine(other));
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerInContact = false;
                if (contactDamageCoroutine != null)
                {
                    StopCoroutine(contactDamageCoroutine);
                    contactDamageCoroutine = null;
                }
            }
        }

        /// <summary>
        /// 플레이어와 겹쳐 있는 동안 0.5초마다 데미지를 줍니다.
        /// </summary>
        private IEnumerator ContactDamageCoroutine(Collider2D playerCollider)
        {
            while (isPlayerInContact && !isDying)
            {
                // TODO: 플레이어 데미지 시스템과 연동
                // 예시: playerCollider.GetComponent<PlayerHealth>()?.TakeDamage(monsterData.attack);
                Debug.Log($"[Monster] {monsterData.monsterName}이(가) 플레이어에게 {monsterData.attack} 데미지!");

                yield return new WaitForSeconds(CONTACT_DAMAGE_INTERVAL);
            }
        }

        // ==========================================
        // 외부 제어용 메서드 (기믹 스크립트에서 사용)
        // ==========================================

        /// <summary>
        /// 이동을 일시정지합니다. 기믹 스크립트에서 호출합니다.
        /// </summary>
        public void PauseMovement()
        {
            isMovementPaused = true;
            rb.linearVelocity = Vector2.zero;
        }

        /// <summary>
        /// 이동을 재개합니다. 기믹 스크립트에서 호출합니다.
        /// </summary>
        public void ResumeMovement()
        {
            isMovementPaused = false;
        }

        /// <summary>
        /// 넉백 면역 상태를 설정합니다. 돌진 등 특수 상태에서 사용합니다.
        /// </summary>
        /// <param name="immune">true면 넉백 면역</param>
        public void SetKnockbackImmune(bool immune)
        {
            if (immune)
            {
                kbCooldownTimer = float.MaxValue;
            }
            else
            {
                kbCooldownTimer = 0f;
            }
        }

        /// <summary>
        /// Rigidbody2D에 직접 속도를 설정합니다. 기믹 스크립트의 돌진 등에서 사용합니다.
        /// </summary>
        /// <param name="velocity">설정할 속도 벡터</param>
        public void SetVelocity(Vector2 velocity)
        {
            rb.linearVelocity = velocity;
        }

        /// <summary>
        /// 플레이어와의 거리를 반환합니다. 기믹 스크립트에서 사용합니다.
        /// </summary>
        /// <returns>플레이어와의 거리 (플레이어 없으면 float.MaxValue)</returns>
        public float GetDistanceToPlayer()
        {
            if (playerTransform == null) return float.MaxValue;
            return Vector2.Distance(transform.position, playerTransform.position);
        }

        /// <summary>
        /// 플레이어를 향한 방향 벡터를 반환합니다. (정규화됨)
        /// </summary>
        /// <returns>플레이어 방향 벡터 (플레이어 없으면 Vector2.zero)</returns>
        public Vector2 GetDirectionToPlayer()
        {
            if (playerTransform == null) return Vector2.zero;
            return ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        }
    }
}
