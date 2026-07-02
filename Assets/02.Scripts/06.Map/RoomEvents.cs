using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
// RoomEvents.cs
// 특수 이벤트 방 시스템 공용 정의:
//  - RoomEventType : 방 이벤트 종류
//  - RoomEventInfo : 이름/설명/색 (배너·미니맵 공용)
//  - RoomEventState: 전역 상태(골드 배율 등)
//  - RoomEventBanner: 입장 시 방 이름(크게)+설명(작게) 3초 배너
// (실제 적용/트리거는 RoomMonsterSpawner가 담당)
// ============================================================

public enum RoomEventType
{
    None,
    Strong,     // 강자의 방: 처치 골드 +100%, 적 공격력 +100%
    Swamp,      // 늪의 방: 플레이어·적 이동속도 -50%
    Challenge,  // 도전의 방: 엘리트 소환, 2분 생존 시 골드(층×1000)
}

public static class RoomEventInfo
{
    public static string Name(RoomEventType t) => t switch
    {
        RoomEventType.Strong    => "강자의 방",
        RoomEventType.Swamp     => "늪의 방",
        RoomEventType.Challenge => "도전의 방",
        _                       => "",
    };

    public static string Desc(RoomEventType t) => t switch
    {
        RoomEventType.Strong    => "처치 골드 +100% · 적 공격력 +100%",
        RoomEventType.Swamp     => "플레이어·적 이동속도 -50%",
        RoomEventType.Challenge => "엘리트 등장! 2분 버티면 골드 보상 (나가면 포기)",
        _                       => "",
    };

    public static Color Color(RoomEventType t) => t switch
    {
        RoomEventType.Strong    => new Color(1.00f, 0.55f, 0.15f), // 주황
        RoomEventType.Swamp     => new Color(0.40f, 0.75f, 0.35f), // 초록
        RoomEventType.Challenge => new Color(0.75f, 0.35f, 1.00f), // 보라
        _                       => UnityEngine.Color.white,
    };
}

/// <summary>이벤트 방 전역 상태(골드 배율 등). 방 진입/이탈 시 갱신.</summary>
public static class RoomEventState
{
    public static float GoldMultiplier = 1f; // 강자의 방: 2

    public static void Reset() => GoldMultiplier = 1f;
}

/// <summary>방 이름(크게)+설명(작게)을 화면 상단에 3초간 띄우는 배너. 자체 캔버스 생성.</summary>
public class RoomEventBanner : MonoBehaviour
{
    private static RoomEventBanner _inst;
    private CanvasGroup _cg;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _desc;
    private float _timer;
    private static TMP_FontAsset _font;

    public static void Show(string title, string desc, Color color)
    {
        if (_inst == null) _inst = Create();
        if (_inst != null) _inst.Play(title, desc, color);
    }

    private static RoomEventBanner Create()
    {
        if (_font == null) _font = Resources.Load<TMP_FontAsset>("Fonts/BoldDunggeunmo SDF Damage");

        var go = new GameObject("RoomEventBanner", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(go);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var b = go.AddComponent<RoomEventBanner>();

        // 상단 중앙 컨테이너
        var panel = new GameObject("Panel", typeof(RectTransform));
        var prt = (RectTransform)panel.transform;
        prt.SetParent(go.transform, false);
        prt.anchorMin = new Vector2(0.5f, 1f);
        prt.anchorMax = new Vector2(0.5f, 1f);
        prt.pivot = new Vector2(0.5f, 1f);
        prt.anchoredPosition = new Vector2(0f, -120f);
        prt.sizeDelta = new Vector2(900f, 150f);

        b._cg = panel.AddComponent<CanvasGroup>();
        b._cg.alpha = 0f;

        b._title = MakeText(prt, 64, new Vector2(0f, -10f), FontStyles.Bold);
        b._desc  = MakeText(prt, 30, new Vector2(0f, -85f), FontStyles.Normal);

        go.SetActive(true);
        return b;
    }

    private static TextMeshProUGUI MakeText(RectTransform parent, float size, Vector2 pos, FontStyles style)
    {
        var go = new GameObject("Txt", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(900f, 80f);

        var t = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }

    private void Play(string title, string desc, Color color)
    {
        _title.text = title;
        _title.color = color;
        _desc.text = desc;
        _desc.color = Color.white;
        _timer = 3f;         // 표시 시간
        _cg.alpha = 1f;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (_timer <= 0f) return;
        _timer -= Time.unscaledDeltaTime; // 일시정지 중에도 배너는 흐름
        if (_timer <= 0.5f) _cg.alpha = Mathf.Clamp01(_timer / 0.5f); // 마지막 0.5초 페이드아웃
    }
}
