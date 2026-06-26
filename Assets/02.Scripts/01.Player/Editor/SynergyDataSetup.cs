// ============================================================
// SynergyDataSetup.cs  (Editor 전용)
//
// 메뉴: BagSurvivor > Setup > ① Create Synergy Data Assets
//         → SO_SkillData / SO_SummonData / SO_SynergyConfig 에셋 일괄 생성
//       BagSurvivor > Setup > ② Fill SynergyManager Bindings
//         → 씬의 SynergyManager Inspector 바인딩 자동 채우기
//
// 데이터 출처: 콘텐츠_시너지기획서_20260608_v0.4
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
                ScalingStatType.WPN_ATK_AVG, dmg:1.0f, cd:3f, range:15f, pierce:999, vSize:1.6f);
            d["SK_ASS_2"] = Sk("SK_ASS_2", "수리검 (실버)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_AVG, dmg:1.1f, cd:3f, range:15f, pierce:999, vSize:1.6f);
            // 골드 '거대 수리검': 1.2(×1.5)에서 추가로 2배 → 2.4
            d["SK_ASS_3"] = Sk("SK_ASS_3", "거대 수리검 (골드)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_AVG, dmg:1.2f, cd:3f, range:15f, extra:1, pierce:999, vSize:2.4f);

            // ── 일렉트로 (Electro) — AutoTimer / WPN_ATK_SUM / RandomEnemy (기획서: 무작위 낙뢰 1/3개) ──
            d["SK_ELEC_1"] = Sk("SK_ELEC_1", "마법 낙뢰 1개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_SUM, dmg:1.0f, cd:2f, range:50f, extra:1, vSize:1.6f);
            d["SK_ELEC_2"] = Sk("SK_ELEC_2", "마법 낙뢰 3개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_SUM, dmg:1.0f, cd:2f, range:50f, extra:3, vSize:1.6f);

            // ── 처형자 (Executioner) — AutoTimer / WPN_ATK_SUM / ForwardDual + InstantDeath ──
            // 낫: 투사체 표시 크기 2배(기본 0.8 → 1.6)
            d["SK_SCYTHE_1"] = Sk("SK_SCYTHE_1", "사신의 낫 (브론즈)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.ForwardDual,
                ScalingStatType.WPN_ATK_SUM, dmg:1.0f, cd:3f, range:20f,
                fx:FixedEffectType.InstantDeath, fxVal:5f, pierce:999, vSize:1.6f);
            d["SK_SCYTHE_2"] = Sk("SK_SCYTHE_2", "사신의 낫 (실버)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.ForwardDual,
                ScalingStatType.WPN_ATK_SUM, dmg:1.1f, cd:3f, range:20f,
                fx:FixedEffectType.InstantDeath, fxVal:10f, pierce:999, vSize:1.6f);
            d["SK_SCYTHE_3"] = Sk("SK_SCYTHE_3", "사신의 낫 (골드)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.ForwardDual,
                ScalingStatType.WPN_ATK_SUM, dmg:1.3f, cd:3f, range:20f,
                fx:FixedEffectType.InstantDeath, fxVal:15f, pierce:999, vSize:1.6f);

            // ── 소드마스터 (SwordMaster) — AutoTimer / WPN_ATK_AVG / Forward ──
            // 검기: 크기 확대(0.8→4.8) + 세로 2배(stretchY) + 바라보는 방향(FacingForward) 발사
            d["SK_SWORD_1"] = Sk("SK_SWORD_1", "검기 (브론즈)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.FacingForward,
                ScalingStatType.WPN_ATK_AVG, dmg:1.0f, cd:2f, range:15f, vSize:4.8f, stretchY:2f);
            d["SK_SWORD_2"] = Sk("SK_SWORD_2", "대형 검기 (실버)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.FacingForward,
                ScalingStatType.WPN_ATK_AVG, dmg:1.1f, cd:2f, range:15f, vSize:4.8f, stretchY:2f);
            d["SK_SWORD_3"] = Sk("SK_SWORD_3", "연속 검기 (골드)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.FacingForward,
                ScalingStatType.WPN_ATK_AVG, dmg:1.3f, cd:2f, range:15f, hits:2, hitInterval:0.15f, vSize:4.8f, stretchY:2f);

            // ── 티탄 (Titan) — AutoTimer / WPN_ATK_AVG / RandomEnemy × N ──
            d["SK_METEOR_1"] = Sk("SK_METEOR_1", "돌 떨구기 3개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_AVG, dmg:5.0f, cd:4f, range:18f, extra:3);
            d["SK_METEOR_2"] = Sk("SK_METEOR_2", "돌 떨구기 6개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_AVG, dmg:5.0f, cd:4f, range:18f, extra:6);
            d["SK_METEOR_3"] = Sk("SK_METEOR_3", "돌 떨구기 9개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_AVG, dmg:5.0f, cd:4f, range:18f, extra:9);

            // ── 난공불락 (Impregnable) — OnHitTaken / ARM_HP_SUM / Self + DamageReduction ──
            // 충격파: 표시 크기 5배(기본 0.8 → 4.0), 바닥 깔림(vfxSort -1)
            d["SK_FORTRESS_1"] = Sk("SK_FORTRESS_1", "충격파 (브론즈)",
                SynergyTriggerType.OnHitTaken, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:10.0f, cd:0f, range:5f,
                fx:FixedEffectType.DamageReduction, fxVal:10f, vSize:4f, vfxSort:1);
            d["SK_FORTRESS_2"] = Sk("SK_FORTRESS_2", "거대 충격파 (실버)",
                SynergyTriggerType.OnHitTaken, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:10.0f, cd:0f, range:10f,
                fx:FixedEffectType.DamageReduction, fxVal:20f, vSize:4f, vfxSort:1);
            d["SK_FORTRESS_3"] = Sk("SK_FORTRESS_3", "파멸 충격파 (골드)",
                SynergyTriggerType.OnHitTaken, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:13.0f, cd:0f, range:10f,
                fx:FixedEffectType.DamageReduction, fxVal:35f, vSize:4f, vfxSort:1);
            d["SK_FORTRESS_4"] = Sk("SK_FORTRESS_4", "무적 충격파 (프리즘)",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:20.0f, cd:0.5f, range:30f,
                fx:FixedEffectType.DamageReduction, fxVal:60f, vSize:4f, vfxSort:1);

            // ── 마왕 (DemonLord) — 누적형 동시 발동 (콘셉트 19~22) ──
            // 투사체(검기)·소용돌이는 스킬, 기어는 소환수(SUM_DEMON_GEAR)로 구성한다.
            d["SK_DEMON_1"] = Sk("SK_DEMON_1", "지옥 검기 (투사체)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_SUM, dmg:1.0f, cd:2f, range:20f,
                fx:FixedEffectType.Burn, fxVal:0f, pierce:999, vSize:1.6f);
            // 연옥 소용돌이: 캐릭터를 감싸는 불꽃 고리(Devil_Slash)가 플레이어를 따라다니며 주변 적 타격
            d["SK_DEMON_2"] = Sk("SK_DEMON_2", "연옥 소용돌이",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:0.5f, cd:1f, range:3f,
                followVfx:true, vSize:3f);
            d["SK_DEMON_3"] = Sk("SK_DEMON_3", "마왕의 멸천참(예비)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:0.2f, cd:0.1f, range:8f, vSize:1.6f);
            d["SK_DEMON_4"] = Sk("SK_DEMON_4", "지옥 검기 3연 (프리즘)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.ForwardTriple,
                ScalingStatType.WPN_ATK_SUM, dmg:1.5f, cd:2f, range:50f,
                fx:FixedEffectType.Burn, fxVal:0f, pierce:999, vSize:1.6f);

            // ── 대부호 (Tycoon) — OnMove / WPN_ATK_SUM / 골드 드랍 ──
            d["SK_GOLD_BOMB_1"] = Sk("SK_GOLD_BOMB_1", "골드 폭발 (브론즈)",
                SynergyTriggerType.OnMove, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:0.7f, cd:0f, range:3f,
                extra:3, duration:2f);
            d["SK_GOLD_BOMB_2"] = Sk("SK_GOLD_BOMB_2", "골드 폭발 (실버)",
                SynergyTriggerType.OnMove, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:0.7f, cd:0f, range:3f,
                extra:5, duration:2f);
            d["SK_GOLD_BOMB_3"] = Sk("SK_GOLD_BOMB_3", "골드 폭발 (골드)",
                SynergyTriggerType.OnMove, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:0.7f, cd:0f, range:3f,
                extra:7, duration:2f);
            d["SK_GOLD_FAST"] = Sk("SK_GOLD_FAST", "골드 폭발 (프리즘)",
                SynergyTriggerType.OnMove, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:0.7f, cd:0f, range:3f,
                extra:15, duration:1f);

            // ── 과부화 (Overload) — Penalty ──
            d["SK_OVERLOAD_PENALTY"] = Sk("SK_OVERLOAD_PENALTY", "과부화 패널티",
                SynergyTriggerType.Penalty, SkillType.Buff, SkillTargetType.Self,
                ScalingStatType.None, dmg:0f, cd:10f, range:0f,
                fx:FixedEffectType.SpeedPenalty, fxVal:50f);
            // 과부화 프리즘: 3종 효과 동시 발동 (콘셉트 슬라이드 24). 각각 Penalty 트리거로 무한 발동.
            d["SK_OVERLOAD_LASER"] = Sk("SK_OVERLOAD_LASER", "과부화 레이저 (프리즘)",
                SynergyTriggerType.Penalty, SkillType.Projectile, SkillTargetType.FacingForward,
                ScalingStatType.WPN_ATK_AVG, dmg:3.0f, cd:0.15f, range:30f, pierce:999);
            d["SK_OVERLOAD_CHAIN"] = Sk("SK_OVERLOAD_CHAIN", "과부화 체인라이트닝 (프리즘)",
                SynergyTriggerType.Penalty, SkillType.AoE, SkillTargetType.ChainLightning,
                ScalingStatType.WPN_ATK_AVG, dmg:6.0f, cd:0.5f, range:50f, extra:4);
            d["SK_OVERLOAD_BUBBLE"] = Sk("SK_OVERLOAD_BUBBLE", "과부화 비눗방울 (프리즘)",
                SynergyTriggerType.Penalty, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_AVG, dmg:4.0f, cd:0.4f, range:50f, extra:3);

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
            // 핀볼: 기존 0.5 → 가로세로 2배(displayScale 1.0)
            d["SUM_PINBALL_1"] = Sum("SUM_PINBALL_1", "핀볼 (브론즈)",
                SummonAIType.Bounce, ScalingStatType.WPN_ATK_AVG,
                atk:1.1f, spd:12f, atkCd:0.2f, atkRange:1f, dur:-1f, scale:1.0f);
            d["SUM_PINBALL_2"] = Sum("SUM_PINBALL_2", "핀볼 (실버)",
                SummonAIType.Bounce, ScalingStatType.WPN_ATK_AVG,
                atk:1.3f, spd:18f, atkCd:0.2f, atkRange:1f, dur:-1f, scale:1.0f);

            // ── 페어리 (Fairy) — OrbitPlayer ──
            d["SUM_FAIRY_1"] = Sum("SUM_FAIRY_1", "요정 (브론즈)",
                SummonAIType.OrbitPlayer, ScalingStatType.WPN_ATK_SUM,
                atk:1.0f, spd:90f, atkCd:0.2f, atkRange:1.5f, dur:-1f);
            d["SUM_FAIRY_2"] = Sum("SUM_FAIRY_2", "요정 (실버)",
                SummonAIType.OrbitPlayer, ScalingStatType.WPN_ATK_SUM,
                atk:1.0f, spd:90f, atkCd:0.2f, atkRange:1.5f, dur:-1f);
            d["SUM_FAIRY_3"] = Sum("SUM_FAIRY_3", "고속 요정 (골드)",
                SummonAIType.OrbitPlayer, ScalingStatType.WPN_ATK_SUM,
                atk:1.0f, spd:150f, atkCd:0.15f, atkRange:1.5f, dur:-1f);

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
            d["SUM_GIANT_GOLEM"] = Sum("SUM_GIANT_GOLEM", "고대 정령 (프리즘)",
                SummonAIType.GuardOffset, ScalingStatType.WPN_ATK_AVG,
                atk:4.5f, spd:0f, atkCd:2.0f, atkRange:6.0f, dur:-1f,
                uniqueSkillCd:8f, scale:0.3f, atkFxScale:3f); // 크기 축소 / 공격 모션은 크게

            // ── 마왕 기어 (DemonLord) — GuardOffset, 플레이어 주변 고정 위치 원거리 공격 ──
            d["SUM_DEMON_GEAR"] = Sum("SUM_DEMON_GEAR", "지옥 기어",
                SummonAIType.GuardOffset, ScalingStatType.WPN_ATK_SUM,
                atk:0.5f, spd:120f, atkCd:1.0f, atkRange:6f, dur:-1f, sortOrder:5); // spd=공전속도, 기어는 캐릭터 아래

            // ── 성기사단 (HolyKnight) — Stationary + HealArmorHpPct ──
            // 성역 장판: 기본 0.6 → 3배 확대(scale 1.8), 바닥 깔림(sortOrder -1)
            d["SUM_SANCTUARY_1"] = Sum("SUM_SANCTUARY_1", "성역 장판 (브론즈)",
                SummonAIType.Stationary, ScalingStatType.ARM_HP_SUM,
                atk:0.5f, spd:0f, atkCd:1.0f, atkRange:5f, dur:5f,
                fx:FixedEffectType.HealArmorHpPct, fxVal:1.0f, scale:1.8f, sortOrder:1);
            d["SUM_SANCTUARY_2"] = Sum("SUM_SANCTUARY_2", "성역 장판 (실버)",
                SummonAIType.Stationary, ScalingStatType.ARM_HP_SUM,
                atk:0.6f, spd:0f, atkCd:1.0f, atkRange:5f, dur:8f,
                fx:FixedEffectType.HealArmorHpPct, fxVal:1.5f, scale:1.8f, sortOrder:1);
            d["SUM_SANCTUARY_3"] = Sum("SUM_SANCTUARY_3", "성역 장판 (골드)",
                SummonAIType.Stationary, ScalingStatType.ARM_HP_SUM,
                atk:0.7f, spd:0f, atkCd:1.0f, atkRange:5f, dur:11f,
                fx:FixedEffectType.HealArmorHpPct, fxVal:2.0f, scale:1.8f, sortOrder:1);

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
                    "가방 무기 공격력 평균에 비례하는 수리검을 3초마다 투척합니다.",
                    "수리검을 던집니다.  대미지 100%",
                    "수리검 속도 30% 증가.  대미지 110%",
                    "수리검 크기 ×1.5.  대미지 120%"),

                // ── 일렉트로 ────────────────────────────────────────
                Th(SynergyType.Electro, "일렉트로", 2, 3, 3, 0,
                    "가방 무기 공격력 총합에 비례하는 낙뢰를 2초마다 무작위 적에게 떨굽니다.",
                    "마법 낙뢰 1개 투하",
                    "마법 낙뢰 3개 투하",
                    ""),

                // ── 핀볼 ────────────────────────────────────────────
                Th(SynergyType.Pinball, "핀볼", 2, 3, 3, 0,
                    "맵 전체를 튕겨 다니며 적에게 피해를 주는 구체를 생성합니다.",
                    "핀볼 생성.  대미지 110%",
                    "핀볼 이동 속도 증가.  대미지 130%",
                    "핀볼 이동 속도 증가.  대미지 130%"),

                // ── 처형자 ──────────────────────────────────────────
                Th(SynergyType.Executioner, "처형자", 2, 4, 5, 0,
                    "3초마다 좌우에 사신의 낫을 발사합니다. 체력이 낮은 적을 즉사시킵니다.",
                    "체력 5% 이하 적 즉사.  대미지 100%",
                    "체력 10% 이하 적 즉사.  대미지 110%",
                    "체력 15% 이하 적 즉사.  대미지 130%"),

                // ── 성기사단 ────────────────────────────────────────
                Th(SynergyType.HolyKnight, "성기사단", 2, 4, 5, 0,
                    "플레이어 위치에 신성한 성역 장판을 생성합니다. 장판 내에서 체력이 회복됩니다.",
                    "장판 5초 유지.  체력 회복 1%.  대미지 50%",
                    "장판 8초 유지.  체력 회복 1.5%.  대미지 60%",
                    "장판 11초 유지.  체력 회복 2%.  대미지 70%"),

                // ── 소드마스터 ──────────────────────────────────────
                Th(SynergyType.SwordMaster, "소드마스터", 3, 4, 5, 0,
                    "2초마다 전방으로 검기를 발사합니다.",
                    "검기 1개 발사.  대미지 100%",
                    "검기 크기 50% 확대.  대미지 110%",
                    "검기 2회 발사.  대미지 130%"),

                // ── 티탄 ────────────────────────────────────────────
                Th(SynergyType.Titan, "티탄", 2, 4, 6, 0,
                    "4초마다 무작위 적에게 돌을 떨굽니다.",
                    "돌 3개 낙하.  대미지 500%",
                    "돌 6개 낙하.  대미지 500%",
                    "돌 9개 낙하.  대미지 500%"),

                // ── 페어리 ──────────────────────────────────────────
                Th(SynergyType.Fairy, "페어리", 2, 4, 6, 0,
                    "플레이어 주변을 회전하며 적에게 피해를 주는 요정을 소환합니다.",
                    "요정 1마리 소환.  대미지 100%",
                    "요정 2마리 소환.  대미지 100%",
                    "요정 3마리 소환 (가속).  대미지 100%"),

                // ── 난공불락 ────────────────────────────────────────
                Th(SynergyType.Impregnable, "난공불락", 3, 6, 9, 12,
                    "피격 시 충격파를 발산하고 받는 피해를 감소시킵니다.\n방어구 체력 총합에 비례합니다.",
                    "충격파.  피해 감소 10%.  대미지 1000%",
                    "충격파 크기 ×2.  피해 감소 20%.  대미지 1000%",
                    "충격파 강화.  피해 감소 35%.  대미지 1300%",
                    "0.5초마다 자동 발동.  피해 감소 60%.  대미지 2000%"),

                // ── 정령술사 ────────────────────────────────────────
                Th(SynergyType.SpiritMage, "정령술사", 3, 5, 7, 9,
                    "전투에 함께하는 원소 정령을 소환합니다. 등급이 오를수록 정령이 추가됩니다.",
                    "물 정령 1기.  대미지 100%",
                    "물·불 정령 2기.",
                    "물·불·바람 정령 3기.",
                    "고대 정령 1기.  대미지 450%.  8초마다 광역 공격"),

                // ── 마왕 ────────────────────────────────────────────
                Th(SynergyType.DemonLord, "마왕", 2, 4, 6, 8,
                    "마왕의 권능이 누적 발동됩니다. 등급이 오를수록 효과가 더해집니다.",
                    "지옥 검기를 전방으로 발사.  대미지 100%",
                    "검기 + 지옥 기어 2기 추가.",
                    "검기 + 기어 2기 + 연옥 소용돌이.",
                    "3연 검기 + 기어 4기 + 소용돌이.  강림."),

                // ── 대부호 ──────────────────────────────────────────
                Th(SynergyType.Tycoon, "대부호", 5, 7, 8, 9,
                    "이동 시 골드를 뿌립니다. 2초 후 폭발하며 범위 피해를 줍니다.",
                    "1칸당 골드 3개.  골드당 70% 대미지",
                    "1칸당 골드 5개.",
                    "1칸당 골드 7개.",
                    "1칸당 골드 15개.  폭발 시간 1초로 단축"),

                // ── 과부화 ──────────────────────────────────────────
                Th(SynergyType.Overload, "과부화", 1, 2, 7, 8,
                    "왕귀형 콘셉트. 브~골드는 패널티만 있고 프리즘 달성 시 신이 됩니다.",
                    "10초마다 1초간 이동속도/피해량 50% 감소",
                    "10초마다 1초간 이동속도/피해량 50% 감소",
                    "10초마다 1초간 이동속도/피해량 50% 감소",
                    "레이저 + 체인라이트닝 + 비눗방울 3종 무한 발동.  신이 됩니다."),
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
                (SynergyType.Electro,     SynergyGrade.Bronze, "SK_ELEC_1"),
                (SynergyType.Electro,     SynergyGrade.Silver, "SK_ELEC_2"),
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
                (SynergyType.Overload,    SynergyGrade.Bronze, "SK_OVERLOAD_PENALTY"),
                (SynergyType.Overload,    SynergyGrade.Silver, "SK_OVERLOAD_PENALTY"),
                (SynergyType.Overload,    SynergyGrade.Gold,   "SK_OVERLOAD_PENALTY"),
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
            FixedEffectType fx = FixedEffectType.None, float fxVal = 0f,
            float duration = 0f, int pierce = 1,
            bool followVfx = false, float vSize = 0f, int vfxSort = 30, float stretchY = 1f)
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
            asset.duration         = duration;
            asset.pierceCount      = pierce;
            asset.vfxFollowPlayer  = followVfx;
            asset.vfxSortingOrder  = vfxSort;
            asset.visualStretchY   = stretchY;
            if (vSize > 0f) asset.visualSize = vSize;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static SO_SummonData Sum(
            string id, string name,
            SummonAIType ai, ScalingStatType scaling,
            float atk, float spd, float atkCd, float atkRange, float dur,
            FixedEffectType fx = FixedEffectType.None, float fxVal = 0f,
            float uniqueSkillCd = 8f, float scale = 0f, int sortOrder = 5, float atkFxScale = 0.5f)
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
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static SynergyThreshold Th(
            SynergyType type, string displayName,
            int bronze, int silver, int gold, int prism,
            string desc, string bronzeEff, string silverEff, string goldEff,
            string prismEff = "")
        {
            return new SynergyThreshold
            {
                type            = type,
                displayName     = displayName,
                bronzeThreshold = bronze,
                silverThreshold = silver,
                goldThreshold   = gold,
                prismThreshold  = prism,
                description     = desc,
                bronzeEffect    = bronzeEff,
                silverEffect    = silverEff,
                goldEffect      = goldEff,
                prismEffect     = prismEff,
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
                // 과부화 프리즘 3종: 레이저=OVERLOAD3_Pr(빔형), 체인=OVERLOAD2_Pr, 비눗방울=OVERLOAD1_Pr
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

            // 정령 골렘·고대 정령은 공격 투사체(spirit_attack) 미사용 — 연결하지 않음.

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
