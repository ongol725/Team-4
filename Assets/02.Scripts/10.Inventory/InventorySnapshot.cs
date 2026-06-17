using System.Collections.Generic;

/// <summary>
/// 인벤토리에 배치된 아이템 집계 결과 (읽기 전용 스냅샷).
/// InventoryAnalyzer.Analyze()로 생성하며, 생성 이후 데이터는 변경되지 않는다.
/// </summary>
public class InventorySnapshot
{
    // ── 아이템 분류 ───────────────────────────────────────────────
    public readonly List<ItemInstance> Weapons     = new();
    public readonly List<ItemInstance> Armors      = new();
    public readonly List<ItemInstance> Accessories = new();

    // ── 스탯 합산 ─────────────────────────────────────────────────
    public int   TotalAttackPower;
    public float TotalAttackSpeed;
    public int   TotalHpBonus;
    public int   TotalHpRegen;

    // ── 시너지 카운트 (SynergyType → 고유 아이템 종류 수) ─────────
    public readonly Dictionary<SynergyType, int> SynergyCounts = new();

    // 같은 SO_ItemData(= 같은 종류)를 두 번 이상 배치해도 시너지는 한 번만 반영한다.
    private readonly HashSet<SO_ItemData> _processedSynergySources = new();

    // ─────────────────────────────────────────────────────────────

    /// <summary>반지 인접 버프를 합산 스탯에 직접 가산한다. InventoryAnalyzer 전용.</summary>
    internal void AddAdjacentBuff(RingAdjacentBuff buff)
    {
        TotalAttackPower += buff.attackPowerBonus;
        TotalAttackSpeed += buff.attackSpeedBonus;
    }

    /// <summary>아이템 한 개를 분류·집계에 추가한다. InventoryAnalyzer 전용.</summary>
    internal void Add(ItemInstance inst)
    {
        switch (inst.data)
        {
            case SO_WeaponData _:
                Weapons.Add(inst);
                var ws = inst.CurrentWeaponStats;
                if (ws != null)
                {
                    TotalAttackPower += ws.attackPower;
                    TotalAttackSpeed += ws.attackSpeed;
                }
                break;

            case SO_ArmorData _:
                Armors.Add(inst);
                var armStat = inst.CurrentArmorStats;
                if (armStat != null)
                {
                    TotalHpBonus += armStat.hpBonus;
                    TotalHpRegen += armStat.hpRegen;
                }
                break;

            case SO_AccessoryData _:
                Accessories.Add(inst);
                break;
        }

        // 시너지 카운트 — 같은 SO_ItemData 종류는 처음 한 번만 반영
        // HashSet.Add()는 새로 추가됐을 때 true, 이미 있으면 false 반환
        if (inst.data.synergies == null || !_processedSynergySources.Add(inst.data)) return;
        foreach (var syn in inst.data.synergies)
        {
            if (syn == SynergyType.None) continue;
            SynergyCounts.TryGetValue(syn, out int prev);
            SynergyCounts[syn] = prev + 1;
        }
    }
}
