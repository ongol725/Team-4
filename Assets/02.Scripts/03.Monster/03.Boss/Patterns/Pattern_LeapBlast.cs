// ============================================================
// Pattern_LeapBlast.cs
// 특수 패턴 - 점프 내려찍기
//  기획서: Use_Range 0~30m, Telegraph 2.5s, 360°, Hit_Delay 0.5s,
//          착지 후 거리대별 3단 동심원(1:0~5m, 2:5~10m, 3:10~15m), Cooldown 25s, Chance 6%
//  - 텔레그래프로 착지 지점(플레이어 위치) 예고 → 공중 이동 → 착지
//  - 착지 시 중심부터 바깥으로 3단 동심원 충격파(각 0.5s 간격). 해당 밴드에 있으면 피격.
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    public class Pattern_LeapBlast : BossPatternBase
    {
        [Header("점프 설정")]
        [Tooltip("공중 체공 시간(초). 이 동안 착지 지점으로 이동")]
        public float jumpDuration = 1.5f;

        [Header("동심원 충격파(밴드 바깥 반경, m)")]
        [Tooltip("1단 바깥 반경")]
        public float ring1Radius = 5f;
        [Tooltip("2단 바깥 반경")]
        public float ring2Radius = 10f;
        [Tooltip("3단 바깥 반경")]
        public float ring3Radius = 15f;

        [Tooltip("동심원 단계 사이 간격(초, Hit_Delay)")]
        public float ringInterval = 0.5f;

        [Header("연출 프리팹(선택)")]
        [Tooltip("착지 지점 예고 표식")]
        public GameObject telegraphPrefab;

        [Tooltip("단계별 충격파 이펙트")]
        public GameObject ringEffectPrefab;

        private void Reset()
        {
            patternName = "LeapBlast";
            isSpecial = true;
            useRange = 30f;
            telegraphTime = 2.5f;
            cooldown = 25f;
            chance = 6f;
        }

        protected override IEnumerator ExecuteRoutine()
        {
            // 착지 지점 = 텔레그래프 시점 플레이어 위치로 고정
            Vector3 landing = controller.PlayerTransform != null
                ? controller.PlayerTransform.position
                : transform.position + (Vector3)DirToPlayer() * 5f;

            GameObject tele = telegraphPrefab != null
                ? SpawnFromPool(telegraphPrefab, landing, Quaternion.identity) : null;
            yield return new WaitForSeconds(telegraphTime);

            // 공중 이동(착지 지점으로 보간). 이동을 직접 제어.
            controller.BeginExternalMovement();
            controller.SetKnockbackImmune(true);

            Vector3 start = transform.position;
            float t = 0f;
            while (t < jumpDuration)
            {
                if (controller == null || controller.IsDead) break;
                t += Time.deltaTime;
                float u = jumpDuration > 0f ? Mathf.Clamp01(t / jumpDuration) : 1f;
                transform.position = Vector3.Lerp(start, landing, u);
                yield return null;
            }
            transform.position = landing;

            controller.SetKnockbackImmune(false);
            controller.EndExternalMovement();
            ReturnPooled(tele);

            // 착지 → 3단 동심원 (중심부터 바깥으로)
            yield return BlastRing(landing, 0f, ring1Radius);
            yield return new WaitForSeconds(ringInterval);
            yield return BlastRing(landing, ring1Radius, ring2Radius);
            yield return new WaitForSeconds(ringInterval);
            yield return BlastRing(landing, ring2Radius, ring3Radius);
        }

        /// <summary>중심 center에서 (inner, outer] 밴드에 플레이어가 있으면 타격.</summary>
        private IEnumerator BlastRing(Vector3 center, float inner, float outer)
        {
            if (ringEffectPrefab != null)
                SpawnFromPool(ringEffectPrefab, center, Quaternion.identity);

            if (controller != null && controller.PlayerTransform != null)
            {
                float dist = Vector2.Distance(center, controller.PlayerTransform.position);
                if (dist > inner && dist <= outer)
                    TryHitPlayer(center, outer);
            }
            yield break;
        }
    }
}
