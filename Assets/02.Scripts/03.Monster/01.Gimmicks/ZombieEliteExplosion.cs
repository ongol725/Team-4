// ============================================================
// ZombieEliteExplosion.cs
// 좀비 엘리트(3층 Elite 방) 사망 폭발 장판.
//  - 활성화 시: 반경(radius) 경고 바닥 표시 → chargeTime(5초) 충전(점점 강조) →
//    폭발 순간 플레이어가 반경 안에 있으면 MaxHP의 dotPercent(50%)를 dotDuration(5초) 동안 도트.
//  - 폭발 순간 범위를 벗어나 있으면 회피(도트 시작 안 함).
//  - GameObjectPool로 스폰/반환. 스프라이트는 '지름 1 월드유닛' 원형(스케일로 반경 표현).
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    public class ZombieEliteExplosion : MonoBehaviour
    {
        [Header("폭발 설정")]
        [Tooltip("폭발 반경(m)")]
        public float radius = 10f;

        [Tooltip("경고→폭발 충전 시간(초)")]
        public float chargeTime = 5f;

        [Header("도트(폭발 명중 시)")]
        [Tooltip("플레이어 최대 체력 대비 총 피해 비율 (0.5 = 50%)")]
        public float dotPercent = 0.5f;

        [Tooltip("도트 지속 시간(초)")]
        public float dotDuration = 5f;

        [Tooltip("도트 틱 간격(초)")]
        public float dotTickInterval = 0.5f;

        [Header("경고 표시")]
        public SpriteRenderer warningRenderer;
        public Color warningColor = new Color(0.8f, 0.1f, 0.1f, 0.25f);
        public Color explodeColor = new Color(1f, 0.3f, 0.1f, 0.7f);

        private PooledObject pooled;

        private void Awake()
        {
            pooled = GetComponent<PooledObject>();
            if (warningRenderer == null) warningRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            // 반경 = 스케일 (스프라이트 지름 1유닛 기준 → 지름 2*radius)
            transform.localScale = Vector3.one * (radius * 2f);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // 1) 충전: 경고색에서 점점 강조(알파 상승 + 깜빡임 가속)
            float t = 0f;
            while (t < chargeTime)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / chargeTime);
                if (warningRenderer != null)
                {
                    // 막판일수록 빠르게 점멸
                    float blink = 0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(4f, 30f, p));
                    Color c = Color.Lerp(warningColor, explodeColor, p);
                    c.a *= blink;
                    warningRenderer.color = c;
                }
                yield return null;
            }

            // 2) 폭발 플래시
            if (warningRenderer != null) warningRenderer.color = explodeColor;

            // 3) 폭발 순간 판정: 플레이어가 반경 안이면 도트 시작
            PlayerHealth ph = FindPlayerHealthInRadius();
            yield return new WaitForSeconds(0.15f); // 짧은 플래시 유지

            if (ph != null && !ph.IsDead)
                yield return ApplyDot(ph);

            if (pooled != null) pooled.ReturnToPool();
            else gameObject.SetActive(false);
        }

        private IEnumerator ApplyDot(PlayerHealth ph)
        {
            int ticks = Mathf.Max(1, Mathf.RoundToInt(dotDuration / Mathf.Max(0.05f, dotTickInterval)));
            int total = Mathf.Max(ticks, Mathf.RoundToInt(ph.MaxHP * dotPercent)); // 폭발 순간 MaxHP 기준
            int perTick = Mathf.Max(1, total / ticks);

            if (warningRenderer != null) warningRenderer.enabled = false; // 폭발 후 장판 숨김
            for (int i = 0; i < ticks; i++)
            {
                if (ph == null || ph.IsDead) yield break;
                ph.TakeDamage(perTick);
                yield return new WaitForSeconds(dotTickInterval);
            }
        }

        private PlayerHealth FindPlayerHealthInRadius()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return null;
            if (Vector2.Distance(p.transform.position, transform.position) > radius) return null;
            return p.GetComponentInParent<PlayerHealth>() ?? p.GetComponent<PlayerHealth>();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
