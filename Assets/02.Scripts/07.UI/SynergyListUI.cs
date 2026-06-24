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

        [Header("툴팁 위치 오프셋(시너지 항목 아래 끝 기준, 스크린 px)")]
        public Vector2 tooltipOffset = new Vector2(0f, -6f);

        [Header("Mock 데이터 (시스템 연결 전)")]
        public bool useMock = true;
        public List<SynergyInfo> synergies = new List<SynergyInfo>();

        private readonly List<SynergyEntry> entries = new List<SynergyEntry>();

        private void Awake()
        {
            if (content == null) content = GetComponent<RectTransform>();
            EscapeInventoryCanvas();
        }

        /// <summary>
        /// InventoryStoreRoot 캔버스 안에 있으면 독립 Canvas(SynergyCanvas)로 이탈.
        /// Canvas.enabled = false 의 영향을 받지 않아 항상 렌더링된다.
        /// </summary>
        private void EscapeInventoryCanvas()
        {
            // InventoryStoreRoot를 찾을 때까지 부모 체인 탐색
            Canvas srcCanvas = null;
            Transform directChild = transform; // InventoryStoreRoot 직접 자식 후보
            Transform t = transform.parent;
            while (t != null)
            {
                if (t.name == "InventoryStoreRoot")
                {
                    srcCanvas = t.GetComponent<Canvas>();
                    break;
                }
                directChild = t;
                t = t.parent;
            }
            if (srcCanvas == null) return; // 이미 분리되어 있거나 다른 계층

            // SynergyCanvas 생성 또는 재사용
            const string CANVAS_NAME = "SynergyCanvas";
            var canvasGO = GameObject.Find(CANVAS_NAME);
            if (canvasGO == null)
            {
                canvasGO = new GameObject(CANVAS_NAME);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                    canvasGO, gameObject.scene);

                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = srcCanvas.sortingOrder + 1;

                // 부모 Canvas의 CanvasScaler 설정 복사 → 좌표계 동일하게 유지
                var srcScaler = srcCanvas.GetComponent<CanvasScaler>();
                var dstScaler = canvasGO.AddComponent<CanvasScaler>();
                if (srcScaler != null)
                {
                    dstScaler.uiScaleMode         = srcScaler.uiScaleMode;
                    dstScaler.referenceResolution  = srcScaler.referenceResolution;
                    dstScaler.screenMatchMode      = srcScaler.screenMatchMode;
                    dstScaler.matchWidthOrHeight   = srcScaler.matchWidthOrHeight;
                }
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            // InventoryStoreRoot의 직접 자식(패널 루트)을 SynergyCanvas로 이동
            directChild.SetParent(canvasGO.transform, false);
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
            txt.font      = (Resources.Load<Font>("Fonts/Galmuri9") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"))
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

            // 시너지 항목(마우스 판정 영역)의 '아래쪽 끝 중앙'을 기준점으로 잡아 그 밑으로 설명을 펼친다.
            Vector3 anchor;
            var er = e.transform as RectTransform;
            if (er != null)
            {
                var corners = new Vector3[4];
                er.GetWorldCorners(corners); // 0:좌하 1:좌상 2:우상 3:우하
                anchor = (corners[0] + corners[3]) * 0.5f;     // 아래쪽 끝 중앙
            }
            else
            {
                anchor = e.transform.position;
            }
            tooltip.Show(e.Info, anchor + (Vector3)tooltipOffset);
        }

        public void HideTooltip()
        {
            if (tooltip != null) tooltip.Hide();
        }
    }
}
