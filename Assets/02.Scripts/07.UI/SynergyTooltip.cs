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

        private RectTransform rt;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            gameObject.SetActive(false);
        }

        public void Show(SynergyInfo i, Vector3 screenPos)
        {
            if (i == null) return;
            if (nameText != null) nameText.text = i.synergyName;
            if (descText != null) descText.text = i.description;
            if (effectText != null) effectText.text = i.effect;
            if (icon != null && i.icon != null) icon.sprite = i.icon;

            gameObject.SetActive(true);
            if (rt != null) rt.position = screenPos;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
