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
        public float ring1Radius = 4.5f;
        [Tooltip("2단 바깥 반경")]
        public float ring2Radius = 7f;
        [Tooltip("3단 바깥 반경")]
        public float ring3Radius = 11.5f;

        [Tooltip("각 동심원 폭발 전 경고 바닥 표시 시간(초, Hit_Delay). 단계 간 간격도 됨")]
        public float ringWarningTime = 0.5f;

        [Tooltip("2페이즈: 1차 폭발 후 2차 폭발까지 딜레이(초)")]
        public float phase2SecondWaveDelay = 0.7f;

        [Tooltip("2페이즈 2차 폭발: 1·2·3단 사이 간격(초). 짧을수록 한번에 퍼버벙")]
        public float phase2SecondWaveRingDelay = 0.2f;

        [Header("연출 프리팹(선택)")]
        [Tooltip("착지 지점 예고 표식(낙하 지점)")]
        public GameObject telegraphPrefab;

        [Tooltip("1단(중심 원) 경고 바닥 표식")]
        public GameObject ringWarningPrefab;

        [Tooltip("2·3단(바깥 링) 경고 바닥 표식(이미 링 모양이라 마스크 불필요). 비우면 ringWarningPrefab 사용")]
        public GameObject ringWarningPrefabOuter;

        [Tooltip("단계별 충격파(폭발) 이펙트")]
        public GameObject ringEffectPrefab;

        [Tooltip("2단+ 동심원에서 안쪽(이미 터진) 범위를 가리는 SpriteMask 프리팹 → 밴드(도넛)만 표시")]
        public GameObject ringMaskPrefab;

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

            controller.BeginExternalMovement();
            controller.SetKnockbackImmune(true);

            // 1) 점프 애니(Execute의 Leap) 동안 체공
            if (hoverTime > 0f) yield return new WaitForSeconds(hoverTime);

            // 2) 보스 완전히 사라짐 + 착지 범위(첫 동심원 크기) 예고 표식 표시
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            GameObject tele = SpawnScaled(telegraphPrefab, landing, ring1Radius);

            // 3) 범위를 보여주는 대기(낙하 예고)
            if (jumpDuration > 0f) yield return new WaitForSeconds(jumpDuration);

            // 4) 착지 지점에 재등장 + 착지(내려찍기) 애니 → 첫 범위에 맞춰 떨어짐
            transform.position = landing;
            if (sr != null) sr.enabled = true;
            if (bossAnimator != null) bossAnimator.PlayPattern("LeapLand");
            ReturnPooled(tele);

            controller.SetKnockbackImmune(false);
            controller.EndExternalMovement();

            // 착지 → 3단 동심원 (중심부터 바깥으로). 각 단은 '경고 바닥 → 폭발' 순서(0.5초 간격).
            ringsActive = true;
            GameObject outerWarn = ringWarningPrefabOuter != null ? ringWarningPrefabOuter : ringWarningPrefab;
            yield return BlastRing(landing, 0f, ring1Radius, ringWarningTime, ringWarningPrefab);   // 1단: 보스바닥경고
            yield return BlastRing(landing, ring1Radius, ring2Radius, ringWarningTime, outerWarn);  // 2단: 테두리
            yield return BlastRing(landing, ring2Radius, ring3Radius, ringWarningTime, outerWarn);  // 3단: 테두리

            // 2페이즈 강화: 0.7초 후 2차 폭발 — 1·2·3단을 빠르게(0.2초 간격) 연속 "퍼버벙"
            if (phase2Mode)
            {
                yield return new WaitForSeconds(phase2SecondWaveDelay);
                // 2차 내려찍기: 착지 모션 한 번 더(바닥 찍는 느낌)
                if (bossAnimator != null) bossAnimator.PlayPattern("LeapLand", true);
                yield return BlastRing(landing, 0f, ring1Radius, phase2SecondWaveRingDelay, ringWarningPrefab);
                yield return BlastRing(landing, ring1Radius, ring2Radius, phase2SecondWaveRingDelay, outerWarn);
                yield return BlastRing(landing, ring2Radius, ring3Radius, phase2SecondWaveRingDelay, outerWarn);
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

        /// <summary>경고 바닥(outer 크기) 표시 → warningTime 후 폭발 + (inner,outer] 밴드 타격.</summary>
        private IEnumerator BlastRing(Vector3 center, float inner, float outer, float warningTime, GameObject warnPrefab)
        {
            // 폭발 전 경고 바닥(밴드 바깥 반경 크기로 스케일)
            GameObject warn = SpawnScaled(warnPrefab, center, outer);
            // 2단+ : 안쪽(이미 터진) 범위를 SpriteMask로 가려 도넛(밴드)만 보이게
            GameObject mask = (inner > 0f && ringMaskPrefab != null) ? SpawnScaled(ringMaskPrefab, center, inner) : null;
            if (warningTime > 0f) yield return new WaitForSeconds(warningTime);
            ReturnPooled(warn);
            ReturnPooled(mask);

            // 폭발 이펙트 + 타격
            SpawnScaled(ringEffectPrefab, center, outer);
            if (controller != null && controller.PlayerTransform != null)
            {
                float dist = Vector2.Distance(center, controller.PlayerTransform.position);
                if (dist > inner && dist <= outer)
                    TryHitPlayer(center, outer);
            }
        }

        /// <summary>풀에서 꺼내 '월드 지름 = 2*radius(m)'가 되도록 스케일. 스프라이트의 실제 크기(PPU 무관)로
        /// 나눠 맞추므로, 슬라이스/PPU가 어떻든 반지름이 유니티 월드 유닛 그대로 나온다.</summary>
        private GameObject SpawnScaled(GameObject prefab, Vector3 pos, float radius)
        {
            GameObject go = SpawnFromPool(prefab, pos, Quaternion.identity);
            if (go == null) return null;
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            float native = (sr != null && sr.sprite != null) ? sr.sprite.bounds.size.x : 1f; // 스프라이트 실제 폭(월드 유닛)
            go.transform.localScale = Vector3.one * (native > 0.0001f ? (radius * 2f) / native : radius * 2f);
            return go;
        }
    }
}
