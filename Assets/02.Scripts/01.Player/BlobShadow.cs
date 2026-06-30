using UnityEngine;

/// <summary>
/// 캐릭터(몬스터/플레이어) 발밑에 납작한 타원 그림자를 그린다.
///  - 그림자 스프라이트는 코드로 1회 생성(소프트 검정 타원) 후 전역 공유.
///  - 설정값은 Resources/ShadowSettings.asset 한 곳에서 읽음(인스펙터로 전체 일괄 조절).
///  - 캐릭터보다 한 단계 뒤에 렌더 → 바닥 위·캐릭터 아래.
///  - OnEnable마다 크기/위치 재계산(오브젝트 풀링 재사용 대응).
/// ponytail: 루트 스케일이 균등(uniform)하다고 가정. 비균등 스케일이면 그림자가 찌그러질 수 있음.
/// </summary>
public class BlobShadow : MonoBehaviour
{
    // 폴백 기본값 (Resources/ShadowSettings.asset 이 없을 때)
    const float DefWidthRatio = 0.8f, DefHeightRatio = 0.4f, DefAlpha = 0.35f, DefFeetYOffset = 0.05f;

    static Sprite sharedSprite;
    static ShadowSettings settings;
    static bool settingsLoaded;
    static ShadowSettings Settings
    {
        get
        {
            if (!settingsLoaded) { settings = Resources.Load<ShadowSettings>("ShadowSettings"); settingsLoaded = true; }
            return settings;
        }
    }

    SpriteRenderer ownerSprite;  // 부모의 메인(가장 큰) 스프라이트
    SpriteRenderer shadowSr;     // 그림자 렌더러
    bool isPlayer;               // true면 playerWidthMultiplier 추가 적용

    /// <summary>플레이어가 부착 직후 호출 — 플레이어 전용 폭 배수를 적용한다.</summary>
    public void MarkAsPlayer() { isPlayer = true; Refresh(); }

    void OnEnable()
    {
        EnsureShadow();
        Refresh();
    }

#if UNITY_EDITOR
    // 에디터에서 ShadowSettings를 실시간 반영(플레이어처럼 재활성 안 되는 대상도 갱신). 빌드엔 미포함.
    void LateUpdate() { Refresh(); }
#endif

    void EnsureShadow()
    {
        if (shadowSr != null) return;
        if (sharedSprite == null) sharedSprite = CreateEllipseSprite();

        var go = new GameObject("BlobShadow");
        go.transform.SetParent(transform, false);
        shadowSr = go.AddComponent<SpriteRenderer>();
        shadowSr.sprite = sharedSprite;
    }

    void Refresh()
    {
        if (shadowSr == null) return;

        var st = Settings;
        float widthRatio  = st != null ? st.widthRatio  : DefWidthRatio;
        float heightRatio = st != null ? st.heightRatio : DefHeightRatio;
        float alpha       = st != null ? st.alpha       : DefAlpha;
        float feetY       = st != null ? st.feetYOffset : DefFeetYOffset;
        if (isPlayer && st != null) widthRatio *= st.playerWidthMultiplier; // 플레이어 전용 축소/확대

        // 메인 스프라이트 = 자식 중 가장 폭이 큰 스프라이트(그림자 자신 제외)
        if (ownerSprite == null)
        {
            float bestW = 0f;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == shadowSr || sr.sprite == null) continue;
                float w = sr.bounds.size.x;
                if (w > bestW) { bestW = w; ownerSprite = sr; }
            }
        }
        if (ownerSprite == null) return;

        Bounds b = ownerSprite.bounds;                 // 월드 기준
        float width = b.size.x * widthRatio;
        if (width <= 0f) return;

        // 그림자 스프라이트는 PPU=텍스처폭 → 스케일 1에서 폭 1유닛(높이 0.5유닛).
        float lossy = transform.lossyScale.x;
        float s = (lossy != 0f) ? width / lossy : width;
        shadowSr.transform.localScale = new Vector3(s, s * (heightRatio / 0.5f), 1f);

        // 발밑(스프라이트 하단 중앙)에 배치 — 자식이라 이후 부모 따라 이동
        shadowSr.transform.position = new Vector3(b.center.x, b.min.y + feetY, 0f);

        // 진하기 + 캐릭터보다 한 단계 뒤
        shadowSr.color = new Color(0f, 0f, 0f, alpha);
        shadowSr.sortingLayerID = ownerSprite.sortingLayerID;
        shadowSr.sortingOrder   = ownerSprite.sortingOrder - 1;
    }

    // 64x32 소프트 타원 텍스처 → 폭 1유닛 스프라이트(2:1 비율)
    static Sprite CreateEllipseSprite()
    {
        const int W = 64, H = 32;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[W * H];
        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float nx = (x - cx) / cx;          // -1..1
                float ny = (y - cy) / cy;
                float a = Mathf.Clamp01(1f - (nx * nx + ny * ny)); // 타원 내부 1 → 경계 0
                a *= a;                            // 가장자리 더 부드럽게
                px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W);
    }
}
