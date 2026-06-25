using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택창의 카드 1장을 담당한다.
/// CharacterSelectUI가 Setup()을 호출해 데이터와 클릭 콜백을 주입한다.
/// </summary>
public class CharacterCardUI : MonoBehaviour
{
    [Header("UI 참조")]
    public Image  characterImage;
    public Text   nameText;
    public Text   hpText;
    public Text   atkText;
    public Text   atkSpeedText;
    public Text   speedText;
    public Text   dpsText;      // 선택적 — 없으면 스킵
    public Text   critText;
    public Text   descText;
    public Image  cardBackground;
    public Button cardButton;

    [Header("선택 강조 색상")]
    public Color normalColor   = new Color(0.2f, 0.2f, 0.2f, 0.85f);
    public Color selectedColor = new Color(0.9f, 0.75f, 0.1f, 1f);

    private System.Action _onClick;

    private void Awake()
    {
        cardButton?.onClick.AddListener(() => _onClick?.Invoke());
    }

    public void Setup(SO_CharacterData data, System.Action onClick)
    {
        _onClick = onClick;
        if (data == null) return;

        if (characterImage != null)
        {
            characterImage.sprite  = data.characterSprite;
            characterImage.enabled = data.characterSprite != null;
        }

        float dps = data.attackMultiplier * data.attackSpeedMultiplier * 10f;

        if (nameText     != null) nameText.text     = data.characterName;
        if (hpText       != null) hpText.text       = $"체력         {data.maxHp}";
        if (atkText      != null) atkText.text      = $"공격력     ×{data.attackMultiplier:F1}";
        if (atkSpeedText != null) atkSpeedText.text = $"공격속도  ×{data.attackSpeedMultiplier:F1}";
        if (speedText    != null) speedText.text    = $"스피드     {data.moveSpeed:F0}";
        if (dpsText      != null) dpsText.text      = $"DPS          {dps:F1}";
        if (critText     != null) critText.text     = $"치명타     {data.critChance * 100f:F0}%";
        if (descText     != null) descText.text     = data.description;
    }

    public void SetSelected(bool selected)
    {
        if (cardBackground != null)
            cardBackground.color = selected ? selectedColor : normalColor;
    }
}
