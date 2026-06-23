// ============================================================
// EliteMonster.cs
// 엘리트 몬스터 표식 + 스탯 배율(인스펙터 조절). 스폰 시 RoomMonsterSpawner가
// 이 배율을 난이도 배율에 곱해 적용한다(일반 몬스터에는 영향 없음).
//  - 슬라임 엘리트(1층), 좀비 엘리트(3층) 등 Elite 방 보스에 부착.
//  - 스케일(예: 슬라임 2.5배)은 프리팹 Transform에 직접 지정.
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class EliteMonster : MonoBehaviour
    {
        [Header("엘리트 스탯 배율 (일반 대비)")]
        [Tooltip("체력 배율 (기본 5배)")]
        public float hpMultiplier = 5f;

        [Tooltip("공격 배율")]
        public float attackMultiplier = 1f;
    }
}
