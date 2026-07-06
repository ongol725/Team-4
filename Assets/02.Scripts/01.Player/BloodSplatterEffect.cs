// ============================================================
// BloodSplatterEffect.cs
// 플레이어 피격 핏방울 이펙트 — 빨간 원 파티클 버스트
//  - 피격 순간 작은 빨간 원 4~6개가 사방으로 튀어 중력 낙하 + 페이드아웃
//  - 아트 에셋 없이 절차 생성 원 텍스처 사용(임시 대체)
//  - ParticleSystem 1개 재사용(Emit) — 오브젝트 생성/파괴 없음, 드로우콜 1 (풀링 불필요)
//  - PlayerHealth가 런타임에 자동 부착 → 씬/프리팹 수정 없음(작업 충돌 방지)
// ============================================================
using UnityEngine;

public class BloodSplatterEffect : MonoBehaviour
{
    [Header("버스트")]
    [Tooltip("피격 1회당 핏방울 개수 최소")] public int dropsMin = 4;
    [Tooltip("피격 1회당 핏방울 개수 최대")] public int dropsMax = 6;

    private ParticleSystem _ps;
    private static Texture2D _circleTex; // 절차 생성 원(전 인스턴스 공유)

    private void Awake()
    {
        Build();
    }

    // 자식 오브젝트에 파티클 시스템을 코드로 구성
    private void Build()
    {
        var go = new GameObject("BloodSplatter");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        _ps = go.AddComponent<ParticleSystem>();
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _ps.main;
        main.playOnAwake     = false;
        main.loop            = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // 이동 중 피격해도 방울이 제자리에 남음
        main.maxParticles    = 64;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 3f);  // 사방으로 튀는 초기 속도
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.gravityModifier = 2f;                                        // 핏방울 낙하
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.08f, 0.08f),   // 선홍
            new Color(0.45f, 0.02f, 0.02f));  // 검붉음

        var emission = _ps.emission;
        emission.rateOverTime = 0f; // 수동 Emit 전용

        var shape = _ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.12f;

        // 수명 끝으로 갈수록 알파 페이드아웃
        var col = _ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // 렌더러: 원 텍스처 + 스프라이트 셰이더 (캐릭터보다 앞에 표시)
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = GetCircleTexture();
        renderer.material = mat;
        renderer.sortingOrder = 30;
    }

    /// <summary>피격 지점에서 핏방울 버스트 1회.</summary>
    public void Play(Vector3 pos)
    {
        if (_ps == null) return;
        _ps.transform.position = pos;
        _ps.Emit(Random.Range(dropsMin, dropsMax + 1));
    }

    // 절차 생성 원 텍스처(안티앨리어싱 가장자리) — 아트 교체 전 임시
    private static Texture2D GetCircleTexture()
    {
        if (_circleTex != null) return _circleTex;

        const int S = 32;
        float r = S * 0.5f - 1f;
        var center = new Vector2(S * 0.5f - 0.5f, S * 0.5f - 0.5f);
        _circleTex = new Texture2D(S, S, TextureFormat.ARGB32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float a = Mathf.Clamp01(r - Vector2.Distance(new Vector2(x, y), center) + 0.5f);
                _circleTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        _circleTex.Apply();
        return _circleTex;
    }
}
