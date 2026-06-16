// ============================================================
// SynergyListUI.cs
// 전투화면 L4 - Player_Synergy 시너지 목록 (Scroll View)
//  - 시너지 시스템 연결 전: Mock 데이터로 표시
//  - 실제 연결 시 SetSynergies(list) 호출로 갱신
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BagSurvivor.UI
{
    public class SynergyListUI : MonoBehaviour
    {
        [Header("참조")]
        public RectTransform content;     // ScrollView Content (VerticalLayoutGroup)
        public GameObject entryPrefab;     // SynergyEntry 프리팹
        public SynergyTooltip tooltip;     // L5 툴팁

        [Header("툴팁 위치 오프셋(스크린 px)")]
        public Vector2 tooltipOffset = new Vector2(160f, 0f);

        [Header("Mock 데이터 (시스템 연결 전)")]
        public bool useMock = true;
        public List<SynergyInfo> synergies = new List<SynergyInfo>();

        private readonly List<SynergyEntry> entries = new List<SynergyEntry>();

        private void Awake()
        {
            // content가 Inspector에서 연결되지 않은 경우 자신의 RectTransform 사용
            if (content == null) content = GetComponent<RectTransform>();
        }

        private void Start()
        {
            if (useMock && synergies.Count == 0) BuildMock();
            Populate();
        }

        private void BuildMock()
        {
            synergies.Add(new SynergyInfo { synergyName = "화염", count = 3, grade = SynergyGrade.Gold, description = "공격 시 화염 피해를 추가로 입힙니다.", effect = "추가 화염 피해 +30%" });
            synergies.Add(new SynergyInfo { synergyName = "강철", count = 2, grade = SynergyGrade.Silver, description = "방어구가 단단해집니다.", effect = "방어력 +15" });
            synergies.Add(new SynergyInfo { synergyName = "신속", count = 1, grade = SynergyGrade.Bronze, description = "발놀림이 빨라집니다.", effect = "이동 속도 +10%" });
        }

        /// <summary>외부(시너지 시스템)에서 목록을 갱신할 때 호출.</summary>
        public void SetSynergies(List<SynergyInfo> list)
        {
            synergies = list ?? new List<SynergyInfo>();
            Populate();
        }

        public void Populate()
        {
            entries.Clear();

            if (content == null) return;

            // 방식에 상관없이 Content의 모든 자식을 제거 (누적 방지)
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            foreach (var s in synergies)
            {
                if (entryPrefab != null)
                {
                    var go    = Instantiate(entryPrefab, content);
                    var entry = go.GetComponent<SynergyEntry>();
                    if (entry != null) { entry.Setup(s, this); entries.Add(entry); }
                }
                else
                {
                    SpawnFallbackEntry(s);
                }
            }
        }

        void SpawnFallbackEntry(SynergyInfo s)
        {
            var go = new GameObject("SynergyEntry_Text", typeof(RectTransform));
            go.transform.SetParent(content, false);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 26;

            var txt = go.AddComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                         ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize  = 11;
            txt.alignment = TextAnchor.MiddleLeft;

            string label = s.grade == SynergyGrade.Gold
                ? $"{s.synergyName}  ★{s.count}"
                : $"{s.synergyName}  {s.count}/{s.nextThreshold}";

            txt.text  = label;
            txt.color = s.grade == SynergyGrade.Gold   ? new Color(1f, 0.84f, 0.3f)
                      : s.grade == SynergyGrade.Silver  ? new Color(0.75f, 0.78f, 0.85f)
                      : new Color(0.8f, 0.5f, 0.3f);
        }

        public void ShowTooltip(SynergyEntry e)
        {
            if (tooltip == null || e == null) return;
            tooltip.Show(e.Info, e.transform.position + (Vector3)tooltipOffset);
        }

        public void HideTooltip()
        {
            if (tooltip != null) tooltip.Hide();
        }
    }
}
