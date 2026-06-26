// ============================================================
// PlayerDirectionalAnimator.cs
// 이동 입력(PlayerMovement.MoveInput)을 Animator의 MoveX/MoveY 파라미터로 전달.
//  - 컨트롤러의 2D 방향 블렌드트리가 이 값으로 상/하/좌/우/정지(Idle) 클립을 선택.
//  - 정지(0,0) → Idle, 입력 방향 → 해당 방향 클립.
// ============================================================
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerDirectionalAnimator : MonoBehaviour
{
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");

    private Animator anim;
    private PlayerMovement move;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        move = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        Vector2 v = move != null ? move.MoveInput : Vector2.zero;
        anim.SetFloat(MoveX, v.x);
        anim.SetFloat(MoveY, v.y);
    }
}
