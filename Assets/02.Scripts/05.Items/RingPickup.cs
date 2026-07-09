using UnityEngine;

/// <summary>바닥에 떨어진 반지. 플레이어가 닿으면 임시칸으로 획득된다(돈 픽업과 동일한 방식).</summary>
[RequireComponent(typeof(Collider2D))]
public class RingPickup : MonoBehaviour
{
    [System.NonSerialized] public SO_ItemData ring;
    private bool _collected;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected || ring == null || !other.CompareTag("Player")) return;
        _collected = true;
        RingDropService.Collect(ring);
        Destroy(gameObject);
    }
}
