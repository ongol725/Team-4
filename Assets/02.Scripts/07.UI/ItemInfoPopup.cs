using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 정보 팝업 싱글톤.
/// ItemInfoPopup.Show(data, gradeIndex, screenPos) / ItemInfoPopup.Hide() 로 제어한다.
///
/// 첫 호출 시 Canvas/Panel을 코드로 자동 생성한다 (프리팹 불필요).
/// L6(인벤/상점) 위 sortingOrder 100에 렌더링된다.
/// </summary>
public class ItemInfoPopup : MonoBehaviour
{
    private static ItemInfoPopup _inst;

    private RectTransform _panelRT;
    private Image         _itemIcon;
    private Text          _nameText;
    private Text          _rarityGradeText;
    private Text          _descText;
    private GameObject    _statsSection;
    private Text          _statsText;
    private GameObject    _synSection;
    private Text          _synText;
    private GameObject    _costSection;
    private Text          _costText;
    private Canvas        _canvas;
    private Coroutine     _hideCoroutine;
    private bool          _isVisible;

    private static readonly string[] RarityLabels = { "일반", "희귀", "영웅", "전설" };
    private static readonly Color[]  RarityColors =
    {
        new Color(0.75f, 0.75f, 0.75f),
        new Color(0.30f, 0.55f, 1.00f),
        new Color(0.65f, 0.25f, 0.95f),
        new Color(1.00f, 0.80f, 0.10f),
    };
    private static readonly Dictionary<SynergyType, string> SynergyNames = new()
    {
        { SynergyType.Assassin,       "암살단"     },
        { SynergyType.SwordMaster,    "소드마스터" },
        { SynergyType.HolyKnight,     "성기사단"   },
        { SynergyType.DemonLord,      "마왕"       },
        { SynergyType.BloodBerserker, "피의광전사" },
        { SynergyType.Tycoon,         "대부호"     },
        { SynergyType.Executioner,    "처형자"     },
        { SynergyType.SpiritMage,     "정령술사"   },
        { SynergyType.GearShift,      "기어시프트" },
        { SynergyType.Pinball,        "핀볼"       },
        { SynergyType.Overload,       "과부하"     },
        { SynergyType.Electro,        "일렉트로"   },
        { SynergyType.Impregnable,    "난공불락"   },
        { SynergyType.Titan,          "티탄"       },
        { SynergyType.Fairy,          "페어리"     },
    };

    // ─────────────────────────────────────────────────────────────
    // Public API

    public static void Show(SO_ItemData data, int gradeIndex, Vector2 screenPos)
    {
        if (data == null) return;
        Ensure();
        _inst.ShowInternal(data, gradeIndex, screenPos);
    }

    public static void Hide()
    {
        if (_inst == null || !_inst._isVisible) return;
        if (_inst._hideCoroutine != null) _inst.StopCoroutine(_inst._hideCoroutine);
        _inst._hideCoroutine = _inst.StartCoroutine(_inst.HideAfterDelay(0.08f));
    }

    // ─────────────────────────────────────────────────────────────

    private static void Ensure()
    {
        if (_inst != null) return;
        var go = new GameObject("ItemInfoPopup");
        DontDestroyOnLoad(go);
        _inst = go.AddComponent<ItemInfoPopup>();
        _inst.BuildUI();
    }

    private void ShowInternal(SO_ItemData data, int gradeIndex, Vector2 screenPos)
    {
        if (_hideCoroutine != null) { StopCoroutine(_hideCoroutine); _hideCoroutine = null; }
        Populate(data, Mathf.Clamp(gradeIndex, 0, 4));
        _panelRT.gameObject.SetActive(true);
        _isVisible = true;
        RepositionNextFrame(screenPos);
    }

    private void RepositionNextFrame(Vector2 screenPos)
    {
        StartCoroutine(DoReposition(screenPos));
    }

    private IEnumerator DoReposition(Vector2 screenPos)
    {
        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRT);
        PlacePopup(screenPos);
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        // timeScale=0(일시정지) 시에도 정상 동작하도록 실시간 대기 사용
        yield return new WaitForSecondsRealtime(delay);
        _panelRT.gameObject.SetActive(false);
        _isVisible     = false;
        _hideCoroutine = null;
    }

    private void PlacePopup(Vector2 screenPos)
    {
        float sf = _canvas.scaleFactor > 0 ? _canvas.scaleFactor : 1f;
        float pw = _panelRT.rect.width  * sf;
        float ph = _panelRT.rect.height * sf;

        const float margin = 14f;
        float sx = screenPos.x + margin;
        float sy = screenPos.y;

        if (sx + pw > Screen.width)  sx = screenPos.x - pw - margin;
        if (sx < 0)                  sx = 0;
        if (sy > Screen.height)      sy = Screen.height;
        if (sy - ph < 0)             sy = ph;

        _panelRT.position = new Vector3(sx, sy, 0f);
    }

    private void Update()
    {
        if (!_isVisible) return;
        PlacePopup(Input.mousePosition);
    }

    // ─────────────────────────────────────────────────────────────
    // 내용 채우기

    private void Populate(SO_ItemData data, int gi)
    {
        _itemIcon.sprite = data.itemImage;
        _itemIcon.color  = data.itemImage != null ? Color.white : Color.clear;
        _nameText.text   = (data is SO_InventoryBlockData)
            ? data.itemName
            : $"{data.itemName}  Lv.{gi + 1}";   // 기획서: 이름 옆 레벨 표기

        if (data is SO_InventoryBlockData)
        {
            _rarityGradeText.text  = "확장 아이템";
            _rarityGradeText.color = new Color(0.25f, 0.80f, 0.35f);
        }
        else
        {
            int ri = Mathf.Clamp((int)data.rarity, 0, RarityColors.Length - 1);
            _rarityGradeText.text  = $"{RarityLabels[ri]}  ·  {gi + 1}등급";
            _rarityGradeText.color = RarityColors[ri];
        }

        _descText.text = data.itemDescription ?? "";

        string stats = BuildStats(data, gi);
        _statsSection.SetActive(!string.IsNullOrEmpty(stats));
        _statsText.text = stats;

        string syn = BuildSynergies(data);
        _synSection.SetActive(!string.IsNullOrEmpty(syn));
        _synText.text = syn;

        bool hasCost = data.cost > 0;
        _costSection.SetActive(hasCost);
        if (hasCost) _costText.text = $"💰  {data.cost} G";
    }

    private static string BuildStats(SO_ItemData data, int gi)
    {
        var sb = new System.Text.StringBuilder();
        if (data is SO_WeaponData wd)
        {
            if (gi < wd.gradeStats.Length)
            {
                var s = wd.gradeStats[gi];
                if (s.attackPower > 0)  sb.AppendLine($"공격력  {s.attackPower}");
                if (s.attackSpeed > 0f) sb.AppendLine($"공격속도  {s.attackSpeed:F1}");
            }
            if (!string.IsNullOrEmpty(wd.attackStyle))
                sb.AppendLine($"공격 방식  {wd.attackStyle}");
            if (!string.IsNullOrEmpty(wd.lv5AttackStyle))
            {
                if (gi >= 4) sb.AppendLine($"5단계 효과  {wd.lv5AttackStyle}");
                else         sb.AppendLine("[잠금] 5레벨 달성 시 특수 능력 개방");  // 4레벨 이하 예고
            }
        }
        else if (data is SO_ArmorData ad)
        {
            if (gi < ad.gradeStats.Length)
            {
                var s = ad.gradeStats[gi];
                if (s.hpBonus > 0) sb.AppendLine($"최대 HP  +{s.hpBonus}");
                if (s.hpRegen > 0) sb.AppendLine($"HP 재생  +{s.hpRegen}/10s");
            }
        }
        else if (data is SO_InventoryBlockData bd)
        {
            if (bd.expandRows != 0) sb.AppendLine($"인벤 행 확장  +{bd.expandRows}");
            if (bd.expandCols != 0) sb.AppendLine($"인벤 열 확장  +{bd.expandCols}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildSynergies(SO_ItemData data)
    {
        if (data.synergies == null) return "";
        var names = new List<string>();
        foreach (var s in data.synergies)
            if (s != SynergyType.None && SynergyNames.TryGetValue(s, out var n))
                names.Add(n);
        return names.Count > 0 ? string.Join("  /  ", names) : "";
    }

    // ─────────────────────────────────────────────────────────────
    // UI 빌드 (런타임 코드 생성)

    private void BuildUI()
    {
        _canvas              = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        var gr = gameObject.AddComponent<GraphicRaycaster>();
        gr.blockingMask = 0; // 팝업 자체는 레이캐스트 차단 안 함

        Font font = (Resources.Load<Font>("Fonts/Galmuri9") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"))
                 ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // ── 패널 루트 ──
        var panelGO = MakeGO("Panel", transform);
        _panelRT    = panelGO.GetComponent<RectTransform>();
        _panelRT.anchorMin = _panelRT.anchorMax = _panelRT.pivot = new Vector2(0f, 1f);
        _panelRT.sizeDelta = new Vector2(270f, 0f);

        var bg = panelGO.AddComponent<Image>();
        // Resources/UI/Popup_Container (빌드 포함). 스프라이트를 Resources로 옮겨 에디터·빌드 공통 로드.
        Sprite containerSprite = Resources.Load<Sprite>("UI/Popup_Container");
        if (containerSprite != null)
        {
            bg.sprite = containerSprite;
            bg.type   = Image.Type.Sliced;
            bg.color  = Color.white;
        }
        else
        {
            bg.color = new Color(0.06f, 0.06f, 0.10f, 0.95f);
            var outline          = panelGO.AddComponent<Outline>();
            outline.effectColor  = new Color(0.38f, 0.38f, 0.50f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        var vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 5f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = panelGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // ── 헤더: 아이콘 + 이름 ──
        var header = MakeHGroup(panelGO.transform, "Header", 8);
        var iconGO = MakeGO("Icon", header.transform);
        _itemIcon  = iconGO.AddComponent<Image>();
        _itemIcon.preserveAspect = true;
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth  = iconLE.minWidth  = 44;
        iconLE.preferredHeight = iconLE.minHeight = 44;
        _nameText = MakeText(header.transform, "Name", font, 14, Color.white, FontStyle.Bold);

        // ── 희귀도 / 등급 ──
        _rarityGradeText = MakeText(panelGO.transform, "RarityGrade", font, 11, Color.gray);

        // ── 구분선 ──
        MakeDivider(panelGO.transform);

        // ── 설명 ──
        _descText = MakeText(panelGO.transform, "Desc", font, 11, new Color(0.82f, 0.82f, 0.82f));

        // ── 스탯 섹션 ──
        _statsSection = MakeSection(panelGO.transform, "StatsSection");
        MakeText(_statsSection.transform, "StatsLabel", font, 11, new Color(0.5f, 0.85f, 1f)).text = "── 스탯 ──";
        _statsText = MakeText(_statsSection.transform, "StatsContent", font, 11, Color.white);

        // ── 시너지 섹션 ──
        _synSection = MakeSection(panelGO.transform, "SynSection");
        MakeText(_synSection.transform, "SynLabel", font, 11, new Color(0.78f, 0.58f, 1f)).text = "── 시너지 ──";
        _synText = MakeText(_synSection.transform, "SynContent", font, 11, Color.white);

        // ── 비용 섹션 (우측 하단 정렬) ──
        _costSection = MakeGO("CostSection", panelGO.transform);
        _costSection.AddComponent<LayoutElement>().flexibleWidth = 1;
        _costText = MakeText(_costSection.transform, "Cost", font, 13, new Color(1f, 0.84f, 0.2f), FontStyle.Bold);
        _costText.alignment = TextAnchor.MiddleRight;

        _panelRT.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────
    // UI 헬퍼

    private static GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject MakeHGroup(Transform parent, string name, float spacing)
    {
        var go  = MakeGO(name, parent);
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing            = spacing;
        hlg.childControlWidth  = hlg.childControlHeight     = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        go.AddComponent<LayoutElement>().flexibleWidth = 1;
        return go;
    }

    private static GameObject MakeSection(Transform parent, string name)
    {
        var go  = MakeGO(name, parent);
        go.AddComponent<LayoutElement>().flexibleWidth = 1;
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2f;
        vlg.childControlWidth  = vlg.childControlHeight     = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go;
    }

    private static Text MakeText(Transform parent, string name, Font font,
        int size, Color color, FontStyle style = FontStyle.Normal)
    {
        var go  = MakeGO(name, parent);
        var txt = go.AddComponent<Text>();
        txt.font               = font;
        txt.fontSize           = size;
        txt.color              = color;
        txt.fontStyle          = style;
        txt.alignment          = TextAnchor.UpperLeft;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow   = VerticalWrapMode.Overflow;
        txt.raycastTarget      = false;
        go.AddComponent<LayoutElement>().flexibleWidth = 1;
        return txt;
    }

    private static void MakeDivider(Transform parent)
    {
        var go  = MakeGO("Divider", parent);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.35f, 0.35f, 0.45f, 0.8f);
        img.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 1f;
        le.flexibleWidth   = 1f;
    }
}
