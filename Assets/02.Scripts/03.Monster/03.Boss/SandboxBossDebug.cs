// ============================================================
// SandboxBossDebug.cs
// 보스 샌드박스 검증 전용 - 키로 보스 체력 감소(페이즈 전환 테스트용)
//  - 샌드박스 플레이어는 공격이 없어 보스 HP를 깎을 수 없으므로,
//    K 키로 보스에 직접 피해를 줘 40% 전환을 확인한다.
//  - 새 Input System 기준(Keyboard.current).
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class SandboxBossDebug : MonoBehaviour
    {
        [Tooltip("보스 피해 키")]
        public Key damageKey = Key.K;

        [Tooltip("한 번 누를 때 보스에 주는 피해(방어력 적용 전)")]
        public int damagePerPress = 2000;

        [Tooltip("2페이즈 강화 모드 토글 키(전환 없이 패턴 강화 테스트)")]
        public Key togglePhase2Key = Key.P;

        private MonsterController mc;
        private bool phase2On;

        private void Awake()
        {
            mc = GetComponent<MonsterController>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || mc == null) return;

            if (kb[damageKey].wasPressedThisFrame)
                mc.TakeDamage(damagePerPress);

            // P: 모든 패턴 phase2Mode 토글(강화 패턴 즉시 확인용)
            if (kb[togglePhase2Key].wasPressedThisFrame)
            {
                phase2On = !phase2On;
                foreach (var p in GetComponents<BossPatternBase>())
                    p.phase2Mode = phase2On;
                Debug.Log($"[SandboxBossDebug] phase2Mode = {phase2On}");
            }
        }
    }
}
