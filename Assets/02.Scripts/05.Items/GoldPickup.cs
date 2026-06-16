// ============================================================
// GoldPickup.cs
// 몬스터 사망 시 떨어지는 골드 픽업
//  - Gold_1~4 4프레임 루프 애니메이션
//  - 플레이어가 밟으면(트리거) 골드 획득 후 풀로 반환
// ============================================================
using UnityEngine;

namespace BagSurvivor.Items
{
    public class GoldPickup : MonoBehaviour
    {
        [Header("애니메이션 (Gold_1~4)")]
        public Sprite[] frames;
        public float frameRate = 10f;

        private SpriteRenderer sr;
        private int amount;
        private bool collected;
        private float animTimer;
        private int frameIndex;
        private GoldDropManager owner;

        private void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>드롭 시 초기화. (풀에서 꺼낸 직후 호출)</summary>
        public void Init(int goldAmount, GoldDropManager manager)
        {
            amount = goldAmount;
            owner = manager;
            collected = false;
            animTimer = 0f;
            frameIndex = 0;
            if (sr != null && frames != null && frames.Length > 0) sr.sprite = frames[0];
        }

        private void Update()
        {
            if (sr == null || frames == null || frames.Length < 2) return;
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
            collected = true;

            if (GameManager.Instance != null) GameManager.Instance.AddGold(amount);

            if (owner != null) owner.Return(this);
            else gameObject.SetActive(false);
        }
    }
}
