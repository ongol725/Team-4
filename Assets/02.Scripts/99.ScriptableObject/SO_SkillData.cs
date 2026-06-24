using UnityEngine;

/// <summary>
/// 시너지 스킬 하나의 데이터 정의.
/// 스킬형 시너지(일렉트로, 메테오 등)에서 사용.
/// </summary>
[CreateAssetMenu(menuName = "BagSurvivor/Skill Data", fileName = "SK_New")]
public class SO_SkillData : ScriptableObject
{
    [Header("식별")]
    [Tooltip("시스템 내부 고유 키. 예: SKILL_ELECTRO_BOLT")]
    public string skillID;
    public string skillName;

    [Header("트리거 & 형태")]
    public SynergyTriggerType triggerType;
    public SkillType          skillType;
    public SkillTargetType    targetType;

    [Header("데미지")]
    [Tooltip("ScalingStatType에 따라 WPN_ATK_AVG 또는 WPN_ATK_SUM 대비 배율")]
    public ScalingStatType scalingStat = ScalingStatType.WPN_ATK_AVG;
    [Tooltip("scalingStat 기준 대비 배율 (1.0 = 100%)")]
    public float dmgMultiplier = 1f;
    [Tooltip("1회 시전 시 총 타격 횟수")]
    public int   hitCount = 1;
    [Tooltip("멀티히트 시 타격 간격 (초)")]
    public float hitInterval = 0.1f;
    [Tooltip("RandomEnemy 등 다수 대상 수. 0이면 1로 처리")]
    public int   extraCount = 0;

    [Header("범위 & 지속")]
    public float rangeRadius = 5f;
    [Tooltip("0 = 즉발, -1 = 전투 종료까지 무한 유지")]
    public float duration = 0f;
    public float cooldown = 2f;

    [Header("고유 고정 효과")]
    public FixedEffectType fixedEffect = FixedEffectType.None;
    [Tooltip("fixedEffect 수치. 예: DamageReduction=10 이면 10% 피해 감소")]
    public float fixedEffectValue = 0f;

    [Header("상태이상 (레거시 호환)")]
    public StatusEffectType statusEffect;
    [Tooltip("넉백 거리(m) 또는 기절 시간(초) 등")]
    public float effectValue;

    [Header("이펙트")]
    public GameObject vfxPrefab;
}
