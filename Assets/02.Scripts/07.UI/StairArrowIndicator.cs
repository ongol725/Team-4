using UnityEngine;
using UnityEngine.UI;

namespace BagSurvivor.UI
{
    /// <summary>
    /// 방/보스 처치로 계단(StairController)이 활성화되면, 계단이 화면 밖일 때
    /// 화면 가장자리에 방향 화살표로 위치를 알려준다. 화면 안에 들어오면 숨긴다.
    ///
    /// 활성화된 계단의 존재 유무만으로 동작하므로 기존 맵/방 코드 수정이 필요 없다.
    /// (계단은 평소 비활성, ClearRoom 시 SetActive(true) 되는 구조를 그대로 이용)
    ///
    /// 부모가 ScreenSpaceOverlay Canvas(예: BattleUI/L5_Overlay)여야 한다.
    /// </summary>
    public class StairArrowIndicator : MonoBehaviour
    {
        [Header("화살표")]
        [Tooltip("비워두면 코드로 삼각형을 생성한다")]
        [SerializeField] private Sprite arrowSprite;
        [SerializeField] private Color arrowColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private float arrowSize = 64f;
        [Tooltip("화면 가장자리에서 안쪽으로 띄울 여백(px)")]
        [SerializeField] private float edgeMargin = 60f;

        private RectTransform _arrowRT;
        private Image _arrowImg;
        private Camera _cam;
        private StairController _stair;

        private void Start()
        {
            _cam = Camera.main;
            BuildArrow();
            SetArrowActive(false);
        }

        private void BuildArrow()
        {
            // 자기 자신의 Transform 종류/스케일에 의존하지 않도록, 화살표는 부모 Canvas 직속으로 생성한다.
            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("StairArrow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _arrowRT = go.GetComponent<RectTransform>();
            _arrowRT.localScale = Vector3.one;
            _arrowRT.sizeDelta = new Vector2(arrowSize, arrowSize);

            _arrowImg = go.GetComponent<Image>();
            _arrowImg.sprite = arrowSprite != null ? arrowSprite : CreateTriangleSprite(64);
            _arrowImg.color = arrowColor;
            _arrowImg.raycastTarget = false;
        }

        private void LateUpdate()
        {
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

            // 활성 계단 확보 (없거나 비활성화되면 다시 탐색). 계단은 1개뿐이라 부담 작음.
            if (_stair == null || !_stair.isActiveAndEnabled)
            {
                _stair = FindFirstObjectByType<StairController>();
                if (_stair == null) { SetArrowActive(false); return; }
            }

            Vector3 sp = _cam.WorldToScreenPoint(_stair.transform.position);
            bool behind = sp.z < 0f;
            if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

            float m = edgeMargin;
            bool onScreen = !behind
                && sp.x >= m && sp.x <= Screen.width - m
                && sp.y >= m && sp.y <= Screen.height - m;
            if (onScreen) { SetArrowActive(false); return; }

            SetArrowActive(true);

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = (Vector2)sp - center;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();

            _arrowRT.position = ClampToEdge(center, dir, m);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f; // 삼각형이 +Y를 향함
            _arrowRT.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>화면 중심에서 dir 방향으로 (여백 적용된) 화면 경계까지의 점</summary>
        private static Vector2 ClampToEdge(Vector2 center, Vector2 dir, float margin)
        {
            float halfW = Screen.width * 0.5f - margin;
            float halfH = Screen.height * 0.5f - margin;
            float sx = Mathf.Abs(dir.x) > 1e-4f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
            float sy = Mathf.Abs(dir.y) > 1e-4f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
            return center + dir * Mathf.Min(sx, sy);
        }

        private void SetArrowActive(bool on)
        {
            if (_arrowRT != null && _arrowRT.gameObject.activeSelf != on)
                _arrowRT.gameObject.SetActive(on);
        }

        /// <summary>위(+Y)를 가리키는 단색 삼각형 스프라이트를 생성</summary>
        private static Sprite CreateTriangleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            {
                float t = (float)y / (size - 1);          // 0=밑변, 1=꼭대기
                float halfW = (1f - t) * (size * 0.5f);
                for (int x = 0; x < size; x++)
                {
                    bool inside = Mathf.Abs(x - size * 0.5f) <= halfW;
                    tex.SetPixel(x, y, inside ? Color.white : clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
