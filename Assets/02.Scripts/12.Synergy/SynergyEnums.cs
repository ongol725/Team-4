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

/// <summary>소환수 행동 패턴 AI 타입</summary>
public enum SummonAIType
{
    FollowAttack,  // 플레이어 추종 + 주변 적 공격
    OrbitPlayer,   // 플레이어 주변 회전
    Stationary,    // 고정형 토템
}
