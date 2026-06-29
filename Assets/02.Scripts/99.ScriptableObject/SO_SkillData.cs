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
    [Tooltip("VFX 생성 후 데미지가 들어가기까지의 지연(초). 0=즉시. 티탄 돌 떨구기처럼 연출이 진행된 뒤 타격하고 싶을 때 사용")]
    public float damageDelay = 0f;

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
    [Tooltip("Projectile 스킬용 투사체 프리팹. 비우면 임시 투사체(원/시트 애니메이션)로 발사된다.")]
    public GameObject projectilePrefab;
    [Tooltip("Projectile 투사체 속도(유닛/초). 0 이하이면 기본 15 적용")]
    public float projectileSpeed = 15f;
    [Tooltip("투사체 관통 횟수. 1=첫 명중 후 소멸, 999=경로상 모든 적 관통(암살단 수리검·처형자 낫 등)")]
    public int pierceCount = 1;

    [Header("발동 비주얼 (스프라이트 시트 애니메이션)")]
    [Tooltip("투사체/이펙트로 재생할 스프라이트 프레임 배열(③ 메뉴로 자동 채움). 비우면 노란 원 fallback")]
    public Sprite[] animFrames;
    [Tooltip("프레임 재생 속도(FPS)")]
    public float animFps = 12f;
    [Tooltip("애니메이션 종료 후 마지막 프레임을 유지하다 사라지기까지의 시간(초). 기본 0.1")]
    public float vfxLingerTime = 0.1f;
    [Tooltip("월드 표시 크기(유닛). 투사체/이펙트 스프라이트의 목표 지름")]
    public float visualSize = 0.8f;
    [Tooltip("스프라이트 세로(로컬 Y) 추가 배율. 1=정사각 유지, 2=세로 2배(소드마스터 검기 등 비균등 확대)")]
    public float visualStretchY = 1f;
    [Tooltip("true이면 이펙트가 플레이어에 부착되어 따라다니며 루프 재생된다(마왕 소용돌이처럼 캐릭터를 감싸는 효과)")]
    public bool vfxFollowPlayer = false;
    [Tooltip("이펙트 스프라이트 정렬 순서. 기본 30(캐릭터 위 스킬 효과). 난공불락 충격파 등 바닥 효과는 -20")]
    public int vfxSortingOrder = 30;
    [Tooltip("true이면 이펙트를 중앙이 아니라 하단 기준으로 배치(위로 뻗음). 낙뢰처럼 하늘에서 내려와 끝(하단)이 대상에 닿게 할 때 사용")]
    public bool vfxAnchorBottom = false;

    [Header("노드 임팩트 (체인라이트닝 등)")]
    [Tooltip("타격 지점(노드)마다 1회 재생할 임팩트 시트 프레임. 비우면 노드 이펙트 없음(연결선만). 과부화 체인라이트닝=OVERLOAD2.1_Pr")]
    public Sprite[] nodeFrames;

    [Header("대부호 골드 드롭 (OnMove 전용)")]
    [Tooltip("이동 시 떨구는 골드 코인 프레임(Gold_Coin 등급별). 비우면 코인 없이 폭발만")]
    public Sprite[] dropFrames;
    [Tooltip("골드 코인 애니메이션 속도(FPS)")]
    public float dropFps = 8f;
}
