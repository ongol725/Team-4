using UnityEngine;

// ============================================================
// WeaponAffix.cs
// 무기 랜덤 강화 옵션(어픽스) 정의 + 롤/수치/표기 테이블.
//  - 합성으로 등급이 오를 때 무기 타입(근접/원거리)의 풀에서 1개 추첨.
//  - 티어 확률: T1 60% / T2 30% / T3 10%. 중복 허용(효과 중첩).
// ============================================================
public enum WeaponAffixType
{
    AttackPower,      // 공격력 +% (근·원)
    AttackSpeed,      // 공격속도 +%(주기↓) (근·원)
    ExtraActivation,  // 추가 발동(원=연발/근=연속타) (근·원)
    Pierce,           // 관통(다중타겟) (원거리 전용)
    AttackSize,       // 공격 크기(원=투사체/근=타격범위) (근·원)
    Crit,             // 치명타(확률로 ×2) (근·원)
}

public enum AffixTier { T1 = 0, T2 = 1, T3 = 2 }

[System.Serializable]
public struct WeaponAffix
{
    public WeaponAffixType type;
    public AffixTier       tier;
    public WeaponAffix(WeaponAffixType t, AffixTier tr) { type = t; tier = tr; }
}

public static class WeaponAffixTable
{
    // 근접/원거리 적용 가능 풀 (관통은 원거리 전용)
    private static readonly WeaponAffixType[] MeleePool =
    {
        WeaponAffixType.AttackPower, WeaponAffixType.AttackSpeed,
        WeaponAffixType.ExtraActivation, WeaponAffixType.AttackSize, WeaponAffixType.Crit,
    };
    private static readonly WeaponAffixType[] RangedPool =
    {
        WeaponAffixType.AttackPower, WeaponAffixType.AttackSpeed,
        WeaponAffixType.ExtraActivation, WeaponAffixType.Pierce,
        WeaponAffixType.AttackSize, WeaponAffixType.Crit,
    };

    public static bool IsMelee(SO_WeaponData wd) =>
        wd != null && (wd.attackStyleType == WeaponAttackStyleType.MeleeFan
                    || wd.attackStyleType == WeaponAttackStyleType.MeleeSingle);

    /// <summary>티어 추첨: 60% T1 / 30% T2 / 10% T3.</summary>
    public static AffixTier RollTier()
    {
        float r = Random.value;
        if (r < 0.60f) return AffixTier.T1;
        if (r < 0.90f) return AffixTier.T2;
        return AffixTier.T3;
    }

    /// <summary>무기 타입 풀에서 옵션 1개 + 티어 추첨.</summary>
    public static WeaponAffix Roll(SO_WeaponData wd)
    {
        var pool = IsMelee(wd) ? MeleePool : RangedPool;
        var type = pool[Random.Range(0, pool.Length)];
        return new WeaponAffix(type, RollTier());
    }

    // ── 수치 값 (Tier T1/T2/T3) ──────────────────────────────
    // 공격력·크기: +% (배수 = 1 + 값)
    public static float PercentValue(AffixTier t) => t switch { AffixTier.T1 => 0.25f, AffixTier.T2 => 0.50f, _ => 1.00f };
    // 공속: 주기 배수(작을수록 빠름)
    public static float SpeedFactor(AffixTier t) => t switch { AffixTier.T1 => 0.85f, AffixTier.T2 => 0.70f, _ => 0.50f };
    // 추가 발동 횟수
    public static int   ExtraCount(AffixTier t)  => (int)t + 1;   // 1 / 2 / 3
    // 관통 수
    public static int   PierceCount(AffixTier t) => t switch { AffixTier.T1 => 1, AffixTier.T2 => 2, _ => 4 };
    // 치명타 확률
    public static float CritChance(AffixTier t)  => t switch { AffixTier.T1 => 0.15f, AffixTier.T2 => 0.25f, _ => 0.40f };

    // ── 표기용 ───────────────────────────────────────────────
    public static string EffectText(WeaponAffix a) => a.type switch
    {
        WeaponAffixType.AttackPower     => $"공격력 +{PercentValue(a.tier) * 100:0}%",
        WeaponAffixType.AttackSpeed     => $"공격속도 +{(1f / SpeedFactor(a.tier) - 1f) * 100:0}%",
        WeaponAffixType.ExtraActivation => $"추가 발동 +{ExtraCount(a.tier)}회",
        WeaponAffixType.Pierce          => $"관통 +{PierceCount(a.tier)}",
        WeaponAffixType.AttackSize      => $"공격 크기 +{PercentValue(a.tier) * 100:0}%",
        WeaponAffixType.Crit            => $"치명타 {CritChance(a.tier) * 100:0}% (×2)",
        _ => a.type.ToString(),
    };

    public static Color TierColor(AffixTier t) => t switch
    {
        AffixTier.T1 => Color.white,
        AffixTier.T2 => new Color(0.40f, 0.72f, 1f),   // 파랑
        _            => new Color(1f, 0.84f, 0.20f),   // 금
    };

    public static string TierLabel(AffixTier t) => t switch { AffixTier.T1 => "I", AffixTier.T2 => "II", _ => "III" };
}
