// ============================================================
// DashStaminaUI.cs
// 캐릭터 머리 위 대시 스태미너 원형 인디케이터
//  - 쿨타임 동안 파이(Radial360)가 시계 방향으로 차오름
//  - 가득 차면(대시 가능) 청록색으로 바뀌고 완충 순간 살짝 커졌다 복귀(펄스)
//  - PlayerMovement가 런타임에 자동 부착 → 씬/프리팹 수정 없음(작업 충돌 방지)
// ============================================================
using UnityEngine;
using UnityEngine.UI;

public class DashStaminaUI : MonoBehaviour
{
    [Header("위치/크기")]
    public Vector3 offset  = new Vector3(0f, 1.05f, 0f); // 머리 위 오프셋(월드)
    public float diameter  = 0.34f;                      // 원 지름(월드 유닛)

    [Header("색상")]
    public Color chargingColor = new Color(1f,    0.85f, 0.35f, 0.95f); // 차오르는 파이(골드)
    public Color readyColor    = new Color(0.45f, 1f,    0.95f, 1f);    // 가득 = 대시 가능(청록, 잔상색 톤)
    public Color backColor     = new Color(0f,    0f,    0f,    0.45f); // 배경 원(반투명 검정)

    [Header("완충 펄스")]
    public float pulseScale    = 1.35f; // 완충 순간 최대 배율
    public float pulseDuration = 0.25f; // 펄스 지속(초)

    private PlayerMovement _pm;
    private Transform _root;      // 게이지 루트(플레이어 자식)
    private Image _fill;
    private Vector3 _baseScale;
    private bool  _wasFull;
    private float _pulse;         // 펄스 잔여 시간

    private static Sprite _circleSprite; // 원형 스프라이트 캐시(전 인스턴스 공유)

    private void Start()
    {
        _pm = GetComponent<PlayerMovement>();
        if (_pm == null) { enabled = false; return; }
        Build();
    }

    // 독립 오브젝트이므로 플레이어 소멸 시 게이지도 함께 정리
    private void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
    }

    // 월드스페이스 캔버스 + 배경/파이 이미지를 코드로 생성.
    // 플레이어 '자식'이 아닌 독립 오브젝트: 로드아웃 빌더 등이 플레이어 자식을
    // 정리할 때 함께 파괴되지 않도록 하고, 위치는 LateUpdate에서 따라간다.
    private void Build()
    {
        // 이전 게이지가 남아있으면 제거(중복 생성 방지)
        if (_root != null) Destroy(_root.gameObject);

        // 주의: Canvas를 생성 '후' AddComponent하면 기존 Transform이 RectTransform으로
        // 교체되어, 먼저 잡아둔 transform 참조가 죽은 객체가 된다(무한 재생성 루프 원인).
        // → 생성 시점에 RectTransform+Canvas를 함께 부여하고 그 뒤에 참조를 잡는다.
        var go = new GameObject("DashStaminaUI", typeof(RectTransform), typeof(Canvas));
        _root = go.transform;
        _root.position = transform.position + offset;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.sortingOrder = 40; // 캐릭터/이펙트 위, 스크린 HUD와는 무관

        var rt = (RectTransform)_root;
        rt.sizeDelta = new Vector2(100f, 100f);
        _baseScale = Vector3.one * (diameter / 100f);
        rt.localScale = _baseScale;

        var sprite = GetCircleSprite();

        // 배경 원(항상 표시 — 게이지 위치를 인지시킴)
        var bg = CreateImage(go.transform, "BG", sprite, backColor);
        bg.rectTransform.anchorMin = Vector2.zero;
        bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.sizeDelta = Vector2.zero;

        // 차오르는 파이(배경보다 살짝 작게 → 테두리처럼 보임)
        _fill = CreateImage(go.transform, "Fill", sprite, chargingColor);
        _fill.rectTransform.anchorMin = Vector2.zero;
        _fill.rectTransform.anchorMax = Vector2.one;
        _fill.rectTransform.sizeDelta = new Vector2(-14f, -14f);
        _fill.type          = Image.Type.Filled;
        _fill.fillMethod    = Image.FillMethod.Radial360;
        _fill.fillOrigin    = (int)Image.Origin360.Top;
        _fill.fillClockwise = true;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite        = sprite;
        img.color         = color;
        img.raycastTarget = false;
        return img;
    }

    // 절차 생성 원형 스프라이트(안티앨리어싱 가장자리).
    // 내장 Knob은 런타임 로드 불가(에디터 전용)라 시도하지 않는다.
    private static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        const int S = 64;
        float r = S * 0.5f - 1f;
        var center = new Vector2(S * 0.5f - 0.5f, S * 0.5f - 0.5f);
        var tex = new Texture2D(S, S, TextureFormat.ARGB32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float a = Mathf.Clamp01(r - Vector2.Distance(new Vector2(x, y), center) + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return _circleSprite;
    }

    private void LateUpdate()
    {
        if (_pm == null) return;

        // 외부에서 게이지 오브젝트가 파괴됐으면 자가 복구(예: 씬 정리 루틴에 휩쓸린 경우)
        if (_root == null || _fill == null)
        {
            Build();
            if (_root == null || _fill == null) return;
        }

        // 독립 오브젝트이므로 매 프레임 플레이어 머리 위로 따라붙기
        _root.position = transform.position + offset;

        float charge = _pm.DashCharge01;
        bool  full   = charge >= 1f;

        _fill.fillAmount = charge;
        _fill.color      = full ? readyColor : chargingColor;

        // 완충 순간 1회 펄스(커졌다 원래 크기로) — "지금 대시 가능" 강조
        if (full && !_wasFull) _pulse = pulseDuration;
        _wasFull = full;

        if (_pulse > 0f)
        {
            _pulse -= Time.deltaTime;
            float k = 1f + (pulseScale - 1f) * Mathf.Clamp01(_pulse / pulseDuration);
            _root.localScale = _baseScale * k;
        }
        else if (_root.localScale != _baseScale)
        {
            _root.localScale = _baseScale;
        }
    }
}
