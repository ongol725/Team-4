// ============================================================
// GoldPickup.cs
// 몬스터 사망 시 떨어지는 골드 픽업
//  - Gold_1~4 4프레임 루프 애니메이션
//  - 플레이어가 밟으면(트리거) 골드 획득 후 풀로 반환
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Items
{
    public class GoldPickup : MonoBehaviour
    {
        [Header("애니메이션 (Gold_1~4) — 액면가 스프라이트 미지정 시에만 사용")]
        public Sprite[] frames;
        public float frameRate = 10f;

        private SpriteRenderer sr;
        private int amount;
        private bool collected;
        private float animTimer;
        private int frameIndex;
        private bool animate;       // 액면가 스프라이트가 지정되면 false(고정 표시)
        private GoldDropManager owner;
        private bool flying;        // 플레이어로 빨려가는 중

        private void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>드롭 시 초기화. (풀에서 꺼낸 직후 호출)
        /// denomSprite를 주면 그 액면가 동전 스프라이트로 고정 표시(애니메이션 안 함).</summary>
        public void Init(int goldAmount, GoldDropManager manager, Sprite denomSprite = null)
        {
            amount = goldAmount;
            owner = manager;
            collected = false;
            flying = false;
            animTimer = 0f;
            frameIndex = 0;

            if (denomSprite != null)
            {
                animate = false;
                if (sr != null) sr.sprite = denomSprite; // 액면가별 고정 스프라이트
            }
            else
            {
                animate = frames != null && frames.Length >= 2;
                if (sr != null && frames != null && frames.Length > 0) sr.sprite = frames[0];
            }
        }

        private void Update()
        {
            // 획득 반경: 플레이어가 가까우면 캐릭터로 빨려온다(자석)
            if (!collected && !flying && owner != null)
            {
                Transform p = owner.PlayerTransform;
                if (p != null &&
                    (transform.position - p.position).sqrMagnitude <= owner.pickupRadius * owner.pickupRadius)
                {
                    FlyToPlayer(p);
                }
            }

            if (!animate || sr == null || frames == null || frames.Length < 2) return;
            animTimer += Time.deltaTime;
            float interval = 1f / Mathf.Max(0.01f, frameRate);
            if (animTimer >= interval)
            {
                animTimer -= interval;
                frameIndex = (frameIndex + 1) % frames.Length;
                sr.sprite = frames[frameIndex];
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || !other.CompareTag("Player")) return;
            CollectNow();
        }

        /// <summary>남은 골드를 즉시 획득 처리하고 풀로 반환한다(밟기·자동수집 공통). 중복 방지.</summary>
        public void CollectNow()
        {
            if (collected) return;
            collected = true;

            if (GameManager.Instance != null) GameManager.Instance.AddGold(amount);

            if (owner != null) owner.Return(this);
            else gameObject.SetActive(false);
        }

        /// <summary>플레이어에게 빨려가듯 날아간 뒤 도달 시 획득(방 이탈 자동수집 연출). 중복 방지.</summary>
        public void FlyToPlayer(Transform target)
        {
            if (collected || flying) return;
            if (target == null) { CollectNow(); return; } // 타깃 없으면 즉시 획득
            flying = true;
            StartCoroutine(FlyRoutine(target));
        }

        private IEnumerator FlyRoutine(Transform target)
        {
            float speed = 4f;             // 시작 속도
            const float accel   = 55f;    // 가속(점점 빨라지며 빨려듦)
            const float arrive  = 0.35f;  // 도달 판정 거리

            while (!collected && target != null)
            {
                speed += accel * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
                if ((transform.position - target.position).sqrMagnitude <= arrive * arrive) break;
                yield return null;
            }

            flying = false;
            CollectNow(); // 도달 → 골드 증가 + 풀 반환
        }
    }
}
