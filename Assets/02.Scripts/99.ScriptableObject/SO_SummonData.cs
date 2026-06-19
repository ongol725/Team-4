using UnityEngine;

/// <summary>
/// 소환수 하나의 데이터 정의.
/// 소환형 시너지(정령술사 등)에서 사용.
/// </summary>
[CreateAssetMenu(menuName = "BagSurvivor/Summon Data", fileName = "SUM_New")]
public class SO_SummonData : ScriptableObject
{
    [Header("식별")]
    [Tooltip("시스템 내부 고유 키. 예: SUM_GOLEM_1")]
    public string summonID;
    public string summonName;

    [Header("AI")]
    public SummonAIType aiType;

    [Header("전투")]
    [Tooltip("시너지 무기 총합 공격력 대비 배율 (1.0 = 100%)")]
    public float atkMultiplier = 0.5f;
    public float moveSpeed    = 4f;
    public float atkCooldown  = 1.5f;
    public float atkRange     = 1.5f;

    [Header("특수 스킬")]
    [Tooltip("평타 외 주기적으로 시전하는 스킬. 없으면 null")]
    public SO_SkillData uniqueSkill;
    [Tooltip("uniqueSkill 발동 주기 (초)")]
    public float uniqueSkillCooldown = 8f;

    [Header("외형")]
    public GameObject modelPrefab;
    [Tooltip("소환수 체력 (0이면 무적)")]
    public int hp = 0;
}
