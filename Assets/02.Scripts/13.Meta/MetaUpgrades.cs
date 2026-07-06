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
        new MetaUpgradeDef("atk",       "공격력 강화",   "공격력 +3%/Lv",                 10, 100, 100),
        new MetaUpgradeDef("rare",      "희귀무기 확률", "상점 상위 등급 확률 +5%/Lv",     5, 150, 150),
        new MetaUpgradeDef("affix",     "무기옵션 등급", "옵션 상위 티어 확률 +5%/Lv",     5, 150, 150),
        new MetaUpgradeDef("spawn",     "몬스터 증원",   "스폰량 +10%/Lv (재화 기회↑)",    5, 120, 120),
        new MetaUpgradeDef("goldroom",  "황금의 기운",   "강자의 방(골드2배) +5%p/Lv",     5, 120, 120),
        new MetaUpgradeDef("dash",      "대시 숙련",     "대시 쿨타임 -10%/Lv",            5, 120, 120),
        new MetaUpgradeDef("shopprice", "단골 할인",     "상점 구매가 -4%/Lv",             5, 150, 150),
        new MetaUpgradeDef("freeroll",  "공짜 리롤",     "상점 리롤 무료 확률 +10%/Lv",    5, 120, 120),
        new MetaUpgradeDef("sale",      "파격 세일",     "반값 할인 확률 +5%p/Lv",         5, 120, 120),
        new MetaUpgradeDef("grade2",    "명품 진열",     "2강 아이템 등장 +5%p/Lv",        5, 150, 150),
        new MetaUpgradeDef("resonance", "시너지 공명",   "시너지 동시 발동 +1 (기본 1개)",  2, 500, 500),
        new MetaUpgradeDef("heal",      "전장의 회복",   "방 클리어 시 HP 1/3/6/10% 회복",  4, 150, 150),
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

    /// <summary>대시 쿨타임 배율: 1 - 10%/Lv (PlayerMovement).</summary>
    public static float DashCooldownMul => 1f - 0.10f * GetLevel("dash");

    /// <summary>상점 구매가 배율: 1 - 4%/Lv (ShopSlotUI — 구매가만, 판매가 무관).</summary>
    public static float ShopPriceMul => 1f - 0.04f * GetLevel("shopprice");

    /// <summary>상점 리롤 무료 확률: 10%/Lv (ShopUI.Reroll).</summary>
    public static float FreeRerollChance => 0.10f * GetLevel("freeroll");

    /// <summary>반값 할인 확률 가산(%p): +5/Lv (ShopSlotUI.SetItem).</summary>
    public static int SaleBonusPct => 5 * GetLevel("sale");

    /// <summary>2강 아이템 등장 확률 가산(%p): +5/Lv (ShopSlotUI.SetItem).</summary>
    public static int Grade2BonusPct => 5 * GetLevel("grade2");

    /// <summary>시너지 동시 발동 최대 수: 기본 1, 업그레이드당 +1 (최대 3).
    /// SynergyManager(발동)·SynergyCalculator(UI 회색 표시)가 함께 읽는다.</summary>
    public static int MaxSynergyActive => 1 + GetLevel("resonance");

    /// <summary>방 클리어 시 최대 HP 회복 비율: Lv1~4 = 1/3/6/10% (RoomController.ClearRoom).</summary>
    public static float ClearHealPct
    {
        get
        {
            switch (GetLevel("heal"))
            {
                case 0:  return 0f;
                case 1:  return 0.01f;
                case 2:  return 0.03f;
                case 3:  return 0.06f;
                default: return 0.10f;
            }
        }
    }
}
