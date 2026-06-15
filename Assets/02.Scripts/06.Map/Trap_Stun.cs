using UnityEngine;
using System.Collections;

public class TrapEffect_Stun : MonoBehaviour, ITrapEffect
{
    public float stunDuration = 2f; 

    // float를 반환하도록 수정
    public float ExecuteTrap(GameObject player)
    {
        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.StartCoroutine(pm.ApplyStun(stunDuration));
        }
        
        // 스턴 시간만큼 함정 오브젝트가 살아있어야 하므로, 그 시간을 그대로 반환해 줌!
        return stunDuration; 
    }
}