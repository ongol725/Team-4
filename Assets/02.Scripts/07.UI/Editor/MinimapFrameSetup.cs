// ============================================================
// MinimapFrameSetup.cs  (Editor 전용)
// 우측하단 미니맵(Dungeon_Export_Group > Canvas > MinimapUI)을 둘러싸는
// Minimap_Frame을 BattleUI.prefab에 추가/정렬한다.
//  - 02.Ingame(또는 MinimapUI·BattleUI가 있는 씬)을 연 상태에서 메뉴 실행.
//  - 열린 씬의 실제 MinimapUI RectTransform을 읽어 프레임을 그 위에 맞춤(오버라이드 반영).
//  - 프레임은 BattleUI 캔버스의 자식으로 추가되어 BattleUI.prefab에 반영(Added GameObject).
//  - 미니맵 로직/06.Map은 건드리지 않음(프레임 시각 배치만).
//
// 메뉴: Tools/UI/① Minimap 프레임 배치 , Tools/UI/② Minimap 프레임 제거
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BagSurvivor.UI.EditorTools
{
    public static class MinimapFrameSetup
    {
        private const string FrameSpritePath = "Assets/04.Images/01.UI/03.Ingames/Minimap_Frame.png";
        private const string BattlePrefabPath = "Assets/03.Prefabs/05.UI/BattleUI.prefab";
        private const string FrameObjectName = "Minimap_Frame";

        [Tooltip("미니맵보다 프레임을 얼마나 크게(가로/세로 각 +px)")]
        private const float Padding = 40f;

        // 프레임을 올릴 HUD 캔버스 이름 우선순위 (항상 보이는 베이스 HUD)
        private static readonly string[] PreferredCanvases = { "L4_HUD2", "L3_HUD" };

        [MenuItem("Tools/UI/① Minimap 프레임 배치", priority = 0)]
        public static void Place()
        {
            Transform minimapT = FindInScene("MinimapUI");
            RectTransform minimap = minimapT != null ? minimapT.GetComponent<RectTransform>() : null;
            if (minimap == null)
            {
                Debug.LogError("[MinimapFrame] 열린 씬에서 'MinimapUI'를 찾지 못했습니다. 02.Ingame 씬을 열고 다시 실행하세요.");
                return;
            }

            Transform battle = FindInScene("BattleUI");
            if (battle == null)
            {
                Debug.LogError("[MinimapFrame] 씬에서 'BattleUI'를 찾지 못했습니다.");
                return;
            }

            Sprite frameSprite = LoadFrameSprite();
            if (frameSprite == null)
            {
                Debug.LogError($"[MinimapFrame] 프레임 스프라이트를 불러오지 못했습니다: {FrameSpritePath}");
                return;
            }

            // 부착할 HUD 캔버스 선택: 선호 이름 → 없으면 첫 Canvas
            Transform parent = null;
            foreach (var n in PreferredCanvases)
            {
                Transform c = FindChildByName(battle, n);
                if (c != null && c.GetComponent<Canvas>() != null) { parent = c; break; }
            }
            if (parent == null)
            {
                Canvas anyCanvas = battle.GetComponentInChildren<Canvas>();
                parent = anyCanvas != null ? anyCanvas.transform : battle;
            }

            // 기존 프레임 재사용 또는 신규 생성
            Transform existing = parent.Find(FrameObjectName);
            bool isNew = existing == null;
            GameObject frameGO = isNew
                ? new GameObject(FrameObjectName, typeof(RectTransform), typeof(Image))
                : existing.gameObject;
            if (isNew) frameGO.transform.SetParent(parent, false);

            // MinimapUI rect를 그대로 복사 + 패딩 (두 캔버스가 동일 스케일러 가정)
            var rt = frameGO.GetComponent<RectTransform>();
            rt.anchorMin = minimap.anchorMin;
            rt.anchorMax = minimap.anchorMax;
            rt.pivot = minimap.pivot;
            rt.anchoredPosition = minimap.anchoredPosition;
            rt.sizeDelta = minimap.sizeDelta + new Vector2(Padding, Padding);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            var img = frameGO.GetComponent<Image>();
            img.sprite = frameSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // BattleUI.prefab에 반영
            GameObject battleRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(battle.gameObject);
            if (battleRoot != null)
            {
                try
                {
                    if (isNew)
                        PrefabUtility.ApplyAddedGameObject(frameGO, BattlePrefabPath, InteractionMode.AutomatedAction);
                    else
                        PrefabUtility.ApplyPrefabInstance(battleRoot, InteractionMode.AutomatedAction);
                    Debug.Log($"[MinimapFrame] 배치 완료 → BattleUI.prefab 반영. pos={rt.anchoredPosition}, size={rt.sizeDelta}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[MinimapFrame] 씬엔 배치됐으나 프리팹 적용 실패: {e.Message}\n" +
                                     "BattleUI 인스턴스에서 우클릭 → Apply All 로 수동 반영하세요.");
                }
            }
            else
            {
                Debug.LogWarning("[MinimapFrame] BattleUI가 프리팹 인스턴스가 아니라 씬에만 배치했습니다. 프리팹 반영은 수동으로 진행하세요.");
            }

            EditorSceneManager.MarkSceneDirty(battle.gameObject.scene);
            Selection.activeGameObject = frameGO;
        }

        [MenuItem("Tools/UI/② Minimap 프레임 제거", priority = 1)]
        public static void Remove()
        {
            Transform battle = FindInScene("BattleUI");
            if (battle == null) return;
            Transform existing = FindChildByName(battle, FrameObjectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                GameObject battleRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(battle.gameObject);
                if (battleRoot != null) PrefabUtility.ApplyPrefabInstance(battleRoot, InteractionMode.AutomatedAction);
                EditorSceneManager.MarkSceneDirty(battle.gameObject.scene);
                Debug.Log("[MinimapFrame] 프레임 제거 완료.");
            }
        }

        private static Sprite LoadFrameSprite()
        {
            // spriteMode=Multiple이므로 서브 에셋 중 Sprite를 찾는다.
            var assets = AssetDatabase.LoadAllAssetsAtPath(FrameSpritePath);
            Sprite first = null;
            foreach (var a in assets)
            {
                if (a is Sprite s)
                {
                    if (s.name == "Minimap_Frame_0") return s;
                    if (first == null) first = s;
                }
            }
            return first;
        }

        private static Transform FindInScene(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
#endif
