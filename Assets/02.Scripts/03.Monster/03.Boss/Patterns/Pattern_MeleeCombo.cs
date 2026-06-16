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

        [Tooltip("1타→2타 사이 간격(초). 기획서상 2타→3타는 0초(연속)")]
        public float firstHitInterval = 0.4f;

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

        private Vector2 lockedDir = Vector2.right; // 시전 시점 고정 방향(기즈모용)

        protected override IEnumerator ExecuteRoutine()
        {
            Vector2 dir = DirToPlayer();
            lockedDir = dir;

            // 텔레그래프(예고) — 플레이어 방향으로 표시
            GameObject tele = ShowTelegraph(telegraphPrefab, transform.position, dir);
            yield return new WaitForSeconds(telegraphTime);
            ReturnPooled(tele);

            // 방향은 시전(텔레그래프) 시점에 고정. 이후 타격마다 재조준하지 않음(Tracking X).
            for (int i = 0; i < hitCount; i++)
            {
                if (hitEffectPrefab != null)
                    SpawnFromPool(hitEffectPrefab, transform.position, Quaternion.identity);

                TryHitPlayer(transform.position, hitRange, attackAngle, dir);

                // 기획서: 1타→2타 사이에만 딜레이(0.4초), 2타→3타는 0초(연속)
                if (i == 0 && hitCount > 1)
                    yield return new WaitForSeconds(firstHitInterval);
            }
        }

        // 빨강=타격 부채꼴(사거리/각도, 플레이어 방향 기준·에디터선 오른쪽 미리보기) / 회색=발동 사거리
        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmo()) return;
            Vector2 dir = Application.isPlaying ? lockedDir : Vector2.right; // 실행 중엔 고정 방향
            GizmoCone(transform.position, dir, hitRange, attackAngle, Color.red);
            GizmoCircle(transform.position, useRange, Color.gray);
        }
    }
}
