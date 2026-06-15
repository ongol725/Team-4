using UnityEngine;

public class DoorController : MonoBehaviour
{
    private Collider2D col;
    private SpriteRenderer sr;

    // 열린 상태: 파랑 / 닫힌 상태: 진홍
    private static readonly Color ColorOpen   = new Color(0.20f, 0.45f, 1.00f); // 파랑
    private static readonly Color ColorClosed = new Color(0.86f, 0.08f, 0.24f); // 진홍 (Crimson)

    void Awake()
    {
        col = GetComponent<Collider2D>();
        sr  = GetComponent<SpriteRenderer>();

        if (sr != null) sr.color = ColorOpen;
    }

    public void Open()
    {
        if (col != null) col.enabled = false;
        if (sr  != null) sr.color = ColorOpen;
    }

    public void Close()
    {
        if (col != null) col.enabled = true;
        if (sr  != null) sr.color = ColorClosed;
    }
}
