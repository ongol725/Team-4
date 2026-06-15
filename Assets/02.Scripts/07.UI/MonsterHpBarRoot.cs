// ============================================================
// MonsterHpBarRoot.cs
// 몬스터 HP바가 부착될 UI 캔버스를 제공하는 로케이터
//  - 우선 전투 HUD 캔버스(L4_HUD2)를 사용
//  - 없으면 임의의 ScreenSpaceOverlay 캔버스, 그래도 없으면 자동 생성
//  - 결과를 캐시하여 매번 탐색 비용을 피함 (최적화)
// ============================================================
using UnityEngine;
using UnityEngine.UI;

namespace BagSurvivor.UI
{
    public static class MonsterHpBarRoot
    {
        private static Canvas cached;

        public static Transform GetParent()
        {
            if (cached != null) return cached.transform;

            // 1) 전투 HUD 레이어 우선
            var l4 = GameObject.Find("L4_HUD2");
            if (l4 != null)
            {
                var c = l4.GetComponent<Canvas>();
                if (c != null) { cached = c; return c.transform; }
            }

            // 2) 아무 ScreenSpaceOverlay 캔버스
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) { cached = c; return c.transform; }
            }

            // 3) 없으면 생성
            var go = new GameObject("MonsterHpBarLayer", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var nc = go.GetComponent<Canvas>();
            nc.renderMode = RenderMode.ScreenSpaceOverlay;
            nc.sortingOrder = 4;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            cached = nc;
            return go.transform;
        }
    }
}