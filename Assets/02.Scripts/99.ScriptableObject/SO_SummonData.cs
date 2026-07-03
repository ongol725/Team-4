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

    [Tooltip("스프라이트 원본이 왼쪽을 보고 그려졌으면 체크(이동 방향 반전 보정용)")]
    public bool spriteFacesLeft;

    [Header("전투")]
    [Tooltip("ScalingStatType에 따라 WPN_ATK_AVG/SUM 또는 ARM_HP_SUM 대비 배율")]
    public ScalingStatType scalingStat = ScalingStatType.WPN_ATK_AVG;
    [Tooltip("scalingStat 기준 대비 배율 (1.0 = 100%)")]
    public float atkMultiplier = 0.5f;
    public float moveSpeed    = 4f;
    public float atkCooldown  = 1.5f;
    public float atkRange     = 1.5f;

    [Header("지속 시간")]
    [Tooltip("-1 = 전투 내내 영구 유지, 양수 = N초 후 소멸")]
    public float duration = -1f;

    [Header("고유 고정 효과")]
    [Tooltip("성기사단 성역처럼 대미지 외 추가 효과가 있는 경우")]
    public FixedEffectType fixedEffect = FixedEffectType.None;
    [Tooltip("fixedEffect 수치. 예: HealArmorHpPct=1 이면 방어구 HP 총합의 1% 회복")]
    public float fixedEffectValue = 0f;

    [Header("특수 스킬")]
    [Tooltip("평타 외 주기적으로 시전하는 스킬. 없으면 null")]
    public SO_SkillData uniqueSkill;
    [Tooltip("uniqueSkill 발동 주기 (초)")]
    public float uniqueSkillCooldown = 8f;

    [Header("외형")]
    [Tooltip("표시 스케일 직접 지정. 0이면 AI 타입별 기본 크기를 사용한다")]
    public float displayScale = 0f;
    [Tooltip("스프라이트 정렬 순서. 기본 5(캐릭터 뒤). 성역=-20(바닥). 캐릭터(플레이어/몬스터)=10")]
    public int sortingOrder = 5;
    public GameObject modelPrefab;
    [Tooltip("modelPrefab 없을 때 사용할 스프라이트 (스프라이트 시트 첫 프레임 등)")]
    public Sprite icon;
    [Tooltip("애니메이션 프레임 배열. ③ Fill Synergy Icons 메뉴로 자동 채워짐. 2개 이상이면 animFps 속도로 순환")]
    public Sprite[] animFrames;
    [Tooltip("초당 프레임 수 (animFrames 사용 시)")]
    public float animFps = 10f;
    [Tooltip("소환수 체력 (0이면 무적)")]
    public int hp = 0;

    [Header("공격 이펙트")]
    [Tooltip("공격 명중 시 타겟 위치에 재생할 이펙트 프레임(정령 골렘 spirit_attack 등). ③ 메뉴로 자동 채움. 비우면 이펙트 없음")]
    public Sprite[] attackEffectFrames;
    [Tooltip("공격 이펙트 재생 속도(FPS)")]
    public float attackEffectFps = 12f;
    [Tooltip("공격 이펙트 표시 크기(지름, 유닛). 기본 0.5. 대정령처럼 크게 보여야 하면 키운다")]
    public float attackEffectScale = 0.5f;
}
