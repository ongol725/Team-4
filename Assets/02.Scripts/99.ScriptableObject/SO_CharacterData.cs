using UnityEngine;

public enum CharacterType { Warrior, Mage, Rogue }

[CreateAssetMenu(fileName = "SO_CharacterData", menuName = "BagSurvivor/Character Data")]
public class SO_CharacterData : ScriptableObject
{
    [Header("기본 정보")]
    public CharacterType characterType;
    public string characterName;
    [TextArea] public string description;
    public Sprite characterSprite;

    [Header("스탯")]
    [Tooltip("최대 체력")]
    public int maxHp;

    [Tooltip("무기 데미지에 곱하는 배율  (최종 데미지 = 무기 데미지 × attackMultiplier)")]
    public float attackMultiplier = 1f;

    [Tooltip("무기 쿨타임을 나누는 배율  (최종 쿨타임 = 무기 쿨타임 / attackSpeedMultiplier)")]
    public float attackSpeedMultiplier = 1f;

    [Tooltip("이동 속도")]
    public float moveSpeed;

    [Range(0f, 1f)]
    [Tooltip("치명타 확률")]
    public float critChance;
}
