// ============================================================
// PhantomWolfDash.cs
// 환영 늑대/분신용 돌진 동반 컴포넌트
//  - MonsterPool로 소환된 MonsterController 위에 부착
//  - Dash() 호출 시: 늑대는 소환 자리에 '가만히' 있고(위치 이동 X), 돌진 방향 예고만
//    플레이어를 계속 추적 → 돌진 직전 추적을 멈춰 방향 확정 → 돌진 → 풀 반환.
//  - 소환 0.5초 간격 + 각자 startDelay 후 돌진 → 돌진도 0.5초씩 어긋남.
//  - 돌진 중 접촉 피해는 MonsterController의 기존 접촉 데미지 로직이 처리.
//  - PhantomDash 패턴과 2페이즈 분신 패턴이 공용으로 사용.
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class PhantomWolfDash : MonoBehaviour
    {
        [Header("돌진 설정")]
        [Tooltip("돌진 속도(m/s)")]
        public float dashSpeed = 16f;

        [Tooltip("돌진 거리(m, 기획서 Attack_Range 20m)")]
        public float dashDistance = 20f;

        [Tooltip("소환 후 돌진 시작까지 총 대기(초). 이 중 앞부분은 방향 추적, 끝의 lockLeadTime 동안 방향 확정")]
        public float startDelay = 2f;

        [Tooltip("돌진 직전 방향 추적을 멈추고 확정 방향을 고정 표시하는 시간(초). 추적 시간 = startDelay - 이 값")]
        public float lockLeadTime = 0.4f;

        [Header("연출 프리팹(선택)")]
        [Tooltip("돌진 방향 예고(텔레그래프) 프리팹")]
        public GameObject dirTelegraphPrefab;

        [Tooltip("텔레그래프(바닥 경고 스트립)를 돌진 거리만큼 늘일지 여부. 멧돼지 소환처럼 바닥을 깔 때 true")]
        [System.NonSerialized] public bool stretchTelegraphToDash = false;

        private MonsterController controller;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
        }

        /// <summary>돌진을 시작합니다(소환 직후 호출). 추적 → 직전 정지/예고 → 돌진 → 풀 반환.
        /// fallbackDir는 플레이어를 못 찾을 때만 사용.</summary>
        public void Dash(Vector2 fallbackDir)
        {
            StartCoroutine(DashRoutine(fallbackDir.sqrMagnitude < 0.0001f ? Vector2.right : fallbackDir.normalized));
        }

        private IEnumerator DashRoutine(Vector2 fallbackDir)
        {
            // 소환 직후부터 제자리 정지(위치 이동 X). 방향 예고만 플레이어를 추적.
            controller.BeginExternalMovement();
            controller.SetKnockbackImmune(true);
            controller.SetVelocity(Vector2.zero);

            Vector2 dir = controller.GetDirectionToPlayer();
            if (dir.sqrMagnitude < 0.0001f) dir = fallbackDir;
            GameObject tele = ShowTelegraph(dir);

            // 1) 조준 단계: 돌진 직전까지 예고 방향이 플레이어를 계속 추적
            // 총 대기는 항상 startDelay와 일치(조준 + 확정고정). startDelay가 짧으면 확정구간도 줄어듦.
            float lockWait = Mathf.Min(lockLeadTime, startDelay);
            float aimTime = Mathf.Max(0f, startDelay - lockWait);
            float t = 0f;
            while (t < aimTime)
            {
                if (controller == null || controller.IsDead) break;
                Vector2 d = controller.GetDirectionToPlayer();
                if (d.sqrMagnitude > 0.0001f) dir = d;
                AimTelegraph(tele, dir);
                t += Time.deltaTime;
                yield return null;
            }

            // 2) 추적 정지 → 돌진 방향 확정(마지막 플레이어 방향으로 고정)
            Vector2 locked = controller != null ? controller.GetDirectionToPlayer() : Vector2.zero;
            if (locked.sqrMagnitude > 0.0001f) dir = locked;
            AimTelegraph(tele, dir);

            // 3) 확정 방향을 짧게 고정 표시 후 돌진
            if (lockWait > 0f) yield return new WaitForSeconds(lockWait);
            ReturnTelegraph(tele);

            // 4) 돌진
            float traveled = 0f;
            while (traveled < dashDistance)
            {
                if (controller == null || controller.IsDead) break;
                controller.SetVelocity(dir * dashSpeed);
                traveled += dashSpeed * Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            controller.SetVelocity(Vector2.zero);
            controller.SetKnockbackImmune(false);
            controller.EndExternalMovement();

            ReturnToPool();
        }

        private GameObject ShowTelegraph(Vector2 dir)
        {
            if (dirTelegraphPrefab == null) return null;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.Euler(0f, 0f, ang);
            GameObject go = GameObjectPool.Instance != null
                ? GameObjectPool.Instance.Get(dirTelegraphPrefab, transform.position, rot)
                : Instantiate(dirTelegraphPrefab, transform.position, rot);

            // 바닥 경고 스트립(Tiled/Sliced 스프라이트)이면 돌진 거리만큼 길이를 늘려 바닥을 깐다.
            // (BoarGimmick.ShowChargeLine과 동일 방식. 화살표 등 Simple 스프라이트는 건드리지 않음)
            if (go != null)
            {
                var sr = go.GetComponentInChildren<SpriteRenderer>();
                if (stretchTelegraphToDash && sr != null && sr.drawMode != SpriteDrawMode.Simple)
                {
                    sr.size = new Vector2(dashDistance, sr.size.y);
                    sr.transform.localPosition = new Vector3(dashDistance * 0.5f, 0f, 0f);
                }
            }
            return go;
        }

        /// <summary>예고 표식을 늑대 위치에 두고 dir 방향으로 회전 갱신(조준 추적용).</summary>
        private void AimTelegraph(GameObject tele, Vector2 dir)
        {
            if (tele == null) return;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            tele.transform.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, 0f, ang));
        }

        private void ReturnTelegraph(GameObject go)
        {
            if (go == null) return;
            PooledObject po = go.GetComponent<PooledObject>();
            if (po != null) po.ReturnToPool();
            else go.SetActive(false);
        }

        private void ReturnToPool()
        {
            if (MonsterPool.Instance != null)
                MonsterPool.Instance.Return(controller);
            else
                gameObject.SetActive(false);
        }
    }
}
