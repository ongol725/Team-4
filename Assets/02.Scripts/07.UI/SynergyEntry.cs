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

        [Header("등급 색상")]
        public Color bronze = new Color(0.80f, 0.50f, 0.30f, 1f);
        public Color silver = new Color(0.75f, 0.78f, 0.85f, 1f);
        public Color gold = new Color(1f, 0.84f, 0.30f, 1f);

        private SynergyInfo info;
        private SynergyListUI owner;

        public SynergyInfo Info { get { return info; } }

        public void Setup(SynergyInfo i, SynergyListUI o)
        {
            info = i;
            owner = o;
            if (nameText != null) nameText.text = i.synergyName;
            if (countText != null) countText.text = "x" + i.count;
            if (iconImage != null && i.icon != null) iconImage.sprite = i.icon;
            if (frameImage != null)
                frameImage.color = i.grade == SynergyGrade.Gold ? gold
                                 : (i.grade == SynergyGrade.Silver ? silver : bronze);
        }

        public void OnPointerEnter(PointerEventData e) { if (owner != null) owner.ShowTooltip(this); }
        public void OnPointerExit(PointerEventData e) { if (owner != null) owner.HideTooltip(); }
    }
}
