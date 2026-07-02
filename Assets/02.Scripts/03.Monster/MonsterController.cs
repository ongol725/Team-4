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

        [Header("HP 바")]
        [Tooltip("이 몬스터 위에 표시할 추적형 HP바 프리팹 (Monster_HpBar)")]
        public GameObject hpBarPrefab;

        [Header("그림자")]
        [Tooltip("발밑 그림자 폭 배수 (1=기본, 작을수록 작아짐). 스프라이트 여백이 큰 몬스터용")]
        [Range(0.1f, 2f)] public float shadowWidthMul = 1f;

        [Header("렌더 정렬")]
        [Tooltip("타일맵(바닥=0/벽=1) 위에 보이도록 하는 스프라이트 정렬 순서")]
        public int sortingOrder = 10;

        [Header("추적 정지")]
        [Tooltip("0이면 콜라이더 크기에 맞춰 자동 접근(아래 '접근 겹침' 사용). 0보다 크면 이 중심거리에서 정지(원거리 몬스터 등 수동 지정)")]
        public float stopDistance = 0f;

        [Tooltip("자동 접근 시 플레이어와 겹치는 정도(월드 단위). 클수록 더 바짝 붙음")]
        public float approachOverlap = 0.4f;

        [Header("충돌 데미지 설정")]
        [Tooltip("접촉 데미지 판정 간격 (초)")]
        private const float CONTACT_DAMAGE_INTERVAL = 0.5f;

        [Header("넉백 설정")]
        [Tooltip("넉백 모션 지속 시간 (초)")]
        private const float KNOCKBACK_DURATION = 0.3f;
        // 타격감 강화: 넉백 거리 배수(전 무기 공통) — "확 뒤로 밀리는" 느낌
        private const float KNOCKBACK_FEEL_MULT = 1.6f;
        // 골드 드롭 배수(스폰 2배와 함께 순수입 유지용). 내림 처리 → 홀수는 0.5 손실.
        private const float GOLD_DROP_MULT = 0.5f;
        // 넉백 최고 속도 상한(m/s). 강한 넉백이 벽을 뚫고 나가는 터널링 방지.
        private const float MAX_KB_SPEED = 16f;

        [Header("사망 설정")]
        [Tooltip("사망 모션/이펙트 재생 후 풀 반환까지 대기 시간 (초). 보스는 길게(예: 5)")]
        public float deathDelay = 0.1f;

        // ==========================================
        // 런타임 변수
        // ==========================================
        private int currentHP;
        private MonsterState currentState = MonsterState.Tracking;
        private Transform playerTransform;
        private Rigidbody2D rb;
        private Collider2D col;
        private SpriteRenderer spriteRenderer;
        private Color baseColor = Color.white; // 풀 재사용 시 사망 페이드/피격 색 복구용

        // 피격 연출(스쿼시&스트레치). 피격 시작 시점의 현재 스케일을 기준으로 캡처해
        // 크기 기믹(분열 슬라임 등)이 바꿔둔 크기도 정확히 복원한다.
        private Transform _spriteTf;
        private Vector3   _restScale = Vector3.one; // 스쿼시 시작 시 캡처한 원래 크기
        private bool      _squashing;               // 스쿼시 진행 중(연속 피격 재캡처 방지)
        private Coroutine _hitFxCo;

        // 넉백 관련
        private float kbCooldownTimer = 0f;
        private Coroutine knockbackCoroutine;

        // 접촉 데미지 관련
        private Coroutine contactDamageCoroutine;
        private bool isPlayerInContact = false;

        // 사망 처리 중 플래그
        private bool isDying = false;

        // 콜라이더 크기 기반 자동 정지 거리(중심간). InitializeMonster에서 계산.
        private float autoStopDistance = 0.3f;

        // HP바 (오브젝트 풀링 대응: 인스턴스 1개를 생성 후 재사용)
        private GameObject hpBarInstance;
        private BagSurvivor.UI.MonsterHpBar hpBar;

        // 특수 기믹에서 이동을 제어하기 위한 플래그
        private bool isMovementPaused = false;

        // 상태이상 관련
        private float     _speedMultiplier = 1f;
        private Coroutine _stunCo;
        private float     _stunReadyTime; // 스턴 적별 재적용 쿨다운 해제 시각(Time.time 기준)
        private Coroutine _slowCo;
        private Coroutine _burnCo;

        // 보스 패턴 등에서 Rigidbody 이동을 직접 제어하기 위해 컨트롤러 기본 이동을 위임받는 플래그.
        // true인 동안 FixedUpdate의 HandleMovement(추적/정지 처리)를 건너뛴다.
        private bool externalMovementControl = false;

        // 무적 플래그(보스 페이즈 전환 연출 등). true인 동안 TakeDamage가 피해를 무시한다.
        private bool isInvincible = false;

        // 받는 피해 배율(1=기본). 보스 강화 버프 등에서 일시적으로 낮춘다.
        private float damageTakenMultiplier = 1f;
        private Coroutine damageReductionCo;

        // 사망 통지 콜백 (스폰 주체가 주입: 방 클리어 통지·풀 반환 위임). null이면 자체 비활성화.
        private System.Action<MonsterController> deathCallback;

        /// <summary>사망 시 발생하는 이벤트(분열 등 기믹용). 풀 반환 직전 1회 호출. OnDisable에서 정리.</summary>
        public event System.Action<MonsterController> OnDeath;

        // 난이도(층/시간) 스탯 배율. 스폰 시 주입되며, 베이스 스탯에 곱해 런타임 스탯을 산출.
        private float hpMultiplier = 1f;
        private float attackMultiplier = 1f;

        // 배율이 적용된 런타임 스탯 (SO 원본은 수정하지 않음)
        private int runtimeMaxHP;
        private int runtimeAttack;

        // ==========================================
        // 프로퍼티 (외부 접근용)
        // ==========================================

        /// <summary>
        /// 현재 HP (읽기 전용)
        /// </summary>
        public int CurrentHP => currentHP;

        /// <summary>
        /// 배율이 적용된 최대 HP (HP바·비율 계산용)
        /// </summary>
        public int MaxHP => runtimeMaxHP;

        /// <summary>현재 적용된 HP/공격 배율 (분열체가 부모 기준으로 자기 배율을 산출할 때 사용).</summary>
        public float HpMul => hpMultiplier;
        public float AtkMul => attackMultiplier;

        /// <summary>true면 사망 시 골드를 드롭하지 않음 (엘리트 슬라임 분열 중간 세대 등). 기믹이 설정.</summary>
        [System.NonSerialized] public bool suppressGoldDrop = false;

        /// <summary>
        /// 배율이 적용된 공격력 (접촉/투사체 데미지용)
        /// </summary>
        public int Attack => runtimeAttack;

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
            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
                _spriteTf = spriteRenderer.transform; // 단일 오브젝트 몬스터면 루트 = 스프라이트
            }

            // 발밑 그림자 자동 부착 (모든 몬스터 공통, 풀링 안전)
            var blobShadow = GetComponent<BlobShadow>();
            if (blobShadow == null) blobShadow = gameObject.AddComponent<BlobShadow>();
            blobShadow.SetWidthMul(shadowWidthMul);

            // 카메라 추적 시 떨림(지터) 방지: 물리 스텝 사이를 부드럽게 보간
            // + 빠른 넉백에도 벽을 통과(터널링)하지 않도록 연속 충돌 감지
            if (rb != null)
            {
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
        }

        private void OnEnable()
        {
            // 오브젝트 풀에서 재활성화될 때마다 초기화
            InitializeMonster();
            ShowHpBar();
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
            externalMovementControl = false;
            isInvincible = false;
            damageTakenMultiplier = 1f;
            damageReductionCo = null;
            _speedMultiplier = 1f;
            _stunCo = null;
            _slowCo = null;
            _burnCo = null;
            _hitFxCo = null;
            if (_squashing && _spriteTf != null) _spriteTf.localScale = _restScale; // 피격 스쿼시 잔존 복원
            _squashing = false;
            deathCallback = null;
            OnDeath = null; // 풀 재사용 시 이전 구독자 잔존 방지(기믹은 OnEnable에서 재구독)
            hpMultiplier = 1f;
            attackMultiplier = 1f;
            HideHpBar();
        }

        private void OnDestroy()
        {
            // 풀에서 완전히 제거될 때 HP바도 함께 정리
            if (hpBarInstance != null) Destroy(hpBarInstance);
        }

        // ==========================================
        // HP바 (자동 생성/재사용)
        // ==========================================

        /// <summary>HP바를 생성(최초 1회)하거나 재사용하여 표시하고 이 몬스터에 연결합니다.</summary>
        private void ShowHpBar()
        {
            if (hpBarPrefab == null) return;

            if (hpBarInstance == null)
            {
                Transform parent = BagSurvivor.UI.MonsterHpBarRoot.GetParent();
                hpBarInstance = Instantiate(hpBarPrefab, parent);
                hpBar = hpBarInstance.GetComponent<BagSurvivor.UI.MonsterHpBar>();
            }

            hpBarInstance.SetActive(true);
            if (hpBar != null) hpBar.SetTarget(this);
        }

        /// <summary>풀 반환 시 HP바를 숨깁니다 (인스턴스는 재사용 위해 유지).</summary>
        private void HideHpBar()
        {
            if (hpBarInstance != null) hpBarInstance.SetActive(false);
        }

        /// <summary>
        /// 스폰 주체가 사망 처리 콜백을 주입합니다(방 클리어 통지·풀 반환 위임).
        /// 스폰할 때마다 새로 설정하며, 풀 반환(OnDisable) 시 자동 해제됩니다.
        /// </summary>
        public void SetDeathCallback(System.Action<MonsterController> callback)
        {
            deathCallback = callback;
        }

        /// <summary>
        /// 난이도(층·경과시간) 스탯 배율을 주입합니다.
        /// 반드시 활성화(SetActive(true)) 전에 호출해야 OnEnable의 초기화에 반영됩니다.
        /// </summary>
        public void SetStatMultiplier(float hpMul, float attackMul)
        {
            hpMultiplier = hpMul <= 0f ? 1f : hpMul;
            attackMultiplier = attackMul <= 0f ? 1f : attackMul;
        }

        /// <summary>체력을 회복합니다(최대 체력 한도). </summary>
        public void Heal(int amount)
        {
            if (isDying || amount <= 0) return;
            currentHP = Mathf.Min(runtimeMaxHP, currentHP + amount);
        }

        /// <summary>최대 체력을 늘립니다(현재 체력도 같이 증가). 보스가 시간에 따라 강해지는 용도.</summary>
        public void IncreaseMaxHP(int amount)
        {
            if (isDying || amount == 0) return;
            runtimeMaxHP = Mathf.Max(1, runtimeMaxHP + amount);
            currentHP = Mathf.Clamp(currentHP + amount, 0, runtimeMaxHP);
        }

        /// <summary>
        /// 몬스터 초기 상태로 리셋합니다.
        /// 오브젝트 풀에서 꺼낼 때 자동 호출됩니다.
        /// </summary>
        private void InitializeMonster()
        {
            if (monsterData == null) return;

            // 배율 적용 런타임 스탯 산출 (SO 원본 불변)
            runtimeMaxHP = Mathf.Max(1, Mathf.RoundToInt(monsterData.maxHP * hpMultiplier));
            runtimeAttack = Mathf.Max(0, Mathf.RoundToInt(monsterData.attack * attackMultiplier));

            currentHP = runtimeMaxHP;
            currentState = MonsterState.Tracking;
            kbCooldownTimer = 0f;
            isDying = false;
            isMovementPaused = false;

            // 콜라이더 활성화
            if (col != null) col.enabled = true;

            // 타일맵 위에 보이도록 정렬 순서 적용 + 색 복구(풀 재사용 시 사망 페이드/피격 잔색 제거)
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = sortingOrder;
                spriteRenderer.color = baseColor;
            }

            // 플레이어 찾기 (태그 기반)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;

            ComputeAutoStopDistance();
        }

        /// <summary>몬스터·플레이어 콜라이더 크기에서 자동 정지 거리를 계산합니다(겹침만큼 더 가까이).</summary>
        private void ComputeAutoStopDistance()
        {
            float monsterR = 0.25f;
            if (col != null)
            {
                Vector3 e = col.bounds.extents;
                monsterR = (e.x + e.y) * 0.5f;
            }

            float playerR = 0.25f;
            if (playerTransform != null)
            {
                Collider2D pc = playerTransform.GetComponent<Collider2D>();
                if (pc != null)
                {
                    Vector3 e = pc.bounds.extents;
                    playerR = (e.x + e.y) * 0.5f;
                }
            }

            autoStopDistance = Mathf.Max(0.05f, monsterR + playerR - approachOverlap);
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

            // 보스 패턴이 이동을 위임받은 동안에는 컨트롤러 기본 이동을 건너뛴다(패턴이 Rigidbody 직접 제어).
            if (externalMovementControl) return;

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

            Vector2 toPlayer = (Vector2)playerTransform.position - (Vector2)transform.position;
            float dist = toPlayer.magnitude;

            // 바라보는 방향에 따른 좌우 반전 (멈춰 있어도 방향 유지)
            if (spriteRenderer != null && toPlayer.x != 0f)
            {
                spriteRenderer.flipX = toPlayer.x < 0f;
            }

            // 정지 거리 안이면 더 파고들지 않고 정지.
            // stopDistance>0이면 수동 지정값, 아니면 콜라이더 기반 자동값(approachOverlap만큼 겹쳐 접근).
            // 플레이어 중심까지 추적하며 방향이 매 프레임 뒤집혀 떨리는 현상을 방지한다.
            float stop = stopDistance > 0f ? stopDistance : autoStopDistance;
            if (dist <= Mathf.Max(stop, 0.0001f))
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 dir = AvoidPillar(toPlayer / dist);
            rb.linearVelocity = dir * monsterData.moveSpeed * _speedMultiplier;
        }

        /// <summary>스프라이트를 좌우 방향(dirX)에 맞춰 뒤집습니다. 돌진 등 외부제어 패턴 중 방향 고정용
        /// (외부제어 동안엔 ChasePlayer가 안 돌아 flipX가 자동 갱신되지 않으므로 패턴이 직접 호출).</summary>
        public void FaceDirection(float dirX)
        {
            if (spriteRenderer != null && Mathf.Abs(dirX) > 0.01f)
                spriteRenderer.flipX = dirX < 0f;
        }

        // 기둥 받침(PillarBlock 트리거)을 '돌아서' 가는 국소 회피. 물리 충돌이 아니라 레이캐스트 감지 +
        // 한 방향으로 커밋(경로가 뚫릴 때까지 유지)해 매끄럽게 우회 → 비비적댐/떨림 없음.
        private static int pillarMaskCache = -1; // -1=미초기화, 0=레이어없음, 그외=레이어마스크
        private int avoidSide;                    // 0=비회피, +1=좌측 접선, -1=우측 접선(커밋)

        private Vector2 AvoidPillar(Vector2 dir)
        {
            if (pillarMaskCache < 0)
            {
                int l = LayerMask.NameToLayer("PillarBlock");
                pillarMaskCache = l < 0 ? 0 : (1 << l);
            }
            if (pillarMaskCache == 0) return dir;

            const float look = 2.5f;
            Vector2 pos = transform.position;
            RaycastHit2D hit = Physics2D.Raycast(pos, dir, look, pillarMaskCache);
            if (!hit) { avoidSide = 0; return dir; }   // 경로 깨끗 → 회피 해제(직진)

            Vector2 left = new Vector2(-dir.y, dir.x);
            if (avoidSide == 0)
            {
                // 받침이 왼쪽이면 오른쪽으로, 오른쪽이면 왼쪽으로 돈다(한 번 정하면 통과까지 유지)
                Vector2 toHit = hit.point - pos;
                avoidSide = Vector2.Dot(left, toHit) > 0f ? -1 : 1;
            }
            Vector2 tangent = avoidSide > 0 ? left : -left;
            float t = Mathf.Clamp01(hit.distance / look);   // 0=코앞(접선 위주) … 1=멀리(전진 위주)
            return (tangent * (1f - t) + dir * t).normalized;
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
            if (isDying || isInvincible) return;

            // 방어력 + 받는 피해 배율 적용
            int finalDamage = monsterData.CalculateDamageTaken(rawDamage);
            if (damageTakenMultiplier != 1f)
                finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * damageTakenMultiplier));
            currentHP -= finalDamage;

            // 데미지 숫자 띄우기 (모든 데미지 소스가 이 메서드로 모임)
            DamagePopup.Show(transform.position, finalDamage);

            // 피격 이펙트 (Hit 상태 - 이동을 방해하지 않음). 연속 피격 시 재시작(스케일 누적 방지)
            if (_hitFxCo != null) StopCoroutine(_hitFxCo);
            _hitFxCo = StartCoroutine(HitEffectCoroutine());

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
            // 연속 피격 중이 아니면 현재 크기를 원래 크기로 캡처(기믹이 바꾼 크기도 정확히 복원)
            if (!_squashing && _spriteTf != null)
            {
                _restScale = _spriteTf.localScale;
                _squashing = true;
            }
            Vector3 rest    = _restScale;
            // 격렬한 탄성 반응: (1) 세로로 확 늘림 → (2) 반동으로 납작하게 오버슈트 → (3) 원래대로 안정화
            Vector3 stretch = Vector3.Scale(rest, new Vector3(0.5f,  1.6f,  1f)); // 강하게 늘림
            Vector3 squash  = Vector3.Scale(rest, new Vector3(1.35f, 0.72f, 1f)); // 반동(납작) 오버슈트

            // 피격 순간: 흰 번쩍(임팩트) → 빨강
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
            if (_spriteTf != null) _spriteTf.localScale = stretch;

            // 1단계: 늘림 → 납작(빠르게 튕김)
            const float d1 = 0.05f;
            float t = 0f;
            while (t < d1)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / d1);
                if (_spriteTf != null) _spriteTf.localScale = Vector3.Lerp(stretch, squash, p);
                if (spriteRenderer != null && p >= 0.4f) spriteRenderer.color = Color.red;
                yield return null;
            }

            // 2단계: 납작 → 원래대로 (ease-out 탄성)
            const float d2 = 0.13f;
            t = 0f;
            while (t < d2)
            {
                t += Time.deltaTime;
                float p  = Mathf.Clamp01(t / d2);
                float ep = 1f - (1f - p) * (1f - p);
                if (_spriteTf != null) _spriteTf.localScale = Vector3.Lerp(squash, rest, ep);
                if (spriteRenderer != null && p >= 0.5f && !isDying) spriteRenderer.color = baseColor;
                yield return null;
            }

            if (_spriteTf != null) _spriteTf.localScale = rest;
            if (spriteRenderer != null && !isDying) spriteRenderer.color = baseColor;
            _squashing = false;
            _hitFxCo = null;
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
            // 넉백 저항력 + 타격감 배수 적용: 최종 거리 = 기본 거리 * (1.0 - KB_Resist) * 강도배수
            float finalDistance = baseKnockbackDistance * (1f - monsterData.kbResist) * KNOCKBACK_FEEL_MULT;

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

            // 감속(ease-out) 모션: 시작 속도가 크고 점점 줄어 '확' 밀렸다가 멈춘다.
            // 등속 아닌 감속이라 순간 충격이 강하게 느껴짐. (평균속도 = distance/DUR 유지: v0 = 2·distance/DUR)
            float v0 = 2f * distance / KNOCKBACK_DURATION;
            float timer = 0f;

            while (timer < KNOCKBACK_DURATION)
            {
                float p = timer / KNOCKBACK_DURATION;
                // 속도 상한 적용: 너무 빠르면 Continuous 충돌이 놓쳐 벽을 뚫으므로 상한으로 캡
                float speed = Mathf.Min(v0 * (1f - p), MAX_KB_SPEED);
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

            GameManager.Instance?.AddKill();   // 결과창 '처치 몬스터' 누적

            if (RunStatsLogger.Instance != null)
            {
                int floor = GameManager.Instance != null ? GameManager.Instance.currentFloor : 1;
                string mName = monsterData != null ? monsterData.name : gameObject.name;
                // 슬라임은 본체(루트)만 층별 처치수에 집계 — 분열체는 제외
                var eliteSlime = GetComponent<EliteSlimeSplitGimmick>();
                bool countForFloor = eliteSlime == null || eliteSlime.generation == eliteSlime.rootGeneration;
                RunStatsLogger.Instance.MonsterKilled(mName, floor, countForFloor);
                // 엘리트/중간보스/최종보스 = '층 보스'로 기록
                if (GetComponent<EliteMonster>() != null || GetComponent<BossPatternDriver>() != null)
                    RunStatsLogger.Instance.BossKilled(floor);
            }

            // 1. 즉시 충돌체 비활성화
            if (col != null) col.enabled = false;
            rb.linearVelocity = Vector2.zero;

            // 2. 사망 모션/이펙트 시작 — 대기 '전'에 호출해 deathDelay 동안 사망 애니가 보이게.
            //    (분열 등 기믹도 여기서 반응. OnDeath → deathCallback 순서는 방 클리어 카운트에 중요)
            try
            {
                OnDeath?.Invoke(this);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MonsterController] OnDeath 핸들러 예외: {e}");
            }

            // 3. 사망 연출 시간(보스는 길게) — 이 동안 Death 애니/이펙트 표시
            yield return new WaitForSeconds(deathDelay);

            // 4. 드롭 + 사망 통지 / 풀 반환
            SpawnDropItem();
            if (deathCallback != null)
                deathCallback.Invoke(this);
            else
                gameObject.SetActive(false);
        }

        /// <summary>
        /// 사망 시 드롭 아이템을 스폰합니다.
        /// </summary>
        private void SpawnDropItem()
        {
            if (suppressGoldDrop) return; // 분열 중간 세대 등: 드롭 억제
            if (monsterData == null || monsterData.dropItemValue <= 0) return;

            // 골드 배수(0.5) 적용 후 내림. 스폰 2배와 함께 순수입 유지.
            int gold = Mathf.FloorToInt(monsterData.dropItemValue * GOLD_DROP_MULT);
            if (gold <= 0) return; // 원래 실드롭이지만 반토막에 0된 경우(현재 최소 4→2라 발생 안 함)

            var mgr = BagSurvivor.Items.GoldDropManager.Instance;
            if (mgr == null) return;

            // 강자의 방 등 이벤트 골드 배율만큼 '코인 개수'를 늘려 떨어뜨림(하나씩→여러 개).
            int coins = Mathf.Max(1, Mathf.RoundToInt(RoomEventState.GoldMultiplier)); // 강자의 방=2
            for (int i = 0; i < coins; i++)
            {
                Vector3 p = transform.position;
                if (coins > 1) p += (Vector3)(Random.insideUnitCircle * 0.45f); // 여러 개면 살짝 흩뿌림
                mgr.DropGold(p, gold);
            }
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
            PlayerHealth playerHealth = playerCollider != null ? playerCollider.GetComponentInParent<PlayerHealth>() : null;

            while (isPlayerInContact && !isDying)
            {
                // 시간 배율이 적용된 공격력으로 플레이어에게 접촉 데미지
                if (playerHealth != null && !playerHealth.IsDead)
                {
                    playerHealth.LastAttacker = monsterData != null ? monsterData.name : gameObject.name; // 킬러 통계
                    playerHealth.TakeDamage(runtimeAttack);
                }

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
        /// 컨트롤러 기본 이동(추적/정지)을 일시 위임받습니다. 보스 패턴이 Rigidbody를 직접 제어할 때 호출.
        /// 호출 후 SetVelocity로 이동을 제어하고, 끝나면 반드시 EndExternalMovement()로 복귀시킵니다.
        /// </summary>
        public void BeginExternalMovement()
        {
            externalMovementControl = true;
        }

        /// <summary>
        /// 위임받은 이동 제어를 컨트롤러에 되돌립니다(추적 복귀). 속도는 0으로 정리합니다.
        /// </summary>
        public void EndExternalMovement()
        {
            externalMovementControl = false;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        /// <summary>무적 상태를 설정합니다. true인 동안 TakeDamage가 무시됩니다(페이즈 전환 연출 등).</summary>
        public void SetInvincible(bool value)
        {
            isInvincible = value;
        }

        /// <summary>현재 무적 여부.</summary>
        public bool IsInvincible => isInvincible;

        /// <summary>일정 시간 동안 받는 피해를 reductionPercent(0~1)만큼 감소시킵니다(보스 강화 버프 등).</summary>
        [Tooltip("데미지 감소 버프 동안 보스 위에 표시할 이펙트(선택). 반투명 권장)")]
        public GameObject damageReductionVfxPrefab;
        private GameObject damageReductionVfx;

        public void ApplyDamageReduction(float reductionPercent, float duration)
        {
            if (damageReductionCo != null) StopCoroutine(damageReductionCo);
            if (damageReductionVfx != null) Destroy(damageReductionVfx); // 중복 적용 시 이전 이펙트 정리
            damageReductionCo = StartCoroutine(DamageReductionRoutine(Mathf.Clamp01(reductionPercent), duration));
        }

        private IEnumerator DamageReductionRoutine(float reduction, float duration)
        {
            damageTakenMultiplier = 1f - reduction;
            if (damageReductionVfxPrefab != null)
            {
                damageReductionVfx = Instantiate(damageReductionVfxPrefab, transform); // 자식 → 보스 따라다님
                damageReductionVfx.transform.localPosition = Vector3.zero;
            }
            yield return new WaitForSeconds(duration);
            damageTakenMultiplier = 1f;
            if (damageReductionVfx != null) { Destroy(damageReductionVfx); damageReductionVfx = null; }
            damageReductionCo = null;
        }

        /// <summary>현재 체력 비율(0~1).</summary>
        public float HpRatio => runtimeMaxHP > 0 ? (float)currentHP / runtimeMaxHP : 0f;

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

        // ==========================================
        // 상태이상 API
        // ==========================================

        /// <summary>duration 초 동안 이동을 멈춥니다. 중복 적용 시 남은 시간을 갱신합니다.</summary>
        public void ApplyStun(float duration)
        {
            if (isDying) return;
            if (monsterData != null && monsterData.grade != MonsterGrade.Normal) return; // 보스·엘리트 스턴 면역
            if (Time.time < _stunReadyTime) return;   // 적별 재적용 쿨다운(연사 무기 영구기절 방지)
            _stunReadyTime = Time.time + 2f;
            if (_stunCo != null) StopCoroutine(_stunCo);
            _stunCo = StartCoroutine(StunRoutine(duration));
        }

        private IEnumerator StunRoutine(float duration)
        {
            isMovementPaused = true;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(duration);
            isMovementPaused = false;
            _stunCo = null;
        }

        /// <summary>duration 초 동안 이동 속도에 multiplier(0~1)를 곱합니다. 중복 시 갱신.</summary>
        public void ApplySlow(float multiplier, float duration)
        {
            if (isDying) return;
            if (monsterData != null && monsterData.grade != MonsterGrade.Normal) return; // 보스·엘리트 슬로우 면역
            if (_slowCo != null) StopCoroutine(_slowCo);
            _slowCo = StartCoroutine(SlowRoutine(Mathf.Clamp01(multiplier), duration));
        }

        private IEnumerator SlowRoutine(float multiplier, float duration)
        {
            _speedMultiplier = multiplier;
            yield return new WaitForSeconds(duration);
            _speedMultiplier = 1f;
            _slowCo = null;
        }

        /// <summary>tickInterval마다 damagePerTick 피해를 ticks회 입힙니다. 중복 시 갱신(재점화).</summary>
        public void ApplyBurn(int damagePerTick, float tickInterval, int ticks)
        {
            if (isDying) return;
            if (_burnCo != null) StopCoroutine(_burnCo);
            _burnCo = StartCoroutine(BurnRoutine(damagePerTick, tickInterval, ticks));
        }

        private IEnumerator BurnRoutine(int dmg, float interval, int ticks)
        {
            for (int i = 0; i < ticks && !isDying; i++)
            {
                yield return new WaitForSeconds(interval);
                if (!isDying) TakeDamage(dmg);
            }
            _burnCo = null;
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
