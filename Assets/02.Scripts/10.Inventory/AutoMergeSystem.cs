using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// L 키 즉시 합성 시스템.
/// 인벤토리 그리드 + 임시칸에 있는 아이템 중
/// 같은 종류 + 같은 등급 쌍을 자동으로 합성한다.
/// 합성 가능한 쌍이 없어질 때까지 반복(연쇄 합성 지원).
///
/// 사용법: 씬 내 아무 오브젝트(메인카메라 포함)에나 부착.
///         Inspector 연결 불필요 — InventoryGridUI 를 런타임에 자동 탐색한다.
///         인벤토리 씬이 Additive Load 되어 있어야 동작한다.
/// </summary>
public class AutoMergeSystem : MonoBehaviour
{
    private InventoryGridUI _gridUI;

    private InventoryGrid _grid     => _gridUI != null ? _gridUI.Grid     : null;
    private TempSlotUI    _tempSlot => _gridUI != null ? _gridUI.TempSlot : null;

    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
            RunAutoMerge();
    }

    // ─────────────────────────────────────────────────────────────

    private void RunAutoMerge()
    {
        // 인벤토리 씬이 아직 로드 안 됐거나 씬 전환 후 캐시가 날아간 경우 재탐색
        if (_gridUI == null)
            _gridUI = FindAnyObjectByType<InventoryGridUI>();

        if (_gridUI == null) return; // 인벤토리 씬이 로드되지 않은 상태

        // 드래그 중인 아이템이 있으면 실행 안 함
        if (_gridUI.IsAnyFollowingMouse) return;

        int count = 0;
        while (TryMergeOnce()) count++;

        if (count > 0)
            Debug.Log($"[AutoMerge] {count}쌍 합성 완료");
    }

    /// <summary>합성 가능한 쌍 하나를 찾아 실행. 실행 여부를 반환.</summary>
    private bool TryMergeOnce()
    {
        var allItems = GatherAllItems();

        // (SO_ItemData, gradeIndex) 기준 그룹화
        // HasGrades == true(무기/방어구) 이고 5등급 미만만 합성 가능
        var groups = new Dictionary<(SO_ItemData data, int grade), List<ItemInstance>>();
        foreach (var inst in allItems)
        {
            if (!inst.HasGrades || inst.gradeIndex >= 4) continue;
            var key = (inst.data, inst.gradeIndex);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<ItemInstance>();
                groups[key] = list;
            }
            list.Add(inst);
        }

        foreach (var kvp in groups)
        {
            if (kvp.Value.Count < 2) continue;

            // [0] = 살아남아 등급이 오르는 아이템
            // [1] = 소멸하는 아이템
            ExecuteMerge(target: kvp.Value[0], consumed: kvp.Value[1]);
            return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────

    private List<ItemInstance> GatherAllItems()
    {
        var result = new List<ItemInstance>();

        foreach (var inst in _grid.GetAllPlacedInstances())
            result.Add(inst);

        if (_tempSlot != null)
            foreach (var block in _tempSlot.HeldBlocks)
                if (block != null && block.Instance != null)
                    result.Add(block.Instance);

        return result;
    }

    private void ExecuteMerge(ItemInstance target, ItemInstance consumed)
    {
        DestroyItem(consumed);
        target.TryUpgrade();
        RefreshVisual(target);
    }

    private void DestroyItem(ItemInstance inst)
    {
        if (_grid.TryGetOrigin(inst, out _))
        {
            // 그리드에 있는 경우
            // FindItemBlock 은 OnItemUnplaced 호출 전에 먼저 가져와야 함 (dict에서 제거되기 때문)
            var block = _gridUI.FindItemBlock(inst);
            _grid.Remove(inst);
            _gridUI.OnItemUnplaced(inst);
            if (block != null) Destroy(block.gameObject);
        }
        else if (_tempSlot != null)
        {
            // 임시칸에 있는 경우
            ItemBlockUI targetBlock = null;
            foreach (var b in _tempSlot.HeldBlocks)
            {
                if (b != null && b.Instance == inst) { targetBlock = b; break; }
            }
            if (targetBlock != null)
            {
                _tempSlot.OnItemPickedUp(targetBlock);
                Destroy(targetBlock.gameObject);
            }
        }
    }

    private void RefreshVisual(ItemInstance inst)
    {
        if (_grid.TryGetOrigin(inst, out _))
        {
            _gridUI.RefreshItemBlockVisual(inst);
        }
        else if (_tempSlot != null)
        {
            foreach (var b in _tempSlot.HeldBlocks)
            {
                if (b != null && b.Instance == inst)
                {
                    b.RefreshVisuals();
                    break;
                }
            }
        }
    }
}
