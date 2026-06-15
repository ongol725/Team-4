// ============================================================
// MonsterData.cs
// 몬스터 개체의 스탯/설정 데이터를 담는 ScriptableObject
// CSV(MonsterDataBase)의 모든 필드를 반영하여 인스펙터에서 수치 조정 가능
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    [CreateAssetMenu(fileName = "MonsterData_", menuName = "BagSurvivor/Monster/MonsterData")]
    public class MonsterData : ScriptableObject
    {
        // ==========================================
        // 기본 정보
        // ==========================================
        [Header("기본 정보")]
        [Tooltip("몬스터 고유 인덱스 (CSV의 Index)")] 
        public int index;

        [Tooltip("몬스터 한글 이름")] 
        public string monsterName;

        [Tooltip("몬스터 영문 이름 (프리팹/코드 참조용)")] 
        public string englishName;

        [Tooltip("몬스터 등급")] 
        public MonsterGrade grade = MonsterGrade.Normal;

        // ==========================================
        // 전투 스탯
        // ==========================================
        [Header("전투 스탯")]
        [Tooltip("최대 체력")] 
        public int maxHP = 20;

        [Tooltip("공격력 (접촉 데미지 또는 투사체 데미지)")] 
        public int attack = 2;

        [Tooltip("방어력 (받는 데미지 = MAX(1, 무기데미지 - Defense))")] 
        public int defense = 0;

        [Tooltip("공격 방식 (근거리/원거리)")] 
        public AttackStyle attackStyle = AttackStyle.Melee;

        [Tooltip("공격 패턴")] 
        public AttackPattern attackPattern = AttackPattern.Contact;

        // ==========================================
        // 이동
        // ==========================================
        [Header("이동")]
        [Tooltip("이동 속도")] 
        public float moveSpeed = 3f;

        [Tooltip("이동 패턴")] 
        public MovePattern movePattern = MovePattern.StraightChase;

        // ==========================================
        // 넉백
        // ==========================================
        [Header("넉백")]
        [Tooltip("넉백 저항력 (0.0 ~ 1.0, 1.0이면 넉백 완전 면역)")]
        [Range(0f, 1f)] 
        public float kbResist = 0f;

        [Tooltip("넉백 쿨다운 (초)")] 
        public float kbCooldown = 0f;

        // ==========================================
        // 스폰 정보 (스포너가 참조용으로 사용, 몬스터 자체는 사용 안 함)
        // ==========================================
        [Header("스폰 정보 (스포너 참조용)")]
        [Tooltip("스폰 방식")] 
        public SpawnType spawnType = SpawnType.ViewportEllipse;

        [Tooltip("스폰 간격 (초)")] 
        public float spawnInterval = 2f;

        [Tooltip("한 번에 스폰되는 개체 수")] 
        public int spawnCount = 1;

        // ==========================================
        // 드롭 아이템
        // ==========================================
        [Header("드롭 아이템")]
        [Tooltip("드롭 아이템 ID")] 
        public string dropItemID = "";

        [Tooltip("드롭 아이템 수량")] 
        public int dropItemValue = 0;

        // ==========================================
        // 프리팹 경로
        // ==========================================
        [Header("프리팹")]
        [Tooltip("프리팹 에셋 경로")] 
        public string prefabPath = "";

        // ==========================================
        // 유틸리티 메서드
        // ==========================================

        /// <summary>
        /// 방어력을 적용한 최종 받는 데미지를 계산합니다.
        /// 기획서 공식: MAX(1, 무기데미지 - Defense)
        /// </summary>
        /// <param name="rawDamage">무기의 기본 데미지</param>
        /// <returns>방어력 적용 후 최종 데미지 (최소 1)</returns>
        public int CalculateDamageTaken(int rawDamage)
        {
            return Mathf.Max(1, rawDamage - defense);
        }
    }
}
