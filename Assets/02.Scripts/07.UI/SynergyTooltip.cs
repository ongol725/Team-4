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

            // 툴팁이 마우스 포인터를 가로채면 시너지 항목의 OnPointerExit가 발동해
            // 깜빡임(보였다 사라졌다)이 생긴다. CanvasGroup으로 레이캐스트를 차단하지 않게 한다.
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            gameObject.SetActive(false);
        }

        /// <summary>topAnchorWorldPos를 툴팁 상단 중앙에 맞춰, 그 지점부터 아래로 펼쳐 표시합니다.</summary>
        public void Show(SynergyInfo i, Vector3 topAnchorWorldPos)
        {
            if (i == null) return;
            if (nameText != null) nameText.text = i.synergyName;
            if (descText != null) descText.text = i.description;
            if (effectText != null) effectText.text = i.effect;
            if (icon != null && i.icon != null) icon.sprite = i.icon;

            gameObject.SetActive(true);
            if (rt != null)
            {
                // 피벗을 상단 중앙으로 → 기준점(시너지 항목 아래 끝)부터 아래로 내용이 펼쳐짐
                rt.pivot = new Vector2(0.5f, 1f);
                rt.position = topAnchorWorldPos;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
