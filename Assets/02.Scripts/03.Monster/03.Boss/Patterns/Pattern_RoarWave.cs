// ============================================================
// Pattern_RoarWave.cs
// 특수 패턴 - 포효 충격파
//  기획서: Use_Range 15m, Telegraph 1.5s, Hit_Delay 0.5s, Attack_Range 30m,
//          중심 안전지대 5m, Cooldown 15s, Chance 7%
//  - 텔레그래프 후, 보스 중심 안전지대(5m) 바깥 ~ 최대 30m 사이에 있는 플레이어 타격.
//  - 즉, 보스에 바짝 붙으면(5m 이내) 회피 가능.
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    public class Pattern_RoarWave : BossPatternBase
    {
        [Header("포효 충격파 설정")]
        [Tooltip("중심 안전지대 반경(m). 이 안에 있으면 피하지 않음")]
        public float safeRadius = 5f;

        [Tooltip("충격파 최대 반경(m, Attack_Range)")]
        public float maxRadius = 30f;

        [Tooltip("텔레그래프 후 충격파 타격까지 지연(초, Hit_Delay)")]
        public float hitDelay = 0.5f;

        [Header("연출 프리팹(선택)")]
        [Tooltip("텔레그래프 표식")]
        public GameObject telegraphPrefab;

        [Tooltip("충격파 이펙트(스폰 후 자체 수명으로 반환)")]
        public GameObject waveEffectPrefab;

        private void Reset()
        {
            patternName = "RoarWave";
            isSpecial = true;
            useRange = 15f;
            telegraphTime = 1.5f;
            cooldown = 15f;
            chance = 7f;
        }

        protected override IEnumerator ExecuteRoutine()
        {
            GameObject tele = telegraphPrefab != null
                ? SpawnFromPool(telegraphPrefab, transform.position, Quaternion.identity) : null;
            yield return new WaitForSeconds(telegraphTime);
            ReturnPooled(tele);

            // 충격파 발생
            if (waveEffectPrefab != null)
                SpawnFromPool(waveEffectPrefab, transform.position, Quaternion.identity);

            yield return new WaitForSeconds(hitDelay);

            // 안전지대(5m) 바깥 ~ 최대 반경 사이에 있으면 타격
            float dist = controller.GetDistanceToPlayer();
            if (dist > safeRadius && dist <= maxRadius)
                TryHitPlayer(transform.position, maxRadius); // maxRadius 안은 이미 확인됨
        }
    }
}
