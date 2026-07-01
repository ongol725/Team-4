using UnityEngine;

/// <summary>
/// 발밑 그림자(BlobShadow) 공용 설정. Resources/ShadowSettings.asset 하나를 모든 그림자가 공유한다.
/// 인스펙터에서 이 에셋 한 곳만 수정하면 몬스터·플레이어 그림자 전체에 적용된다.
/// </summary>
[CreateAssetMenu(fileName = "ShadowSettings", menuName = "BagSurvivor/Shadow Settings")]
public class ShadowSettings : ScriptableObject
{
    [Tooltip("메인 스프라이트 가로폭 대비 그림자 폭 (0.8 = 80%)")]
    [Range(0f, 2f)] public float widthRatio = 0.8f;

    [Tooltip("그림자 폭 대비 높이 (납작한 정도, 0.4 = 폭의 40%)")]
    [Range(0.05f, 1f)] public float heightRatio = 0.4f;

    [Tooltip("그림자 진하기(불투명도)")]
    [Range(0f, 1f)] public float alpha = 0.35f;

    [Tooltip("발밑 기준 Y 오프셋(겹침 방지로 살짝 위로)")]
    public float feetYOffset = 0.05f;

    [Header("플레이어 전용")]
    [Tooltip("플레이어 그림자 폭 배수 (1=몬스터와 동일, 0.6=60%로 축소). 몬스터엔 영향 없음")]
    [Range(0f, 2f)] public float playerWidthMultiplier = 1f;

    [Tooltip("플레이어 그림자의 루트 기준 로컬 Y 위치(발밑 자동계산 대신 고정)")]
    public float playerShadowLocalY = -0.4f;
}
