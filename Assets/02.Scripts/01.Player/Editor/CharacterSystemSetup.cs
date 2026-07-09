// ============================================================
// CharacterSystemSetup.cs  (Editor 전용)
//
// Unity 메뉴: BagSurvivor > Setup > Create Character System
//
// 한 번 실행하면:
//  1) Assets/Resources/ScriptableObjects/Characters/ 에 SO 에셋 3개 생성
//  2) Assets/03.Prefabs/06.Gimmicks/CharacterManager.prefab 생성
//  3) Assets/03.Prefabs/05.UI/CharacterSelectUI.prefab 생성
// ============================================================
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BagSurvivor.CharacterEditor
{
    public static class CharacterSystemSetup
    {
        private const string SoFolder      = "Assets/Resources/ScriptableObjects/Characters";
        private const string GimmickFolder = "Assets/03.Prefabs/06.Gimmicks";
        private const string UiFolder      = "Assets/03.Prefabs/05.UI";

        [MenuItem("BagSurvivor/Setup/Create Character System")]
        public static void Run()
        {
            EnsureFolder("Assets/09.ScriptableObjects");
            EnsureFolder(SoFolder);

            // 1. SO 에셋 ---------------------------------------------------
            var warrior = GetOrCreateSO("SO_Warrior", CharacterType.Warrior, "전사",   25, 0.7f, 0.9f,  7f, 0f, "체력이 높은 탱커");
            var mage    = GetOrCreateSO("SO_Mage",    CharacterType.Mage,    "마법사", 11, 1.1f, 1.1f, 10f, 0f, "밸런스 잡힌 딜러");
            var rogue   = GetOrCreateSO("SO_Rogue",   CharacterType.Rogue,   "도적",    8, 0.6f, 1.3f, 13f, 0f, "빠른 공속의 딜러");

            // 2. CharacterManager 프리팹 ------------------------------------
            CreateManagerPrefab(warrior);

            // 3. CharacterSelectUI 프리팹 -----------------------------------
            CreateSelectUIPrefab(warrior, mage, rogue);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[CharacterSystemSetup] 완료!\n" +
                $"  SO  → {SoFolder}/\n" +
                $"  MGR → {GimmickFolder}/CharacterManager.prefab\n" +
                $"  UI  → {UiFolder}/CharacterSelectUI.prefab\n\n" +
                "씬에 CharacterManager 프리팹을 GameManager 옆에 배치하세요.\n" +
                "캐릭터 선택창은 CharacterSelectUI 프리팹을 씬에 올리거나\n" +
                "Lobby 씬 담당자에게 전달하세요."
            );
        }

        // ── Build Settings 씬 등록 ────────────────────────────────────────

        [MenuItem("BagSurvivor/Setup/Add All Scenes to Build Settings")]
        public static void AddScenesToBuild()
        {
            string[] paths =
            {
                "Assets/01.Scenes/01.Lobby.unity",
                "Assets/01.Scenes/99.InventoryStore.unity",
                "Assets/01.Scenes/02.Ingame.unity",
                "Assets/01.Scenes/00.TestBattle.unity",
                "Assets/01.Scenes/06.BossRoom.unity",
            };

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string p in paths)
            {
                if (!System.IO.File.Exists(p)) continue;
                if (list.Exists(s => s.path == p)) continue;
                list.Add(new EditorBuildSettingsScene(p, true));
                Debug.Log($"[Build Settings] 추가: {p}");
            }
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[CharacterSystemSetup] Build Settings 업데이트 완료!");
        }

        // ── 씬 배치 ───────────────────────────────────────────────────────

        [MenuItem("BagSurvivor/Setup/Place Character Prefabs in Lobby")]
        public static void PlaceInLobby()
        {
            const string scenePath   = "Assets/01.Scenes/01.Lobby.unity";
            const string managerPath = GimmickFolder + "/CharacterManager.prefab";
            const string uiPath      = UiFolder      + "/CharacterSelectUI.prefab";

            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogError($"[CharacterSystemSetup] 씬을 찾을 수 없음: {scenePath}");
                return;
            }

            // 현재 씬 저장 후 로비 씬 열기
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            bool changed = false;

            // CharacterManager 배치 (중복 방지)
            if (Object.FindFirstObjectByType<CharacterManager>() == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(managerPath);
                if (prefab != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    go.name = "CharacterManager";
                    changed = true;
                    Debug.Log("[CharacterSystemSetup] CharacterManager 배치 완료");
                }
                else
                    Debug.LogWarning($"[CharacterSystemSetup] 프리팹 없음: {managerPath}\n'Create Character System' 메뉴를 먼저 실행하세요.");
            }
            else
                Debug.Log("[CharacterSystemSetup] CharacterManager 이미 씬에 존재 — 스킵");

            // CharacterSelectUI 배치 — 항상 교체 (Override 누적 방지)
            var existingUI = Object.FindFirstObjectByType<CharacterSelectUI>();
            if (existingUI != null)
            {
                Object.DestroyImmediate(existingUI.gameObject);
                changed = true;
                Debug.Log("[CharacterSystemSetup] 기존 CharacterSelectUI 제거");
            }
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(uiPath);
                if (prefab != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    go.name = "CharacterSelectUI";
                    changed = true;
                    Debug.Log("[CharacterSystemSetup] CharacterSelectUI 새 인스턴스 배치 완료");
                }
                else
                    Debug.LogWarning($"[CharacterSystemSetup] 프리팹 없음: {uiPath}\n'Create Character System' 메뉴를 먼저 실행하세요.");
            }

            // EventSystem 없으면 추가 (없으면 버튼 클릭 자체가 안 됨)
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
                EditorSceneManager.MoveGameObjectToScene(esGO, scene);
                changed = true;
                Debug.Log("[CharacterSystemSetup] EventSystem 추가");
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[CharacterSystemSetup] 01.Lobby.unity 저장 완료!");
            }
        }

        // ── SO 에셋 ───────────────────────────────────────────────────────

        static SO_CharacterData GetOrCreateSO(
            string fileName, CharacterType type, string charName,
            int hp, float atk, float atkSpd, float spd, float crit, string desc)
        {
            string path = $"{SoFolder}/{fileName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<SO_CharacterData>(path);
            bool isNew = so == null;
            if (isNew) so = ScriptableObject.CreateInstance<SO_CharacterData>();

            // 기존 에셋도 항상 전체 갱신 (characterName 등 누락 방지)
            so.characterType         = type;
            so.characterName         = charName;
            so.maxHp                 = hp;
            so.attackMultiplier      = atk;
            so.attackSpeedMultiplier = atkSpd;
            so.moveSpeed             = spd;
            so.critChance            = crit;
            so.description           = desc;

            if (isNew) AssetDatabase.CreateAsset(so, path);
            else       EditorUtility.SetDirty(so);

            return so;
        }

        // ── CharacterManager 프리팹 ───────────────────────────────────────

        static void CreateManagerPrefab(SO_CharacterData defaultChar)
        {
            string path = $"{GimmickFolder}/CharacterManager.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = new GameObject("CharacterManager");
            var cm = go.AddComponent<CharacterManager>();
            cm.defaultCharacter = defaultChar;
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        // ── CharacterSelectUI 프리팹 ──────────────────────────────────────

        static void CreateSelectUIPrefab(SO_CharacterData warrior, SO_CharacterData mage, SO_CharacterData rogue)
        {
            string path = $"{UiFolder}/CharacterSelectUI.prefab";

            // 루트 (CharacterSelectUI 컴포넌트 보유)
            var root     = new GameObject("CharacterSelectUI");
            var selectUI = root.AddComponent<CharacterSelectUI>();

            // Canvas
            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(root.transform, false);
            var canvas            = canvasGO.AddComponent<Canvas>();
            canvas.renderMode     = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder   = 10;
            var scaler                    = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode            = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution    = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight     = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // 배경
            var bg = MakePanel(canvasGO.transform, "BG", new Color(0.07f, 0.07f, 0.1f));
            Stretch(bg);

            // 타이틀
            var titleT = MakeText(canvasGO.transform, "Title", "캐릭터 선택", 60, FontStyle.Bold, Color.white);
            Anchor(titleT, 0f, 0.88f, 1f, 1f);
            selectUI.titleText = titleT;

            // 카드 컨테이너
            var cardsGO = new GameObject("Cards");
            cardsGO.transform.SetParent(canvasGO.transform, false);
            cardsGO.AddComponent<RectTransform>();
            Anchor(cardsGO.GetComponent<RectTransform>(), 0.03f, 0.16f, 0.97f, 0.87f);
            var hlg                   = cardsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 30f;
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;

            // 카드 3장
            SO_CharacterData[] datas = { warrior, mage, rogue };
            var cards = new CharacterCardUI[3];
            for (int i = 0; i < 3; i++)
                cards[i] = BuildCard(cardsGO.transform, datas[i].characterName);

            // 확인 버튼
            var confirmGO = MakeButton(canvasGO.transform, "Btn_Confirm", "게임 시작");
            Anchor(confirmGO.GetComponent<RectTransform>(), 0.38f, 0.03f, 0.62f, 0.13f);

            // 필드 연결
            selectUI.characters    = datas;
            selectUI.cards         = cards;
            selectUI.confirmButton = confirmGO.GetComponent<Button>();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        // ── 카드 한 장 빌드 ───────────────────────────────────────────────

        static CharacterCardUI BuildCard(Transform parent, string charName)
        {
            var go = new GameObject($"Card_{charName}");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            // 배경 이미지 + 버튼
            var bgImg  = go.AddComponent<Image>();
            bgImg.color = new Color(0.18f, 0.18f, 0.23f, 0.95f);
            var btn    = go.AddComponent<Button>();
            var cc     = btn.colors;
            cc.highlightedColor = new Color(0.28f, 0.28f, 0.36f);
            cc.pressedColor     = new Color(0.12f, 0.12f, 0.18f);
            btn.colors = cc;

            var card          = go.AddComponent<CharacterCardUI>();
            card.cardBackground = bgImg;
            card.cardButton     = btn;

            // 캐릭터 이미지 (상단)
            var charImgGO = new GameObject("CharImg");
            charImgGO.transform.SetParent(go.transform, false);
            var charImg            = charImgGO.AddComponent<Image>();
            charImg.color          = new Color(1f, 1f, 1f, 0.5f);
            charImg.preserveAspect = true;
            Anchor(charImgGO.GetComponent<RectTransform>(), 0.12f, 0.52f, 0.88f, 0.95f);
            card.characterImage = charImg;

            // 구분선
            var line = MakePanel(go.transform, "Line", new Color(1f, 1f, 1f, 0.15f));
            Anchor(line.GetComponent<RectTransform>(), 0.06f, 0.50f, 0.94f, 0.515f);

            // 스탯 그룹 — 이름을 첫 행으로 포함 (7행)
            var statsGO = new GameObject("Stats");
            statsGO.transform.SetParent(go.transform, false);
            statsGO.AddComponent<RectTransform>();
            Anchor(statsGO.GetComponent<RectTransform>(), 0.06f, 0.08f, 0.94f, 0.50f);
            var vlg                    = statsGO.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment         = TextAnchor.MiddleLeft;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = true;
            vlg.spacing = 1f;

            // 이름을 스탯 영역 첫 행에 배치
            card.nameText     = MakeText(statsGO.transform, "Name",   charName, 20, FontStyle.Bold,   new Color(1f,    0.88f, 0.2f));
            card.hpText       = MakeText(statsGO.transform, "HP",     "HP",     16, FontStyle.Normal, new Color(0.65f, 0.9f,  1f));
            card.atkText      = MakeText(statsGO.transform, "ATK",    "공격력", 16, FontStyle.Normal, new Color(1f,    0.7f,  0.45f));
            card.atkSpeedText = MakeText(statsGO.transform, "ATKSPD", "공격속도",16, FontStyle.Normal, new Color(0.55f, 1f,   0.55f));
            card.speedText    = MakeText(statsGO.transform, "SPD",    "스피드", 16, FontStyle.Normal, new Color(1f,    0.95f, 0.4f));
            card.dpsText      = MakeText(statsGO.transform, "DPS",    "DPS",    16, FontStyle.Normal, new Color(1f,    0.75f, 0.0f));
            card.critText     = MakeText(statsGO.transform, "CRIT",   "치명타", 16, FontStyle.Normal, new Color(1f,    0.45f, 0.45f));

            // 설명
            var descT = MakeText(go.transform, "Desc", "", 15, FontStyle.Italic, new Color(0.65f, 0.65f, 0.65f));
            Anchor(descT.GetComponent<RectTransform>(), 0.05f, 0.01f, 0.95f, 0.09f);
            card.descText = descT;

            return card;
        }

        // ── UI 생성 유틸 ─────────────────────────────────────────────────

        static Image MakePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img   = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static Text MakeText(Transform parent, string name, string content,
            int size, FontStyle style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var t       = go.AddComponent<Text>();
            t.text      = content;
            t.fontSize  = size;
            t.fontStyle = style;
            t.color     = color;
            t.alignment = TextAnchor.MiddleCenter;
            return t;
        }

        static GameObject MakeButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var img   = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.55f, 0.95f);
            var btn   = go.AddComponent<Button>();
            var cc    = btn.colors;
            cc.highlightedColor = new Color(0.3f, 0.7f, 1f);
            cc.pressedColor     = new Color(0.1f, 0.4f, 0.8f);
            btn.colors = cc;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            Stretch(labelGO.AddComponent<RectTransform>());
            var t       = labelGO.AddComponent<Text>();
            t.text      = label;
            t.fontSize  = 36;
            t.fontStyle = FontStyle.Bold;
            t.color     = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            return go;
        }

        // ── RectTransform 유틸 ───────────────────────────────────────────

        static void Anchor(Text t, float xMin, float yMin, float xMax, float yMax)
            => Anchor(t.GetComponent<RectTransform>(), xMin, yMin, xMax, yMax);

        static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform rt)
            => Anchor(rt, 0f, 0f, 1f, 1f);

        static void Stretch(Image img)
            => Stretch(img.GetComponent<RectTransform>());

        // ── 폴더 생성 유틸 ───────────────────────────────────────────────

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent ?? "Assets", folder);
        }
    }
}
