// ============================================================
// MetaProgression.cs
// 영구 메타 진행 시스템 (반복 플레이 기틀)
//  - 메타 재화(정수): 런 종료 시 성과 환산 지급, 로비에서 업그레이드/리롤에 소모
//  - 영구 업그레이드 레벨 저장 (id → level)
//  - JSON 파일 저장: persistentDataPath/meta_save.json (게임 재시작에도 유지)
//  - 정적 클래스 — 씬/오브젝트 무관, 어디서든 접근 가능
// ============================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class MetaProgression
{
    /// <summary>메타 재화 표시명 (기획 확정 전 임시).</summary>
    public const string CURRENCY_NAME = "정수";

    [Serializable]
    private class SaveData
    {
        public int currency;                                   // 메타 재화 잔액
        public List<string> upgradeIds = new List<string>();   // 업그레이드 id 목록
        public List<int> upgradeLevels = new List<int>();      // 위와 1:1 대응 레벨
        public int totalRuns;                                  // 누적 런 수
        public int totalClears;                                // 누적 클리어 수
    }

    private static SaveData _data;
    private static string SavePath => Path.Combine(Application.persistentDataPath, "meta_save.json");

    /// <summary>재화 변동 통지 (로비 UI 갱신용). 인자: 현재 잔액</summary>
    public static event Action<int> onCurrencyChanged;

    public static int Currency    { get { Load(); return _data.currency; } }
    public static int TotalRuns   { get { Load(); return _data.totalRuns; } }
    public static int TotalClears { get { Load(); return _data.totalClears; } }

    // ── 저장/로드 ────────────────────────────────────────────
    private static void Load()
    {
        if (_data != null) return;
        try
        {
            if (File.Exists(SavePath))
                _data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
        }
        catch (Exception e) { Debug.LogWarning($"[Meta] 저장 파일 로드 실패(새로 시작): {e.Message}"); }
        if (_data == null) _data = new SaveData();
    }

    private static void Save()
    {
        try { File.WriteAllText(SavePath, JsonUtility.ToJson(_data, true)); }
        catch (Exception e) { Debug.LogError($"[Meta] 저장 실패: {e.Message}"); }
    }

    // ── 재화 ────────────────────────────────────────────────
    public static void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        Load();
        _data.currency += amount;
        Save();
        onCurrencyChanged?.Invoke(_data.currency);
    }

    /// <summary>재화 소모. 잔액 부족 시 false(차감 없음).</summary>
    public static bool SpendCurrency(int amount)
    {
        Load();
        if (amount <= 0 || _data.currency < amount) return false;
        _data.currency -= amount;
        Save();
        onCurrencyChanged?.Invoke(_data.currency);
        return true;
    }

    // ── 업그레이드 레벨 ──────────────────────────────────────
    public static int GetUpgradeLevel(string id)
    {
        Load();
        int i = _data.upgradeIds.IndexOf(id);
        return i >= 0 ? _data.upgradeLevels[i] : 0;
    }

    public static void SetUpgradeLevel(string id, int level)
    {
        Load();
        int i = _data.upgradeIds.IndexOf(id);
        if (i >= 0) _data.upgradeLevels[i] = level;
        else { _data.upgradeIds.Add(id); _data.upgradeLevels.Add(level); }
        Save();
    }

    // ── 런 보상 ──────────────────────────────────────────────
    /// <summary>런 종료 재화 환산·지급: 획득골드×10% + 도달층×50 + 킬수×1.
    /// 패배해도 지급(반복 플레이 동기). 반환: 지급된 재화량.</summary>
    public static int GrantRunReward(int goldGained, int floor, int kills, bool cleared)
    {
        Load();
        int reward = Mathf.Max(0, goldGained / 10)
                   + Mathf.Max(0, floor) * 50
                   + Mathf.Max(0, kills);

        _data.totalRuns++;
        if (cleared) _data.totalClears++;
        _data.currency += reward;
        Save();
        onCurrencyChanged?.Invoke(_data.currency);

        Debug.Log($"[Meta] 런 보상 +{reward} {CURRENCY_NAME} (골드 {goldGained}, {floor}층, {kills}킬, 클리어={cleared}) → 잔액 {_data.currency}");
        return reward;
    }
}
