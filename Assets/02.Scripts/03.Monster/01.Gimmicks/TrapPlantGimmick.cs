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

            damageArea.Initialize(
                controller.monsterData.attack,
                areaDuration,
                damageTickInterval
            );
        }

        /// <summary>
        /// 프리팹이 없을 때 사용할 기본 원형 장판을 생성합니다.
        /// </summary>
        private GameObject CreateDefaultArea()
        {
            GameObject area = new GameObject("DamageArea_TrapPlant");
            area.transform.position = transform.position;

            // 시각적 표시 (반투명 빨간 원)
            SpriteRenderer sr = area.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(1f, 0f, 0f, 0.3f);
            sr.sortingOrder = -1;
            area.transform.localScale = Vector3.one * areaRadius * 2f;

            // 트리거 콜라이더
            CircleCollider2D col = area.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f; // localScale로 크기 조절되므로 기본 0.5

            return area;
        }

        /// <summary>
        /// 간단한 원형 스프라이트를 런타임에 생성합니다.
        /// </summary>
        private Sprite CreateCircleSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = size / 2f;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist <= radius)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
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
        private bool isInitialized = false;

        /// <summary>
        /// 장판을 초기화합니다.
        /// </summary>
        /// <param name="damage">틱당 데미지</param>
        /// <param name="duration">지속 시간 (초)</param>
        /// <param name="tickInterval">데미지 판정 간격 (초)</param>
        public void Initialize(int damage, float duration, float tickInterval)
        {
            this.damage = damage;
            this.duration = duration;
            this.tickInterval = tickInterval;
            this.isInitialized = true;

            // 지속 시간 후 자동 파괴
            // TODO: 오브젝트 풀링으로 교체
            Destroy(gameObject, duration);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isInitialized) return;

            if (other.CompareTag("Player"))
            {
                StartCoroutine(DamageTickCoroutine(other));
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                // 장판에서 나가면 해당 코루틴은 자동으로 다음 체크에서 종료됨
            }
        }

        /// <summary>
        /// 장판 위에 있는 동안 주기적으로 데미지를 줍니다.
        /// </summary>
        private IEnumerator DamageTickCoroutine(Collider2D playerCollider)
        {
            while (playerCollider != null && isInitialized)
            {
                // 플레이어가 아직 장판 위에 있는지 간이 체크
                float dist = Vector2.Distance(transform.position, playerCollider.transform.position);
                float areaSize = transform.localScale.x / 2f;

                if (dist > areaSize) yield break;

                // TODO: 플레이어 데미지 시스템과 연동
                // 예시: playerCollider.GetComponent<PlayerHealth>()?.TakeDamage(damage);
                Debug.Log($"[DamageArea] 장판 데미지: {damage}");

                yield return new WaitForSeconds(tickInterval);
            }
        }
    }
}
