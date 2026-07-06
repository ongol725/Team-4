// ============================================================
// MetaUpgrades.cs
// 영구 업그레이드 정의 테이블 + 효과 조회 (Phase 2)
//  - 레벨 저장은 MetaProgression(JSON), 여기는 정의/비용/효과 계산만
//  - 인게임 시스템들은 효과 프로퍼티(AttackBonusMul 등)만 읽는다
//  - 항목 추가 시 All 배열에 정의 + 효과 프로퍼티 + 주입점 연결
// ============================================================
using UnityEngine;

public class MetaUpgradeDef
{
    public string id;
    public string displayName;
    public string desc;       // 레벨당 효과 설명
    public int maxLevel;
    public int baseCost;      // 1레벨 비용
    public int costStep;      // 레벨당 비용 증가

    public MetaUpgradeDef(string id, string name, string desc, int maxLevel, int baseCost, int costStep)
    {
        this.id = id; displayName = name; this.desc = desc;
        this.maxLevel = maxLevel; this.baseCost = baseCost; this.costStep = costStep;
    }
}

public static class MetaUpgrades
{
    public static readonly MetaUpgradeDef[] All =
    {
        new MetaUpgradeDef("atk",      "공격력 강화",     "공격력 +3%/Lv",                 10, 100, 100),
        new MetaUpgradeDef("rare",     "희귀무기 확률",   "상점 상위 등급 확률 +5%/Lv",     5, 150, 150),
        new MetaUpgradeDef("affix",    "무기옵션 등급",   "옵션 상위 티어 확률 +5%/Lv",     5, 150, 150),
        new MetaUpgradeDef("spawn",    "몬스터 증원",     "스폰량 +10%/Lv (재화 기회↑)",    5, 120, 120),
        new MetaUpgradeDef("goldroom", "황금의 기운",     "강자의 방(골드2배) +5%p/Lv",     5, 120, 120),
    };

    public static int GetLevel(string id) => MetaProgression.GetUpgradeLevel(id);

    public static int NextCost(MetaUpgradeDef d) => d.baseCost + d.costStep * GetLevel(d.id);

    public static bool IsMaxed(MetaUpgradeDef d) => GetLevel(d.id) >= d.maxLevel;

    /// <summary>재화를 소모해 1레벨 구매. 최대 레벨/잔액 부족 시 false.</summary>
    public static bool TryBuy(MetaUpgradeDef d)
    {
        if (d == null || IsMaxed(d)) return false;
        if (!MetaProgression.SpendCurrency(NextCost(d))) return false;
        MetaProgression.SetUpgradeLevel(d.id, GetLevel(d.id) + 1);
        Debug.Log($"[Meta] 업그레이드 구매: {d.displayName} Lv.{GetLevel(d.id)}");
        return true;
    }

    // ── 효과 조회 (인게임 주입점들이 읽음) ─────────────────────
    /// <summary>공격력 배율: 1 + 3%/Lv (PlayerAttack/PlayerStats).</summary>
    public static float AttackBonusMul => 1f + 0.03f * GetLevel("atk");

    /// <summary>상점 레어도 한 등급 상향 확률: 5%/Lv (ShopManager.PickRarity).</summary>
    public static float RareUpChance => 0.05f * GetLevel("rare");

    /// <summary>어픽스 한 티어 상향 확률: 5%/Lv (WeaponAffixTable.RollTier).</summary>
    public static float AffixTierUpChance => 0.05f * GetLevel("affix");

    /// <summary>일반방 총 스폰량 배율: 1 + 10%/Lv (RoomMonsterSpawner).</summary>
    public static float SpawnCountMul => 1f + 0.10f * GetLevel("spawn");

    /// <summary>강자의 방 배정 확률 가산: +5%p/Lv (RoomMonsterSpawner.RescanRooms).</summary>
    public static float GoldRoomBonus => 0.05f * GetLevel("goldroom");
}
