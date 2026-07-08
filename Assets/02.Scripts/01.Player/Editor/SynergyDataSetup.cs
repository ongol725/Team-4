// ============================================================
// SynergyDataSetup.cs  (Editor 전용)
//
// 메뉴: BagSurvivor > Setup > ① Create Synergy Data Assets
//         → SO_SkillData / SO_SummonData / SO_SynergyConfig 에셋 일괄 생성
//       BagSurvivor > Setup > ② Fill SynergyManager Bindings
//         → 씬의 SynergyManager Inspector 바인딩 자동 채우기
//
// 데이터 출처: 콘텐츠_시너지기획서_20260608_v0.4
//             (툴팁 텍스트: Assets/02.Scripts/시너지_툴팁_텍스트_테이블.md 기준)
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BagSurvivor.UI;

namespace BagSurvivor.SynergyEditor
{
    public static class SynergyDataSetup
    {
        private const string SkillDir  = "Assets/Resources/Synergy/Skills";
        private const string SummonDir = "Assets/Resources/Synergy/Summons";
        private const string ConfigDir = "Assets/Resources/Synergy";
        private const string ConfigPath = "Assets/Resources/Synergy/SynergyConfig.asset";

        // ── 메뉴 ① : 에셋 생성 ───────────────────────────────────────
        [MenuItem("BagSurvivor/Setup/① Create Synergy Data Assets")]
        public static void CreateAssets()
        {
            EnsureFolder(ConfigDir);
            EnsureFolder(SkillDir);
            EnsureFolder(SummonDir);

            var skills  = BuildAllSkills();
            var summons = BuildAllSummons();

            // 대정령 프리즘: 주기적 광역 공격 스킬을 uniqueSkill 로 연결
            if (summons.TryGetValue("SUM_GIANT_GOLEM", out var giant) &&
                skills.TryGetValue("SK_SPIRIT_NOVA", out var nova))
            {
                giant.uniqueSkill = nova;
                EditorUtility.SetDirty(giant);
            }

            BuildSynergyConfig(skills, summons);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SynergyDataSetup] 완료! Skills {skills.Count}개, Summons {summons.Count}개 생성 → {ConfigDir}/");
        }

        // ── 메뉴 ② : 씬 SynergyManager 바인딩 채우기 ────────────────
        [MenuItem("BagSurvivor/Setup/② Fill SynergyManager Bindings")]
        public static void FillBindings()
        {
            var mgr = Object.FindFirstObjectByType<SynergyManager>();
            if (mgr == null)
            {
                Debug.LogWarning("[SynergyDataSetup] 씬에서 SynergyManager를 찾지 못했습니다. 씬을 열고 다시 실행하세요.");
                return;
            }

            var so = new SerializedObject(mgr);
            FillSkillBindings(so);
            FillSummonBindings(so);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(mgr);
            Debug.Log("[SynergyDataSetup] SynergyManager 바인딩 완료!");
        }

        // ─────────────────────────────────────────────────────────────
        // 스킬 에셋 전체 생성
        // ─────────────────────────────────────────────────────────────

        static Dictionary<string, SO_SkillData> BuildAllSkills()
        {
            var d = new Dictionary<string, SO_SkillData>();

            // ── 암살단 (Assassin) — AutoTimer / WPN_ATK_AVG / Forward ──
            // 수리검은 콘셉트(슬라이드 1)대로 적을 관통(pierce 999)하며 날아간다. 투사체 크기 2배(0.8→1.6)
            d["SK_ASS_1"] = Sk("SK_ASS_1", "수리검 (브론즈)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_AVG, dmg:2.5f, cd:3f, range:15f, pierce:999, vSize:1.6f);
            // 실버부터 공격 속도 1.5배(쿨타임 3→2초) — 골드도 승계
            d["SK_ASS_2"] = Sk("SK_ASS_2", "수리검 (실버)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_AVG, dmg:3.0f, cd:2f, range:15f, pierce:999, vSize:1.6f);
            // 골드 '거대 수리검': 1.2(×1.5)에서 추가로 2배 → 2.4
            d["SK_ASS_3"] = Sk("SK_ASS_3", "거대 수리검 (골드)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_AVG, dmg:3.5f, cd:2f, range:15f, extra:1, pierce:999, vSize:2.4f);

            // ── 일렉트로 (Electro) — AutoTimer / WPN_ATK_SUM / RandomEnemy (기획서: 무작위 낙뢰 1/3개) ──
            // 번개: 가로:세로 = 1:1.5 비율 유지(stretchY 1.5), 전체 크기 vSize 2.4
            // 낙뢰: 캐릭터 주변 일정 반경(range=6) 내 랜덤 적 타격, 애니 4배속(fps 48)
            d["SK_ELEC_1"] = Sk("SK_ELEC_1", "마법 낙뢰 1개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_SUM, dmg:0.8f, cd:3f, range:6f, extra:1,
                fx:FixedEffectType.KillCooldownReduction, fxVal:50f, vSize:2.4f, stretchY:1.5f, fps:48f, anchorBottom:true);
            d["SK_ELEC_2"] = Sk("SK_ELEC_2", "마법 낙뢰 3개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_SUM, dmg:0.8f, cd:3f, range:6f, extra:3,
                fx:FixedEffectType.KillCooldownReduction, fxVal:50f, vSize:2.4f, stretchY:1.5f, fps:48f, anchorBottom:true);

            // ── 처형자 (Executioner) — AutoTimer / WPN_ATK_SUM / ForwardDual + InstantDeath ──
            // 낫: 투사체 크기 ×1.5(1.6→2.4), 이동속도 -50%(15→7.5), 애니 2.5배속(12→30)
            d["SK_SCYTHE_1"] = Sk("SK_SCYTHE_1", "사신의 낫 (브론즈)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.ForwardDual,
                ScalingStatType.WPN_ATK_SUM, dmg:1.0f, cd:3f, range:20f,
                fx:FixedEffectType.InstantDeath, fxVal:5f, pierce:999, vSize:2.4f, fps:30f, projSpeed:7.5f);
            d["SK_SCYTHE_2"] = Sk("SK_SCYTHE_2", "사신의 낫 (실버)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.ForwardDual,
                ScalingStatType.WPN_ATK_SUM, dmg:1.1f, cd:3f, range:20f,
                fx:FixedEffectType.InstantDeath, fxVal:10f, pierce:999, vSize:2.4f, fps:30f, projSpeed:7.5f);
            d["SK_SCYTHE_3"] = Sk("SK_SCYTHE_3", "사신의 낫 (골드)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.ForwardDual,
                ScalingStatType.WPN_ATK_SUM, dmg:1.3f, cd:3f, range:20f,
                fx:FixedEffectType.InstantDeath, fxVal:15f, pierce:999, vSize:2.4f, fps:30f, projSpeed:7.5f);

            // ── 소드마스터 (SwordMaster) — AutoTimer / WPN_ATK_AVG / Forward ──
            // 검기: 크기 확대(0.8→4.8) + 세로 2배(stretchY) + 바라보는 방향(FacingForward) 발사
            // 대미지 전 등급 2배 상향(1.0/1.1/1.3 → 2.0/2.2/2.6), 실버부터 크기·범위 50% 확대(vSize 4.8→7.2, 골드 승계)
            d["SK_SWORD_1"] = Sk("SK_SWORD_1", "검기 (브론즈)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.FacingForward,
                ScalingStatType.WPN_ATK_AVG, dmg:2.0f, cd:2f, range:15f, vSize:4.8f, stretchY:2f);
            d["SK_SWORD_2"] = Sk("SK_SWORD_2", "대형 검기 (실버)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.FacingForward,
                ScalingStatType.WPN_ATK_AVG, dmg:2.2f, cd:2f, range:15f, vSize:7.2f, stretchY:2f);
            d["SK_SWORD_3"] = Sk("SK_SWORD_3", "연속 검기 (골드)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.FacingForward,
                ScalingStatType.WPN_ATK_AVG, dmg:2.6f, cd:2f, range:15f, hits:2, hitInterval:0.15f, vSize:7.2f, stretchY:2f);

            // ── 티탄 (Titan) — AutoTimer / WPN_ATK_AVG / RandomEnemy × N ──
            // 돌: 크기 5배(vSize 0.8→4.0), 낙하 애니 2배속(fps 60→120; 총 0.2→0.1초),
            //     하단 앵커(돌 하단이 적 중심에 착지), 착지 시점 타격(dmgDelay 0.06초), 착지 후 0.5초 잔류(linger)
            d["SK_METEOR_1"] = Sk("SK_METEOR_1", "돌 떨구기 3개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_AVG, dmg:5.0f, cd:4f, range:18f, extra:3, vSize:4f, dmgDelay:0.06f, fps:120f, linger:0.5f, anchorBottom:true);
            d["SK_METEOR_2"] = Sk("SK_METEOR_2", "돌 떨구기 6개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_AVG, dmg:5.0f, cd:4f, range:18f, extra:6, vSize:4f, dmgDelay:0.06f, fps:120f, linger:0.5f, anchorBottom:true);
            d["SK_METEOR_3"] = Sk("SK_METEOR_3", "돌 떨구기 9개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_AVG, dmg:5.0f, cd:4f, range:18f, extra:9, vSize:4f, dmgDelay:0.06f, fps:120f, linger:0.5f, anchorBottom:true);

            // ── 난공불락 (Impregnable) — OnHitTaken / ARM_HP_SUM / Self + DamageReduction ──
            // 충격파: 표시 크기 5배(기본 0.8 → 4.0), 바닥 깔림(vfxSort -1)
            d["SK_FORTRESS_1"] = Sk("SK_FORTRESS_1", "충격파 (브론즈)",
                SynergyTriggerType.OnHitTaken, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:0.1f, cd:0f, range:2f,
                fx:FixedEffectType.DamageReduction, fxVal:10f, vSize:4f, vfxSort:1);
            d["SK_FORTRESS_2"] = Sk("SK_FORTRESS_2", "거대 충격파 (실버)",
                SynergyTriggerType.OnHitTaken, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:0.1f, cd:0f, range:3f,
                fx:FixedEffectType.DamageReduction, fxVal:20f, vSize:6f, vfxSort:1);
            d["SK_FORTRESS_3"] = Sk("SK_FORTRESS_3", "파멸 충격파 (골드)",
                SynergyTriggerType.OnHitTaken, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:0.15f, cd:0f, range:3f,
                fx:FixedEffectType.DamageReduction, fxVal:35f, vSize:6f, vfxSort:1);
            d["SK_FORTRESS_4"] = Sk("SK_FORTRESS_4", "무적 충격파 (프리즘)",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:0.2f, cd:1f, range:4f,
                fx:FixedEffectType.DamageReduction, fxVal:60f, vSize:8f, vfxSort:1);

            // ── 마왕 (DemonLord) — 누적형 동시 발동 (콘셉트 19~22) ──
            // 투사체(검기)·소용돌이는 스킬, 기어는 소환수(SUM_DEMON_GEAR)로 구성한다.
            d["SK_DEMON_1"] = Sk("SK_DEMON_1", "지옥 검기 (투사체)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_SUM, dmg:0.7f, cd:2f, range:20f,
                fx:FixedEffectType.Burn, fxVal:0f, pierce:999, vSize:3.2f);
            // 연옥 소용돌이: 캐릭터를 감싸는 불꽃 고리(Devil_Slash)가 플레이어를 따라다니며 주변 적 타격
            d["SK_DEMON_2"] = Sk("SK_DEMON_2", "연옥 소용돌이",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:0.45f, cd:1f, range:3f,
                followVfx:true, vSize:3f);
            d["SK_DEMON_3"] = Sk("SK_DEMON_3", "마왕의 멸천참(예비)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:0.2f, cd:0.1f, range:8f, vSize:1.6f);
            d["SK_DEMON_4"] = Sk("SK_DEMON_4", "지옥 검기 3연 (프리즘)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.ForwardTriple,
                ScalingStatType.WPN_ATK_SUM, dmg:1.2f, cd:2f, range:50f,
                fx:FixedEffectType.Burn, fxVal:0f, pierce:999, vSize:3.2f);

            // ── 대부호 (Tycoon) — OnMove / WPN_ATK_SUM / 골드 드랍 ──
            d["SK_GOLD_BOMB_1"] = Sk("SK_GOLD_BOMB_1", "골드 폭발 (브론즈)",
                SynergyTriggerType.OnMove, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:0.9f, cd:0f, range:3f,
                extra:3, duration:1.5f);
            d["SK_GOLD_BOMB_2"] = Sk("SK_GOLD_BOMB_2", "골드 폭발 (실버)",
                SynergyTriggerType.OnMove, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:1.0f, cd:0f, range:3f,
                extra:5, duration:1.5f);
            d["SK_GOLD_BOMB_3"] = Sk("SK_GOLD_BOMB_3", "골드 폭발 (골드)",
                SynergyTriggerType.OnMove, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:1.1f, cd:0f, range:3f,
                extra:7, duration:1.5f);
            d["SK_GOLD_FAST"] = Sk("SK_GOLD_FAST", "골드 폭발 (프리즘)",
                SynergyTriggerType.OnMove, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:1.5f, cd:0f, range:3f,
                extra:15, duration:0.5f);

            // ── 과부하 (Overload) — Penalty ──
            d["SK_OVERLOAD_PENALTY"] = Sk("SK_OVERLOAD_PENALTY", "과부하 패널티",
                SynergyTriggerType.Penalty, SkillType.Buff, SkillTargetType.Self,
                ScalingStatType.None, dmg:0f, cd:10f, range:0f,
                fx:FixedEffectType.SpeedPenalty, fxVal:65f, effectVal:30f); // 이동속도 65%·피해량 30% 감소
            // 과부하 프리즘: 3종 효과 동시 발동 (콘셉트 슬라이드 24). 각각 Penalty 트리거로 무한 발동.
            // 레이저: 발사형 투사체가 아니라 바라보는 방향으로 생성되는 지속 빔.
            // range=빔 길이, vSize=빔 두께, cd=데미지 틱 간격으로 해석된다(SynergyBeam).
            d["SK_OVERLOAD_LASER"] = Sk("SK_OVERLOAD_LASER", "과부하 레이저 (프리즘)",
                SynergyTriggerType.Penalty, SkillType.Beam, SkillTargetType.FacingForward,
                ScalingStatType.WPN_ATK_AVG, dmg:3.0f, cd:0.2f, range:4f, vSize:2f);
            d["SK_OVERLOAD_CHAIN"] = Sk("SK_OVERLOAD_CHAIN", "과부하 체인라이트닝 (프리즘)",
                SynergyTriggerType.Penalty, SkillType.AoE, SkillTargetType.ChainLightning,
                ScalingStatType.WPN_ATK_AVG, dmg:6.0f, cd:0.5f, range:50f, extra:4);
            // 비눗방울: 적 조준이 아니라 플레이어 주변 랜덤 위치에 흩뿌려 그 자리 적을 타격.
            // range=흩뿌리는 반경, vSize=비눗방울 지름(타격 반경=절반).
            d["SK_OVERLOAD_BUBBLE"] = Sk("SK_OVERLOAD_BUBBLE", "과부하 비눗방울 (프리즘)",
                SynergyTriggerType.Penalty, SkillType.AoE, SkillTargetType.RandomAroundSelf,
                ScalingStatType.WPN_ATK_AVG, dmg:4.0f, cd:0.4f, range:4f, extra:3, vSize:1.6f);

            // ── 대정령 광역(프리즘) — SUM_GIANT_GOLEM.uniqueSkill 로 연결 ──
            // 스펙: 8초마다 화면 내 모든 적에게 대량 피해(range 100 = 전체 화면 커버).
            d["SK_SPIRIT_NOVA"] = Sk("SK_SPIRIT_NOVA", "대정령 광역 강타",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.AreaCenter,
                ScalingStatType.WPN_ATK_AVG, dmg:5.0f, cd:0f, range:100f, vSize:3f); // 발동 모션 크기

            return d;
        }

        // ─────────────────────────────────────────────────────────────
        // 소환수 에셋 전체 생성
        // ─────────────────────────────────────────────────────────────

        static Dictionary<string, SO_SummonData> BuildAllSummons()
        {
            var d = new Dictionary<string, SO_SummonData>();

            // ── 핀볼 (Pinball) — Bounce ──
            // 핀볼: 오브젝트 크기 ×1.5 (displayScale 1.0 → 1.5)
            d["SUM_PINBALL_1"] = Sum("SUM_PINBALL_1", "핀볼 (브론즈)",
                SummonAIType.Bounce, ScalingStatType.WPN_ATK_AVG,
                atk:1.8f, spd:12f, atkCd:0.2f, atkRange:1f, dur:-1f, scale:1.5f);
            d["SUM_PINBALL_2"] = Sum("SUM_PINBALL_2", "핀볼 (실버)",
                SummonAIType.Bounce, ScalingStatType.WPN_ATK_AVG,
                atk:2.25f, spd:18f, atkCd:0.2f, atkRange:1f, dur:-1f, scale:1.5f);

            // ── 페어리 (Fairy) — OrbitPlayer ──
            // 크기 ×2(scale 0.4→0.8), 타격 범위를 시각 크기에 맞춤(atkRange 1.5→0.75 = 시각 반경)
            d["SUM_FAIRY_1"] = Sum("SUM_FAIRY_1", "요정 (브론즈)",
                SummonAIType.OrbitPlayer, ScalingStatType.WPN_ATK_SUM,
                atk:1.0f, spd:90f, atkCd:0.5f, atkRange:0.75f, dur:-1f, scale:0.8f);
            d["SUM_FAIRY_2"] = Sum("SUM_FAIRY_2", "요정 (실버)",
                SummonAIType.OrbitPlayer, ScalingStatType.WPN_ATK_SUM,
                atk:1.0f, spd:90f, atkCd:0.5f, atkRange:0.75f, dur:-1f, scale:0.8f);
            d["SUM_FAIRY_3"] = Sum("SUM_FAIRY_3", "고속 요정 (골드)",
                SummonAIType.OrbitPlayer, ScalingStatType.WPN_ATK_SUM,
                atk:1.0f, spd:150f, atkCd:0.375f, atkRange:0.75f, dur:-1f, scale:0.8f);

            // ── 정령술사 (SpiritMage) — FollowAttack ──
            d["SUM_GOLEM_1"] = Sum("SUM_GOLEM_1", "정령 골렘 (브론즈)",
                SummonAIType.FollowAttack, ScalingStatType.WPN_ATK_AVG,
                atk:1.0f, spd:4f, atkCd:1.0f, atkRange:1.5f, dur:-1f);
            d["SUM_GOLEM_2"] = Sum("SUM_GOLEM_2", "정령 골렘 (실버)",
                SummonAIType.FollowAttack, ScalingStatType.WPN_ATK_AVG,
                atk:1.0f, spd:5.2f, atkCd:1.0f, atkRange:1.5f, dur:-1f);
            d["SUM_GOLEM_3"] = Sum("SUM_GOLEM_3", "정령 골렘 (골드)",
                SummonAIType.FollowAttack, ScalingStatType.WPN_ATK_AVG,
                atk:2.0f, spd:5.2f, atkCd:1.0f, atkRange:1.5f, dur:-1f);
            // 대정령: 캐릭터 머리 위 고정(GuardOffset + spd 0 = 공전 안 함). 주기 광역(uniqueSkill)으로 공격.
            // 일반 공격은 가까운 순 4명 동시 타격(maxTargets), 타격 이펙트는 Elemental_Explosion 시트(③ 메뉴로 연결)
            d["SUM_GIANT_GOLEM"] = Sum("SUM_GIANT_GOLEM", "고대 정령 (프리즘)",
                SummonAIType.GuardOffset, ScalingStatType.WPN_ATK_AVG,
                atk:4.5f, spd:0f, atkCd:2.0f, atkRange:6.0f, dur:-1f,
                uniqueSkillCd:8f, scale:0.3f, atkFxScale:3f, maxTargets:4); // 크기 축소 / 공격 모션은 크게

            // ── 마왕 기어 (DemonLord) — GuardOffset, 플레이어 주변 고정 위치 원거리 공격 ──
            d["SUM_DEMON_GEAR"] = Sum("SUM_DEMON_GEAR", "지옥 기어",
                SummonAIType.GuardOffset, ScalingStatType.WPN_ATK_SUM,
                atk:0.4f, spd:120f, atkCd:1.0f, atkRange:6f, dur:-1f, sortOrder:5); // spd=공전속도, 기어는 캐릭터 아래

            // ── 성기사단 (HolyKnight) — Stationary + ZoneRegenBuff ──
            // 성역 장판: 화면 내 랜덤 위치에 15초 유지 후 다른 곳으로 이동(SummonRespawnLoop).
            // 위에 서 있는 동안 체력 재생 배율(fxVal: 2/2.5/4배) + 받는 피해 20% 감소(SummonController 상수).
            // 공격 없음(atk 0). 구역 판정 반경 = 표시되는 장판 크기(SummonController가 스프라이트 bounds로 산출), atkRange는 폴백.
            d["SUM_SANCTUARY_1"] = Sum("SUM_SANCTUARY_1", "성역 장판 (브론즈)",
                SummonAIType.Stationary, ScalingStatType.ARM_HP_SUM,
                atk:0f, spd:0f, atkCd:1.0f, atkRange:5f, dur:15f,
                fx:FixedEffectType.ZoneRegenBuff, fxVal:2.0f, scale:1.8f, sortOrder:1);
            d["SUM_SANCTUARY_2"] = Sum("SUM_SANCTUARY_2", "성역 장판 (실버)",
                SummonAIType.Stationary, ScalingStatType.ARM_HP_SUM,
                atk:0f, spd:0f, atkCd:1.0f, atkRange:5f, dur:15f,
                fx:FixedEffectType.ZoneRegenBuff, fxVal:2.5f, scale:1.8f, sortOrder:1);
            d["SUM_SANCTUARY_3"] = Sum("SUM_SANCTUARY_3", "성역 장판 (골드)",
                SummonAIType.Stationary, ScalingStatType.ARM_HP_SUM,
                atk:0f, spd:0f, atkCd:1.0f, atkRange:5f, dur:15f,
                fx:FixedEffectType.ZoneRegenBuff, fxVal:4.0f, scale:1.8f, sortOrder:1);

            return d;
        }

        // ─────────────────────────────────────────────────────────────
        // SynergyConfig 생성
        // ─────────────────────────────────────────────────────────────

        static void BuildSynergyConfig(
            Dictionary<string, SO_SkillData>  skills,
            Dictionary<string, SO_SummonData> summons)
        {
            var config = LoadOrCreate<SO_SynergyConfig>(ConfigPath);

            // ① 메뉴 실행 시 아이콘이 null로 초기화되지 않도록 기존 아이콘 보존
            var savedIcons = new Dictionary<SynergyType, Sprite>();
            if (config.thresholds != null)
            {
                foreach (var t in config.thresholds)
                {
                    if (t != null && t.icon != null)
                        savedIcons[t.type] = t.icon;
                }
            }

            config.thresholds = new SynergyThreshold[]
            {
                // ── 암살단 ──────────────────────────────────────────
                Th(SynergyType.Assassin, "암살단", 2, 4, 5, 0,
                    "단검, 권총, 수리검, 카타나, 너클",
                    "적을 관통해서 날아가는 거대한 수리검을 던집니다. (기본 쿨타임 3초, 실버부터 2초) *수리검 공격력은 가방 무기 공격력 평균에 비례합니다.",
                    "수리검을 던집니다.",
                    "수리검의 공격 속도가 더 빨라집니다.",
                    "또한 수리검이 더 커집니다."),

                // ── 일렉트로 ────────────────────────────────────────
                // 임계값 2=실버(낙뢰1)/3=골드(낙뢰3) — 브론즈 등급은 건너뜀(핀볼과 동일 구조)
                Th(SynergyType.Electro, "일렉트로", 2, 2, 3, 0,
                    "번개 구슬, 지팡이, 마도서",
                    "낙뢰를 소환하여 무작위 적을 공격합니다. 낙뢰가 적을 처치하면 쿨타임이 50% 감소합니다. (쿨타임 3초) *낙뢰의 대미지는 가방 무기 공격력 총합에 비례합니다.",
                    "",
                    "마법 낙뢰 1개 투하",
                    "마법 낙뢰 3개 투하"),

                // ── 핀볼 ────────────────────────────────────────────
                Th(SynergyType.Pinball, "핀볼", 2, 3, 3, 0,
                    "부메랑, 쇠뇌, 수리검",
                    "맵 전체를 돌아다니는 구체를 생성합니다. 생성된 구체는 피격된 적에게 대미지를 입힙니다. *핀볼 공격력은 가방 무기 공격력 평균에 비례합니다.",
                    "핀볼 생성",
                    "",
                    "핀볼 이동 속도 증가"),

                // ── 처형자 ──────────────────────────────────────────
                Th(SynergyType.Executioner, "처형자", 2, 4, 5, 0,
                    "플레일, 채찍, 도끼, 할버드, 사이드",
                    "캐릭터 좌우에 거대한 사신의 낫 투사체를 번갈아 발사합니다. 사신의 낫은 닿은 일반 몬스터의 HP가 일정 이하인 경우 즉사합니다. *낫 공격력은 가방 무기 공격력 총합에 비례합니다.",
                    "체력 5% 이하 적 즉사",
                    "체력 10% 이하 적 즉사",
                    "체력 15% 이하 적 즉사"),

                // ── 성기사단 ────────────────────────────────────────
                Th(SynergyType.HolyKnight, "성기사단", 3, 4, 5, 0,
                    "무기: 장검, 메이스, 워해머 / 방어구: 문장, 방패",
                    "랜덤한 위치에 성역을 생성합니다. 성역 위에서는 체력 재생이 빨라집니다. 또한 받는 대미지가 20% 감소합니다.",
                    "체력 재생 2배",
                    "체력 재생 2.5배",
                    "체력 재생 4배"),

                // ── 소드마스터 ──────────────────────────────────────
                Th(SynergyType.SwordMaster, "소드마스터", 3, 4, 5, 0,
                    "단검, 장검, 대검, 카타나, 플레일",
                    "캐릭터가 보는 방향으로 검기를 발사합니다. *검기 공격력은 가방 무기 공격력 평균에 비례합니다.",
                    "2초마다 전방에 검기 1개 발사",
                    "검기의 크기가 50% 확대",
                    "강화된 검기를 2회 발사"),

                // ── 티탄 ────────────────────────────────────────────
                Th(SynergyType.Titan, "티탄", 2, 4, 6, 0,
                    "철퇴, 도끼, 산탄총, 대검, 몽둥이, 워해머",
                    "랜덤한 방향에 돌을 떨어트립니다. *돌 공격력은 가방 무기 공격력 평균에 비례합니다.",
                    "돌 3개 떨구기",
                    "돌 6개 떨구기",
                    "돌 9개 떨구기"),

                // ── 페어리 ──────────────────────────────────────────
                Th(SynergyType.Fairy, "페어리", 2, 4, 6, 0,
                    "권총, 도끼, 활, 카타나, 플레일, 너클",
                    "플레이어 주변을 회전하며 대미지를 주는 요정을 소환합니다. *요정 피해는 가방 무기 공격력 총합에 비례합니다.",
                    "1개의 요정이 회전합니다.",
                    "2개의 요정이 회전합니다.",
                    "3개의 요정이 더욱 빠르게 회전합니다."),

                // ── 난공불락 ────────────────────────────────────────
                Th(SynergyType.Impregnable, "난공불락", 3, 6, 9, 12,
                    "무기: 스피어, 대검 / 방어구: (방어구 전원)",
                    "적에게 피격 시 시너지 레벨에 따라 피해량을 감소시키고, 적에게 범위 피해를 입힙니다. *충격파 대미지는 가방 방어구 체력 총합에 비례합니다.",
                    "피해량 감소 10%",
                    "피해량 감소 20%, 충격파의 크기 50% 증가",
                    "피해량 감소 35%, 충격파 대미지 증가",
                    "그 누구도 뚫을 수 없습니다."),

                // ── 정령술사 ────────────────────────────────────────
                Th(SynergyType.SpiritMage, "정령술사", 3, 5, 7, 9,
                    "번개 구슬, 채찍, 지팡이, 마도서, 장궁, 레일건, 스피어, 메이스, 화염 방사기",
                    "동행하는 정령 골렘이 같이 전투를 진행합니다. 시너지 레벨에 따라 다양한 효과가 발동됩니다. *정령 피해는 가방 무기 공격력 평균에 비례합니다.",
                    "정령 골렘 1기 소환",
                    "정령 골렘 2기 소환",
                    "정령 골렘 3기 소환",
                    "정령들이 힘을 모아 고대 정령을 소환"),

                // ── 마왕 ────────────────────────────────────────────
                Th(SynergyType.DemonLord, "마왕", 2, 4, 6, 8,
                    "장검, 철퇴, 산탄총, 수류탄, 라이플, 바주카, 화염 방사기, 사이드",
                    "마왕의 스킬이 발동됩니다. 레벨에 따라 강력한 스킬이 발동됩니다. *마왕 스킬은 가방 무기 공격력 총합에 비례합니다.",
                    "지옥불 불씨",
                    "연옥의 기어",
                    "마왕의 멸천",
                    "마왕 강림"),

                // ── 대부호 ──────────────────────────────────────────
                Th(SynergyType.Tycoon, "대부호", 5, 7, 8, 9,
                    "철퇴, 채찍, 활, 라이플, 너클, 화염 방사기, 할버드, 몽둥이, 바주카",
                    "플레이어가 이동 시 골드를 떨어트립니다. 떨어진 골드는 1.5초 뒤 폭발합니다. *코인은 가방 무기 공격력 총합에 비례합니다.",
                    "이동 거리 1칸당 3개의 골드를 뿌립니다.",
                    "이동 거리 1칸당 5개의 골드를 뿌립니다.",
                    "이동 거리 1칸당 7개의 골드를 뿌립니다.",
                    "당신의 걸음을 당당합니다."),

                // ── 과부하 ──────────────────────────────────────────
                Th(SynergyType.Overload, "과부하", 4, 5, 6, 8,
                    "쇠뇌, 장궁, 부메랑, 지팡이, 마도서, 번개 구슬, 수리검, 레일건",
                    "당신이 마지막에 도달한다면... *스킬 피해는 가방 무기 공격력 평균에 비례합니다.",
                    "10초마다 1초동안 발동합니다. / 1초간 이동속도 65% 감소 / 1초간 피해량 감소 30%",
                    "아무 효과도 없습니다.",
                    "조금 더 있으면 효과가 발동합니다.",
                    "과부하를 해제합니다."),
            };

            // 보존된 아이콘 복원
            foreach (var t in config.thresholds)
            {
                if (t != null && savedIcons.TryGetValue(t.type, out var icon))
                    t.icon = icon;
            }

            EditorUtility.SetDirty(config);
            Debug.Log($"[SynergyDataSetup] SynergyConfig 생성 완료: {ConfigPath}");
        }

        // ─────────────────────────────────────────────────────────────
        // SynergyManager 바인딩 자동 채우기
        // ─────────────────────────────────────────────────────────────

        static void FillSkillBindings(SerializedObject so)
        {
            // (SynergyType, SynergyGrade) → 스킬 에셋 경로
            var map = new (SynergyType type, SynergyGrade grade, string assetName)[]
            {
                (SynergyType.Assassin,    SynergyGrade.Bronze, "SK_ASS_1"),
                (SynergyType.Assassin,    SynergyGrade.Silver, "SK_ASS_2"),
                (SynergyType.Assassin,    SynergyGrade.Gold,   "SK_ASS_3"),
                // 일렉트로: 2종=실버(낙뢰1) / 3종=골드(낙뢰3) — 임계값 2/2/3에 맞춘 바인딩
                (SynergyType.Electro,     SynergyGrade.Silver, "SK_ELEC_1"),
                (SynergyType.Electro,     SynergyGrade.Gold,   "SK_ELEC_2"),
                (SynergyType.Executioner, SynergyGrade.Bronze, "SK_SCYTHE_1"),
                (SynergyType.Executioner, SynergyGrade.Silver, "SK_SCYTHE_2"),
                (SynergyType.Executioner, SynergyGrade.Gold,   "SK_SCYTHE_3"),
                (SynergyType.SwordMaster, SynergyGrade.Bronze, "SK_SWORD_1"),
                (SynergyType.SwordMaster, SynergyGrade.Silver, "SK_SWORD_2"),
                (SynergyType.SwordMaster, SynergyGrade.Gold,   "SK_SWORD_3"),
                (SynergyType.Titan,       SynergyGrade.Bronze, "SK_METEOR_1"),
                (SynergyType.Titan,       SynergyGrade.Silver, "SK_METEOR_2"),
                (SynergyType.Titan,       SynergyGrade.Gold,   "SK_METEOR_3"),
                (SynergyType.Impregnable, SynergyGrade.Bronze, "SK_FORTRESS_1"),
                (SynergyType.Impregnable, SynergyGrade.Silver, "SK_FORTRESS_2"),
                (SynergyType.Impregnable, SynergyGrade.Gold,   "SK_FORTRESS_3"),
                (SynergyType.Impregnable, SynergyGrade.Prism,  "SK_FORTRESS_4"),
                // 마왕: 누적 발동 — 등급이 오를수록 검기에 소용돌이가 더해지고 프리즘은 3연 검기.
                //   (기어는 소환형 바인딩 SUM_DEMON_GEAR 로 별도 추가)
                (SynergyType.DemonLord,   SynergyGrade.Bronze, "SK_DEMON_1"),                       // 검기
                (SynergyType.DemonLord,   SynergyGrade.Silver, "SK_DEMON_1"),                       // 검기(+기어2)
                (SynergyType.DemonLord,   SynergyGrade.Gold,   "SK_DEMON_1"),                       // 검기
                (SynergyType.DemonLord,   SynergyGrade.Gold,   "SK_DEMON_2"),                       // +소용돌이(+기어2)
                (SynergyType.DemonLord,   SynergyGrade.Prism,  "SK_DEMON_4"),                       // 3연 검기
                (SynergyType.DemonLord,   SynergyGrade.Prism,  "SK_DEMON_2"),                       // +소용돌이(+기어4)
                (SynergyType.Tycoon,      SynergyGrade.Bronze, "SK_GOLD_BOMB_1"),
                (SynergyType.Tycoon,      SynergyGrade.Silver, "SK_GOLD_BOMB_2"),
                (SynergyType.Tycoon,      SynergyGrade.Gold,   "SK_GOLD_BOMB_3"),
                (SynergyType.Tycoon,      SynergyGrade.Prism,  "SK_GOLD_FAST"),
                // 브론즈만 디버프, 실버·골드는 무효과(바인딩 없음) — 프리즘 도달 시 각성
                (SynergyType.Overload,    SynergyGrade.Bronze, "SK_OVERLOAD_PENALTY"),
                // 프리즘: 레이저 + 체인라이트닝 + 비눗방울 3종 동시 발동
                (SynergyType.Overload,    SynergyGrade.Prism,  "SK_OVERLOAD_LASER"),
                (SynergyType.Overload,    SynergyGrade.Prism,  "SK_OVERLOAD_CHAIN"),
                (SynergyType.Overload,    SynergyGrade.Prism,  "SK_OVERLOAD_BUBBLE"),
            };

            var prop = so.FindProperty("_skillBindings");
            prop.arraySize = map.Length;
            for (int i = 0; i < map.Length; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("synergyType").intValue = (int)map[i].type;
                elem.FindPropertyRelative("grade").intValue       = (int)map[i].grade;
                var skillPath = $"{SkillDir}/{map[i].assetName}.asset";
                var skill = AssetDatabase.LoadAssetAtPath<SO_SkillData>(skillPath);
                elem.FindPropertyRelative("skill").objectReferenceValue = skill;
                if (skill == null)
                    Debug.LogWarning($"[SynergyDataSetup] 스킬 에셋 없음: {skillPath}  ← ① 먼저 실행하세요.");
            }
        }

        static void FillSummonBindings(SerializedObject so)
        {
            var map = new (SynergyType type, SynergyGrade grade, string assetName, int count)[]
            {
                (SynergyType.Pinball,    SynergyGrade.Bronze, "SUM_PINBALL_1", 1),
                // 핀볼은 공 개수가 늘지 않는다(기획): 등급↑ = 이동속도·대미지만 증가하므로 count는 항상 1.
                // 임계값상 3개 = Gold 등급이므로 업그레이드 소환수(PINBALL_2)는 Gold에 바인딩한다.
                (SynergyType.Pinball,    SynergyGrade.Gold,   "SUM_PINBALL_2", 1),
                (SynergyType.Fairy,      SynergyGrade.Bronze, "SUM_FAIRY_1",   1),
                (SynergyType.Fairy,      SynergyGrade.Silver, "SUM_FAIRY_2",   2),
                (SynergyType.Fairy,      SynergyGrade.Gold,   "SUM_FAIRY_3",   3),
                // 마왕 기어: 실버·골드 2기, 프리즘 4기 (검기·소용돌이 스킬과 함께 누적 발동)
                (SynergyType.DemonLord,  SynergyGrade.Silver, "SUM_DEMON_GEAR", 2),
                (SynergyType.DemonLord,  SynergyGrade.Gold,   "SUM_DEMON_GEAR", 2),
                (SynergyType.DemonLord,  SynergyGrade.Prism,  "SUM_DEMON_GEAR", 4),
                (SynergyType.HolyKnight, SynergyGrade.Bronze, "SUM_SANCTUARY_1", 1),
                (SynergyType.HolyKnight, SynergyGrade.Silver, "SUM_SANCTUARY_2", 1),
                (SynergyType.HolyKnight, SynergyGrade.Gold,   "SUM_SANCTUARY_3", 1),
                // 정령술사: 콘셉트(슬라이드 15~17)대로 등급이 오를수록 원소 정령이 누적된다.
                //   브론즈 = 물(GOLEM_1) / 실버 = 물+불(GOLEM_1·2) / 골드 = 물+불+바람(GOLEM_1·2·3)
                //   같은 (시너지·등급)에 여러 줄을 두면 SynergyManager가 모두 소환한다.
                (SynergyType.SpiritMage, SynergyGrade.Bronze, "SUM_GOLEM_1",    1),
                (SynergyType.SpiritMage, SynergyGrade.Silver, "SUM_GOLEM_1",    1),
                (SynergyType.SpiritMage, SynergyGrade.Silver, "SUM_GOLEM_2",    1),
                (SynergyType.SpiritMage, SynergyGrade.Gold,   "SUM_GOLEM_1",    1),
                (SynergyType.SpiritMage, SynergyGrade.Gold,   "SUM_GOLEM_2",    1),
                (SynergyType.SpiritMage, SynergyGrade.Gold,   "SUM_GOLEM_3",    1),
                // 프리즘 = 골드의 3정령(물·불·바람) + 대정령(머리 위)
                (SynergyType.SpiritMage, SynergyGrade.Prism,  "SUM_GOLEM_1",    1),
                (SynergyType.SpiritMage, SynergyGrade.Prism,  "SUM_GOLEM_2",    1),
                (SynergyType.SpiritMage, SynergyGrade.Prism,  "SUM_GOLEM_3",    1),
                (SynergyType.SpiritMage, SynergyGrade.Prism,  "SUM_GIANT_GOLEM",1),
            };

            var prop = so.FindProperty("_summonBindings");
            prop.arraySize = map.Length;
            for (int i = 0; i < map.Length; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("synergyType").intValue = (int)map[i].type;
                elem.FindPropertyRelative("grade").intValue       = (int)map[i].grade;
                elem.FindPropertyRelative("count").intValue       = map[i].count;
                var summonPath = $"{SummonDir}/{map[i].assetName}.asset";
                var summon = AssetDatabase.LoadAssetAtPath<SO_SummonData>(summonPath);
                elem.FindPropertyRelative("summon").objectReferenceValue = summon;
                if (summon == null)
                    Debug.LogWarning($"[SynergyDataSetup] 소환수 에셋 없음: {summonPath}  ← ① 먼저 실행하세요.");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼 — SO 생성
        // ─────────────────────────────────────────────────────────────

        static SO_SkillData Sk(
            string id, string name,
            SynergyTriggerType trigger, SkillType type, SkillTargetType target,
            ScalingStatType scaling, float dmg, float cd, float range,
            int hits = 1, float hitInterval = 0.1f, int extra = 0,
            FixedEffectType fx = FixedEffectType.None, float fxVal = 0f, float effectVal = 0f,
            float duration = 0f, int pierce = 1,
            bool followVfx = false, float vSize = 0f, int vfxSort = 30, float stretchY = 1f,
            float dmgDelay = 0f, float fps = 0f, float linger = -1f, bool anchorBottom = false,
            float projSpeed = 0f)
        {
            string path = $"{SkillDir}/{id}.asset";
            var asset = LoadOrCreate<SO_SkillData>(path);
            asset.skillID          = id;
            asset.skillName        = name;
            asset.triggerType      = trigger;
            asset.skillType        = type;
            asset.targetType       = target;
            asset.scalingStat      = scaling;
            asset.dmgMultiplier    = dmg;
            asset.cooldown         = cd;
            asset.rangeRadius      = range;
            asset.hitCount         = hits;
            asset.hitInterval      = hitInterval;
            asset.extraCount       = extra;
            asset.fixedEffect      = fx;
            asset.fixedEffectValue = fxVal;
            asset.effectValue      = effectVal; // 보조 수치 (예: 과부하 피해량 감소율)
            asset.duration         = duration;
            asset.pierceCount      = pierce;
            asset.vfxFollowPlayer  = followVfx;
            asset.vfxSortingOrder  = vfxSort;
            asset.vfxAnchorBottom  = anchorBottom;
            asset.visualStretchY   = stretchY;
            asset.damageDelay      = dmgDelay;
            if (vSize > 0f) asset.visualSize = vSize;
            if (fps   > 0f) asset.animFps    = fps;
            if (projSpeed > 0f) asset.projectileSpeed = projSpeed;
            if (linger >= 0f) asset.vfxLingerTime = linger;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static SO_SummonData Sum(
            string id, string name,
            SummonAIType ai, ScalingStatType scaling,
            float atk, float spd, float atkCd, float atkRange, float dur,
            FixedEffectType fx = FixedEffectType.None, float fxVal = 0f,
            float uniqueSkillCd = 8f, float scale = 0f, int sortOrder = 5, float atkFxScale = 0.5f,
            int maxTargets = 1)
        {
            string path = $"{SummonDir}/{id}.asset";
            var asset = LoadOrCreate<SO_SummonData>(path);
            asset.summonID          = id;
            asset.summonName        = name;
            asset.aiType            = ai;
            asset.scalingStat       = scaling;
            asset.atkMultiplier     = atk;
            asset.moveSpeed         = spd;
            asset.atkCooldown       = atkCd;
            asset.atkRange          = atkRange;
            asset.duration          = dur;
            asset.fixedEffect       = fx;
            asset.fixedEffectValue  = fxVal;
            asset.uniqueSkillCooldown = uniqueSkillCd;
            asset.displayScale        = scale;
            asset.sortingOrder        = sortOrder;
            asset.attackEffectScale   = atkFxScale;
            asset.maxTargets          = maxTargets;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static SynergyThreshold Th(
            SynergyType type, string displayName,
            int bronze, int silver, int gold, int prism,
            string cond, string desc,
            string bronzeEff, string silverEff, string goldEff,
            string prismEff = "")
        {
            return new SynergyThreshold
            {
                type             = type,
                displayName      = displayName,
                bronzeThreshold  = bronze,
                silverThreshold  = silver,
                goldThreshold    = gold,
                prismThreshold   = prism,
                triggerCondition = cond,
                description      = desc,
                bronzeEffect     = bronzeEff,
                silverEffect     = silverEff,
                goldEffect       = goldEff,
                prismEffect      = prismEff,
            };
        }

        // ─────────────────────────────────────────────────────────────
        // 유틸
        // ─────────────────────────────────────────────────────────────

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ── 메뉴 ③ : 시너지 아이콘 자동 할당 ───────────────────────────
        [MenuItem("BagSurvivor/Setup/③ Fill Synergy Icons")]
        public static void FillSynergyIcons()
        {
            var config = AssetDatabase.LoadAssetAtPath<SO_SynergyConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("[SynergyDataSetup] SynergyConfig를 찾을 수 없습니다. ① 먼저 실행하세요.");
                return;
            }

            const string SheetDir = "Assets/03.Prefabs/08.Synergy";

            // SynergyType → 스프라이트 시트 경로 (파일의 첫 프레임 _0 사용)
            // Electro·Overload 는 전용 이미지 없음 → SynergyIconHelper 런타임 컬러 아이콘 fallback
            var iconMap = new Dictionary<SynergyType, string>
            {
                { SynergyType.Assassin,    $"{SheetDir}/Shuriken_Sv.png"      },
                { SynergyType.SwordMaster, $"{SheetDir}/SwordWave.png"        },
                { SynergyType.HolyKnight,  $"{SheetDir}/Sanctuary_Sv.png"     },
                { SynergyType.DemonLord,   $"{SheetDir}/Scythe_BlackRed.png"  },
                { SynergyType.Titan,       $"{SheetDir}/Shockwave_RED.png"    },
                { SynergyType.Tycoon,      $"{SheetDir}/Gold_Coin1.png"       },
                { SynergyType.Executioner, $"{SheetDir}/Scythe_Black.png"     },
                { SynergyType.SpiritMage,  $"{SheetDir}/Spirit_Sv.png"        },
                { SynergyType.Fairy,       $"{SheetDir}/Fairy.png"            },
                { SynergyType.Pinball,     $"{SheetDir}/Pinball.png"          },
                // Impregnable: Shockwave_Common 첫 프레임이 노란 원이라 SynergyIconHelper fallback 사용
            };

            int filled = 0;
            foreach (var th in config.thresholds)
            {
                if (th == null) continue;
                if (!iconMap.TryGetValue(th.type, out var pngPath)) continue;

                // 해당 PNG 에서 이름이 _0 으로 끝나는 첫 번째 Sprite 로드
                Sprite found = null;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(pngPath))
                {
                    if (asset is Sprite sp && sp.name.EndsWith("_0"))
                    {
                        found = sp;
                        break;
                    }
                }

                if (found == null)
                {
                    Debug.LogWarning($"[SynergyDataSetup] 아이콘 스프라이트 없음: {pngPath}");
                    continue;
                }

                th.icon = found;
                filled++;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SynergyDataSetup] 시너지 아이콘 {filled}개 적용 완료" +
                      " (Electro·Overload는 런타임 컬러 아이콘 사용)");

            // ── 소환수 아이콘도 함께 할당 ──────────────────────────────
            FillSummonIcons(SheetDir);

            // ── 스킬 발동 비주얼(시트 애니메이션) 할당 ───────────────
            FillSkillAnimFrames(SheetDir);
        }

        // ─────────────────────────────────────────────────────────────
        // 스킬 발동 비주얼 — 투사체/이펙트용 시트 프레임 배열 할당
        // ─────────────────────────────────────────────────────────────
        static void FillSkillAnimFrames(string sheetDir)
        {
            // 스킬 ID → 스프라이트 시트 파일명 (Electro는 전용 시트 없음 → 노란 원 fallback)
            var skillSheetMap = new Dictionary<string, string>
            {
                { "SK_ASS_1", "Shuriken_Sv" }, { "SK_ASS_2", "Shuriken_Sv" }, { "SK_ASS_3", "Shuriken_Sv" },
                // 일렉트로 번개 기둥 → 전용 ElectroShockwave 시트(10프레임, 신규)
                { "SK_ELEC_1", "ElectroShockwave" }, { "SK_ELEC_2", "ElectroShockwave" },
                { "SK_SWORD_1", "SwordWave" }, { "SK_SWORD_2", "SwordWave" }, { "SK_SWORD_3", "SwordWave" },
                { "SK_SCYTHE_1", "Scythe_Black" }, { "SK_SCYTHE_2", "Scythe_BlackRed" }, { "SK_SCYTHE_3", "Scythe_DarkRed" },
                // 티탄 돌 떨구기 → 전용 StoneDrop_Common (신규)
                { "SK_METEOR_1", "StoneDrop_Common" }, { "SK_METEOR_2", "StoneDrop_Common" }, { "SK_METEOR_3", "StoneDrop_Common" },
                // 난공불락: 브~골 일반 충격파, 프리즘은 강화 RED 충격파(콘셉트 슬라이드 14)
                { "SK_FORTRESS_1", "Shockwave_Common" }, { "SK_FORTRESS_2", "Shockwave_Common" },
                { "SK_FORTRESS_3", "Shockwave_Common" }, { "SK_FORTRESS_4", "Shockwave_RED" },
                // 마왕(DemonLord): 투사체=Devil_Fireball(창/검기), 소용돌이=Devil_Slash(감싸는 불꽃 고리)
                //                  (기어=Devil_Wave는 소환수 아이콘으로 별도 연결)
                { "SK_DEMON_1", "Devil_Fireball" }, { "SK_DEMON_2", "Devil_Slash" },
                { "SK_DEMON_3", "Devil_Fireball" }, { "SK_DEMON_4", "Devil_Fireball" },
                // 과부하 프리즘 3종: 레이저=OVERLOAD3_Pr(빔형), 체인=OVERLOAD2_Pr, 비눗방울=OVERLOAD1_Pr
                { "SK_OVERLOAD_LASER",  "OVERLOAD3_Pr" },
                { "SK_OVERLOAD_CHAIN",  "OVERLOAD2_Pr" },
                { "SK_OVERLOAD_BUBBLE", "OVERLOAD1_Pr" },
                { "SK_GOLD_BOMB_1", "RichCoin_BOMB1" }, { "SK_GOLD_BOMB_2", "RichCoin_BOMB2" },
                { "SK_GOLD_BOMB_3", "RichCoin_BOMB3" }, { "SK_GOLD_FAST", "RichCoin_BOMB4" },
                // 대정령 광역 강타(프리즘) → 정령 공격 이펙트 시트 재사용
                { "SK_SPIRIT_NOVA", "spirit_attack" },
            };

            var framesCache = new Dictionary<string, Sprite[]>();
            int filled = 0;
            foreach (var kvp in skillSheetMap)
            {
                var skillPath = $"{SkillDir}/{kvp.Key}.asset";
                var skill = AssetDatabase.LoadAssetAtPath<SO_SkillData>(skillPath);
                if (skill == null) continue;

                var sheetPath = $"{sheetDir}/{kvp.Value}.png";
                if (!framesCache.TryGetValue(sheetPath, out var frames))
                {
                    frames = LoadLargeFramesSorted(sheetPath);
                    framesCache[sheetPath] = frames;
                }
                if (frames == null || frames.Length == 0)
                {
                    Debug.LogWarning($"[SynergyDataSetup] 스킬 프레임 없음: {sheetPath}");
                    continue;
                }

                skill.animFrames = frames;
                if (skill.animFps <= 0f) skill.animFps = 12f;
                EditorUtility.SetDirty(skill);
                filled++;
            }

            // ── 대부호 골드 코인 드롭 프레임 (Gold_Coin 등급별) ──────────
            var dropSheetMap = new Dictionary<string, string>
            {
                { "SK_GOLD_BOMB_1", "Gold_Coin1" }, { "SK_GOLD_BOMB_2", "Gold_Coin2" },
                { "SK_GOLD_BOMB_3", "Gold_Coin3" }, { "SK_GOLD_FAST", "Gold_Coin4" },
            };
            foreach (var kvp in dropSheetMap)
            {
                var skill = AssetDatabase.LoadAssetAtPath<SO_SkillData>($"{SkillDir}/{kvp.Key}.asset");
                if (skill == null) continue;
                var coinFrames = LoadLargeFramesSorted($"{sheetDir}/{kvp.Value}.png");
                if (coinFrames.Length == 0) continue;
                skill.dropFrames = coinFrames;
                if (skill.dropFps <= 0f) skill.dropFps = 8f;
                EditorUtility.SetDirty(skill);
            }

            // ── 노드 임팩트 프레임 (체인라이트닝 등) ──────────────────────
            // 연결선은 animFrames(OVERLOAD2_Pr), 노드 임팩트는 nodeFrames(OVERLOAD2.1_Pr)로 분리.
            var nodeSheetMap = new Dictionary<string, string>
            {
                { "SK_OVERLOAD_CHAIN", "OVERLOAD2.1_Pr" },
                { "SK_SPIRIT_NOVA",    "Elemental_Explosion2_SpriteSheet" }, // 광역 강타 폭발 이펙트
            };
            foreach (var kvp in nodeSheetMap)
            {
                var skill = AssetDatabase.LoadAssetAtPath<SO_SkillData>($"{SkillDir}/{kvp.Key}.asset");
                if (skill == null) continue;
                var nodeFrames = LoadLargeFramesSorted($"{sheetDir}/{kvp.Value}.png");
                if (nodeFrames.Length == 0) continue;
                skill.nodeFrames = nodeFrames;
                EditorUtility.SetDirty(skill);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SynergyDataSetup] 스킬 발동 비주얼(시트 애니메이션) {filled}개 적용 완료");
        }

        // 시트에서 충분히 큰 프레임만 골라 번호순 정렬해 반환 (작은 점/잔상 프레임 제외)
        static Sprite[] LoadLargeFramesSorted(string sheetPath)
        {
            var all = new System.Collections.Generic.List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
                if (asset is Sprite sp) all.Add(sp);
            if (all.Count == 0) return new Sprite[0];

            // 최대 프레임 면적의 40% 미만인 작은 프레임(점·잔상)은 제외
            float maxArea = 0f;
            foreach (var sp in all) maxArea = Mathf.Max(maxArea, sp.rect.width * sp.rect.height);
            float threshold = maxArea * 0.4f;

            var big = new System.Collections.Generic.List<Sprite>();
            foreach (var sp in all)
                if (sp.rect.width * sp.rect.height >= threshold) big.Add(sp);
            if (big.Count == 0) big = all;

            big.Sort((a, b) => ExtractTrailingNumber(a.name).CompareTo(ExtractTrailingNumber(b.name)));
            return big.ToArray();
        }

        static void FillSummonIcons(string sheetDir)
        {
            // SO_SummonData ID → (스프라이트 시트 경로, 프레임 접미사)
            var summonIconMap = new Dictionary<string, string>
            {
                { "SUM_FAIRY_1",      $"{sheetDir}/Fairy.png"        },
                { "SUM_FAIRY_2",      $"{sheetDir}/Fairy.png"        },
                { "SUM_FAIRY_3",      $"{sheetDir}/Fairy.png"        },
                { "SUM_PINBALL_1",    $"{sheetDir}/Pinball.png"      },
                { "SUM_PINBALL_2",    $"{sheetDir}/Pinball.png"      },
                { "SUM_GOLEM_1",      $"{sheetDir}/Spirit_Br.png"    },
                { "SUM_GOLEM_2",      $"{sheetDir}/Spirit_Sv.png"    },
                { "SUM_GOLEM_3",      $"{sheetDir}/Spirit_Gd.png"    },
                { "SUM_GIANT_GOLEM",  $"{sheetDir}/GiantGolem.png"   },
                { "SUM_DEMON_GEAR",   $"{sheetDir}/Devil_Wave.png"   },
                { "SUM_SANCTUARY_1",  $"{sheetDir}/Sanctuary_Br.png" },
                { "SUM_SANCTUARY_2",  $"{sheetDir}/Sanctuary_Sv.png" },
                { "SUM_SANCTUARY_3",  $"{sheetDir}/Sanctuary_Gd.png" },
            };

            // 스프라이트 시트 경로 → (icon _0, 전체 프레임 배열) 캐시
            var iconCache   = new Dictionary<string, Sprite>();
            var framesCache = new Dictionary<string, Sprite[]>();

            int filled = 0;
            foreach (var kvp in summonIconMap)
            {
                var path = $"{SummonDir}/{kvp.Key}.asset";
                var summon = AssetDatabase.LoadAssetAtPath<SO_SummonData>(path);
                if (summon == null)
                {
                    Debug.LogWarning($"[SynergyDataSetup] 소환수 에셋 없음: {path}  ← ① 먼저 실행하세요.");
                    continue;
                }

                // _0 아이콘
                if (!iconCache.TryGetValue(kvp.Value, out var sprite))
                {
                    sprite = null;
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(kvp.Value))
                    {
                        if (asset is Sprite sp && sp.name.EndsWith("_0"))
                        {
                            sprite = sp;
                            break;
                        }
                    }
                    iconCache[kvp.Value] = sprite;
                }

                if (sprite == null)
                {
                    Debug.LogWarning($"[SynergyDataSetup] 소환수 스프라이트 없음: {kvp.Value}");
                    continue;
                }

                // 전체 애니메이션 프레임 배열 (번호 순 정렬)
                if (!framesCache.TryGetValue(kvp.Value, out var frames))
                {
                    var list = new System.Collections.Generic.List<Sprite>();
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(kvp.Value))
                    {
                        if (asset is Sprite sp) list.Add(sp);
                    }
                    // 이름 끝 숫자 기준 오름차순 정렬 (_0, _1, _2 ...)
                    list.Sort((a, b) =>
                    {
                        int numA = ExtractTrailingNumber(a.name);
                        int numB = ExtractTrailingNumber(b.name);
                        return numA.CompareTo(numB);
                    });
                    frames = list.ToArray();
                    framesCache[kvp.Value] = frames;
                }

                summon.icon       = sprite;
                summon.animFrames = frames;
                EditorUtility.SetDirty(summon);
                filled++;
            }

            // 정령 골렘은 공격 투사체(spirit_attack) 미사용 — 연결하지 않음.

            // ── 고대 정령(프리즘) 일반 공격 타격 이펙트 — Elemental_Explosion 시트(4×2 격자 8프레임) ──
            var giantGolem = AssetDatabase.LoadAssetAtPath<SO_SummonData>($"{SummonDir}/SUM_GIANT_GOLEM.asset");
            if (giantGolem != null)
            {
                var fxFrames = LoadLargeFramesSorted($"{sheetDir}/Elemental_Explosion_SpriteSheet.png");
                if (fxFrames.Length > 0)
                {
                    giantGolem.attackEffectFrames = fxFrames;
                    giantGolem.attackEffectFps    = 16f; // 8프레임 → 0.5초 재생 (공격 주기 2초 내 종료)
                    EditorUtility.SetDirty(giantGolem);
                }
                else
                {
                    Debug.LogWarning($"[SynergyDataSetup] 대정령 타격 이펙트 시트 없음: {sheetDir}/Elemental_Explosion_SpriteSheet.png");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SynergyDataSetup] 소환수 아이콘 {filled}개 적용 완료");
        }

        // ─────────────────────────────────────────────────────────────

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent ?? "Assets", folder);
        }

        // 스프라이트 이름 끝의 숫자 추출 ("Fairy.prefab_12" → 12)
        static int ExtractTrailingNumber(string name)
        {
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i])) i--;
            return (i < name.Length - 1)
                ? int.Parse(name.Substring(i + 1))
                : int.MaxValue;
        }
    }
}
