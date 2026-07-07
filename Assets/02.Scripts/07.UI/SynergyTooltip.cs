// ============================================================
// SynergyTooltip.cs
// 전투화면 L5 - 시너지 툴팁 팝업 (Synergy_TooltipPopup)
//  - 시너지 행 hover 시 이름(122001)/설명(122002)/효과(122003) 표시
// ============================================================
using UnityEngine;
using UnityEngine.UI;

namespace BagSurvivor.UI
{
    public class SynergyTooltip : MonoBehaviour
    {
        [Header("참조")]
        public Image icon;
        public Text nameText;
        public Text descText;
        public Text effectText;

        [Header("배경 (등급 패널 — 지정 시 시너지 등급에 맞는 패널로 교체)")]
        public Image background;
        public Sprite bronzeSprite;
        public Sprite silverSprite;
        public Sprite goldSprite;
        public Sprite prismSprite;
        public Sprite graySprite;   // 비활성(조건 미달) 배경

        private RectTransform rt;

        // 프리즘 배경은 밝은 무지개색이라 흰 글씨가 안 보인다 → 프리즘일 때만 어두운 글씨로.
        private static readonly Color PrismTextColor = new Color32(0x24, 0x18, 0x30, 0xff); // 어두운 보라
        private Color _origNameColor = Color.white;
        private Color _origDescColor = Color.white;
        private Color _origEffectColor = Color.white;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            if (nameText != null)   _origNameColor   = nameText.color;
            if (descText != null)   _origDescColor   = descText.color;
            if (effectText != null) _origEffectColor = effectText.color;

            // 툴팁이 마우스 포인터를 가로채면 시너지 항목의 OnPointerExit가 발동해
            // 깜빡임(보였다 사라졌다)이 생긴다. CanvasGroup으로 레이캐스트를 차단하지 않게 한다.
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // 인벤토리(캔버스 정렬 0~10) 위에 렌더되도록 자체 Canvas로 정렬을 올린다.
            // (hover 시에만 보이므로 인벤토리 위에 겹쳐 떠도 무방)
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 200;

            gameObject.SetActive(false);
        }

        /// <summary>topAnchorWorldPos를 툴팁 상단 중앙에 맞춰, 그 지점부터 아래로 펼쳐 표시합니다.</summary>
        public void Show(SynergyInfo i, Vector3 topAnchorWorldPos)
        {
            if (i == null) return;

            // 프리즘(활성)은 밝은 배경이라 글씨를 어둡게 → 잘 보이게
            bool prism = i.isActive && i.grade == SynergyGrade.Prism;

            if (nameText != null)
            {
                nameText.text  = i.synergyName;
                nameText.color = prism ? PrismTextColor : _origNameColor;
            }
            if (descText != null)
            {
                string condColor = prism ? "#241830" : "#8FA6FF";
                descText.text = string.IsNullOrEmpty(i.condition)
                    ? i.description
                    : "<color=" + condColor + ">발동 조건: " + i.condition + "</color>\n" + i.description;
                descText.color = prism ? PrismTextColor : _origDescColor;
            }
            if (effectText != null)
            {
                string fx = i.effect ?? string.Empty;
                if (prism) // 마일스톤 내장 색태그(활성 흰색/비활성 회색)를 어둡게 치환
                    fx = fx.Replace("#FFFFFF", "#241830").Replace("#88888880", "#5A4A6A80");
                effectText.text  = fx;
                effectText.color = prism ? PrismTextColor : _origEffectColor;
            }
            if (icon != null && i.icon != null) icon.sprite = i.icon;

            if (background != null)
            {
                var panelSprite = !i.isActive                     ? graySprite
                                : i.grade == SynergyGrade.Prism   ? prismSprite
                                : i.grade == SynergyGrade.Gold    ? goldSprite
                                : i.grade == SynergyGrade.Silver  ? silverSprite : bronzeSprite;
                if (panelSprite != null)
                {
                    background.sprite = panelSprite;
                    background.color  = Color.white; // 패널 원본 색 그대로
                }
            }

            gameObject.SetActive(true);
            if (rt != null)
            {
                // 피벗을 상단 중앙으로 → 기준점(시너지 항목 아래 끝)부터 아래로 내용이 펼쳐짐.
                // 인벤토리 위에 렌더되도록 자체 Canvas 정렬을 올려두었으므로 아래로 펼쳐도 가리지 않음.
                rt.pivot = new Vector2(0.5f, 1f);
                ResizeToFit();
                rt.position = topAnchorWorldPos;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────
        // 내용 길이에 맞춘 높이 자동 확장 (발동 조건·설명·단계 효과가 잘리지 않도록)

        private const float TooltipW   = 330f; // 툴팁 전체 폭
        private const float ContentW   = 300f; // 텍스트 영역 폭
        private const float HeaderH    = 65f;  // 아이콘·이름 영역 높이
        private const float SectionGap = 12f;  // 설명 ↔ 효과 사이 간격
        private const float BottomPad  = 14f;  // 하단 여백

        private void ResizeToFit()
        {
            if (rt == null || descText == null || effectText == null) return;

            // 모든 요소를 상단 기준으로 재배치 → 높이가 늘어나도 레이아웃 유지
            SetTopAnchor(icon    != null ? icon.rectTransform     : null, new Vector2(-125f, -35f), new Vector2(0.5f, 0.5f));
            SetTopAnchor(nameText != null ? nameText.rectTransform : null, new Vector2(35f, -35f),   new Vector2(0.5f, 0.5f));

            var descRt = descText.rectTransform;
            var fxRt   = effectText.rectTransform;
            SetTopAnchor(descRt, new Vector2(0f, -HeaderH), new Vector2(0.5f, 1f));

            descText.horizontalOverflow   = HorizontalWrapMode.Wrap;
            descText.verticalOverflow     = VerticalWrapMode.Overflow;
            effectText.horizontalOverflow = HorizontalWrapMode.Wrap;
            effectText.verticalOverflow   = VerticalWrapMode.Overflow;

            descRt.sizeDelta = new Vector2(ContentW, 0f);
            float descH = descText.preferredHeight;
            descRt.sizeDelta = new Vector2(ContentW, descH);

            fxRt.sizeDelta = new Vector2(ContentW, 0f);
            float fxH = effectText.preferredHeight;
            SetTopAnchor(fxRt, new Vector2(0f, -(HeaderH + descH + SectionGap)), new Vector2(0.5f, 1f));
            fxRt.sizeDelta = new Vector2(ContentW, fxH);

            rt.sizeDelta = new Vector2(TooltipW, HeaderH + descH + SectionGap + fxH + BottomPad);

            // 배경이 별도 자식(BG)인 경우 루트 크기를 꽉 채우도록 스트레치
            if (background != null && background.rectTransform != rt)
            {
                var bgRt = background.rectTransform;
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;
            }
        }

        private static void SetTopAnchor(RectTransform r, Vector2 anchoredPos, Vector2 pivot)
        {
            if (r == null) return;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot            = pivot;
            r.anchoredPosition = anchoredPos;
        }
    }
}
