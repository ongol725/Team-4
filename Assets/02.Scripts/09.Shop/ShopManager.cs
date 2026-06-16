using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    // 타입별 등장 확률 가중치: weapon=75, armor=15, inventory=10
    private static readonly int[] TypeWeights = { 75, 15, 10 };
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

    // 버킷 0=무기, 1=방어구, 2=인벤토리 블록
    private readonly List<SO_ItemData>[] _buckets =
    {
        new(),
        new(),
        new(),
    };

    private void Awake() => LoadAllItems();

    private void LoadAllItems()
    {
        foreach (var b in _buckets) b.Clear();
        _buckets[0].AddRange(Resources.LoadAll<SO_ItemData>("ScriptableObjects/Weapons"));
        _buckets[1].AddRange(Resources.LoadAll<SO_ItemData>("ScriptableObjects/Armor"));
        _buckets[2].AddRange(Resources.LoadAll<SO_ItemData>("ScriptableObjects/InventoryBlocks"));
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

                // 인벤토리 블록은 레어도 무관 균등 선택
                SO_ItemData candidate;
                if (typeIdx == 2)
                {
                    candidate = bucket[Random.Range(0, bucket.Count)];
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
