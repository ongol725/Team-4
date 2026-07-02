using UnityEngine;

namespace BagSurvivor.Monster
{
    /// <summary>
    /// 스폰 예고 마커의 명멸/맥동 연출(자기완결).
    /// 스프라이트 알파와 크기를 사인파로 진동시켜 "여기서 곧 적이 나온다"를 눈에 띄게 알린다.
    /// RoomMonsterSpawner가 예고 마커에 AddComponent로 부착한다.
    /// </summary>
    public class SpawnMarkerPulse : MonoBehaviour
    {
        public float pulseSpeed = 6f;   // 명멸 속도
        public float alphaMin   = 0.35f;
        public float alphaMax   = 0.9f;
        public float scalePulse = 0.12f; // 크기 진동 폭(±)

        private SpriteRenderer _sr;
        private Vector3        _baseScale;
        private float          _t;

        private void Awake()
        {
            _sr        = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            _t += Time.deltaTime * pulseSpeed;
            float s = (Mathf.Sin(_t) + 1f) * 0.5f; // 0~1

            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = Mathf.Lerp(alphaMin, alphaMax, s);
                _sr.color = c;
            }
            transform.localScale = _baseScale * (1f + scalePulse * s);
        }
    }
}
