// ============================================================
// ScreenDamageFlash.cs
// 피격 시 화면 가장자리가 붉게 번쩍이는 비네트(뱀서식)
//  - 중앙은 투명, 가장자리로 갈수록 붉음 → 시야를 가리지 않으면서 피격 강조
//  - 피격 순간 확 나타나고 fadeTime에 걸쳐 사라짐(연타 피격 시 갱신 유지)
//  - 절차 생성 비네트 텍스처 + 코드 생성 오버레이 캔버스(씬/프리팹 무수정)
//  - PlayerHealth가 런타임에 자동 부착
// ============================================================
using UnityEngine;
using UnityEngine.UI;

public class ScreenDamageFlash : MonoBehaviour
{
    [Header("연출")]
    [Tooltip("피격 순간 최대 불투명도(0~1)")] public float peakAlpha = 0.45f;
    [Tooltip("사라지는 데 걸리는 시간(초)")]  public float fadeTime = 0.35f;
    [Tooltip("비네트 색")]                    public Color flashColor = new Color(0.8f, 0.05f, 0.05f);

    private Image _img;
    private GameObject _root; // 오버레이 캔버스 루트
    private float _t;         // 남은 페이드 시간 (0이면 꺼짐)

    private static Texture2D _vignetteTex; // 절차 생성 비네트(전 인스턴스 공유)

    private void Awake()
    {
        Build();
    }

    // 오버레이 캔버스 + 풀스크린 비네트 이미지를 코드로 생성
    private void Build()
    {
        var go = new GameObject("ScreenDamageFlash", typeof(RectTransform), typeof(Canvas));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400; // 게임 HUD 위, 이벤트 배너(500)보다는 아래

        var imgGo = new GameObject("Vignette", typeof(RectTransform));
        var rt = (RectTransform)imgGo.transform;
        rt.SetParent(go.transform, false);
        rt.anchorMin = Vector2.zero;   // 풀스크린 스트레치
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        _img = imgGo.AddComponent<Image>();
        _img.sprite = Sprite.Create(GetVignetteTexture(),
            new Rect(0, 0, _vignetteTex.width, _vignetteTex.height), new Vector2(0.5f, 0.5f));
        _img.raycastTarget = false;
        _img.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

        go.SetActive(false); // 평소엔 캔버스 자체를 꺼둠 (렌더 비용 0)
        _root = go;
    }

    /// <summary>피격 플래시 1회(연타 시 다시 최대로).</summary>
    public void Play()
    {
        if (_img == null) return;
        _t = fadeTime;
        _root.SetActive(true);
    }

    private void Update()
    {
        if (_t <= 0f) return;

        _t -= Time.unscaledDeltaTime; // 일시정지 중에도 자연 소멸
        float a = Mathf.Clamp01(_t / fadeTime) * peakAlpha;
        _img.color = new Color(flashColor.r, flashColor.g, flashColor.b, a);

        if (_t <= 0f) _root.SetActive(false);
    }

    // 절차 생성 비네트: 중앙 투명 → 가장자리 불투명 (부드러운 곡선)
    private static Texture2D GetVignetteTexture()
    {
        if (_vignetteTex != null) return _vignetteTex;

        const int S = 128;
        var center = new Vector2((S - 1) * 0.5f, (S - 1) * 0.5f);
        float maxDist = center.magnitude; // 모서리까지 거리
        _vignetteTex = new Texture2D(S, S, TextureFormat.ARGB32, false);
        _vignetteTex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist; // 0(중앙)~1(모서리)
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d)); // 중앙 45%까진 완전 투명
                _vignetteTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        _vignetteTex.Apply();
        return _vignetteTex;
    }
}
