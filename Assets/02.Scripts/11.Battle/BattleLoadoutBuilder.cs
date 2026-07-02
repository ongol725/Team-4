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

    private void OnEnable()
    {
        // 아이템을 배치/이동/제거하는 즉시 전투에 반영(실시간). 편집 중엔 전투가 일시정지되므로 부담이 적다.
        if (_analyzer != null) _analyzer.OnSnapshotChanged += OnSnapshotChanged;
    }

    private void OnDisable()
    {
        if (_analyzer != null) _analyzer.OnSnapshotChanged -= OnSnapshotChanged;
    }

    private void OnSnapshotChanged(InventorySnapshot _) => BuildAndDeliver(log: false);

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
        BuildTempSlotPenalty(loadout);
        return loadout;
    }

    // ── 임시칸 과적 페널티 (반지 제외, 3개 무료, 가속 곡선) ──
    private const int   TEMP_FREE      = 3;
    private const float MOVE_BASE      = 4f,  MOVE_ACCEL   = 2f,   MOVE_CAP   = 60f;
    private const float ATKSPD_BASE    = 3f,  ATKSPD_ACCEL = 1.5f, ATKSPD_CAP = 45f;

    private void BuildTempSlotPenalty(BattleLoadout loadout)
    {
        int count = CountTempItemsExcludingRings();
        int n = Mathf.Max(0, count - TEMP_FREE); // 무료 초과분
        float tri = n * (n - 1) * 0.5f;          // 삼각수 가중(가속)

        float movePen = Mathf.Min(MOVE_CAP,   MOVE_BASE   * n + MOVE_ACCEL   * tri);
        float atkPen  = Mathf.Min(ATKSPD_CAP, ATKSPD_BASE * n + ATKSPD_ACCEL * tri);

        loadout.tempMoveMult   = 1f - movePen / 100f;
        loadout.tempAtkSpdMult = 1f - atkPen  / 100f;
    }

    /// <summary>무료 임시칸 개수(호버 팝업 표기용).</summary>
    public static int TempFreeCount => TEMP_FREE;

    /// <summary>임시칸 과적 페널티 %(이속·공속). 반올림 정수. 페널티 공식과 단일 소스.</summary>
    public static void GetTempPenaltyPercents(int countExcludingRings, out int movePct, out int atkPct)
    {
        int n = Mathf.Max(0, countExcludingRings - TEMP_FREE);
        float tri = n * (n - 1) * 0.5f;
        movePct = Mathf.RoundToInt(Mathf.Min(MOVE_CAP,   MOVE_BASE   * n + MOVE_ACCEL   * tri));
        atkPct  = Mathf.RoundToInt(Mathf.Min(ATKSPD_CAP, ATKSPD_BASE * n + ATKSPD_ACCEL * tri));
    }

    /// <summary>임시칸 과적 심각도 0~1 (UI 색 표시용, 페널티 공식과 단일 소스).
    /// count = 반지 제외 임시칸 아이템 수. 이속 페널티가 상한에 닿을 때 1.</summary>
    public static float GetTempSeverity(int countExcludingRings)
    {
        int n = Mathf.Max(0, countExcludingRings - TEMP_FREE);
        float movePen = Mathf.Min(MOVE_CAP, MOVE_BASE * n + MOVE_ACCEL * (n * (n - 1) * 0.5f));
        return MOVE_CAP > 0f ? Mathf.Clamp01(movePen / MOVE_CAP) : 0f;
    }

    // 임시칸에 든 아이템 중 반지(장신구)를 제외한 수
    private static int CountTempItemsExcludingRings()
    {
        var temp = FindFirstObjectByType<TempSlotUI>();
        if (temp == null) return 0;

        int n = 0;
        foreach (var block in temp.HeldBlocks)
        {
            var data = block != null && block.Instance != null ? block.Instance.data : null;
            if (data == null) continue;
            if (data is SO_AccessoryData) continue; // 반지는 노카운트
            n++;
        }
        return n;
    }

    /// <summary>
    /// BattleLoadout을 빌드하고 OnLoadoutReady 이벤트를 발행한다.
    /// ShopUI.Close()에서 호출한다.
    /// </summary>
    public void BuildAndDeliver() => BuildAndDeliver(log: true);

    private void BuildAndDeliver(bool log)
    {
        var loadout = Build();
        if (loadout == null)
        {
            if (log) Debug.LogWarning("[BattleLoadoutBuilder] 스냅샷을 가져올 수 없습니다.");
            return;
        }
        GameManager.Instance?.ApplyLoadout(loadout);   // 씬 간 브릿지
        OnLoadoutReady?.Invoke(loadout);
        if (log) LogLoadout(loadout);                  // 실시간 갱신(log:false) 시 로그 스팸 방지
    }

    // ─────────────────────────────────────────────────────────────

    private static void BuildWeapons(InventorySnapshot snapshot, BattleLoadout loadout)
    {
        foreach (var inst in snapshot.Weapons)
        {
            if (inst.data is not SO_WeaponData wd) continue;

            int effectiveGrade = Mathf.Clamp(inst.gradeIndex + inst.RingGradeBonus, 0, 4);
            var stats          = (wd.gradeStats != null && effectiveGrade < wd.gradeStats.Length)
                                     ? wd.gradeStats[effectiveGrade]
                                     : null;

            // 반지 인접 배율(합산): 다이아=공격력, 금=공속. 표시·계산에 미리 반영.
            int   baseAtk = stats?.attackPower ?? 0;
            float baseSpd = stats?.attackSpeed ?? 0f;
            var entry = new WeaponLoadoutEntry
            {
                data           = wd,
                effectiveGrade = effectiveGrade,
                attackPower    = Mathf.RoundToInt(baseAtk * (1f + inst.RingAtkBonus)),
                attackSpeed    = baseSpd * (1f + inst.RingSpdBonus),
                ringStunChance = inst.RingStunChance,
                ringSlowSec    = inst.RingSlowSec,
                ringProjScale  = 1f + inst.RingProjScaleBonus,
                ringProjSpeed  = Mathf.Max(0.1f, 1f + inst.RingProjSpeedBonus),
            };

            // 반지 버프 기록
            if (snapshot.WeaponRingBuffs.TryGetValue(inst, out var rings))
            {
                foreach (var ring in rings)
                {
                    if (ring.data is SO_AccessoryData accData)
                        entry.RingBuffs.Add(new RingBuffRecord
                        {
                            ringName   = accData.itemName,
                            effectDesc = BuildRingEffectDesc(accData),
                        });
                }
            }

            loadout.Weapons.Add(entry);
        }
    }

    /// <summary>
    /// 반지 효과 설명 생성.
    /// adjacentGimmick이 있으면 그대로, 없으면 adjacentBuff 수치로 파생한다.
    /// </summary>
    private static string BuildRingEffectDesc(SO_AccessoryData acc)
    {
        if (!string.IsNullOrWhiteSpace(acc.adjacentGimmick))
            return acc.adjacentGimmick;

        var b  = acc.adjacentBuff;
        var sb = new System.Text.StringBuilder();
        if (b.attackPowerBonus != 0)  sb.Append($"공격력+{b.attackPowerBonus} ");
        if (b.attackSpeedBonus != 0f) sb.Append($"공격속도+{b.attackSpeedBonus:F2} ");
        if (b.hpBonus          != 0)  sb.Append($"HP+{b.hpBonus} ");
        if (b.hpRegen          != 0)  sb.Append($"재생+{b.hpRegen} ");
        return sb.Length > 0 ? sb.ToString().TrimEnd() : "등급+1";
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
