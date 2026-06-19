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

    [Header("형태")]
    public SkillType     skillType;
    public SkillTargetType targetType;

    [Header("데미지")]
    [Tooltip("시너지 무기 총합 공격력 대비 배율 (1.0 = 100%)")]
    public float dmgMultiplier = 1f;
    [Tooltip("1회 시전 시 총 타격 횟수. 99 = 상시 지속형")]
    public int   hitCount = 1;

    [Header("범위 & 지속")]
    public float rangeRadius = 5f;
    [Tooltip("0 = 즉발, -1 = 전투 종료까지 무한 유지")]
    public float duration = 0f;
    public float cooldown = 2f;

    [Header("상태이상")]
    public StatusEffectType statusEffect;
    [Tooltip("넉백 거리(m) 또는 기절 시간(초) 또는 방어력 무시(%)")]
    public float effectValue;

    [Header("이펙트")]
    public GameObject vfxPrefab;
}
