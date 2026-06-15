using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Items/Weapon")]
public class SO_WeaponData : SO_ItemData
{
    [Header("무기 스탯")]
    public int attackPower;
    public float attackSpeed;

    [Header("투사체")]
    [Tooltip("원거리 무기일 경우 발사체 프리팹 연결. 근거리면 비워두세요.")]
    public GameObject projectile;
    public float projectileSpeed;
    public int range;

    [Header("공격 방식")]
    public string attackStyle;
    public int maxTargets;
    public string lv5AttackStyle;
}
