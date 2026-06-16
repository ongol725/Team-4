// ============================================================
// MonsterHpBar.cs
// 일반 몬스터용 추적형 HP바 (월드 좌표 -> 스크린 좌표)
//  - MonsterController의 CurrentHP / monsterData.maxHP 를 읽어 표시 (몬스터 스크립트 수정 불필요)
//  - 몬스터 머리 위를 따라다님
//  - 오브젝트 풀링 대응 (SetTarget으로 재사용, 사망/풀반환 시 비활성화)
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using BagSurvivor.Monster;

namespace BagSurvivor.UI
{
    public class MonsterHpBar : MonoBehaviour
    {
        [Header("게이지")]
        [Tooltip("Filled(Horizontal) 타입 Image")]
        public Image fill;

        [Header("위치")]
        [Tooltip("몬스터 기준 머리 위 오프셋(월드)")]
        public Vector3 worldOffset = new Vector3(0f, 0.9f, 0f);

        [Header("데모 (타겟 없을 때 미리보기)")]
        public bool demoMode = false;
        [Range(0f, 1f)] public float demoFill = 0.7f;

        private MonsterController target;
        private RectTransform rt;
        private Camera cam;

        private void Awake() { rt = GetComponent<RectTransform>(); }
        private void OnEnable() { cam = Camera.main; }

        /// <summary>풀에서 꺼내 특정 몬스터에 연결.</summary>
        public void SetTarget(MonsterController mc)
        {
            target = mc;
            gameObject.SetActive(mc != null);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (demoMode && fill != null) fill.fillAmount = demoFill;
                return;
            }

            if (target.IsDead)
            {
                gameObject.SetActive(false);
                return;
            }

            if (cam == null) cam = Camera.main;
            if (cam != null)
                rt.position = cam.WorldToScreenPoint(target.transform.position + worldOffset);

            int max = target.MaxHP > 0 ? target.MaxHP : 1;
            if (fill != null)
                fill.fillAmount = (float)target.CurrentHP / max;
        }
    }
}