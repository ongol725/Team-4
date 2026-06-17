using System;
using UnityEngine;
using BagSurvivor.UI;

/// <summary>
/// 인벤토리 스냅샷 + 시너지 계산 결과를 BattleLoadout으로 패키징해 이벤트로 전달한다.
/// ShopUI.Close() 시점에 BuildAndDeliver()를 호출한다.
/// </summary>
public class BattleLoadoutBuilder : MonoBehaviour
{
    [SerializeField] private InventoryAnalyzer _analyzer;
    [SerializeField] private SynergyCalculator _synergyCalc;

    /// <summary>전투 진입 로드아웃이 확정됐을 때 발행된다.</summary>
    public event Action<BattleLoadout> OnLoadoutReady;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_analyzer    == null) _analyzer    = GetComponent<InventoryAnalyzer>();
        if (_synergyCalc == null) _synergyCalc = GetComponent<SynergyCalculator>();
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 스냅샷으로 BattleLoadout을 빌드해 반환한다. 이벤트·로그 없음.
    /// BattleLoadoutDebugUI 등 실시간 갱신용 호출에 사용한다.
    /// </summary>
    public BattleLoadout Build()
    {
        var snapshot = _analyzer != null
            ? (_analyzer.LatestSnapshot ?? _analyzer.Analyze())
            : null;
        if (snapshot == null) return null;

        var loadout = new BattleLoadout();
        BuildWeapons(snapshot, loadout);
        BuildArmorStats(snapshot, loadout);
        BuildSynergies(snapshot, loadout);
        return loadout;
    }

    /// <summary>
    /// BattleLoadout을 빌드하고 OnLoadoutReady 이벤트를 발행한다.
    /// ShopUI.Close()에서 호출한다.
    /// </summary>
    public void BuildAndDeliver()
    {
        var loadout = Build();
        if (loadout == null)
        {
            Debug.LogWarning("[BattleLoadoutBuilder] 스냅샷을 가져올 수 없습니다.");
            return;
        }
        OnLoadoutReady?.Invoke(loadout);
        LogLoadout(loadout);
    }

    // ─────────────────────────────────────────────────────────────

    private static void BuildWeapons(InventorySnapshot snapshot, BattleLoadout loadout)
    {
        foreach (var inst in snapshot.Weapons)
        {
            if (inst.data is not SO_WeaponData wd) continue;

            int effectiveGrade = Mathf.Clamp(inst.gradeIndex + inst.RingGradeBonus, 0, 4);
            var stats          = effectiveGrade < wd.gradeStats.Length
                                     ? wd.gradeStats[effectiveGrade]
                                     : null;

            loadout.Weapons.Add(new WeaponLoadoutEntry
            {
                data          = wd,
                effectiveGrade = effectiveGrade,
                attackPower   = stats?.attackPower  ?? 0,
                attackSpeed   = stats?.attackSpeed  ?? 0f,
            });
        }
    }

    private static void BuildArmorStats(InventorySnapshot snapshot, BattleLoadout loadout)
    {
        loadout.TotalHpBonus = snapshot.TotalHpBonus;
        loadout.TotalHpRegen = snapshot.TotalHpRegen;
    }

    private void BuildSynergies(InventorySnapshot snapshot, BattleLoadout loadout)
    {
        if (_synergyCalc == null) return;

        var synList = _synergyCalc.Calculate(snapshot);
        foreach (var syn in synList)
        {
            loadout.ActiveSynergies.Add(new ActiveSynergyEntry
            {
                type  = syn.type,
                grade = syn.grade,
                count = syn.count,
            });
        }
    }

    // ─────────────────────────────────────────────────────────────

    private static void LogLoadout(BattleLoadout loadout)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[BattleLoadout] ===== 전투 진입 종합정보 =====");

        sb.AppendLine($"  [무기 {loadout.Weapons.Count}개]");
        foreach (var w in loadout.Weapons)
            sb.AppendLine($"    · {w.data.itemName}  등급:{w.effectiveGrade + 1}  " +
                          $"공격력:{w.attackPower}  공격속도:{w.attackSpeed:F2}  " +
                          $"투사체:{w.data.projectileData?.name ?? "없음"}");

        sb.AppendLine($"  [방어구 합산]  HP보너스:{loadout.TotalHpBonus}  HP재생:{loadout.TotalHpRegen}");

        sb.AppendLine($"  [활성 시너지 {loadout.ActiveSynergies.Count}개]");
        foreach (var s in loadout.ActiveSynergies)
            sb.AppendLine($"    · {s.type}  {s.grade}  ({s.count}개)");

        sb.AppendLine("  ========================================");
        Debug.Log(sb.ToString());
    }
}
