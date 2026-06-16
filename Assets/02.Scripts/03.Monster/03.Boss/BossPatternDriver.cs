// ============================================================
// BossPatternDriver.cs
// 보스/중간보스에 부착된 BossPatternBase 컴포넌트들을 수집해
// 기본/특수 분기 → 쿨다운·사거리·확률로 패턴을 선택·실행하는 구동기.
//  - 기획서 1페이즈 FSM: State_Chase 기준 기본 패턴 80% / 특수 패턴 20%
//  - 기본 패턴 사이 간격(내부 쿨타임) 1초 / 특수 패턴 사이 간격 10초
//  - 패턴이 없는 그룹이면 다른 그룹으로 폴백, 둘 다 준비 안 됐으면 추적 유지
//
// 중간 보스 재사용: 해당 프리팹에 원하는 Pattern_* 컴포넌트만 붙이면
// 이 드라이버가 자동으로 수집해 굴린다.
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class BossPatternDriver : MonoBehaviour
    {
        [Header("발동 범위")]
        [Tooltip("플레이어가 이 거리(m) 안에 들어오면 패턴 구동 시작 (보스 인식범위)")]
        public float detectionRange = 10f;

        [Header("기본/특수 분기")]
        [Tooltip("기본 패턴이 선택될 확률 (나머지는 특수 패턴). 기획서 기준 0.8")]
        [Range(0f, 1f)]
        public float basicChance = 0.8f;

        [Header("패턴 사이 간격(초)")]
        [Tooltip("기본 패턴 실행 후 다음 선택까지 대기(내부 쿨타임)")]
        public float basicGap = 1f;

        [Tooltip("특수 패턴 실행 후 다음 선택까지 대기")]
        public float specialGap = 10f;

        [Tooltip("패턴을 못 고른 동안(추적 중) 재시도 간격")]
        public float decisionInterval = 0.2f;

        [Header("디버그(검증용)")]
        [Tooltip("켜면 자동 패턴 선택을 멈추고, 숫자키(1~N)로 패턴을 직접 발동. 쿨다운·확률·사거리 무시.")]
        public bool debugManualMode = false;

        [Tooltip("디버그 키 매핑 범례를 화면 좌상단에 표시")]
        public bool debugShowLegend = true;

        private MonsterController controller;
        private readonly List<BossPatternBase> basics = new List<BossPatternBase>();
        private readonly List<BossPatternBase> specials = new List<BossPatternBase>();
        private readonly List<BossPatternBase> all = new List<BossPatternBase>(); // 디버그 키 인덱싱용(부착 순서)
        private Coroutine loop;
        private bool aggroed;   // 한 번 인식범위에 진입하면 유지(보스룸 입장 후 퇴장 불가)
        private bool executing; // 패턴 실행 중(수동/자동 공용 가드)

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
            CollectPatterns();
        }

        /// <summary>부착된 모든 BossPatternBase를 기본/특수로 분류해 수집합니다.</summary>
        private void CollectPatterns()
        {
            basics.Clear();
            specials.Clear();
            all.Clear();
            var patterns = GetComponents<BossPatternBase>();
            foreach (var p in patterns)
            {
                all.Add(p);
                if (p.IsSpecial) specials.Add(p);
                else basics.Add(p);
            }
        }

        private void OnEnable()
        {
            aggroed = false;
            loop = StartCoroutine(DriveLoop());
        }

        private void OnDisable()
        {
            if (loop != null) StopCoroutine(loop);
            loop = null;
        }

        private IEnumerator DriveLoop()
        {
            var waitDecision = new WaitForSeconds(decisionInterval);

            while (true)
            {
                if (controller == null || controller.IsDead)
                {
                    yield return waitDecision;
                    continue;
                }

                // 디버그 수동 모드: 자동 선택 중지(추적만 유지). 발동은 Update의 숫자키가 담당.
                if (debugManualMode)
                {
                    yield return waitDecision;
                    continue;
                }

                // 인식범위 진입 전이면 추적만 하며 대기 (MonsterController가 Tracking에서 자동 추적).
                // 한 번 진입하면 어그로 유지(보스룸 특성상 퇴장 불가).
                if (!aggroed)
                {
                    if (controller.GetDistanceToPlayer() > detectionRange)
                    {
                        yield return waitDecision;
                        continue;
                    }
                    aggroed = true;
                }

                // 기본/특수 그룹 선택 (기획서 80/20)
                bool special = Random.value > basicChance;
                BossPatternBase chosen = PickReady(special ? specials : basics);

                // 선택된 그룹에 준비된 패턴이 없으면 반대 그룹으로 폴백
                if (chosen == null)
                {
                    special = !special;
                    chosen = PickReady(special ? specials : basics);
                }

                // 둘 다 준비 안 됨 → 추적 유지하며 재시도
                if (chosen == null)
                {
                    yield return waitDecision;
                    continue;
                }

                // 패턴 실행: 추적 정지 → 실행 → 추적 재개 → 그룹 간격 대기
                executing = true;
                controller.PauseMovement();
                yield return chosen.Execute();
                executing = false;

                if (controller == null || controller.IsDead) yield break;
                controller.ResumeMovement();

                yield return new WaitForSeconds(chosen.IsSpecial ? specialGap : basicGap);
            }
        }

        // ==========================================
        // 디버그: 숫자키로 패턴 강제 발동
        // ==========================================
        private void Update()
        {
            if (!debugManualMode || executing) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            int n = Mathf.Min(all.Count, 9);
            for (int i = 0; i < n; i++)
            {
                if (DigitPressed(kb, i))
                {
                    StartCoroutine(RunForced(all[i]));
                    break;
                }
            }
        }

        /// <summary>디버그 강제 발동: 쿨다운·확률·사거리 무시하고 즉시 실행.</summary>
        private IEnumerator RunForced(BossPatternBase p)
        {
            if (p == null || controller == null || controller.IsDead) yield break;

            executing = true;
            controller.PauseMovement();
            yield return p.Execute();
            executing = false;

            if (controller != null && !controller.IsDead) controller.ResumeMovement();
        }

        /// <summary>i번째 숫자키(0→Digit1 … 8→Digit9)가 이번 프레임에 눌렸는지.</summary>
        private static bool DigitPressed(Keyboard kb, int i)
        {
            switch (i)
            {
                case 0: return kb.digit1Key.wasPressedThisFrame;
                case 1: return kb.digit2Key.wasPressedThisFrame;
                case 2: return kb.digit3Key.wasPressedThisFrame;
                case 3: return kb.digit4Key.wasPressedThisFrame;
                case 4: return kb.digit5Key.wasPressedThisFrame;
                case 5: return kb.digit6Key.wasPressedThisFrame;
                case 6: return kb.digit7Key.wasPressedThisFrame;
                case 7: return kb.digit8Key.wasPressedThisFrame;
                case 8: return kb.digit9Key.wasPressedThisFrame;
                default: return false;
            }
        }

        private void OnGUI()
        {
            if (!debugManualMode || !debugShowLegend) return;

            int n = Mathf.Min(all.Count, 9);
            GUILayout.BeginArea(new Rect(10, 10, 320, 24 + n * 20 + (executing ? 22 : 0)));
            GUILayout.Label("[디버그] 숫자키로 패턴 발동");
            for (int i = 0; i < n; i++)
                GUILayout.Label($"  {i + 1} : {all[i].patternName}{(all[i].IsSpecial ? " (특수)" : "")}");
            if (executing) GUILayout.Label("  ▶ 실행 중...");
            GUILayout.EndArea();
        }

        /// <summary>그룹에서 실행 가능한 패턴을 확률 가중치로 하나 뽑습니다. 없으면 null.</summary>
        private BossPatternBase PickReady(List<BossPatternBase> group)
        {
            if (group == null || group.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < group.Count; i++)
                if (group[i].CanExecute()) total += Mathf.Max(0f, group[i].Chance);

            if (total <= 0f) return null;

            float roll = Random.value * total;
            for (int i = 0; i < group.Count; i++)
            {
                if (!group[i].CanExecute()) continue;
                roll -= Mathf.Max(0f, group[i].Chance);
                if (roll <= 0f) return group[i];
            }
            return null;
        }
    }
}
