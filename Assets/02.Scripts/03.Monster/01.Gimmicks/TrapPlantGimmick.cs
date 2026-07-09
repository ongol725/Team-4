// ============================================================
// TrapPlantGimmick.cs
// 함정식물 전용 제자리 장판 기믹
// 일정 주기마다 주변에 데미지 장판을 생성
// MonsterController에 컴포넌트로 부착하여 사용 (컴포지션 패턴)
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class TrapPlantGimmick : MonoBehaviour
    {
        // ==========================================
        // 인스펙터 설정 (수정 용이하도록 노출)
        // ==========================================
        [Header("장판 설정")]
        [Tooltip("장판 프리팹 (없으면 기본 원형 장판 자동 생성)")]
        public GameObject areaPrefab;

        [Tooltip("첫 장판 생성까지 대기 시간 (초)")]
        public float initialDelay = 3f;

        [Tooltip("장판 생성 주기 (초)")]
        public float spawnInterval = 10f;

        [Tooltip("생성된 장판 지속 시간 (초)")]
        public float areaDuration = 5f;

        [Tooltip("장판 반경")]
        public float areaRadius = 2f;

        [Tooltip("장판 데미지 판정 간격 (초)")]
        public float damageTickInterval = 0.5f;

        [Tooltip("틱당 데미지. 몬스터 attack이 0일 때 이 값을 사용 (함정식물은 attack=0이라 이 값으로 동작)")]
        public int areaDamage = 8;

        // ==========================================
        // 내부 변수
        // ==========================================
        private MonsterController controller;
        private Coroutine spawnRoutine;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
        }

        private void OnEnable()
        {
            // 오브젝트 풀에서 재활성화될 때마다 장판 생성 루틴 시작
            spawnRoutine = StartCoroutine(AreaSpawnRoutine());
        }

        private void OnDisable()
        {
            // 오브젝트 풀 반환 시 코루틴 정지
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }
        }

        /// <summary>
        /// 일정 주기마다 장판을 생성하는 반복 루틴입니다.
        /// </summary>
        private IEnumerator AreaSpawnRoutine()
        {
            // 스폰 후 초기 대기
            yield return new WaitForSeconds(initialDelay);

            while (!controller.IsDead)
            {
                SpawnDamageArea();
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        /// <summary>
        /// 데미지 장판을 몬스터 위치에 생성합니다.
        /// </summary>
        private void SpawnDamageArea()
        {
            if (controller.IsDead) return;

            GameObject area;

            if (areaPrefab != null)
            {
                // 프리팹이 설정되어 있으면 프리팹에서 생성
                // TODO: 오브젝트 풀링으로 교체
                area = Instantiate(areaPrefab, transform.position, Quaternion.identity);
            }
            else
            {
                // 프리팹 없으면 기본 원형 장판 자동 생성
                area = CreateDefaultArea();
            }

            // 장판에 데미지 컴포넌트 부착
            DamageArea damageArea = area.GetComponent<DamageArea>();
            if (damageArea == null)
            {
                damageArea = area.AddComponent<DamageArea>();
            }

            // attack이 0이면(함정식물 기본값) areaDamage 사용
            int tickDamage = controller.monsterData.attack > 0 ? controller.monsterData.attack : areaDamage;
            damageArea.Initialize(
                tickDamage,
                areaDuration,
                damageTickInterval,
                areaRadius
            );
        }

        /// <summary>
        /// 프리팹이 없을 때 사용할 기본 원형 장판을 생성합니다.
        /// </summary>
        private GameObject CreateDefaultArea()
        {
            GameObject area = new GameObject("DamageArea_TrapPlant");
            area.transform.position = transform.position;

            // 시각적 표시 (반투명 보라색 뭉게구름 바닥)
            SpriteRenderer sr = area.AddComponent<SpriteRenderer>();
            sr.sprite = GetCloudSprite();
            sr.color = new Color(0.6f, 0.15f, 0.85f, 0.5f); // 보라
            sr.sortingOrder = 10;                            // 바닥(0)·벽(1) 위로 보이게
            area.transform.localScale = Vector3.one * areaRadius * 2f;

            // 데미지는 DamageArea가 반경으로 직접 판정(폴링)하므로 콜라이더 불필요
            return area;
        }

        /// <summary>
        /// 뭉게구름 형태의 부드러운 원형 스프라이트(흰색)를 1회만 생성해 캐싱합니다.
        /// 색은 SpriteRenderer.color로 입히므로 흰색으로 만듭니다.
        /// </summary>
        private static Sprite _cloudSprite;
        private static Sprite GetCloudSprite()
        {
            if (_cloudSprite != null) return _cloudSprite;

            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float c = size / 2f;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dx = x - c, dy = y - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / c; // 0(중심)~1(가장자리)
                    // 각도별로 반경을 울퉁불퉁하게 → 뭉게구름 윤곽
                    float ang = Mathf.Atan2(dy, dx);
                    float lobe = 0.09f * Mathf.Sin(ang * 5f) + 0.06f * Mathf.Sin(ang * 8f + 1.3f);
                    float edge = 0.9f + lobe;
                    // 중심은 진하고 가장자리로 갈수록 부드럽게 사라짐
                    float a = 1f - Mathf.SmoothStep(edge * 0.45f, edge, dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            }
            tex.Apply();

            _cloudSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cloudSprite;
        }
    }

    // ============================================================
    // DamageArea.cs (같은 파일에 포함 - 장판 데미지 처리)
    // 장판 위의 플레이어에게 주기적 데미지를 주고, 지속시간 후 자동 파괴
    // ============================================================

    /// <summary>
    /// 데미지 장판 컴포넌트. 범위 내 플레이어에게 주기적 데미지를 줍니다.
    /// </summary>
    public class DamageArea : MonoBehaviour
    {
        private int damage;
        private float duration;
        private float tickInterval;
        private float radius;
        private bool isInitialized = false;

        /// <summary>
        /// 장판을 초기화합니다.
        /// </summary>
        /// <param name="damage">틱당 데미지</param>
        /// <param name="duration">지속 시간 (초)</param>
        /// <param name="tickInterval">데미지 판정 간격 (초)</param>
        /// <param name="radius">데미지 판정 반경 (월드 유닛)</param>
        public void Initialize(int damage, float duration, float tickInterval, float radius)
        {
            this.damage = damage;
            this.duration = duration;
            this.tickInterval = tickInterval;
            this.radius = radius;
            this.isInitialized = true;

            StartCoroutine(DamageRoutine());

            // 지속 시간 후 자동 파괴
            // TODO: 오브젝트 풀링으로 교체
            Destroy(gameObject, duration);
        }

        /// <summary>
        /// 지속 시간 동안 주기적으로, 반경 안에 있는 플레이어에게 데미지를 줍니다.
        /// 트리거가 아니라 반경 직접 판정 → 시각 범위와 정확히 일치.
        /// </summary>
        private IEnumerator DamageRoutine()
        {
            float elapsed = 0f;
            while (elapsed < duration && isInitialized)
            {
                PlayerHealth ph = FindPlayerInRadius();
                if (ph != null && !ph.IsDead) ph.TakeDamage(damage);

                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
            }
        }

        /// <summary>태그로 플레이어를 찾아 반경 안에 있으면 PlayerHealth 반환(없으면 null).</summary>
        private PlayerHealth FindPlayerInRadius()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return null;
            if (Vector2.Distance(p.transform.position, transform.position) > radius) return null;
            return p.GetComponentInParent<PlayerHealth>() ?? p.GetComponent<PlayerHealth>();
        }
    }
}
