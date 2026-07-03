using System.Collections.Generic;
using UnityEngine;
using BagSurvivor.UI;

/// <summary>
/// InventorySnapshot의 시너지 카운트를 UI 표시용 SynergyInfo 목록으로 변환하고
/// SynergyListUI에 자동으로 전달한다.
///
/// ▶ 연결 구조
///   InventoryAnalyzer.OnSnapshotChanged
///     → SynergyCalculator.HandleSnapshotChanged()
///     → SynergyListUI.SetSynergies()
///
/// ▶ 등급 판정 규칙 (SO_SynergyConfig 없을 때 기본값 적용)
///   count >= 6 → Gold   (config 있으면 goldThreshold 우선)
///   count >= 4 → Silver
///   count >= 2 → Bronze
///   count <  2 → 목록에서 제외 (미활성)
/// </summary>
public class SynergyCalculator : MonoBehaviour
{
    [SerializeField] private SO_SynergyConfig  _config;
    [SerializeField] private InventoryAnalyzer _analyzer;
    [SerializeField] private SynergyListUI     _synergyListUI;

    // ── 기본 임계값 (SO_SynergyConfig 미설정 시 사용) ─────────────
    const int DefaultBronze = 2;
    const int DefaultSilver = 4;
    const int DefaultGold   = 6;

    // ── 한국어 이름 폴백 테이블 ────────────────────────────────────
    static readonly Dictionary<SynergyType, string> KoreanNames =
        new Dictionary<SynergyType, string>
        {
            { SynergyType.Assassin,       "암살단"     },
            { SynergyType.SwordMaster,    "소드마스터" },
            { SynergyType.HolyKnight,     "성기사단"   },
            { SynergyType.DemonLord,      "마왕"       },
            { SynergyType.BloodBerserker, "피의광전사" },
            { SynergyType.Tycoon,         "대부호"     },
            { SynergyType.Executioner,    "처형자"     },
            { SynergyType.SpiritMage,     "정령술사"   },
            { SynergyType.GearShift,      "기어시프트" },
            { SynergyType.Pinball,        "핀볼"       },
            { SynergyType.Overload,       "과부하"     },
            { SynergyType.Electro,        "일렉트로"   },
            { SynergyType.Impregnable,    "난공불락"   },
            { SynergyType.Titan,          "티탄"       },
            { SynergyType.Fairy,          "페어리"     },
        };

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Inspector에서 연결되지 않은 경우 자동 탐색
        if (_analyzer      == null) _analyzer      = GetComponent<InventoryAnalyzer>();
        if (_synergyListUI == null) _synergyListUI = Object.FindFirstObjectByType<SynergyListUI>();
    }

    private void OnEnable()
    {
        if (_analyzer != null)
            _analyzer.OnSnapshotChanged += HandleSnapshotChanged;
    }

    private void OnDisable()
    {
        if (_analyzer != null)
            _analyzer.OnSnapshotChanged -= HandleSnapshotChanged;
    }

    private void HandleSnapshotChanged(InventorySnapshot snapshot)
    {
        var list = Calculate(snapshot);
        if (_synergyListUI == null) return;
        _synergyListUI.SetSynergies(list);
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 스냅샷을 분석해 활성화된 시너지 목록을 반환한다.
    /// SO_SynergyConfig가 없으면 기본 임계값(2/4/6)과 한국어 이름으로 표시한다.
    /// Gold → Silver → Bronze 순으로 정렬된다.
    /// </summary>
    public List<SynergyInfo> Calculate(InventorySnapshot snapshot)
    {
        var result = new List<SynergyInfo>();

        foreach (var kvp in snapshot.SynergyCounts)
        {
            int count = kvp.Value;

            var threshold = _config != null ? _config.GetThreshold(kvp.Key) : null;

            int bronzeMin = threshold != null ? threshold.bronzeThreshold : DefaultBronze;
            int silverMin = threshold != null ? threshold.silverThreshold : DefaultSilver;
            int goldMin   = threshold != null ? threshold.goldThreshold   : DefaultGold;
            int prismMin  = threshold != null ? threshold.prismThreshold  : 0;

            if (count < bronzeMin) continue;

            var grade = (prismMin > 0 && count >= prismMin) ? SynergyGrade.Prism
                      : count >= goldMin                    ? SynergyGrade.Gold
                      : count >= silverMin                  ? SynergyGrade.Silver
                      : SynergyGrade.Bronze;

            int nextMin = prismMin > 0 ? prismMin : goldMin;

            string displayName = threshold != null && !string.IsNullOrEmpty(threshold.displayName)
                ? threshold.displayName
                : KoreanNames.TryGetValue(kvp.Key, out var n) ? n : kvp.Key.ToString();

            string effect = threshold != null
                ? BuildMilestoneText(threshold, grade)
                : string.Empty;

            result.Add(new SynergyInfo
            {
                type          = kvp.Key,
                synergyName   = displayName,
                count         = count,
                nextThreshold = nextMin,
                grade         = grade,
                condition     = threshold?.triggerCondition ?? string.Empty,
                description   = threshold?.description ?? string.Empty,
                effect        = effect,
                icon          = threshold?.icon ?? SynergyIconHelper.GetIcon(kvp.Key),
            });
        }

        result.Sort((a, b) => b.grade.CompareTo(a.grade));
        return result;
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 시너지의 전체 마일스톤 단계를 "(임계값) 효과" 형식으로 세로 나열한다.
    /// 현재 적용 중인 단계는 흰색(#FFFFFF), 미도달·지나간 단계는 회색 50%(#88888880)로 표시.
    /// (기획: 시너지_툴팁_텍스트_테이블.md)
    /// </summary>
    private static string BuildMilestoneText(SynergyThreshold t, SynergyGrade activeGrade)
    {
        var sb = new System.Text.StringBuilder();
        AppendMilestone(sb, t.bronzeThreshold, t.bronzeEffect, activeGrade == SynergyGrade.Bronze);
        AppendMilestone(sb, t.silverThreshold, t.silverEffect, activeGrade == SynergyGrade.Silver);
        AppendMilestone(sb, t.goldThreshold,   t.goldEffect,   activeGrade == SynergyGrade.Gold);
        AppendMilestone(sb, t.prismThreshold,  t.prismEffect,  activeGrade == SynergyGrade.Prism);
        return sb.ToString().TrimEnd('\n');
    }

    private static void AppendMilestone(System.Text.StringBuilder sb, int threshold, string effectText, bool isActive)
    {
        if (threshold <= 0 || string.IsNullOrEmpty(effectText)) return; // 미정의 단계는 생략
        sb.Append(isActive ? "<color=#FFFFFF>" : "<color=#88888880>")
          .Append('(').Append(threshold).Append(") ").Append(effectText)
          .Append("</color>\n");
    }
}
