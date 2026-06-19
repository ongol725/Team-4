public enum WeaponAttackStyleType
{
    Unknown = 0,
    SingleTarget,       // 범위 내 가장 가까운 적 1인 추적 투사체
    MeleeFan,           // 보는 방향 부채꼴 근접 (범위 안 다수)
    MeleeSingle,        // 보는 방향 단일 근접
    SpreadShot,         // 산탄 (다방향 투사체)
    BurstFire,          // 연속 발사
    AreaDrop,           // 적 위 낙하 범위 공격
    ThrownExplosive,    // 투척 후 폭발
    Boomerang,          // 왕복 부메랑
    PierceLine,         // 전방 직선 관통
    Sniper,             // 가장 먼 적 저격
}
