// ============================================================
// BossPhaseController.cs
// 보스 1→2 페이즈 전환 제어 (기획서 기준)
//  - 체력 40% 미만 → 쓰던 패턴을 끝까지 마저 쓴 뒤 2페이즈 전환 실행.
//  - 예외(안전장치): 패턴이 길어 도중 35% 이하로 떨어지면, 패턴을 중단하지 않고
//    그대로 진행시키되 '무적'을 부여해 전환 전에 죽지 않게 한다.
//  - 전환 진행: 보스 무적 + 패턴 정지(제자리) → 연출(시간) → 바닥 영구 장판 생성.
//  - 2페이즈 진입: 기존 패턴 쿨다운 단축(강화) 후 패턴 구동 재개.
//    (신규 2페이즈 전용 패턴: 분신 군무·힐링 토템은 별도 작업)
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    [RequireComponent(typeof(BossPatternDriver))]
    public class BossPhaseController : MonoBehaviour
    {
        [Header("전환 임계치(체력 비율)")]
        [Tooltip("이 비율 미만이면 2페이즈 전환 시작(기획서 40%)")]
        [Range(0f, 1f)] public float phase2Threshold = 0.40f;

        [Tooltip("전환 대기 중 이 비율 이하로 떨어지면 무적 부여(패턴은 중단 않음). 기획서 35%")]
        [Range(0f, 1f)] public float invincibleThreshold = 0.35f;

        [Header("전환 연출")]
        [Tooltip("무적 상태로 멈춰 있는 전환 연출 시간(초)")]
        public float transitionDuration = 2f;

        [Tooltip("전환 시 내려앉을 가운데 = 스폰 위치 + 이 오프셋. 예: (0,-8,0)이면 아래로 8 이동")]
        public Vector3 phaseCenterOffset = Vector3.zero;

        [Header("2페이즈")]
        [Tooltip("진입 시 바닥에 생성할 영구 장판 프리팹(선택)")]
        public GameObject floorHazardPrefab;

        [Tooltip("2페이즈 기본 패턴 선택 확률(나머지는 특수). 기획서 60% 기본")]
        [Range(0f, 1f)] public float phase2BasicChance = 0.6f;

        private MonsterController controller;
        private BossPatternDriver driver;
        private BossAnimator bossAnimator;
        private Vector3 phaseCenter;   // 전환 시 내려앉을 가운데 = 스폰(시작) 위치
        private bool transitioning;
        private bool transitioned;
        private GameObject hazardInstance; // 2페이즈 영구 장판 인스턴스(보스 사망/비활성 시 정리)

        /// <summary>2페이즈 진입 완료 여부.</summary>
        public bool IsPhase2 => transitioned;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
            driver = GetComponent<BossPatternDriver>();
            bossAnimator = GetComponent<BossAnimator>();
        }

        private void OnEnable()
        {
            // 풀 재사용/재시작 대비 초기화
            transitioning = false;
            transitioned = false;
            phaseCenter = transform.position + phaseCenterOffset; // 스폰 위치 + 오프셋 = 전환 시 내려앉을 가운데
        }

        private void OnDisable()
        {
            DestroyHazard(); // 풀 반환/씬 종료 시에도 장판이 남지 않도록 정리
        }

        private void Update()
        {
            if (controller == null) return;
            if (controller.IsDead) { DestroyHazard(); return; } // 보스 사망 → 영구 장판 제거
            if (transitioning || transitioned) return;

            if (controller.HpRatio < phase2Threshold)
                StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            transitioning = true;

            // 40%: 진행 중인 패턴이 끝날 때까지 대기(중단하지 않음).
            // 대기 중 35% 이하로 떨어지면 죽지 않도록 무적만 부여하고 패턴은 계속 진행.
            while (driver != null && driver.IsExecuting)
            {
                if (!controller.IsInvincible && controller.HpRatio <= invincibleThreshold)
                    controller.SetInvincible(true);
                yield return null;
            }

            // 패턴 종료 → 전환 진행: 무적 + 패턴 정지 + 이동 제어 위임
            controller.SetInvincible(true);
            if (driver != null) driver.Halt();
            controller.BeginExternalMovement();
            controller.SetKnockbackImmune(true);

            Vector3 start = transform.position;
            float half = Mathf.Max(0.1f, transitionDuration * 0.5f);

            // 1) 1페이즈 점프 모션(체공)
            if (bossAnimator != null) bossAnimator.PlayPattern("Leap");
            yield return new WaitForSeconds(half);

            // 2) 2페이즈 전환 → 착지 모션으로 가운데(스폰 위치)로 하강 이동
            ApplyPhase2Strengthen();
            transitioned = true;                 // IsPhase2 → BossAnimator가 P2 컨트롤러로 스왑
            yield return null;                   // 스왑 한 프레임 대기
            if (bossAnimator != null) bossAnimator.PlayPattern("LeapLand");
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, phaseCenter, Mathf.Clamp01(t / half));
                yield return null;
            }
            transform.position = phaseCenter;    // 바닥 위치 고정(가운데)

            // 바닥 영구 장판(가운데). 영구 지속이라 풀 대신 직접 생성하고, 보스 사망 시 정리한다.
            if (floorHazardPrefab != null)
                hazardInstance = Instantiate(floorHazardPrefab, transform.position, Quaternion.identity);

            transitioning = false;

            // 제어/무적 해제 + locomotion 복귀 + 패턴 재개
            controller.SetKnockbackImmune(false);
            controller.EndExternalMovement();
            if (bossAnimator != null) bossAnimator.BackToLocomotion();
            controller.SetInvincible(false);
            if (driver != null) driver.Resume();
        }

        /// <summary>2페이즈 강화: 모든 패턴을 phase2 모드로 전환 + 드라이버 기본/특수 비율 변경.</summary>
        private void ApplyPhase2Strengthen()
        {
            var patterns = GetComponents<BossPatternBase>();
            foreach (var p in patterns)
                p.phase2Mode = true;

            if (driver != null) driver.basicChance = phase2BasicChance; // 60/40
        }

        private void DestroyHazard()
        {
            if (hazardInstance != null) { Destroy(hazardInstance); hazardInstance = null; }
        }
    }
}
