using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    // 희귀도 가중치: Common=60, Rare=30, Epic=8, Legendary=2
    private static readonly int[] RarityWeights = { 60, 30, 8, 2 };

    private List<SO_ItemData> _allItems = new();

    private void Awake() => LoadAllItems();

    private void LoadAllItems()
    {
        _allItems.Clear();
        _allItems.AddRange(Resources.LoadAll<SO_ItemData>("ScriptableObjects/Weapons"));
        _allItems.AddRange(Resources.LoadAll<SO_ItemData>("ScriptableObjects/Armor"));
        _allItems.AddRange(Resources.LoadAll<SO_ItemData>("ScriptableObjects/Accessories"));
    }

    public SO_ItemData[] GenerateShopItems(int count = 5)
    {
        if (_allItems.Count == 0) LoadAllItems();

        var pool   = BuildWeightedPool();
        var result = new SO_ItemData[count];
        var usedIDs = new HashSet<string>();

        for (int i = 0; i < count; i++)
        {
            SO_ItemData pick = null;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var candidate = pool[Random.Range(0, pool.Count)];
                if (string.IsNullOrEmpty(candidate.itemID) || !usedIDs.Contains(candidate.itemID))
                {
                    pick = candidate;
                    break;
                }
            }

            // 중복 회피 실패 시 그냥 랜덤 선택
            if (pick == null)
                pick = pool[Random.Range(0, pool.Count)];

            result[i] = pick;
            if (!string.IsNullOrEmpty(pick.itemID))
                usedIDs.Add(pick.itemID);
        }
        return result;
    }

    private List<SO_ItemData> BuildWeightedPool()
    {
        var pool = new List<SO_ItemData>();
        foreach (var item in _allItems)
        {
            int w = RarityWeights[Mathf.Clamp((int)item.rarity, 0, RarityWeights.Length - 1)];
            for (int i = 0; i < w; i++)
                pool.Add(item);
        }
        return pool;
    }
}
