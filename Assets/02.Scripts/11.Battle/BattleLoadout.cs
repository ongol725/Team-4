using System.Collections.Generic;
using BagSurvivor.UI;

/// <summary>
/// 특정 반지가 무기에 준 인접 버프 기록 1건.
/// </summary>
public class RingBuffRecord
{
    public string ringName;   // 반지 아이템명
    public string effectDesc; // 버프 효과 설명 (adjacentGimmick 또는 수치 파생)
}

/// <summary>
/// 무기 1개의 전투 진입 시 유효 데이터.
/// SO_WeaponData는 투사체·사거리·maxTargets 등 공통 정보를 직접 참조한다.
/// </summary>
public class WeaponLoadoutEntry
{
    public SO_WeaponData data;
    public int           effectiveGrade; // gradeIndex + RingGradeBonus (0~4)
    public int           attackPower;    // effectiveGrade 기준 등급 스탯 (다이아 반지 배율 반영)
    public float         attackSpeed;    // (금 반지 배율 반영)
    public float         ringStunChance; // 뼈 반지: 피격 시 스턴 확률(합산)
    public float         ringSlowSec;    // 나무 반지: 피격 시 슬로우 지속(초)
    public float         ringProjScale = 1f; // 강철 반지: 투사체 크기 배율(피격범위 동반)
    public float         ringProjSpeed = 1f; // 강철 반지: 발사·비행 속도 배율
    public System.Collections.Generic.List<RingBuffRecord> RingBuffs = new();
}

/// <summary>
/// 활성화된 시너지 1개의 전투 진입 시 상태.
/// </summary>
public class ActiveSynergyEntry
{
    public SynergyType  type;
    public SynergyGrade grade;  // Bronze / Silver / Gold
    public int          count;  // 현재 보유 고유 아이템 종류 수
}

/// <summary>
/// 상점·인벤토리 팝업이 닫히는 순간 확정되는 전투 종합 정보.
/// BattleLoadoutBuilder.OnLoadoutReady 이벤트로 전달된다.
/// </summary>
public class BattleLoadout
{
    /// <summary>배치된 무기 전체 (반지 인접 버프 적용 등급 포함)</summary>
    public readonly List<WeaponLoadoutEntry> Weapons         = new();

    /// <summary>방어구 합산 체력 보너스 (반지 HP 버프 포함)</summary>
    public int TotalHpBonus;

    /// <summary>방어구 전용 기본 체력 합산 (ARM_HP_SUM 시너지 스케일링 기준)</summary>
    public int TotalArmorHp;

    /// <summary>방어구 합산 10초당 체력 재생</summary>
    public int TotalHpRegen;

    /// <summary>임시칸 과적 페널티: 이동속도 배율(1=무패널티, &lt;1 감소)</summary>
    public float tempMoveMult = 1f;
    /// <summary>임시칸 과적 페널티: 공격속도 배율(1=무패널티, &lt;1 감소=느려짐)</summary>
    public float tempAtkSpdMult = 1f;

    /// <summary>Bronze 이상 활성화된 시너지 목록 (Gold → Silver → Bronze 순 정렬)</summary>
    public readonly List<ActiveSynergyEntry> ActiveSynergies = new();

    /// <summary>시너지 스케일링 기준 스탯 값 반환.</summary>
    public int GetScaledBase(ScalingStatType stat)
    {
        switch (stat)
        {
            case ScalingStatType.WPN_ATK_SUM:
            {
                int sum = 0;
                foreach (var w in Weapons) sum += w.attackPower;
                return sum;
            }
            case ScalingStatType.WPN_ATK_AVG:
            {
                if (Weapons.Count == 0) return 0;
                int sum = 0;
                foreach (var w in Weapons) sum += w.attackPower;
                return sum / Weapons.Count;
            }
            case ScalingStatType.ARM_HP_SUM:
                return TotalArmorHp;
            default:
                return 0;
        }
    }
}
