// ============================================================
// SpriteSheetAnimator.cs
// 스프라이트 시트 프레임 배열을 일정 FPS로 순환 재생하는 단순 애니메이터.
//  - 시너지 투사체/이펙트가 03.Prefabs/08.Synergy 시트 애니메이션을 표시할 때 사용.
//  - SpriteRenderer 가 없으면 자동 추가.
// ============================================================
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteSheetAnimator : MonoBehaviour
{
    private Sprite[] _frames;
    private float    _fps   = 12f;
    private bool     _loop  = true;
    private int      _idx;
    private float    _timer;
    private SpriteRenderer _sr;

    /// <summary>프레임 배열 재생 시작. frames 가 비면 아무것도 하지 않는다.</summary>
    public void Play(Sprite[] frames, float fps = 12f, bool loop = true)
    {
        _frames = frames;
        _fps    = fps > 0f ? fps : 12f;
        _loop   = loop;
        _idx    = 0;
        _timer  = 0f;

        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_frames != null && _frames.Length > 0 && _sr != null)
            _sr.sprite = _frames[0];
    }

    private void Update()
    {
        if (_frames == null || _frames.Length < 2 || _sr == null) return;

        _timer += Time.deltaTime * _fps;
        while (_timer >= 1f)
        {
            _timer -= 1f;
            _idx++;
            if (_idx >= _frames.Length)
            {
                if (_loop) _idx = 0;
                else { _idx = _frames.Length - 1; enabled = false; }
            }
            _sr.sprite = _frames[_idx];
        }
    }
}
