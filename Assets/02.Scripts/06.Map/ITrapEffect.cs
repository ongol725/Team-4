using UnityEngine;

// 모든 함정 효과들이 반드시 지켜야 하는 규칙
public interface ITrapEffect
{
    // 함정이 발동될 때 호출될 함수 (밟은 플레이어의 정보를 넘겨받음)
    float ExecuteTrap(GameObject player);
}