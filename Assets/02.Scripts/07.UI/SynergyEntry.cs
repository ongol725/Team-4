// ============================================================
// SynergyEntry.cs
// 시너지 목록의 한 행 (등급 프레임 / 아이콘 / 이름 / 개수)
//  - 마우스 hover 시 목록(SynergyListUI)에 툴팁 표시 요청
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BagSurvivor.UI
{
    public class SynergyEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("참조")]
        public Image frameImage;  // 등급 프레임
        public Image iconImage;
        public Text nameText;
        public Text countText;

        [Header("등급 색상 (패널 스프라이트 미지정 시 폴백)")]
        public Color bronze = new Color(0.80f, 0.50f, 0.30f, 1f);
        public Color silver = new Color(0.75f, 0.78f, 0.85f, 1f);
        public Color gold   = new Color(1f,    0.84f, 0.30f, 1f);
        public Color prism  = new Color(0.75f, 0.45f, 1.00f, 1f);
        public Color gray   = new Color(0.55f, 0.55f, 0.58f, 1f);

        [Header("등급 패널 스프라이트 (지정 시 색상 틴트 대신 사용)")]
        public Sprite bronzeSprite;
        public Sprite silverSprite;
        public Sprite goldSprite;
        public Sprite prismSprite;
        public Sprite graySprite;   // 비활성(조건 미달) 패널

        private SynergyInfo info;
        private SynergyListUI owner;

        public SynergyInfo Info { get { return info; } }

        public void Setup(SynergyInfo i, SynergyListUI o)
        {
            info = i;
            owner = o;
            if (nameText != null) nameText.text = i.synergyName;
            if (countText != null)
            {
                countText.horizontalOverflow = HorizontalWrapMode.Overflow;
                countText.text = (i.grade == SynergyGrade.Prism || i.count >= i.nextThreshold)
                    ? i.count.ToString()
                    : $"{i.count}/{i.nextThreshold}";
            }
            if (iconImage != null && i.icon != null) iconImage.sprite = i.icon;
            if (frameImage != null)
            {
                var panelSprite = !i.isActive                     ? graySprite
                                : i.grade == SynergyGrade.Prism   ? prismSprite
                                : i.grade == SynergyGrade.Gold    ? goldSprite
                                : i.grade == SynergyGrade.Silver  ? silverSprite : bronzeSprite;
                if (panelSprite != null)
                {
                    frameImage.sprite = panelSprite;
                    frameImage.color  = Color.white; // 패널 원본 색 그대로 노출
                }
                else
                {
                    frameImage.color = !i.isActive                    ? gray
                                     : i.grade == SynergyGrade.Prism  ? prism
                                     : i.grade == SynergyGrade.Gold   ? gold
                                     : i.grade == SynergyGrade.Silver ? silver : bronze;
                }
            }

            // 비활성(조건 미달) 항목은 아이콘·텍스트를 살짝 어둡게 표시
            // (엔트리는 Populate마다 프리팹에서 새로 생성되므로 원복 처리는 불필요)
            if (!i.isActive)
            {
                var dim = new Color(0.72f, 0.72f, 0.72f, 1f);
                if (iconImage != null) iconImage.color *= dim;
                if (nameText  != null) nameText.color  *= dim;
                if (countText != null) countText.color *= dim;
            }
        }

        public void OnPointerEnter(PointerEventData e) { if (owner != null) owner.ShowTooltip(this); }
        public void OnPointerExit(PointerEventData e) { if (owner != null) owner.HideTooltip(); }
    }
}
