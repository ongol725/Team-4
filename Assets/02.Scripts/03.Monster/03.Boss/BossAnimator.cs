// ============================================================
// BossAnimator.cs
// 보스 애니메이션 상태 제어 (컴포지션). MonsterController와 같은 오브젝트에 부착.
//  - 평상시: 이동속도로 Idle/Move 자동 전환
//  - 패턴 실행 중: BossPatternBase.Execute()가 PlayPattern(animState)/BackToLocomotion() 호출
//  - 사망: MonsterController.OnDeath 구독 → Death 재생(고정)
//  - 그로기: StoneBreak 성공 등에서 PlayStun()/StopStun() 호출(행동 배선 시 연결)
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class BossAnimator : MonoBehaviour
    {
        [Header("애니메이터")]
        [Tooltip("비우면 자식에서 자동 탐색")]
        public Animator animator;

        [Header("페이즈별 컨트롤러 (1페이즈=파랑 / 2페이즈=빨강)")]
        [Tooltip("BossPhaseController.IsPhase2 에 따라 자동 전환. 같은 상태명을 가져야 함")]
        public RuntimeAnimatorController phase1Controller;
        public RuntimeAnimatorController phase2Controller;

        [Header("기본 상태 이름 (AnimatorController 상태명과 일치)")]
        public string idleState  = "Idle";
        public string moveState  = "Move";
        public string stunState  = "Stun";
        public string deathState = "Death";

        [Header("설정")]
        [Tooltip("이 속도(유닛/초) 이상이면 Move, 미만이면 Idle")]
        public float moveThreshold = 0.1f;
        public float crossFade = 0.05f;

        private MonsterController mc;
        private Rigidbody2D rb;
        private BossPhaseController phaseCtrl;
        private bool inPattern;   // 패턴 실행 중(자동 Idle/Move 정지)
        private bool stunned;
        private bool dead;
        private string current;

        private void Awake()
        {
            mc = GetComponent<MonsterController>();
            rb = GetComponent<Rigidbody2D>();
            phaseCtrl = GetComponent<BossPhaseController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            // 시작은 1페이즈 컨트롤러
            if (animator != null && phase1Controller != null) animator.runtimeAnimatorController = phase1Controller;
        }

        /// <summary>페이즈에 맞는 컨트롤러(파랑/빨강)로 전환. 같은 상태명이라 현재 상태 유지.</summary>
        private void ApplyPhaseController()
        {
            if (animator == null) return;
            var want = (phaseCtrl != null && phaseCtrl.IsPhase2) ? phase2Controller : phase1Controller;
            if (want != null && animator.runtimeAnimatorController != want)
            {
                animator.runtimeAnimatorController = want;
                if (!string.IsNullOrEmpty(current)) animator.Play(current);
            }
        }

        private void OnEnable()
        {
            if (mc != null) mc.OnDeath += HandleDeath;
            inPattern = stunned = dead = false;
            current = null;
        }

        private void OnDisable()
        {
            if (mc != null) mc.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            ApplyPhaseController(); // 페이즈 색(파랑/빨강) 자동 전환
            if (animator == null || dead || inPattern || stunned) return;
            bool moving = rb != null && rb.linearVelocity.sqrMagnitude > moveThreshold * moveThreshold;
            Cross(moving ? moveState : idleState);
        }

        /// <summary>패턴 상태 재생(자동 Idle/Move 정지). retrigger=true면 같은 상태도 처음부터 재시작(연타용).</summary>
        public void PlayPattern(string state, bool retrigger = false)
        {
            if (dead) return;
            inPattern = true;
            if (string.IsNullOrEmpty(state)) return;
            if (retrigger && animator != null) { current = state; animator.Play(state, 0, 0f); }
            else Cross(state);
        }

        /// <summary>패턴 종료 시 호출 — 자동 Idle/Move 복귀.</summary>
        public void BackToLocomotion() { if (!dead) inPattern = false; }

        /// <summary>그로기 시작(예: StoneBreak 성공). 끝나면 StopStun() 호출.</summary>
        public void PlayStun() { if (dead) return; stunned = true; Cross(stunState); }
        public void StopStun() { stunned = false; }

        private void HandleDeath(MonsterController _)
        {
            dead = true; inPattern = stunned = false;
            Cross(deathState);
        }

        private void Cross(string state)
        {
            if (string.IsNullOrEmpty(state) || state == current) return;
            current = state;
            animator.CrossFade(state, crossFade);
        }
    }
}
