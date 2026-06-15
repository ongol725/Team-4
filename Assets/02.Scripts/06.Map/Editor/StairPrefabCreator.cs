using UnityEngine;
using UnityEditor;

public static class StairPrefabCreator
{
    [MenuItem("Tools/던전/계단 프리팹 생성")]
    public static void CreateStairPrefab()
    {
        // --- 루트 오브젝트 ---
        GameObject root = new GameObject("StairPrefab");

        // SpriteRenderer: Unity 내장 Square 스프라이트, 노란색
        SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        sr.color = new Color(1f, 0.85f, 0.1f); // 노란색 (계단 임시 색)
        sr.sortingOrder = 10;

        // StairController 먼저 부착
        root.AddComponent<StairController>();

        // 스프라이트 native 크기 → 1x1 유닛이 되도록 scale 보정 (벽 타일 1칸과 동일)
        if (sr.sprite != null)
        {
            Vector2 spriteSize = sr.sprite.bounds.size;
            root.transform.localScale = new Vector3(1f / spriteSize.x, 1f / spriteSize.y, 1f);
        }

        // BoxCollider2D: size = 스프라이트 native 크기 → scale 곱하면 정확히 1x1 유닛
        // (col.size × localScale = spriteSize × (1/spriteSize) = 1)
        BoxCollider2D col = root.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = (sr.sprite != null) ? sr.sprite.bounds.size : Vector2.one;

        // --- 저장 경로 ---
        string folder = "Assets/03.Prefebs";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "03.Prefebs");

        string path = folder + "/StairPrefab.prefab";

        // 기존 파일 있으면 덮어쓰기
        PrefabUtility.SaveAsPrefabAsset(root, path);
        GameObject.DestroyImmediate(root);

        AssetDatabase.Refresh();
        Debug.Log($"[StairPrefabCreator] 계단 프리팹 생성 완료: {path}");

        // Project 창에서 선택
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }
}
