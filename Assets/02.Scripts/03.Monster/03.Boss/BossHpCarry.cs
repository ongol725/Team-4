// ============================================================
// BossHpCarry.cs
// 던전(02.Ingame)에서 시간에 따라 오른 보스 최대 체력을 보스룸(06.BossRoom)으로 이월.
//  - BossHpBar가 standalone(보스 미연결) 상태로 상승하는 동안 최신 값을 여기에 기록.
//  - 보스룸 진입 시 BossHpBarLink가 이 값을 보스 실제 최대 체력으로 적용.
//  - 정적이라 씬 전환을 넘어 유지된다(같은 플레이 세션). 새 런이 02.Ingame을 다시 띄우면
//    그 씬의 바가 5000부터 다시 기록하므로 자동으로 갱신된다.
// ============================================================
namespace BagSurvivor.Monster
{
    public static class BossHpCarry
    {
        public static bool Has;
        public static int MaxHP;

        public static void Set(int maxHP) { MaxHP = maxHP; Has = true; }
        public static void Clear() { Has = false; MaxHP = 0; }
    }
}
