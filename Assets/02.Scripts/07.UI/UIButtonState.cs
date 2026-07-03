// ============================================================
// UIButtonState.cs
// 기획서 "버튼 상태" 규칙 구현 (Default / Hover / Active)
//  - Default: 원본 (Dim 0%)
//  - Hover  : 블랙 Dim 20%
//  - Active : 총 30~35% 어둡게 + 크기 95% 축소 + SFX_Btn_Click 1회
// 버튼 위에 블랙 Dim 오버레이 Image를 자동 생성하여 명암을 제어.
// 어떤 UI 오브젝트(Image/Button)에도 부착 가능. 오브젝트 풀링 대응.
// ============================================================
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BagSurvivor.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class UIButtonState : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [Header("Dim 설정 (블랙 오버레이 알파)")]
        [Tooltip("Hover 시 어둡기 (기획서: 20%)")]
        [Range(0f, 1f)] public float hoverDim = 0.20f;

        [Tooltip("Active(클릭) 시 어둡기 (기획서: 총 30~35%)")]
        [Range(0f, 1f)] public float activeDim = 0.33f;

        [Header("Active 축소 연출")]
        [Tooltip("기획서: 크기 95% 축소")]
        [Range(0.5f, 1f)] public float activeScale = 0.95f;

        [Header("클릭 SFX (선택)")]
        [Tooltip("SFX_Btn_Click 클립 (비우면 기본 클릭음 DefaultClick 사용)")]
        public AudioClip clickSfx;

        // 기본 클릭음 (개별 지정 없을 때) — 전역 1회 로드
        private static AudioClip _defaultClick;
        private static bool _defaultLoaded;

        private Image dimOverlay;
        private Vector3 originalScale;
        private bool isPointerInside;
        private bool isPointerDown;

        private void Awake()
        {
            originalScale = transform.localScale;
            CreateDimOverlay();
        }

        private void OnEnable()
        {
            // 풀링/재활성화 대비 초기화
            isPointerInside = false;
            isPointerDown = false;
            transform.localScale = originalScale;
            ApplyDim(0f);
        }

        private void CreateDimOverlay()
        {
            Transform existing = transform.Find("__DimOverlay");
            if (existing != null) { dimOverlay = existing.GetComponent<Image>(); return; }

            GameObject go = new GameObject("__DimOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            dimOverlay = go.GetComponent<Image>();
            dimOverlay.color = new Color(0f, 0f, 0f, 0f);
            dimOverlay.raycastTarget = false; // 클릭을 가로채지 않음
        }

        private void ApplyDim(float alpha)
        {
            if (dimOverlay == null) return;
            Color c = dimOverlay.color;
            c.a = alpha;
            dimOverlay.color = c;
        }

        private void RefreshVisual()
        {
            if (isPointerDown && isPointerInside)
            {
                ApplyDim(activeDim);
                transform.localScale = originalScale * activeScale;
            }
            else if (isPointerInside)
            {
                ApplyDim(hoverDim);
                transform.localScale = originalScale;
            }
            else
            {
                ApplyDim(0f);
                transform.localScale = originalScale;
            }
        }

        public void OnPointerEnter(PointerEventData e) { isPointerInside = true; RefreshVisual(); }
        public void OnPointerExit(PointerEventData e) { isPointerInside = false; RefreshVisual(); }

        public void OnPointerDown(PointerEventData e)
        {
            isPointerDown = true;
            RefreshVisual();
            PlayClickSfx(); // 기획서: 클릭 직후 출력
        }

        public void OnPointerUp(PointerEventData e)
        {
            isPointerDown = false;
            RefreshVisual();
        }

        private void PlayClickSfx()
        {
            AudioClip clip = clickSfx;
            if (clip == null)
            {
                if (!_defaultLoaded)
                {
                    _defaultClick = Resources.Load<AudioClip>("01.SFX/03.UI/DefaultClick");
                    _defaultLoaded = true;
                }
                clip = _defaultClick;
            }
            AudioUtil.PlaySfx(clip); // 2D + 설정(sfxOn/sfxVol) 반영
        }
    }
}