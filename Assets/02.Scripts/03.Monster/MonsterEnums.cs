// ============================================================
// MonsterEnums.cs
// 몬스터 시스템에서 사용하는 모든 열거형(Enum) 정의
// 팀 충돌 방지를 위해 몬스터 관련 Enum은 이 파일에서만 관리
// ============================================================
namespace BagSurvivor.Monster
{
    /// <summary>
    /// 몬스터 등급
    /// </summary>
    public enum MonsterGrade
    {
        Normal     = 0,  // 일반 몬스터
        Elite      = 1,  // 정예 몬스터
        MiddleBoss = 2,  // 중간 보스
        Boss       = 3   // 보스
    }

    /// <summary>
    /// 공격 방식 (근거리/원거리)
    /// </summary>
    public enum AttackStyle
    {
        Melee,   // 근접 공격
        Ranged   // 원거리 공격
    }

    /// <summary>
    /// 스폰 방식 (스포너가 참조용으로 사용)
    /// </summary>
    public enum SpawnType
    {
        ViewportEllipse,    // 화면 타원형 범위에서 스폰
        ViewportBothEdge,   // 화면 양쪽 가장자리에서 스폰
        DefaultSpawner      // 특수 조건 스폰 (보스방 진입, 분열 등)
    }

    /// <summary>
    /// 이동 패턴
    /// </summary>
    public enum MovePattern
    {
        StraightChase,    // 플레이어를 향해 최단 거리로 추적
        StopOnCondition,  // 조건 충족 시 정지 후 특수 행동
        Stationary        // 제자리 고정 (이동하지 않음)
    }

    /// <summary>
    /// 공격 패턴
    /// </summary>
    public enum AttackPattern
    {
        Contact,       // 접촉 데미지
        Ranged,        // 원거리 투사체
        Charge,        // 돌진 공격
        AreaOfEffect   // 범위 장판 공격
    }

    /// <summary>
    /// FSM 몬스터 상태
    /// </summary>
    public enum MonsterState
    {
        Tracking,   // 추적/이동 상태
        Hit,        // 피격 이펙트 처리 (이동 방해 없음)
        Knockback,  // 넉백 상태 (이동 정지)
        Die         // 사망 상태
    }
}
