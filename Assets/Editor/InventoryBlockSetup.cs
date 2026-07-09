// 스크립트 컴파일 완료 시 자동 실행:
// Assets/Resources/ScriptableObjects/InventoryBlocks/ 폴더에 10종 블록 에셋을 생성한다.
// 각 에셋이 이미 존재하면 건너뛰고, 구 에셋(InventoryBlock_Expand)은 자동 삭제한다.

using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class InventoryBlockSetup
{
    const string ParentFolder = "Assets/Resources/ScriptableObjects";
    const string FolderName   = "InventoryBlocks";
    const string FolderPath   = ParentFolder + "/" + FolderName;
    const string OldAssetPath = FolderPath + "/InventoryBlock_Expand.asset";

    static InventoryBlockSetup()
    {
        EditorApplication.delayCall += CreateAssetsIfMissing;
    }

    // ─────────────────────────────────────────────────────────────

    struct BlockDef
    {
        public string       fileName;
        public string       itemID;
        public string       itemName;
        public int          cost;
        public Vector2Int[] cells;
    }

    static readonly BlockDef[] Defs =
    {
        // ── 1칸 ──
        new BlockDef
        {
            fileName = "InventoryBlock_1x1",
            itemID   = "inv_block_1x1",
            itemName = "인벤토리 블록 (1칸)",
            cost     = 10,
            cells    = new[] { new Vector2Int(0, 0) },
        },

        // ── 2칸 ──
        new BlockDef
        {
            fileName = "InventoryBlock_2H",
            itemID   = "inv_block_2h",
            itemName = "인벤토리 블록 (2칸 가로)",
            cost     = 10,
            cells    = new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) },
        },
        new BlockDef
        {
            fileName = "InventoryBlock_2V",
            itemID   = "inv_block_2v",
            itemName = "인벤토리 블록 (2칸 세로)",
            cost     = 10,
            cells    = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) },
        },

        // ── 3칸 직선 ──
        new BlockDef
        {
            fileName = "InventoryBlock_3H",
            itemID   = "inv_block_3h",
            itemName = "인벤토리 블록 (3칸 가로)",
            cost     = 15,
            cells    = new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) },
        },
        new BlockDef
        {
            fileName = "InventoryBlock_3V",
            itemID   = "inv_block_3v",
            itemName = "인벤토리 블록 (3칸 세로)",
            cost     = 15,
            cells    = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
        },

        // ── 3칸 ㄱ자 4종 ──
        // A형:  ■·      B형:  ·■
        //       ■■            ■■
        new BlockDef
        {
            fileName = "InventoryBlock_LA",
            itemID   = "inv_block_la",
            itemName = "인벤토리 블록 (ㄱ자 A)",
            cost     = 15,
            cells    = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1) },
        },
        new BlockDef
        {
            fileName = "InventoryBlock_LB",
            itemID   = "inv_block_lb",
            itemName = "인벤토리 블록 (ㄱ자 B)",
            cost     = 15,
            cells    = new[] { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) },
        },
        // C형:  ■■      D형:  ■■
        //       ■·            ·■
        new BlockDef
        {
            fileName = "InventoryBlock_LC",
            itemID   = "inv_block_lc",
            itemName = "인벤토리 블록 (ㄱ자 C)",
            cost     = 15,
            cells    = new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0) },
        },
        new BlockDef
        {
            fileName = "InventoryBlock_LD",
            itemID   = "inv_block_ld",
            itemName = "인벤토리 블록 (ㄱ자 D)",
            cost     = 15,
            cells    = new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },
        },

        // ── 4칸 2×2 ──
        new BlockDef
        {
            fileName = "InventoryBlock_2x2",
            itemID   = "inv_block_2x2",
            itemName = "인벤토리 블록 (2×2)",
            cost     = 20,
            cells    = new[]
            {
                new Vector2Int(0, 0), new Vector2Int(0, 1),
                new Vector2Int(1, 0), new Vector2Int(1, 1),
            },
        },
    };

    // ─────────────────────────────────────────────────────────────

    static void CreateAssetsIfMissing()
    {
        // 구 에셋 삭제 (1칸짜리 단일 블록으로 대체됨)
        if (AssetDatabase.LoadAssetAtPath<SO_InventoryBlockData>(OldAssetPath) != null)
        {
            AssetDatabase.DeleteAsset(OldAssetPath);
            Debug.Log("[Team4] 구 인벤토리 블록 에셋 삭제 → " + OldAssetPath);
        }

        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder(ParentFolder, FolderName);

        bool anyCreated = false;
        foreach (var def in Defs)
        {
            string path = FolderPath + "/" + def.fileName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<SO_InventoryBlockData>(path) != null)
                continue;

            var data = ScriptableObject.CreateInstance<SO_InventoryBlockData>();
            data.itemID          = def.itemID;
            data.itemName        = def.itemName;
            data.itemDescription = $"잠긴 인벤토리 칸 {def.cells.Length}개를 개방합니다.";
            data.rarity          = ItemRarity.Common;
            data.cost            = def.cost;
            data.cells           = def.cells;
            data.expandRows      = 0;
            data.expandCols      = 0;

            AssetDatabase.CreateAsset(data, path);
            anyCreated = true;
            Debug.Log("[Team4] 인벤토리 블록 SO 생성 → " + path);
        }

        if (anyCreated)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Team4] 인벤토리 블록 10종 에셋 생성 완료");
        }
    }
}
