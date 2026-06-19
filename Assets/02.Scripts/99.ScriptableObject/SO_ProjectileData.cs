using UnityEngine;

[CreateAssetMenu(fileName = "NewProjectile", menuName = "BagSurvivor/Projectile")]
public class SO_ProjectileData : ScriptableObject
{
    [Header("비주얼")]
    public Sprite projectileSprite;

    [Header("기본 스탯")]
    public float damage;
    public float lifetime;      // 소멸시간 (사거리)

    [Header("특수 속성")]
    public int   bounceCount;   // 도탄 횟수
    public int   pierceCount;   // 관통 횟수 (0 = 첫 충돌 즉시 소멸)
    public bool  hasExplosion;
    [Tooltip("hasExplosion = true 시 유효")]
    public float explosionRadius;
}
