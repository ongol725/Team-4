// 시너지 타입별 컬러 플레이스홀더 스프라이트를 런타임에 생성·캐싱한다.
// SO_SynergyConfig.icon 이 null 일 때 SynergyCalculator 가 fallback 으로 사용한다.
using System.Collections.Generic;
using UnityEngine;

public static class SynergyIconHelper
{
    static readonly Dictionary<SynergyType, Color> Colors =
        new Dictionary<SynergyType, Color>
        {
            { SynergyType.Assassin,    new Color(0.40f, 0.10f, 0.55f) }, // 암살단  — 보라
            { SynergyType.SwordMaster, new Color(0.25f, 0.50f, 0.80f) }, // 소드마스터 — 강철 청색
            { SynergyType.HolyKnight,  new Color(0.95f, 0.85f, 0.20f) }, // 성기사단 — 황금
            { SynergyType.DemonLord,   new Color(0.75f, 0.08f, 0.08f) }, // 마왕    — 짙은 빨강
            { SynergyType.Titan,       new Color(0.45f, 0.45f, 0.45f) }, // 티탄    — 회색
            { SynergyType.Tycoon,      new Color(0.95f, 0.60f, 0.10f) }, // 대부호  — 주황/금
            { SynergyType.Executioner, new Color(0.55f, 0.05f, 0.05f) }, // 처형자  — 암적색
            { SynergyType.SpiritMage,  new Color(0.10f, 0.75f, 0.75f) }, // 정령술사 — 청록
            { SynergyType.Fairy,       new Color(0.95f, 0.55f, 0.85f) }, // 페어리  — 분홍
            { SynergyType.Pinball,     new Color(0.95f, 0.45f, 0.10f) }, // 핀볼    — 오렌지
            { SynergyType.Overload,    new Color(0.90f, 0.80f, 0.00f) }, // 과부하  — 노랑
            { SynergyType.Electro,     new Color(0.30f, 0.60f, 0.95f) }, // 일렉트로 — 밝은 파랑
            { SynergyType.Impregnable, new Color(0.10f, 0.50f, 0.25f) }, // 난공불락 — 녹색
        };

    static readonly Dictionary<SynergyType, Sprite> Cache =
        new Dictionary<SynergyType, Sprite>();

    const int Size = 40;

    /// <summary>시너지 타입에 맞는 플레이스홀더 스프라이트를 반환한다. 최초 호출 시 생성 후 캐싱한다.</summary>
    public static Sprite GetIcon(SynergyType type)
    {
        // 씬 재로드 시 생성된 Sprite/Texture2D가 파괴될 수 있으므로 null 여부를 재확인
        if (Cache.TryGetValue(type, out var cached) && cached != null) return cached;

        Color fill = Colors.TryGetValue(type, out var c) ? c : Color.white;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = $"SynergyIcon_{type}" };

        float cx = (Size - 1) * 0.5f;
        float cy = (Size - 1) * 0.5f;
        float r  = cx;
        float rInner = r - 3f;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float dx   = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                Color pixel;
                if (dist <= rInner)
                {
                    // 위쪽(y 큰 쪽)을 밝게 — 가짜 조명
                    float t = y / (float)(Size - 1);
                    pixel   = Color.Lerp(fill * 0.6f, fill * 1.25f, t);
                    pixel.a = 1f;
                }
                else if (dist <= r)
                {
                    pixel   = Color.white * 0.85f;
                    pixel.a = 1f;
                }
                else
                {
                    pixel = Color.clear;
                }

                tex.SetPixel(x, y, pixel);
            }
        }
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        sprite.name = $"SynergyIcon_{type}";

        Cache[type] = sprite;
        return sprite;
    }
}
