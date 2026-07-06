using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 런(플레이) 통계 로거.
///  - 이벤트마다 RunEvents.csv에 즉시 append(강제종료/크래시 대비).
///  - 런 종료(사망/클리어) 시 RunStats.csv에 요약 1행 append(엑셀 누적 분석용).
///  - 저장 위치: Application.persistentDataPath (에디터·빌드 공통).
/// 씬 진입 전 자동 생성(부트스트랩)되어 별도 배치 불필요. 층 전환(씬 로드) 넘어 유지.
/// </summary>
public class RunStatsLogger : MonoBehaviour
{
    public static RunStatsLogger Instance { get; private set; }

    private const int MaxFloor = 5;

    private float _runStart;
    private readonly float[] _floorEnter = new float[MaxFloor + 1];
    private readonly float[] _bossKill   = new float[MaxFloor + 1];
    private readonly int[]   _killsByFloor = new int[MaxFloor + 1];
    private int _inventoryOpens, _rerolls, _goldGained, _goldSpent;
    private float _distance;
    private int _currentFloor = 1;
    private bool _runEnded;

    private string _summaryPath, _eventPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("RunStatsLogger");
        go.AddComponent<RunStatsLogger>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _summaryPath = Path.Combine(Application.persistentDataPath, "RunStats.csv");
        _eventPath   = Path.Combine(Application.persistentDataPath, "RunEvents.csv");
        BeginRun();
    }

    private float Elapsed => Time.time - _runStart;
    /// <summary>런 시작부터 경과한 초(층 넘어가도 누적). 보스 체력 겹 상승 등에 사용.</summary>
    public float RunElapsedSeconds => Time.time - _runStart;

    /// <summary>이번 런에서 획득한 총 골드 (메타 재화 환산용).</summary>
    public int GoldGainedTotal => _goldGained;
    private static string F(float v) => v.ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>새 런 시작(리셋). 다시하기/로비→인게임 진입 시 호출.</summary>
    public void BeginRun()
    {
        _runStart = Time.time;
        Array.Clear(_floorEnter, 0, _floorEnter.Length);
        Array.Clear(_bossKill, 0, _bossKill.Length);
        Array.Clear(_killsByFloor, 0, _killsByFloor.Length);
        _inventoryOpens = _rerolls = _goldGained = _goldSpent = 0;
        _distance = 0f;
        _currentFloor = 1;
        _runEnded = false;
        AppendEvent("RUN_START", "");
        FloorEnter(1);   // 1층 진입 기록(진입 시각 0) — 이벤트 로그에도 남김
    }

    // ── 이벤트 훅 ──────────────────────────────────────────────
    public void FloorEnter(int floor)
    {
        _currentFloor = floor;
        if (floor >= 1 && floor <= MaxFloor) _floorEnter[floor] = Elapsed;
        AppendEvent("FLOOR_ENTER", "floor=" + floor);
    }

    public void BossKilled(int floor)
    {
        if (floor >= 1 && floor <= MaxFloor) _bossKill[floor] = Elapsed;
        AppendEvent("BOSS_KILL", "floor=" + floor);
    }

    /// <summary>몬스터 처치. countForFloor=false면 층별 처치수 미집계(슬라임 분열체 등).</summary>
    public void MonsterKilled(string monsterName, int floor, bool countForFloor)
    {
        if (countForFloor && floor >= 1 && floor <= MaxFloor) _killsByFloor[floor]++;
        AppendEvent("KILL", "floor=" + floor + ";name=" + monsterName + ";counted=" + countForFloor);
    }

    public void InventoryOpened() { _inventoryOpens++; AppendEvent("INVENTORY_OPEN", "count=" + _inventoryOpens); }
    public void Reroll()          { _rerolls++;        AppendEvent("REROLL", "count=" + _rerolls); }
    public void GoldGained(int a) { if (a > 0) _goldGained += a; }
    public void GoldSpent(int a)  { if (a > 0) _goldSpent  += a; }
    public void AddDistance(float d) { if (d > 0f) _distance += d; }

    /// <summary>런 종료(사망/클리어). 요약 1행 기록. 중복 호출 무시.</summary>
    public void RunEnd(bool cleared, int deathFloor, string deathZone, string killer, string synergies)
    {
        if (_runEnded) return;
        _runEnded = true;

        AppendEvent(cleared ? "RUN_CLEAR" : "RUN_DEATH",
            "floor=" + deathFloor + ";zone=" + deathZone + ";killer=" + killer);

        string row = BuildSummaryRow(cleared, deathFloor, deathZone, killer, synergies);

        try
        {
            bool newFile = !File.Exists(_summaryPath);
            using var w = new StreamWriter(_summaryPath, true, Encoding.UTF8);
            if (newFile) w.WriteLine(Header());
            w.WriteLine(row);
        }
        catch (Exception e) { Debug.LogWarning("[RunStats] 요약 기록 실패: " + e.Message); }

        // 구글 폼 자동 업로드(설정 시). 로컬 CSV는 항상 백업으로 남음.
        if (UploadEnabled && !string.IsNullOrEmpty(FormUrl) && !string.IsNullOrEmpty(EntryId))
            StartCoroutine(UploadRow(row));

        Debug.Log("[RunStats] 저장: " + _summaryPath);
    }

    private string BuildSummaryRow(bool cleared, int deathFloor, string deathZone, string killer, string synergies)
    {
        var sb = new StringBuilder();
        sb.Append(Csv(Application.version)).Append(',');   // 버전(Player Settings → Version)
        sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(',');
        sb.Append(F(Elapsed)).Append(',');
        sb.Append(cleared ? "CLEAR" : "DEATH").Append(',');
        sb.Append(deathFloor).Append(',');
        sb.Append(Csv(deathZone)).Append(',');
        sb.Append(Csv(killer)).Append(',');
        for (int i = 1; i <= MaxFloor; i++) sb.Append(F(_floorEnter[i])).Append(',');
        for (int i = 1; i <= MaxFloor; i++) sb.Append(F(_bossKill[i])).Append(',');
        for (int i = 1; i <= MaxFloor; i++) sb.Append(_killsByFloor[i]).Append(',');
        int totalKills = 0; for (int i = 1; i <= MaxFloor; i++) totalKills += _killsByFloor[i];
        sb.Append(totalKills).Append(',');
        sb.Append(_goldGained).Append(',');
        sb.Append(_goldSpent).Append(',');
        sb.Append(_inventoryOpens).Append(',');
        sb.Append(_rerolls).Append(',');
        sb.Append(F(_distance)).Append(',');
        sb.Append(Csv(synergies));
        return sb.ToString();
    }

    // ── 구글 폼 업로드 ────────────────────────────────────────
    // 사용법: 구글 폼(긴 답변 1개) 만들고 → 미리채우기 링크로 formResponse URL + entry ID 확인 →
    //         아래 3개를 채우고 UploadEnabled=true. (폼 필드 = CSV 한 줄 전체를 받음)
    private static readonly bool   UploadEnabled = true;
    private static readonly string FormUrl = "https://docs.google.com/forms/d/e/1FAIpQLSeBJGxjzvi5NHpPwYwsfn_SfL0OtXA3-9_j7O_k0RO6gOvbHw/formResponse";
    private static readonly string EntryId = "entry.804028772";

    private System.Collections.IEnumerator UploadRow(string row)
    {
        var form = new WWWForm();
        form.AddField(EntryId, row);
        using var req = UnityWebRequest.Post(FormUrl, form);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning("[RunStats] 업로드 실패: " + req.error);
        else
            Debug.Log("[RunStats] 업로드 완료");
    }

    private static string Header()
    {
        var sb = new StringBuilder("버전,시작시각,플레이시간,결과,사망층,사망존,킬러몬스터,");
        for (int i = 1; i <= MaxFloor; i++) sb.Append("층").Append(i).Append("진입,");
        for (int i = 1; i <= MaxFloor; i++) sb.Append("층").Append(i).Append("보스사망,");
        for (int i = 1; i <= MaxFloor; i++) sb.Append("층").Append(i).Append("처치,");
        sb.Append("총처치,획득골드,소모골드,인벤오픈,리롤,이동거리,종료시시너지");
        return sb.ToString();
    }

    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(",") || s.Contains("\"")) return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private void AppendEvent(string type, string data)
    {
        try
        {
            using var w = new StreamWriter(_eventPath, true, Encoding.UTF8);
            w.WriteLine($"{DateTime.Now:HH:mm:ss},{F(Elapsed)},{type},{data}");
        }
        catch { /* 로깅 실패는 게임 진행에 영향 주지 않음 */ }
    }
}
