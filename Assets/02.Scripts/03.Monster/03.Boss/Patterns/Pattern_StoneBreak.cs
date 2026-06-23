// ============================================================
// Pattern_StoneBreak.cs
// 2페이즈 신규 특수 - 돌 파괴 기믹
//  기획서: Telegraph 1.5s, 돌 4개 생성(1개에 무력화 아이템), 보스가 플레이어 향해
//          1.5s 경로 표시 후 돌진을 최대 6회 반복(맵 끝까지). Cooldown 45s, Chance 10%
//          성공(아이템 돌 파괴) → 보스 5초 그로기(무력화)
//          실패(6회 내 미파괴) → 맵 전체 60% Max HP 광역 피해
//  - 돌은 보스가 '돌진하며 지나가면서' 부순다(멈추지 않고 관통). 플레이어는 아이템 돌
//    쪽으로 보스 돌진을 유도하는 식. (토템과 달리 플레이어가 직접 부수지 않음)
// ============================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace BagSurvivor.Monster
{
    public class Pattern_StoneBreak : BossPatternBase
    {
        [Header("돌 설정")]
        [Tooltip("생성할 돌 개수")]
        public int stoneCount = 4;

        [Tooltip("돌 배치 반경(맵 중앙 기준, m)")]
        public float stoneRadius = 6f;

        [Tooltip("맵 중앙(보스가 이동해 기믹을 펼치는 위치)")]
        public Vector3 mapCenter = Vector3.zero;

        [Header("돌진 설정")]
        [Tooltip("최대 돌진 횟수")]
        public int dashCount = 6;

        [Tooltip("매 돌진 전 경로 표시(텔레그래프) 시간(초)")]
        public float dashTelegraph = 1.5f;

        [Tooltip("돌진 속도(m/s)")]
        public float dashSpeed = 22f;

        [Tooltip("1회 돌진 거리(m)")]
        public float dashDistance = 12f;

        [Tooltip("돌진과 다음 돌진(경고선) 사이 휴식(초)")]
        public float dashRestTime = 1f;

        [Tooltip("돌진 중 플레이어 타격 반경(m)")]
        public float bodyHitRadius = 1.2f;

        [Tooltip("돌진 중 보스가 지나가며 돌을 부수는 반경(m)")]
        public float stoneBreakRadius = 1.5f;

        [Header("성공/실패")]
        [Tooltip("성공(아이템 획득) 시 보스 그로기(무력화) 시간(초)")]
        public float stunDuration = 5f;

        [Tooltip("실패 시 플레이어 Max HP 비율 피해. 0.6 = 60%")]
        [Range(0f, 1f)] public float failDamagePercent = 0.6f;

        [Header("프리팹")]
        [Tooltip("파괴 가능한 돌 프리팹 (HittableObject + PooledObject)")]
        public GameObject stonePrefab;

        [Tooltip("돌을 부쉈을 때 드롭되는 무력화 아이템 프리팹 (GimmickPickup + PooledObject)")]
        public GameObject itemPickupPrefab;

        [Tooltip("아이템 돌 디버그 표식(선택). 인게임에선 비워둠(돌 구분 불가가 정상)")]
        public GameObject itemMarkerPrefab;

        [Tooltip("돌진 경로 텔레그래프(선택)")]
        public GameObject dashTelegraphPrefab;

        private bool success;

        private void Reset()
        {
            patternName = "StoneBreak";
            isSpecial = true;
            phase2Only = true; // 2페이즈 전용
            useRange = 100f;
            telegraphTime = 1.5f;
            cooldown = 45f;
            chance = 10f;
        }

        protected override IEnumerator ExecuteRoutine()
        {
            success = false;
            controller.BeginExternalMovement();
            controller.SetKnockbackImmune(true);

            // 1) 맵 중앙으로 이동
            yield return MoveTo(mapCenter, 0.5f);

            // 2) 돌 4개 생성 (1개에 아이템)
            int itemIndex = Random.Range(0, Mathf.Max(1, stoneCount));
            var stones = new List<HittableObject>();
            GameObject marker = null;
            for (int i = 0; i < stoneCount; i++)
            {
                float ang = (360f / Mathf.Max(1, stoneCount)) * i * Mathf.Deg2Rad;
                Vector3 pos = mapCenter + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * stoneRadius;
                GameObject go = SpawnFromPool(stonePrefab, pos, Quaternion.identity);
                if (go == null) continue;
                var h = go.GetComponent<HittableObject>();
                if (h == null) continue;

                bool hasItem = (i == itemIndex);
                if (hasItem)
                {
                    // 아이템 돌이 부서지면 그 자리에 아이템 드롭(플레이어가 주워야 성공)
                    h.onDestroyed = hs => DropItem(hs.transform.position);
                    if (itemMarkerPrefab != null) // 디버그 표식(인게임에선 미사용)
                        marker = SpawnFromPool(itemMarkerPrefab, pos, Quaternion.identity);
                }
                stones.Add(h);
            }

            yield return new WaitForSeconds(telegraphTime);

            // 3) dashCount회 돌진: (경고선→돌진) 후 1초 휴식, 반복. 아이템 획득하면 즉시 중단.
            for (int d = 0; d < dashCount && !success; d++)
            {
                yield return DashOnce(stones);
                if (controller == null || controller.IsDead) break;
                if (success) break;
                if (d < dashCount - 1 && dashRestTime > 0f)
                    yield return WaitOrSuccess(dashRestTime); // 휴식 중 아이템 획득해도 즉시 종료
            }

            // 4) 남은 돌/표식 정리
            foreach (var s in stones)
                if (s != null && s.gameObject.activeSelf) s.ForceReturn();
            ReturnPooled(marker);

            controller.SetKnockbackImmune(false);
            controller.EndExternalMovement();

            // 5) 결과 처리
            if (success)
            {
                // 그로기(무력화): 제자리에서 일정 시간 정지(피해는 받는 상태)
                yield return new WaitForSeconds(stunDuration);
            }
            else
            {
                DealFailDamage();
            }
        }

        /// <summary>플레이어 방향으로 1.5초 경로 표시 후 직선 돌진. 지나가는 돌은 부수며 관통.
        /// 아이템 획득(success) 시 경고선/돌진 도중에도 즉시 중단.</summary>
        private IEnumerator DashOnce(List<HittableObject> stones)
        {
            Vector2 dir = DirToPlayer();
            GameObject tele = ShowTelegraph(dashTelegraphPrefab, transform.position, dir);
            yield return WaitOrSuccess(dashTelegraph);
            ReturnPooled(tele);
            if (success) yield break; // 경고선 도중 획득 → 돌진 생략

            float traveled = 0f;
            bool hitPlayer = false;
            while (traveled < dashDistance && !success) // 돌진 중 획득 시 즉시 정지
            {
                if (controller == null || controller.IsDead) break;
                controller.SetVelocity(dir * dashSpeed);

                if (!hitPlayer && TryHitPlayer(transform.position, bodyHitRadius)) hitPlayer = true;

                // 지나가며 돌 파괴 (멈추지 않음). 아이템 돌이면 onDestroyed에서 아이템 드롭.
                BreakStonesInRange(stones);

                traveled += dashSpeed * Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            controller.SetVelocity(Vector2.zero);
        }

        /// <summary>seconds 동안 대기하되 success가 되면 즉시 종료.</summary>
        private IEnumerator WaitOrSuccess(float seconds)
        {
            float t = 0f;
            while (t < seconds && !success)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>아이템 돌 파괴 위치에 무력화 아이템을 드롭. 플레이어가 주우면 success.</summary>
        private void DropItem(Vector3 pos)
        {
            if (itemPickupPrefab == null) { success = true; return; } // 픽업 프리팹 없으면 파괴 즉시 성공(폴백)
            GameObject go = SpawnFromPool(itemPickupPrefab, pos, Quaternion.identity);
            var pickup = go != null ? go.GetComponent<GimmickPickup>() : null;
            if (pickup != null) pickup.onCollected = () => success = true;
            else success = true; // 픽업 컴포넌트 없으면 폴백
        }

        /// <summary>보스 주변 stoneBreakRadius 안의 살아있는 돌을 부순다.</summary>
        private void BreakStonesInRange(List<HittableObject> stones)
        {
            Vector2 p = transform.position;
            for (int i = 0; i < stones.Count; i++)
            {
                var s = stones[i];
                if (s == null || !s.gameObject.activeSelf || !s.IsAlive) continue;
                if (Vector2.Distance(p, s.transform.position) <= stoneBreakRadius)
                    s.Hit(s.hitsToDestroy); // 즉시 파괴(보스 관통 파괴)
            }
        }

        private IEnumerator MoveTo(Vector3 dest, float duration)
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < duration)
            {
                if (controller == null || controller.IsDead) break;
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, dest, duration > 0f ? t / duration : 1f);
                yield return null;
            }
            transform.position = dest;
        }

        private void DealFailDamage()
        {
            if (controller == null || controller.PlayerTransform == null) return;
            var ph = controller.PlayerTransform.GetComponentInParent<PlayerHealth>();
            if (ph != null && !ph.IsDead)
                ph.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(ph.MaxHP * failDamagePercent)));
        }

        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmo()) return;
            GizmoCircle(mapCenter, stoneRadius, Color.yellow);
        }
    }
}
