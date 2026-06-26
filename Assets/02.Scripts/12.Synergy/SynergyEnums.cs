/// <summary>스킬이 전개되는 물리적 메커니즘 형태</summary>
public enum SkillType
{
    Projectile,   // 투사체
    AoE,          // 장판/범위
    Slash,        // 즉발 참격
    Buff,         // 플레이어 강화
}

/// <summary>스킬 발동 시 조준 대상 / 생성 위치</summary>
public enum SkillTargetType
{
    Forward,       // 전방 방향
    RandomEnemy,   // 무작위 적
    Self,          // 플레이어 본인
    AreaCenter,    // 화면 중앙
    ForwardDual,   // 좌·우 각각 가장 가까운 적 2명 (처형자)
    ForwardTriple, // 가장 가까운 적 3명 순차 타격 (마왕 프리즘)
    SidePillars,   // 플레이어 좌·우 고정 위치에 기둥 생성 (일렉트로)
    ChainLightning,// 최근접 적부터 차례로 연쇄 타격 (과부화 체인라이트닝)
    FacingForward, // 플레이어가 바라보는 방향으로 발사 (과부화 레이저)
}

/// <summary>피격 시 부여되는 상태이상</summary>
public enum StatusEffectType
{
    None,
    Knockback,    // 넉백
    Stun,         // 기절
    DefIgnore,    // 방어력 무시
    Invincible,   // 무적 (버프용)
}

/// <summary>스킬/소환수의 고유 고정 효과 종류</summary>
public enum FixedEffectType
{
    None,
    Burn,            // 화상 DoT
    Knockback,       // 넉백
    InstantDeath,    // 즉사 (체력 X% 이하 적)
    DamageReduction, // 피해 감소 (난공불락)
    HealArmorHpPct,  // 방어구 HP 비율 회복 (성기사단)
    SpeedPenalty,    // 이동속도 페널티 (과부화)
}

/// <summary>스킬 발동 방식 트리거</summary>
public enum SynergyTriggerType
{
    AutoTimer,   // 쿨타임마다 자동 발동
    OnHitTaken,  // 플레이어 피격 시
    OnMove,      // 이동 거리 누적
    Passive,     // 소환/오브젝트 상시 유지
    Penalty,     // 과부화 패널티
}

/// <summary>시너지 스케일링 기준 스탯</summary>
public enum ScalingStatType
{
    None,
    WPN_ATK_AVG,  // 무기 공격력 평균
    WPN_ATK_SUM,  // 무기 공격력 총합
    ARM_HP_SUM,   // 방어구 체력 총합
}

/// <summary>소환수 행동 패턴 AI 타입</summary>
public enum SummonAIType
{
    FollowAttack,  // 플레이어 추종 + 주변 적 공격
    OrbitPlayer,   // 플레이어 주변 회전
    Stationary,    // 고정형 토템
    Bounce,        // 맵을 튕겨 다니며 접촉 피해 (핀볼)
    GuardOffset,   // 플레이어 기준 고정 오프셋 유지 + 원거리 공격 (마왕 기어)
}
