// ============================================================
// RunStartStats.cs
// 런 시작 랜덤 스탯 (반복 플레이 요소)
//  - 로비 진입 시 공격력/공격속도/이동속도 배율을 무료 1회 랜덤 추첨
//  - 리롤: 메타 재화(정수) 소모, 비용은 리롤할수록 증가(리세마라 방지)
//  - 정적 클래스라 씬 전환에도 유지 → 인게임에서 PlayerAttack/PlayerStats/
//    PlayerMovement가 배율을 읽어 적용
//  - 로비를 거치지 않으면(에디터 직행 등) 모든 배율 1 = 영향 없음
// ============================================================
using UnityEngine;

public static class RunStartStats
{
    public const float MIN_MUL = 0.85f;  // 배율 하한
    public const float MAX_MUL = 1.25f;  // 배율 상한
    public const int REROLL_BASE_COST = 10;  // 첫 리롤 비용
    public const int REROLL_COST_STEP = 10;  // 리롤마다 비용 증가폭

    public static float AttackMul      { get; private set; } = 1f;
    public static float AttackSpeedMul { get; private set; } = 1f;
    public static float MoveSpeedMul   { get; private set; } = 1f;
    public static bool  Rolled         { get; private set; }
    public static int   RerollCount    { get; private set; }

    /// <summary>다음 리롤 비용 (10, 20, 30, ... — 로비 재진입 시 초기화).</summary>
    public static int NextRerollCost => REROLL_BASE_COST + REROLL_COST_STEP * RerollCount;

    /// <summary>롤/리롤 완료 통지 (로비 UI 갱신용).</summary>
    public static event System.Action onRolled;

    /// <summary>로비 진입 시 무료 추첨 (세션 리롤 비용도 초기화).</summary>
    public static void RollFree()
    {
        RerollCount = 0;
        DoRoll();
    }

    /// <summary>재화를 소모해 리롤. 잔액 부족 시 false.</summary>
    public static bool TryReroll()
    {
        if (!MetaProgression.SpendCurrency(NextRerollCost)) return false;
        RerollCount++;
        DoRoll();
        return true;
    }

    private static void DoRoll()
    {
        AttackMul      = Random.Range(MIN_MUL, MAX_MUL);
        AttackSpeedMul = Random.Range(MIN_MUL, MAX_MUL);
        MoveSpeedMul   = Random.Range(MIN_MUL, MAX_MUL);
        Rolled = true;
        onRolled?.Invoke();
        Debug.Log($"[Meta] 시작 스탯 롤 — 공격 ×{AttackMul:F2}, 공속 ×{AttackSpeedMul:F2}, 이속 ×{MoveSpeedMul:F2} (리롤 {RerollCount}회)");
    }

    /// <summary>배율의 등급 색 (표시용): 회(불리)→흰(보통)→초록(좋음)→파랑(훌륭).</summary>
    public static Color GradeColor(float mul)
    {
        if (mul < 0.95f) return new Color(0.60f, 0.60f, 0.62f);
        if (mul < 1.05f) return Color.white;
        if (mul < 1.15f) return new Color(0.45f, 0.95f, 0.45f);
        return new Color(0.40f, 0.70f, 1f);
    }
}
