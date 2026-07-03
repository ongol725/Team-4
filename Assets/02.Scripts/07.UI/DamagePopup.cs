// ============================================================
// DamagePopup.cs
// 몬스터 피격 시 머리 위에 데미지 숫자를 띄우는 플로팅 텍스트 (메이플식)
//  - World-space TextMeshPro (캔버스 불필요)
//  - 자체 정적 풀(Queue) 내장 → 모든 씬에서 안전, GC 최소화
//  - 폰트: Resources/Fonts/BoldDunggeunmo SDF Damage (외곽선 머티리얼 포함)
//  - 사용: DamagePopup.Show(worldPos, amount);
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace BagSurvivor
{
    public class DamagePopup : MonoBehaviour
    {
        // ── 튜닝 값 (느낌 보고 조절) ──────────────────────────
        const float Lifetime = 0.8f;     // 총 표시 시간(초)
        const float RiseSpeed = 1.8f;    // 위로 떠오르는 초기 속도
        const float Gravity = 2.2f;      // 떠오른 뒤 살짝 감속(아치 느낌)
        const float FontSize = 6f;       // 월드 폰트 최대 크기(강타 기준). 약타는 TierSizeMul로 축소
        const float HitYOffset = 0.7f;   // 피격 위치에서 위로 띄울 높이
        const int SortingOrder = 100;    // 스프라이트/이펙트(≈10) 위로 보이게

        // ── 타격감(A안) 튜닝 ─────────────────────────────────
        const float PopDur          = 0.12f; // 등장 팝 지속(초)
        const float PopStartScale   = 0.55f; // 팝 시작 스케일(작게 → 오버슈트하며 커짐)
        const float HScatter        = 0.9f;  // 좌우 산개 속도(아치 느낌)
        const float MaxTilt         = 4f;    // 랜덤 기울기(도)
        const float HoldUntil       = 0.55f; // 이 비율까지 불투명 유지 후 빠르게 페이드
        static readonly Color NormalColor = Color.white;
        static readonly Color CritColor = new Color(1f, 0.82f, 0.2f);

        // 데미지 크기별 색(오름차순). <30 흰색 / 30~59 노랑 / 60~99 주황 / 100+ 빨강
        static readonly (int min, Color col)[] DamageTiers =
        {
            (0,   new Color(1.00f, 1.00f, 1.00f)), // 흰색(약타)
            (30,  new Color(1.00f, 0.95f, 0.30f)), // 노랑
            (60,  new Color(1.00f, 0.60f, 0.15f)), // 주황
            (100, new Color(1.00f, 0.25f, 0.20f)), // 빨강(최강타)
        };

        static Color TierColor(int amount)
        {
            Color c = DamageTiers[0].col;
            for (int i = 0; i < DamageTiers.Length; i++)
            {
                if (amount >= DamageTiers[i].min) c = DamageTiers[i].col;
                else break;
            }
            return c;
        }

        // 데미지 크기별 글자 크기 배율: 약타 70% → 강타 100%(현재 크기).
        static float TierSizeMul(int amount)
        {
            if (amount >= 100) return 1.00f;
            if (amount >= 60)  return 0.90f;
            if (amount >= 30)  return 0.80f;
            return 0.70f;
        }

        // 오버슈트 이징(등장 팝) — 끝에서 1을 살짝 넘겼다 정착.
        static float EaseOutBack(float p)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = p - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        // ── 풀 ───────────────────────────────────────────────
        static readonly Queue<DamagePopup> pool = new Queue<DamagePopup>();
        static TMP_FontAsset font;

        TextMeshPro tmp;
        float age;
        Vector3 vel;
        Color baseColor;

        /// <summary>피격 지점(월드)에 데미지 숫자를 띄웁니다. overrideColor 지정 시 그 색으로(예: 플레이어 피격=연한 빨강).</summary>
        public static void Show(Vector3 worldPos, int amount, bool isCritical = false, Color? overrideColor = null)
        {
            DamagePopup p = pool.Count > 0 ? pool.Dequeue() : Create();
            if (p == null) return;

            // 겹치는 숫자가 완전히 포개지지 않도록 살짝 흩뿌림
            Vector3 pos = worldPos + new Vector3(Random.Range(-0.2f, 0.2f), HitYOffset, 0f);
            p.Setup(pos, amount, isCritical, overrideColor);
        }

        static DamagePopup Create()
        {
            if (font == null)
            {
                font = Resources.Load<TMP_FontAsset>("Fonts/BoldDunggeunmo SDF Damage");
                if (font == null)
                    Debug.LogWarning("[DamagePopup] 폰트를 찾지 못했습니다: Resources/Fonts/BoldDunggeunmo SDF Damage");
            }

            GameObject go = new GameObject("DamagePopup");
            DontDestroyOnLoad(go); // 씬 전환에도 풀(정적 Queue) 유효성 유지

            DamagePopup p = go.AddComponent<DamagePopup>();
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font; // 폰트 기본 머티리얼(외곽선) 자동 적용
            tmp.fontSize = FontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = SortingOrder;

            p.tmp = tmp;
            go.SetActive(false);
            return p;
        }

        void Setup(Vector3 pos, int amount, bool crit, Color? overrideColor)
        {
            transform.position = pos;
            transform.localScale = Vector3.one * PopStartScale;                  // 팝 시작(작게)
            transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-MaxTilt, MaxTilt)); // 랜덤 기울기
            age = 0f;
            // 위로 떠오르며 좌우로 살짝 산개(아치)
            vel = Vector3.up * RiseSpeed + Vector3.right * Random.Range(-HScatter, HScatter);
            baseColor = overrideColor ?? (crit ? CritColor : TierColor(amount));

            tmp.text = amount.ToString();
            // 강타(크리 포함)는 현재 크기(100%), 약타는 70%까지 축소
            tmp.fontSize = FontSize * (crit ? 1.0f : TierSizeMul(amount));
            tmp.color = baseColor;

            gameObject.SetActive(true);
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= Lifetime)
            {
                gameObject.SetActive(false);
                pool.Enqueue(this);
                return;
            }

            // 이동(아치)
            transform.position += vel * Time.deltaTime;
            vel.y -= Gravity * Time.deltaTime;

            // 등장 팝: 작게 → 오버슈트하며 1.0으로 정착("팍")
            float scale = age < PopDur
                ? Mathf.LerpUnclamped(PopStartScale, 1f, EaseOutBack(age / PopDur))
                : 1f;
            transform.localScale = Vector3.one * scale;

            // HoldUntil 이후 빠르게 페이드
            float t = age / Lifetime;
            Color c = baseColor;
            c.a = t < HoldUntil ? 1f : Mathf.Clamp01(1f - (t - HoldUntil) / (1f - HoldUntil));
            tmp.color = c;
        }
    }
}
