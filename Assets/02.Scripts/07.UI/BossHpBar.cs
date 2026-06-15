// ============================================================
// BossHpBar.cs
// 보스(MiddleBoss/Boss) 전용 상단 고정 HP바
//  - 메인 게이지(Red): 데미지 시 즉시 감소 (현재 HP)
//  - 지연 게이지(Yellow): 0.2초 대기 후 0.4초에 "걸쳐" 메인을 따라잡음 (최근 피해량 시각화)
//  - MonsterController의 CurrentHP / monsterData.maxHP 를 읽어 표시
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

        [Header("지연 게이지 타이밍 (기획서: 0.2s 대기 후 0.4s에 걸쳐 추적)")]
        public float delayBeforeCatch = 0.2f;
        public float catchDuration = 0.4f;

        [Header("데모 (타겟 없이 연출 확인용)")]
        public bool demoMode = false;
        public float demoMaxHP = 1000f;
        public float demoCurHP = 1000f;
        public float demoInterval = 1.5f;   // n초마다 피해
        [Range(0f, 1f)] public float demoHitPercent = 0.15f;

        private MonsterController target;
        private int maxHP = 1;
        private float displayedDelayed = 1f; // 0~1
        private float delayTimer;
        private float catchTimer;
        private float catchFrom = 1f;
        private float lastCur = 1f;
        private float demoTimer;

        /// <summary>보스 몬스터 연결.</summary>
        public void SetTarget(MonsterController mc, string bossName)
        {
            target = mc;
            maxHP = (mc != null && mc.monsterData != null) ? mc.monsterData.maxHP : 1;
            if (nameText != null) nameText.text = bossName;

            float n = CurrentNormalized();
            lastCur = n;
            displayedDelayed = n;
            catchFrom = n;
            SetMain(n);
            SetDelayed(n);
            RefreshHpText();
        }

        private void Update()
        {
            // 데모: 타겟 없을 때 주기적으로 피해를 줘 연출 확인
            if (target == null && demoMode)
            {
                demoTimer += Time.deltaTime;
                if (demoTimer >= demoInterval)
                {
                    demoTimer = 0f;
                    demoCurHP -= demoMaxHP * demoHitPercent;
                    if (demoCurHP <= 0f) demoCurHP = demoMaxHP; // 루프
                }
            }

            float cur = CurrentNormalized();

            // 메인: 즉시 반영
            SetMain(cur);
            RefreshHpText();

            // 새 피해 감지 -> 지연 타이머/추적 리셋 (현재 지연값에서 다시 출발)
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
                    // 대기 구간: 아직 안 움직임 (잔상 유지)
                    delayTimer += Time.deltaTime;
                    catchFrom = displayedDelayed;
                }
                else
                {
                    // 추적 구간: catchFrom -> cur 를 catchDuration 에 "걸쳐" 보간
                    catchTimer += Time.deltaTime;
                    float t = catchDuration <= 0f ? 1f : Mathf.Clamp01(catchTimer / catchDuration);
                    displayedDelayed = Mathf.Lerp(catchFrom, cur, t);
                    SetDelayed(displayedDelayed);
                }
            }
            else if (displayedDelayed < cur)
            {
                // 회복 시 즉시 맞춤
                displayedDelayed = cur;
                SetDelayed(cur);
            }
        }

        private float CurrentNormalized()
        {
            if (target != null && target.monsterData != null)
                return Mathf.Clamp01((float)target.CurrentHP / Mathf.Max(1, target.monsterData.maxHP));
            if (demoMode)
                return Mathf.Clamp01(demoCurHP / Mathf.Max(1f, demoMaxHP));
            return 1f;
        }

        private void RefreshHpText()
        {
            if (hpText == null) return;
            if (target != null && target.monsterData != null)
                hpText.text = target.CurrentHP + " / " + target.monsterData.maxHP;
            else if (demoMode)
                hpText.text = Mathf.CeilToInt(demoCurHP) + " / " + Mathf.CeilToInt(demoMaxHP);
        }

        private void SetMain(float v) { if (mainFill != null) mainFill.fillAmount = v; }
        private void SetDelayed(float v) { if (delayedFill != null) delayedFill.fillAmount = v; }
    }
}