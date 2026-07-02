using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    // 타입별 등장 확률 가중치: weapon=70, armor=18, inventory=12, accessory=0 (반지는 상점 제외)
    private static readonly int[] TypeWeights = { 70, 18, 12, 0 };
    private const int TypeWeightTotal = 100;

    // 상점 등급(1~7)별 레어도 가중치 [shopGrade-1][0=Common, 1=Rare, 2=Epic, 3=Legendary]
    private static readonly int[,] RarityWeightsByGrade =
    {
        { 90, 10,  0,  0 }, // grade 1
        { 80, 10, 10,  0 }, // grade 2
        { 65, 20, 10,  5 }, // grade 3
        { 55, 20, 15, 10 }, // grade 4
        { 40, 30, 15, 15 }, // grade 5
        { 30, 30, 20, 20 }, // grade 6
        { 20, 30, 25, 25 }, // grade 7
    };
    private const int RarityCount = 4;

    // 버킷 0=무기, 1=방어구, 2=인벤토리 블록, 3=장신구(반지)
    private readonly List<SO_ItemData>[] _buckets =
    {
        new(),
        new(),
        new(),
        new(),
    };

    // 활성 칸이 이 비율 이상이면 인벤토리 블록을 '배치 가능한 형태'로만 등장시킴
    private const float BLOCK_FILTER_RATIO = 0.7f;

    private InventoryGrid _grid;
    private InventoryGrid Grid
    {
        get
        {
            if (_grid == null) _grid = FindFirstObjectByType<InventoryGrid>();
            return _grid;
        }
    }

    private void Awake() => LoadAllItems();

    private void LoadAllItems()
    {
        foreach (var b in _buckets) b.Clear();
        _buckets[0].AddRange(Resources.LoadAll<SO_ItemData>("ScriptableObjects/Weapons"));
        // 보스 무기(031~033)는 상점에서 제외
        _buckets[0].RemoveAll(w => w != null &&
            (w.name.StartsWith("031") || w.name.StartsWith("032") || w.name.StartsWith("033")));
        _buckets[1].AddRange(Resources.LoadAll<SO_ItemData>("ScriptableObjects/Armor"));
        _buckets[2].AddRange(Resources.LoadAll<SO_ItemData>("ScriptableObjects/InventoryBlocks"));
        // 반지(장신구)는 상점 제외 — 중간보스/엘리트 드랍으로만 획득 (_buckets[3] 비움)
    }

    public SO_ItemData[] GenerateShopItems(int count = 5, int shopGrade = 1)
    {
        if (_buckets[0].Count == 0) LoadAllItems();

        var result  = new SO_ItemData[count];
        var usedIDs = new HashSet<string>();

        for (int i = 0; i < count; i++)
        {
            SO_ItemData pick = null;

            for (int attempt = 0; attempt < 100; attempt++)
            {
                int typeIdx = PickType();
                var bucket  = _buckets[typeIdx];
                if (bucket.Count == 0) continue;

                // 인벤토리 블록·장신구는 레어도 무관 균등 선택
                SO_ItemData candidate;
                if (typeIdx == 2 || typeIdx == 3)
                {
                    candidate = bucket[Random.Range(0, bucket.Count)];

                    // 인벤토리 블록(2): 활성 70% 이상이면 남은 잠긴 영역에 배치 가능한 형태만 허용(못 놓으면 재추첨)
                    if (typeIdx == 2 && Grid != null &&
                        Grid.ActiveCellCount() >= Grid.TotalCellCount * BLOCK_FILTER_RATIO &&
                        !Grid.CanPlaceBlockAnywhere(candidate))
                        continue;
                }
                else
                {
                    int rarity   = PickRarity(shopGrade);
                    var filtered = FindByRarity(bucket, rarity);
                    // 해당 레어도 아이템 없으면 타입 전체에서 폴백
                    var pool = filtered.Count > 0 ? filtered : bucket;
                    candidate = pool[Random.Range(0, pool.Count)];
                }

                if (string.IsNullOrEmpty(candidate.itemID) || !usedIDs.Contains(candidate.itemID))
                {
                    pick = candidate;
                    break;
                }
            }

            // 중복 회피 100회 실패 시 폴백
            if (pick == null)
            {
                var bucket = _buckets[PickType()];
                if (bucket.Count > 0)
                    pick = bucket[Random.Range(0, bucket.Count)];
            }

            if (pick == null) continue;
            result[i] = pick;
            if (!string.IsNullOrEmpty(pick.itemID))
                usedIDs.Add(pick.itemID);
        }

        return result;
    }

    // 누적 가중치로 타입 인덱스 반환 (0=무기, 1=방어구, 2=인벤)
    private int PickType()
    {
        int roll       = Random.Range(0, TypeWeightTotal);
        int cumulative = 0;
        for (int i = 0; i < TypeWeights.Length; i++)
        {
            cumulative += TypeWeights[i];
            if (roll < cumulative) return i;
        }
        return TypeWeights.Length - 1;
    }

    // 상점 등급 기반 레어도 인덱스 반환 (0=Common~3=Legendary)
    private int PickRarity(int shopGrade)
    {
        int gradeIdx = Mathf.Clamp(shopGrade - 1, 0, RarityWeightsByGrade.GetLength(0) - 1);

        int total = 0;
        for (int i = 0; i < RarityCount; i++)
            total += RarityWeightsByGrade[gradeIdx, i];

        int roll       = Random.Range(0, total);
        int cumulative = 0;
        for (int i = 0; i < RarityCount; i++)
        {
            cumulative += RarityWeightsByGrade[gradeIdx, i];
            if (roll < cumulative) return i;
        }
        return 0;
    }

    private static List<SO_ItemData> FindByRarity(List<SO_ItemData> bucket, int rarityIdx)
    {
        var result = new List<SO_ItemData>();
        foreach (var item in bucket)
        {
            if ((int)item.rarity == rarityIdx)
                result.Add(item);
        }
        return result;
    }
}
