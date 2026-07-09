// ============================================================
// PixelPerfectUIScaler.cs
// 픽셀 폰트(Galmuri9) 선명도 유지용 전역 캔버스 배율 락.
//
// 문제: CanvasScaler(ScaleWithScreenSize)는 창 크기가 참조 해상도(1920x1080)의
//       비정수 배일 때 소수 배율(예: 0.667)을 만든다. 픽셀 폰트는 이 소수 배율에서
//       글자가 native 격자(9px)에 안 맞게 래스터되어 획이 뭉치거나 빠져 "깨짐"이 발생한다.
//       (빌드 창모드에서 특히 두드러짐)
//
// 해결: 모든 ScaleWithScreenSize 캔버스를 런타임에 수집해 배율을
//       "정수(1,2,3…) 또는 1/정수(1/2,1/3…)"로 스냅한다. 이러면 glyph 래스터가
//       항상 native 픽셀의 정수 배로 떨어져 어떤 창 크기에서도 선명하다.
//       씬/프리팹을 수정하지 않으므로 팀 작업 충돌이 없고, 런타임 생성 캔버스도 자동 처리.
//
// [RuntimeInitializeOnLoadMethod]로 자동 생성 — 씬 배치 불필요.
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PixelPerfectUIScaler : MonoBehaviour
{
    private static PixelPerfectUIScaler _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;
        var go = new GameObject("PixelPerfectUIScaler");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PixelPerfectUIScaler>();
    }

    // 모드를 ConstantPixelSize로 바꾸면 원본 참조 해상도/매치값이 무의미해지므로 최초 스캔 시점에 보존한다.
    private struct Entry { public CanvasScaler scaler; public Vector2 refRes; public float match; }
    private readonly List<Entry> _entries = new();

    private int   _lastW, _lastH;
    private float _rescanTimer;

    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
    private void OnSceneLoaded(Scene s, LoadSceneMode m) { Rescan(); Apply(); }

    private void Start() { Rescan(); Apply(); }

    private void Update()
    {
        // 창 크기 변경 즉시 반영
        if (Screen.width != _lastW || Screen.height != _lastH) Apply();

        // 런타임 생성 캔버스(아이템 툴팁·시너지 캔버스 등)를 잡기 위한 가벼운 주기적 재스캔
        _rescanTimer -= Time.unscaledDeltaTime;
        if (_rescanTimer <= 0f) { _rescanTimer = 0.5f; Rescan(); Apply(); }
    }

    /// <summary>씬 내 CanvasScaler를 수집한다. ScaleWithScreenSize 인 것만 관리 대상으로 삼고
    /// ConstantPixelSize로 전환한다(직접 배율을 주입하기 위해). 이미 관리 중이면 건너뛴다.</summary>
    private void Rescan()
    {
        _entries.RemoveAll(e => e.scaler == null);

        var found = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var sc in found)
        {
            if (sc == null) continue;
            if (sc.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue; // 이미 상수/관리대상 or 의도적 상수

            bool known = false;
            foreach (var e in _entries) if (e.scaler == sc) { known = true; break; }
            if (known) continue;

            Vector2 refRes = sc.referenceResolution;
            if (refRes.x < 1f || refRes.y < 1f) refRes = new Vector2(1920f, 1080f);

            _entries.Add(new Entry { scaler = sc, refRes = refRes, match = Mathf.Clamp01(sc.matchWidthOrHeight) });
            sc.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // 이후 scaleFactor를 우리가 직접 세팅
        }
    }

    /// <summary>현재 창 크기에 맞춰 각 캔버스의 배율을 clean 값으로 스냅한다.</summary>
    private void Apply()
    {
        _lastW = Screen.width;
        _lastH = Screen.height;
        if (_lastW <= 0 || _lastH <= 0) return; // 최소화 등

        foreach (var e in _entries)
        {
            if (e.scaler == null) continue;

            // ScaleWithScreenSize 등가 배율 = (w/refW)^(1-match) * (h/refH)^match
            float logW  = Mathf.Log(_lastW / e.refRes.x);
            float logH  = Mathf.Log(_lastH / e.refRes.y);
            float ideal = Mathf.Exp(Mathf.Lerp(logW, logH, e.match));

            e.scaler.scaleFactor = SnapToClean(ideal);
        }
    }

    /// <summary>배율을 정수(확대) 또는 1/정수(축소)로 스냅한다.
    /// 확대는 내림(초과분은 여백), 축소는 1/올림(부족분은 여백) — UI가 창 밖으로 잘리지 않도록 항상 여백 방향.</summary>
    private static float SnapToClean(float s)
    {
        const float eps = 0.001f;
        if (s <= 0f) return 1f;
        if (s >= 1f) return Mathf.Max(1f, Mathf.Floor(s + eps));   // 2560x1440→1x, 3840x2160→2x
        return 1f / Mathf.Ceil(1f / s - eps);                      // 1280x720→0.5x, 960x540→0.5x
    }
}
