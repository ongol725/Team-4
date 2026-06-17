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
        [Tooltip("도약 후 공중 체공 시간(초). 기획서: 도약 대기 1초")]
        public float hoverTime = 1f;

        [Tooltip("낙하(착지 지점으로 이동) 시간(초). 기획서: 낙하까지 1.5초")]
        public float jumpDuration = 1.5f;

        [Header("동심원 충격파(밴드 바깥 반경, m)")]
        [Tooltip("1단 바깥 반경")]
        public float ring1Radius = 5f;
        [Tooltip("2단 바깥 반경")]
        public float ring2Radius = 10f;
        [Tooltip("3단 바깥 반경")]
        public float ring3Radius = 15f;

        [Tooltip("각 동심원 폭발 전 경고 바닥 표시 시간(초, Hit_Delay). 단계 간 간격도 됨")]
        public float ringWarningTime = 0.5f;

        [Tooltip("2페이즈: 1차 폭발 후 2차 폭발까지 딜레이(초)")]
        public float phase2SecondWaveDelay = 0.7f;

        [Header("연출 프리팹(선택)")]
        [Tooltip("착지 지점 예고 표식(낙하 지점)")]
        public GameObject telegraphPrefab;

        [Tooltip("각 동심원 폭발 전 경고 바닥 표식")]
        public GameObject ringWarningPrefab;

        [Tooltip("단계별 충격파(폭발) 이펙트")]
        public GameObject ringEffectPrefab;

        private void Reset()
        {
            patternName = "LeapBlast";
            isSpecial = true;
            useRange = 30f;
            telegraphTime = 0f; // 전조는 hoverTime+jumpDuration(총 2.5초)로 표현
            cooldown = 25f;
            chance = 6f;
        }

        // 기즈모용 런타임 착지 지점/활성 플래그
        private Vector3 lastLanding;
        private bool ringsActive;

        protected override IEnumerator ExecuteRoutine()
        {
            // 착지 지점 = 텔레그래프 시점 플레이어 위치로 고정
            Vector3 landing = controller.PlayerTransform != null
                ? controller.PlayerTransform.position
                : transform.position + (Vector3)DirToPlayer() * 5f;
            lastLanding = landing;

            // 착지 지점 예고 표식을 도약 시작 시 띄우고 낙하까지 유지
            GameObject tele = telegraphPrefab != null
                ? SpawnFromPool(telegraphPrefab, landing, Quaternion.identity) : null;

            // 도약 후 공중 체공(기획서: 1초)
            controller.BeginExternalMovement();
            controller.SetKnockbackImmune(true);
            if (hoverTime > 0f) yield return new WaitForSeconds(hoverTime);

            // 낙하: 착지 지점으로 보간 이동(기획서: 1.5초)
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

            // 착지 → 3단 동심원 (중심부터 바깥으로). 각 단은 '경고 바닥 → 폭발' 순서.
            ringsActive = true;
            yield return BlastRing(landing, 0f, ring1Radius);
            yield return BlastRing(landing, ring1Radius, ring2Radius);
            yield return BlastRing(landing, ring2Radius, ring3Radius);

            // 2페이즈 강화: 0.7초 후 같은 순서로 2차 폭발
            if (phase2Mode)
            {
                yield return new WaitForSeconds(phase2SecondWaveDelay);
                yield return BlastRing(landing, 0f, ring1Radius);
                yield return BlastRing(landing, ring1Radius, ring2Radius);
                yield return BlastRing(landing, ring2Radius, ring3Radius);
            }

            ringsActive = false;
        }

        // 1단(노랑)/2단(주황)/3단(빨강) 동심원. 플레이 중에는 실제 착지점, 에디터에선 보스 기준 미리보기.
        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmo()) return;
            Vector3 c = ringsActive ? lastLanding : transform.position;
            GizmoCircle(c, ring1Radius, Color.yellow);
            GizmoCircle(c, ring2Radius, new Color(1f, 0.5f, 0f));
            GizmoCircle(c, ring3Radius, Color.red);
        }

        /// <summary>경고 바닥(outer 크기) 표시 → ringWarningTime 후 폭발 + (inner,outer] 밴드 타격.</summary>
        private IEnumerator BlastRing(Vector3 center, float inner, float outer)
        {
            // 폭발 전 경고 바닥(밴드 바깥 반경 크기로 스케일)
            GameObject warn = SpawnScaled(ringWarningPrefab, center, outer);
            if (ringWarningTime > 0f) yield return new WaitForSeconds(ringWarningTime);
            ReturnPooled(warn);

            // 폭발 이펙트 + 타격
            SpawnScaled(ringEffectPrefab, center, outer);
            if (controller != null && controller.PlayerTransform != null)
            {
                float dist = Vector2.Distance(center, controller.PlayerTransform.position);
                if (dist > inner && dist <= outer)
                    TryHitPlayer(center, outer);
            }
        }

        /// <summary>풀에서 꺼내 지름=2*radius로 스케일해 배치(더미 1유닛 스프라이트가 범위를 덮도록).</summary>
        private GameObject SpawnScaled(GameObject prefab, Vector3 pos, float radius)
        {
            GameObject go = SpawnFromPool(prefab, pos, Quaternion.identity);
            if (go != null) go.transform.localScale = Vector3.one * (radius * 2f);
            return go;
        }
    }
}
