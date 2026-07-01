using UnityEngine;

/// <summary>
/// 합성 등급별로 변하는 스탯만 담는다. (인덱스 0 = 1등급 ~ 4 = 5등급)
/// </summary>
[System.Serializable]
public class WeaponGradeStats
{
    public int   attackPower;
    public float attackSpeed;
}

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Items/Weapon")]
public class SO_WeaponData : SO_ItemData
{
    [Header("공격 방식 (등급 공통)")]
    public WeaponAttackStyleType attackStyleType;
    public string     attackStyle;
    public string     lv5AttackStyle;

    [Header("투사체 (등급 공통)")]
    [Tooltip("원거리 무기 발사체 프리팹. 근거리는 비워두세요.")]
    public GameObject        projectile;
    public SO_ProjectileData projectileData;
    public float             projectileSpeed;
    public int               range;
    public int               maxTargets;

    [Header("공격 애니메이션 (03.Prefabs/03.Weapons 시트, ④ 메뉴로 자동 채움)")]
    [Tooltip("원거리=투사체, 근접=휘두르는 이펙트로 재생되는 프레임 배열. 비우면 기존 itemImage 정적 표시로 폴백")]
    public Sprite[] attackFrames;
    [Tooltip("공격 애니메이션 재생 속도(FPS)")]
    public float    attackFps = 12f;
    [Tooltip("보조 투사체(예: 할버드 5단계 검기) 프레임. 비우면 미사용")]
    public Sprite[] auxProjectileFrames;

    [Header("근접 모션 (플레이어 중심 외부 회전, 무기는 12시 기준)")]
    [Tooltip("Swing=호로 휘두르기 / Thrust=앞으로 찌르기. 무기별로 지정(기본 Swing)")]
    public MeleeMotionType meleeMotion = MeleeMotionType.Swing;
    [Tooltip("스윙 호 각도(도). 0이면 90 사용. 등급별 확장은 추후")]
    public float meleeSwingAngle = 90f;
    [Tooltip("무기를 플레이어 앞쪽으로 띄우는 거리(유닛). 캐릭터 몸을 벗어나도록. 찌르기 전진 거리에도 사용. 0이면 1.6")]
    public float meleeReach = 1.6f;
    [Tooltip("근접 모션 1회 시간(초). 0이면 0.2")]
    public float meleeMotionDuration = 0.2f;

    [Header("합성 등급별 스탯 (인덱스 0 = 1등급 ~ 4 = 5등급)")]
    public WeaponGradeStats[] gradeStats = new WeaponGradeStats[5];
}
