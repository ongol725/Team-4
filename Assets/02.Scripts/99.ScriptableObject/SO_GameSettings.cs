using UnityEngine;

/// <summary>
/// 게임 전반의 수치를 팀원이 Inspector에서 쉽게 조정할 수 있는 설정 파일.
/// Assets/00.Settings/GameSettings.asset 에서 편집하세요.
/// </summary>
[CreateAssetMenu(fileName = "GameSettings", menuName = "BagSurvivor/Game Settings")]
public class SO_GameSettings : ScriptableObject
{
    [Header("골드")]
    [Tooltip("게임 시작 시 지급되는 초기 골드")]
    public int startingGold = 50;
}
