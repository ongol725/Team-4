/// <summary>근접 무기의 휘두르는 모션 종류 (플레이어 중심 외부 회전 기반).</summary>
public enum MeleeMotionType
{
    Swing,   // 호(arc)로 휘두르기 (코드 회전)
    Thrust,  // 앞으로 찌르기(전진→복귀)
    Baked,   // 모션이 프레임에 포함된 시트를 제자리 재생(회전 없음)
}

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
