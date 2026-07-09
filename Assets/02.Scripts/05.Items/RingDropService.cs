using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 반지(장신구) 드랍·획득 처리.
///  - 엘리트/중간보스 사망 시 확률로 반지 픽업을 바닥에 생성(6종 균등 랜덤).
///  - 플레이어가 주우면 인벤토리 임시칸(TempSlotUI)에 블록으로 투입.
///  - InventoryStore 씬이 전투 중 additive 로드돼 있어 즉시 전달 가능(없으면 대기 후 FlushPending).
/// 픽업 스프라이트는 임시로 반지 아이콘을 사용(전용 스프라이트 준비되면 교체).
/// </summary>
public static class RingDropService
{
    private static SO_ItemData[] _rings;
    private static readonly List<SO_ItemData> _pending = new();

    private static SO_ItemData[] Rings
    {
        get
        {
            if (_rings == null || _rings.Length == 0)
                _rings = Resources.LoadAll<SO_ItemData>("ScriptableObjects/Accessories");
            return _rings;
        }
    }

    /// <summary>확률(chance) 판정 후 통과하면 임의 반지 픽업을 pos에 생성한다.</summary>
    public static void TryDrop(Vector3 pos, float chance)
    {
        var rings = Rings;
        if (rings == null || rings.Length == 0) return;
        if (Random.value > chance) return;

        var ring = rings[Random.Range(0, rings.Length)];
        SpawnPickup(pos, ring);
    }

    private static void SpawnPickup(Vector3 pos, SO_ItemData ring)
    {
        var go = new GameObject("RingPickup") { transform = { position = new Vector3(pos.x, pos.y, 0f) } };
        go.transform.localScale = Vector3.one * 0.5f; // 드랍 이미지 가로세로 1/2배(과대 표시 방지)
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ring.itemImage;   // ponytail: 임시 = 반지 아이콘. 전용 픽업 스프라이트 준비되면 교체.
        sr.sortingOrder = 20;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.8f;
        go.AddComponent<RingPickup>().ring = ring;
    }

    /// <summary>픽업 시 호출 — 임시칸에 반지 블록 생성(실패 시 대기열 보관).</summary>
    public static void Collect(SO_ItemData ring)
    {
        if (ring != null && !DeliverToTempSlot(ring)) _pending.Add(ring);
    }

    /// <summary>대기 중인 반지를 임시칸에 투입(인벤토리 열 때 호출).</summary>
    public static void FlushPending()
    {
        if (_pending.Count == 0) return;
        var copy = _pending.ToArray();
        _pending.Clear();
        foreach (var r in copy)
            if (!DeliverToTempSlot(r)) _pending.Add(r);
    }

    private static bool DeliverToTempSlot(SO_ItemData ring)
    {
        var temp   = Object.FindFirstObjectByType<TempSlotUI>();
        var gridUI = Object.FindFirstObjectByType<InventoryGridUI>();
        if (temp == null || gridUI == null || gridUI.Grid == null) return false;

        var go = new GameObject("RingBlock", typeof(RectTransform));
        go.transform.SetParent(gridUI.transform.root, false);
        var block = go.AddComponent<ItemBlockUI>();
        block.Initialize(new ItemInstance { data = ring }, gridUI, gridUI.Grid, gridUI.CellSize);
        temp.ReceiveBlock(block);
        return true;
    }
}
