// ============================================================
// Pattern_MeleeCombo.cs
// 기본 패턴 - 근접 3연타
//  기획서: Use_Range 2m, Telegraph 2.5s, Attack_Range 10m / 120°,
//          1타 후 0.4초 간격으로 2·3타, Cooldown 10s, Chance 30%
//  - 텔레그래프 후 플레이어 방향 120° 부채꼴로 3회 타격
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    public class Pattern_MeleeCombo : BossPatternBase
    {
        [Header("근접 3연타 설정")]
        [Tooltip("타격 횟수")]
        public int hitCount = 3;

        [Tooltip("타격 사이 간격(초)")]
        public float hitInterval = 0.4f;

        [Tooltip("타격 판정 사거리(m, Attack_Range)")]
        public float hitRange = 10f;

        [Tooltip("타격 판정 부채꼴 전체 각도(도)")]
        public float attackAngle = 120f;

        [Header("연출 프리팹(선택)")]
        [Tooltip("텔레그래프(예고) 표식 프리팹")]
        public GameObject telegraphPrefab;

        [Tooltip("타격 이펙트 프리팹")]
        public GameObject hitEffectPrefab;

        private void Reset()
        {
            patternName = "MeleeCombo";
            isSpecial = false;
            useRange = 2f;
            telegraphTime = 2.5f;
            cooldown = 10f;
            chance = 30f;
        }

        protected override IEnumerator ExecuteRoutine()
        {
            Vector2 dir = DirToPlayer();

            // 텔레그래프(예고) — 플레이어 방향으로 표시
            GameObject tele = ShowTelegraph(telegraphPrefab, transform.position, dir);
            yield return new WaitForSeconds(telegraphTime);
            ReturnPooled(tele);

            for (int i = 0; i < hitCount; i++)
            {
                // 각 타격 직전 방향 재조준(추적 옵션 없으면 첫 방향 유지해도 무방하나 근접은 약간 추적)
                dir = DirToPlayer();

                if (hitEffectPrefab != null)
                    SpawnFromPool(hitEffectPrefab, transform.position, Quaternion.identity);

                TryHitPlayer(transform.position, hitRange, attackAngle, dir);

                if (i < hitCount - 1)
                    yield return new WaitForSeconds(hitInterval);
            }
        }
    }
}
