// ============================================================
// BossHpBar.cs
// 보스(MiddleBoss/Boss) 전용 상단 고정 HP바
//  - 메인 게이지(Red): 데미지 시 즉시 감소 (현재 HP)
//  - 지연 게이지(Yellow): delayBeforeCatch초 대기 후 catchDuration초에 걸쳐 추적
//  - HP 텍스트: 현재/최대 정수 표시
//  - HP 증가(회복): regenInterval초마다 regenAmount씩 상승 (인스펙터 조절)
//  - 실제 보스 연결 시 그 보스 HP를, 미연결 시 독립 표시값(standalone)을 사용
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using BagSurvivor.Monster;

namespace BagSurvivor.UI
{
    public class BossHpBar : MonoBehaviour
    {
        [Header("게이지 (Filled Horizontal)")]
        public Image mainFill;     // 빨강 - 즉시
        public Image delayedFill;  // 노랑 - 지연

        [Header("텍스트")]
        public Text nameText;
        public Text hpText;

        [Header("지연 게이지 타이밍")]
        public float delayBeforeCatch = 0.2f;
        public float catchDuration = 0.4f;

        [Header("최대 체력 증가")]
        [Tooltip("이 간격(초)마다 최대 체력이 오릅니다.")]
        public float growInterval = 5f;
        [Tooltip("한 번에 오르는 최대 체력 수치.")]
        public int growAmount = 1;

        [Header("보스 미연결 시 표시용 기본 체력")]
        public int standaloneMaxHP = 1000;
        public int standaloneCurHP = 1000;

        private MonsterController target;
        private float displayedDelayed = 1f; // 0~1
        private float delayTimer;
        private float catchTimer;
        private float catchFrom = 1f;
        private float lastCur = 1f;
        private float growTimer;

        /// <summary>보스 몬스터 연결.</summary>
        public void SetTarget(MonsterController mc, string bossName)
        {
            target = mc;
            if (nameText != null) nameText.text = bossName;

            float n = CurrentNormalized();
            lastCur = n;
            displayedDelayed = n;
            catchFrom = n;
            growTimer = 0f;
            SetMain(n);
            SetDelayed(n);
            RefreshHpText();
        }

        private void Update()
        {
            // 최대 체력 증가: growInterval초마다 growAmount씩 최대 체력 상승(현재 체력도 같이 증가)
            if (growInterval > 0f && growAmount != 0)
            {
                growTimer += Time.deltaTime;
                while (growTimer >= growInterval)
                {
                    growTimer -= growInterval;
                    if (target != null && !target.IsDead)
                    {
                        target.IncreaseMaxHP(growAmount);
                    }
                    else
                    {
                        standaloneMaxHP = Mathf.Max(1, standaloneMaxHP + growAmount);
                        standaloneCurHP = Mathf.Clamp(standaloneCurHP + growAmount, 0, standaloneMaxHP);
                    }
                }
            }

            float cur = CurrentNormalized();

            // 메인: 즉시 반영
            SetMain(cur);
            RefreshHpText();

            // 새 피해 감지 -> 지연 타이머/추적 리셋
            if (cur < lastCur - 0.0001f)
            {
                delayTimer = 0f;
                catchTimer = 0f;
                catchFrom = displayedDelayed;
            }
            lastCur = cur;

            // 지연 게이지 추적
            if (displayedDelayed > cur + 0.0001f)
            {
                if (delayTimer < delayBeforeCatch)
                {
                    delayTimer += Time.deltaTime;
                    catchFrom = displayedDelayed;
                }
                else
                {
                    catchTimer += Time.deltaTime;
                    float t = catchDuration <= 0f ? 1f : Mathf.Clamp01(catchTimer / catchDuration);
                    displayedDelayed = Mathf.Lerp(catchFrom, cur, t);
                    SetDelayed(displayedDelayed);
                }
            }
            else if (displayedDelayed < cur)
            {
                // 회복(상승) 시 즉시 맞춤
                displayedDelayed = cur;
                SetDelayed(cur);
            }

            // 보스 미연결(던전)일 때만: 오른 최대 체력을 보스룸으로 이월 기록
            if (target == null) BossHpCarry.Set(standaloneMaxHP);
        }

        // 현재 HP(정수)와 최대 HP(정수)를 반환 (연결된 보스 우선, 없으면 standalone)
        private int CurHP() { return target != null ? target.CurrentHP : standaloneCurHP; }
        private int MaxHP() { return target != null ? Mathf.Max(1, target.MaxHP) : Mathf.Max(1, standaloneMaxHP); }

        private float CurrentNormalized()
        {
            return Mathf.Clamp01((float)CurHP() / MaxHP());
        }

        private void RefreshHpText()
        {
            if (hpText == null) return;
            hpText.text = CurHP() + " / " + MaxHP();
        }

        private void SetMain(float v) { if (mainFill != null) mainFill.fillAmount = v; }
        private void SetDelayed(float v) { if (delayedFill != null) delayedFill.fillAmount = v; }
    }
}
