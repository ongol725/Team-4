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
    public string     attackStyle;
    public string     lv5AttackStyle;

    [Header("투사체 (등급 공통)")]
    [Tooltip("원거리 무기 발사체 프리팹. 근거리는 비워두세요.")]
    public GameObject        projectile;
    public SO_ProjectileData projectileData;
    public float             projectileSpeed;
    public int               range;
    public int               maxTargets;

    [Header("합성 등급별 스탯 (인덱스 0 = 1등급 ~ 4 = 5등급)")]
    public WeaponGradeStats[] gradeStats = new WeaponGradeStats[5];
}
