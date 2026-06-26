// ============================================================
// SynergyTestPanel.cs  (개발/디버그 전용)
//
// 아이템 배치 없이 시너지를 임의로 구성해 즉시 발동시켜 보는 테스트 패널.
//  - 빈 GameObject(또는 SynergyManager 오브젝트)에 이 컴포넌트를 붙이면 됨.
//  - 플레이 모드에서 F1 으로 패널 토글.
//  - 각 시너지의 등급을 골라 [적용] → SynergyManager 가 즉시 재구성.
//  - 난공불락(피격)·대부호(이동) 같은 트리거형은 하단 시뮬레이트 버튼으로 강제 발동.
//
// UNITY_EDITOR 또는 DEVELOPMENT_BUILD 에서만 컴파일된다(출시 빌드 제외).
// ============================================================
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using BagSurvivor.UI;

public class SynergyTestPanel : MonoBehaviour
{
    [Header("참조 (비우면 자동 탐색)")]
    [SerializeField] private SynergyManager _manager;

    [Header("설정")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
    [Tooltip("WPN_ATK_SUM/AVG 스케일링 기준값 (더미 무기 공격력)")]
    [SerializeField] private int _wpnAtk = 100;
    [Tooltip("ARM_HP_SUM 스케일링 기준값 (방어구 체력 총합)")]
    [SerializeField] private int _armHp = 100;

    private bool _show = true;
    private bool _minimized = false;
    private Vector2 _scroll;

    // 시너지별 선택 등급 (-1 = 해제, 0~3 = Bronze/Silver/Gold/Prism)
    private readonly Dictionary<SynergyType, int> _sel = new();

    // 표시할 시너지 목록 (None·미구현 제외)
    private static readonly (SynergyType type, string name)[] Synergies =
    {
        (SynergyType.Assassin,    "암살단"),
        (SynergyType.SwordMaster, "소드마스터"),
        (SynergyType.HolyKnight,  "성기사단"),
        (SynergyType.DemonLord,   "마왕"),
        (SynergyType.Tycoon,      "대부호"),
        (SynergyType.Executioner, "처형자"),
        (SynergyType.SpiritMage,  "정령술사"),
        (SynergyType.Pinball,     "핀볼"),
        (SynergyType.Overload,    "과부화"),
        (SynergyType.Electro,     "일렉트로"),
        (SynergyType.Impregnable, "난공불락"),
        (SynergyType.Titan,       "티탄"),
        (SynergyType.Fairy,       "페어리"),
    };

    private static readonly string[] GradeLabels = { "해제", "브론즈", "실버", "골드", "프리즘" };

    private void Awake()
    {
        if (_manager == null) _manager = FindFirstObjectByType<SynergyManager>();
        foreach (var s in Synergies) _sel[s.type] = -1;
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey)) _show = !_show;
    }

    private void OnGUI()
    {
        if (!_show) return;

        // ── 최소화 상태: 작은 복원 버튼만 표시 ──────────────
        if (_minimized)
        {
            GUILayout.BeginArea(new Rect(10, 10, 150, 30), GUI.skin.box);
            if (GUILayout.Button("시너지 테스트 ▢")) _minimized = false;
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 380, Screen.height - 20), GUI.skin.box);

        // ── 헤더 (제목 + 최소화 버튼) ───────────────────────
        GUILayout.BeginHorizontal();
        GUILayout.Label($"시너지 테스트 패널  ({_toggleKey} 토글)");
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("─", GUILayout.Width(28))) _minimized = true;
        GUILayout.EndHorizontal();

        if (_manager == null)
        {
            GUILayout.Label("SynergyManager 를 찾지 못했습니다.");
            if (GUILayout.Button("다시 탐색")) _manager = FindFirstObjectByType<SynergyManager>();
            GUILayout.EndArea();
            return;
        }

        // ── 스케일링 기준 스탯 ──────────────────────────────
        GUILayout.BeginHorizontal();
        GUILayout.Label("무기공격력", GUILayout.Width(70));
        _wpnAtk = ParseField(_wpnAtk, 60);
        GUILayout.Space(8);
        GUILayout.Label("방어구HP", GUILayout.Width(60));
        _armHp = ParseField(_armHp, 60);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ── 시너지별 등급 선택 ──────────────────────────────
        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (var s in Synergies)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(s.name, GUILayout.Width(72));
            int cur = _sel[s.type];

            // '해제'는 항상 표시
            DrawGradeToggle(s.type, -1, cur);

            // 실제 바인딩된 등급만 버튼으로 표시(존재하지 않는 등급은 숨김 → 혼란 방지)
            for (int g = 0; g <= 3; g++)
            {
                if (!_manager.HasBindingFor(s.type, (SynergyGrade)g)) continue;
                DrawGradeToggle(s.type, g, cur);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        GUILayout.Space(4);

        // ── 액션 버튼 ───────────────────────────────────────
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("적용", GUILayout.Height(28))) Apply();
        if (GUILayout.Button("해제(정식 복귀)", GUILayout.Height(28)))
        {
            foreach (var s in Synergies) _sel[s.type] = -1;
            _manager.ReleaseTestLock();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("피격 시뮬레이트")) _manager.TestSimulateHit();
        if (GUILayout.Button("이동 시뮬레이트")) _manager.TestSimulateMove();
        GUILayout.EndHorizontal();

        GUILayout.Label("· [적용] 시 방 진입 등 정식 로드아웃 덮어쓰기를 잠금");
        GUILayout.Label("· 게임 정상 진행하려면 [해제(정식 복귀)]");
        GUILayout.Label("· AutoTimer형(암살단·티탄 등)은 [적용] 후 자동 발동");
        GUILayout.Label("· 난공불락=피격, 대부호=이동 시 발동 → 시뮬레이트 버튼 사용");
        GUILayout.Label("· 각 시너지는 실제 존재하는 등급 버튼만 표시됨");

        GUILayout.EndArea();
    }

    private int ParseField(int value, float width)
    {
        string s = GUILayout.TextField(value.ToString(), GUILayout.Width(width));
        return int.TryParse(s, out int v) ? Mathf.Max(0, v) : value;
    }

    /// <summary>등급 토글 버튼 1개 렌더링. grade: -1=해제, 0~3=브/실/골/프리즘.</summary>
    private void DrawGradeToggle(SynergyType type, int grade, int cur)
    {
        bool on  = cur == grade;
        bool now = GUILayout.Toggle(on, GradeLabels[grade + 1], GUI.skin.button, GUILayout.Width(48));
        if (now && !on) _sel[type] = grade;
    }

    private void Apply()
    {
        var loadout = new BattleLoadout();

        // 스케일링용 더미 무기 1개 + 방어구 HP (실제 무기 시스템과 무관)
        loadout.Weapons.Add(new WeaponLoadoutEntry { attackPower = _wpnAtk });
        loadout.TotalArmorHp = _armHp;
        loadout.TotalHpBonus = _armHp;

        foreach (var s in Synergies)
        {
            int g = _sel[s.type];
            if (g < 0) continue;
            loadout.ActiveSynergies.Add(new ActiveSynergyEntry
            {
                type  = s.type,
                grade = (SynergyGrade)g,
                count = 99,
            });
        }

        _manager.ApplyLoadoutForTest(loadout);
        Debug.Log($"[SynergyTestPanel] 시너지 {loadout.ActiveSynergies.Count}개 적용 " +
                  $"(무기 {_wpnAtk} / 방어구HP {_armHp})");
    }
}
#endif
