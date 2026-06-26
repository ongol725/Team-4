/// <summary>
/// 시너지 렌더 정렬(sortingOrder) 밴드. 프로젝트는 Sorting Layer가 "Default" 하나뿐이라
/// 모든 그리기 순서를 이 정수 밴드로 통일한다.
///
/// 원칙: 바닥 깔개 &lt; 캐릭터 아래 &lt; 캐릭터 &lt; 스킬 효과 &lt; 연결선
///   - 플레이어/몬스터 본체 = Character(10) (Player.prefab·MonsterController 와 동일)
/// </summary>
public static class SynergyLayers
{
    /// <summary>바닥 깔개 — 성역·난공불락 충격파·대부호 골드코인 (캐릭터 아래로 깔림)</summary>
    public const int Ground    = -20;
    /// <summary>캐릭터 뒤 — 소환수 전반(골렘·페어리·핀볼·대정령·기어)·마왕 소용돌이</summary>
    public const int BelowChar = 5;
    /// <summary>캐릭터 평면 — 플레이어·몬스터</summary>
    public const int Character = 10;
    /// <summary>스킬 효과 — 투사체·낙뢰·광역 VFX·공격 이펙트 (캐릭터 위)</summary>
    public const int Effect    = 30;
    /// <summary>연결선/상단 — 체인라이트닝 등</summary>
    public const int Link      = 35;
}
