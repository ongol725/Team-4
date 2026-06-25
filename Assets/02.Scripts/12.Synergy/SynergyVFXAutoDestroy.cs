using UnityEngine;

/// <summary>
/// 시너지 VFX 프리팹에 자동 부착.
/// Animator 재생이 끝나면 오브젝트를 자동으로 파괴한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class SynergyVFXAutoDestroy : MonoBehaviour
{
    private Animator _anim;

    private void Awake() => _anim = GetComponent<Animator>();

    private void Update()
    {
        if (_anim == null) return;
        var info = _anim.GetCurrentAnimatorStateInfo(0);
        if (info.normalizedTime >= 1f && !_anim.IsInTransition(0))
            Destroy(gameObject);
    }
}
