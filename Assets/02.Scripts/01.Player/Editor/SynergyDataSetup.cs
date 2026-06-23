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
            d["SK_ASS_1"] = Sk("SK_ASS_1", "수리검 (브론즈)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_AVG, dmg:1.0f, cd:3f, range:15f);
            d["SK_ASS_2"] = Sk("SK_ASS_2", "수리검 (실버)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_AVG, dmg:1.1f, cd:3f, range:15f);
            d["SK_ASS_3"] = Sk("SK_ASS_3", "거대 수리검 (골드)",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_AVG, dmg:1.2f, cd:3f, range:15f, extra:1);

            // ── 일렉트로 (Electro) — AutoTimer / WPN_ATK_SUM / RandomEnemy ──
            d["SK_ELEC_1"] = Sk("SK_ELEC_1", "마법 낙뢰 1개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_SUM, dmg:1.0f, cd:2f, range:50f, extra:1);
            d["SK_ELEC_2"] = Sk("SK_ELEC_2", "마법 낙뢰 3개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_SUM, dmg:1.0f, cd:2f, range:50f, extra:3);

            // ── 처형자 (Executioner) — AutoTimer / WPN_ATK_SUM / ForwardDual + InstantDeath ──
            d["SK_SCYTHE_1"] = Sk("SK_SCYTHE_1", "사신의 낫 (브론즈)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.ForwardDual,
                ScalingStatType.WPN_ATK_SUM, dmg:1.0f, cd:3f, range:20f,
                fx:FixedEffectType.InstantDeath, fxVal:5f);
            d["SK_SCYTHE_2"] = Sk("SK_SCYTHE_2", "사신의 낫 (실버)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.ForwardDual,
                ScalingStatType.WPN_ATK_SUM, dmg:1.1f, cd:3f, range:20f,
                fx:FixedEffectType.InstantDeath, fxVal:10f);
            d["SK_SCYTHE_3"] = Sk("SK_SCYTHE_3", "사신의 낫 (골드)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.ForwardDual,
                ScalingStatType.WPN_ATK_SUM, dmg:1.3f, cd:3f, range:20f,
                fx:FixedEffectType.InstantDeath, fxVal:15f);

            // ── 소드마스터 (SwordMaster) — AutoTimer / WPN_ATK_AVG / Forward ──
            d["SK_SWORD_1"] = Sk("SK_SWORD_1", "검기 (브론즈)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_AVG, dmg:1.0f, cd:2f, range:15f);
            d["SK_SWORD_2"] = Sk("SK_SWORD_2", "대형 검기 (실버)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_AVG, dmg:1.1f, cd:2f, range:15f);
            d["SK_SWORD_3"] = Sk("SK_SWORD_3", "연속 검기 (골드)",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.Forward,
                ScalingStatType.WPN_ATK_AVG, dmg:1.3f, cd:2f, range:15f, hits:2, hitInterval:0.15f);

            // ── 티탄 (Titan) — AutoTimer / WPN_ATK_AVG / RandomEnemy × N ──
            d["SK_METEOR_1"] = Sk("SK_METEOR_1", "돌 떨구기 3개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_AVG, dmg:5.0f, cd:4f, range:50f, extra:3);
            d["SK_METEOR_2"] = Sk("SK_METEOR_2", "돌 떨구기 6개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_AVG, dmg:5.0f, cd:4f, range:50f, extra:6);
            d["SK_METEOR_3"] = Sk("SK_METEOR_3", "돌 떨구기 9개",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_AVG, dmg:5.0f, cd:4f, range:50f, extra:9);

            // ── 난공불락 (Impregnable) — OnHitTaken / ARM_HP_SUM / Self + DamageReduction ──
            d["SK_FORTRESS_1"] = Sk("SK_FORTRESS_1", "충격파 (브론즈)",
                SynergyTriggerType.OnHitTaken, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:10.0f, cd:0f, range:5f,
                fx:FixedEffectType.DamageReduction, fxVal:10f);
            d["SK_FORTRESS_2"] = Sk("SK_FORTRESS_2", "거대 충격파 (실버)",
                SynergyTriggerType.OnHitTaken, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:10.0f, cd:0f, range:10f,
                fx:FixedEffectType.DamageReduction, fxVal:20f);
            d["SK_FORTRESS_3"] = Sk("SK_FORTRESS_3", "파멸 충격파 (골드)",
                SynergyTriggerType.OnHitTaken, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:13.0f, cd:0f, range:10f,
                fx:FixedEffectType.DamageReduction, fxVal:35f);
            d["SK_FORTRESS_4"] = Sk("SK_FORTRESS_4", "무적 충격파 (프리즘)",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.ARM_HP_SUM, dmg:20.0f, cd:0.5f, range:30f,
                fx:FixedEffectType.DamageReduction, fxVal:60f);

            // ── 마왕 (DemonLord) — AutoTimer / WPN_ATK_SUM ──
            d["SK_DEMON_1"] = Sk("SK_DEMON_1", "지옥불 불씨",
                SynergyTriggerType.AutoTimer, SkillType.Projectile, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_SUM, dmg:1.0f, cd:2f, range:50f,
                fx:FixedEffectType.Burn, fxVal:0f);
            d["SK_DEMON_2"] = Sk("SK_DEMON_2", "연옥의 파도",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:1.0f, cd:1f, range:8f);
            d["SK_DEMON_3"] = Sk("SK_DEMON_3", "마왕의 멸천참",
                SynergyTriggerType.AutoTimer, SkillType.Slash, SkillTargetType.Self,
                ScalingStatType.WPN_ATK_SUM, dmg:0.2f, cd:0.1f, range:8f);
            d["SK_DEMON_4"] = Sk("SK_DEMON_4", "마왕 강림 (프리즘)",
                SynergyTriggerType.AutoTimer, SkillType.AoE, SkillTargetType.ForwardTriple,
                ScalingStatType.WPN_ATK_SUM, dmg:1.5f, cd:2f, range:50f,
                extra:3, fx:FixedEffectType.Burn, fxVal:0f);

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
            d["SK_OVERLOAD_PRISM"] = Sk("SK_OVERLOAD_PRISM", "초강력 난사 (프리즘)",
                SynergyTriggerType.Penalty, SkillType.AoE, SkillTargetType.RandomEnemy,
                ScalingStatType.WPN_ATK_AVG, dmg:10.0f, cd:0.33f, range:50f, extra:3);

            return d;
        }

        // ─────────────────────────────────────────────────────────────
        // 소환수 에셋 전체 생성
        // ─────────────────────────────────────────────────────────────

        static Dictionary<string, SO_SummonData> BuildAllSummons()
        {
            var d = new Dictionary<string, SO_SummonData>();

            // ── 핀볼 (Pinball) — Bounce ──
            d["SUM_PINBALL_1"] = Sum("SUM_PINBALL_1", "핀볼 (브론즈)",
                SummonAIType.Bounce, ScalingStatType.WPN_ATK_AVG,
                atk:1.1f, spd:12f, atkCd:0.2f, atkRange:1f, dur:-1f);
            d["SUM_PINBALL_2"] = Sum("SUM_PINBALL_2", "핀볼 (실버)",
                SummonAIType.Bounce, ScalingStatType.WPN_ATK_AVG,
                atk:1.3f, spd:18f, atkCd:0.2f, atkRange:1f, dur:-1f);

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
            d["SUM_GIANT_GOLEM"] = Sum("SUM_GIANT_GOLEM", "고대 정령 (프리즘)",
                SummonAIType.FollowAttack, ScalingStatType.WPN_ATK_AVG,
                atk:4.5f, spd:3.5f, atkCd:2.0f, atkRange:2.0f, dur:-1f,
                uniqueSkillCd:8f);

            // ── 성기사단 (HolyKnight) — Stationary + HealArmorHpPct ──
            d["SUM_SANCTUARY_1"] = Sum("SUM_SANCTUARY_1", "성역 장판 (브론즈)",
                SummonAIType.Stationary, ScalingStatType.ARM_HP_SUM,
                atk:0.5f, spd:0f, atkCd:1.0f, atkRange:5f, dur:5f,
                fx:FixedEffectType.HealArmorHpPct, fxVal:1.0f);
            d["SUM_SANCTUARY_2"] = Sum("SUM_SANCTUARY_2", "성역 장판 (실버)",
                SummonAIType.Stationary, ScalingStatType.ARM_HP_SUM,
                atk:0.6f, spd:0f, atkCd:1.0f, atkRange:5f, dur:8f,
                fx:FixedEffectType.HealArmorHpPct, fxVal:1.5f);
            d["SUM_SANCTUARY_3"] = Sum("SUM_SANCTUARY_3", "성역 장판 (골드)",
                SummonAIType.Stationary, ScalingStatType.ARM_HP_SUM,
                atk:0.7f, spd:0f, atkCd:1.0f, atkRange:5f, dur:11f,
                fx:FixedEffectType.HealArmorHpPct, fxVal:2.0f);

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

            config.thresholds = new SynergyThreshold[]
            {
                // ── 암살단 ──────────────────────────────────────────
                Th(SynergyType.Assassin, "암살단", 2, 4, 6, 0,
                    "가방 무기 공격력 평균에 비례하는 수리검을 3초마다 투척합니다.",
                    "수리검을 던집니다.  대미지 100%",
                    "수리검 속도 30% 증가.  대미지 110%",
                    "수리검 크기 ×1.5.  대미지 120%"),

                // ── 일렉트로 ────────────────────────────────────────
                Th(SynergyType.Electro, "일렉트로", 2, 3, 99, 0,
                    "가방 무기 공격력 총합에 비례하는 낙뢰를 2초마다 떨굽니다.\n낙뢰가 적을 처치하면 쿨타임 50% 감소.",
                    "마법 낙뢰 1개 투하",
                    "마법 낙뢰 3개 투하",
                    ""),

                // ── 핀볼 ────────────────────────────────────────────
                Th(SynergyType.Pinball, "핀볼", 2, 3, 99, 0,
                    "맵 전체를 튕겨 다니며 적에게 피해를 주는 구체를 생성합니다.",
                    "핀볼 생성.  대미지 110%",
                    "핀볼 이동 속도 증가.  대미지 130%",
                    ""),

                // ── 처형자 ──────────────────────────────────────────
                Th(SynergyType.Executioner, "처형자", 2, 4, 6, 0,
                    "3초마다 좌우에 사신의 낫을 발사합니다. 체력이 낮은 적을 즉사시킵니다.",
                    "체력 5% 이하 적 즉사.  대미지 100%",
                    "체력 10% 이하 적 즉사.  대미지 110%",
                    "체력 15% 이하 적 즉사.  대미지 130%"),

                // ── 성기사단 ────────────────────────────────────────
                Th(SynergyType.HolyKnight, "성기사단", 2, 3, 4, 0,
                    "플레이어 위치에 신성한 성역 장판을 생성합니다. 장판 내에서 체력이 회복됩니다.",
                    "장판 5초 유지.  체력 회복 1%.  대미지 50%",
                    "장판 8초 유지.  체력 회복 1.5%.  대미지 60%",
                    "장판 11초 유지.  체력 회복 2%.  대미지 70%"),

                // ── 소드마스터 ──────────────────────────────────────
                Th(SynergyType.SwordMaster, "소드마스터", 2, 4, 6, 0,
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
                Th(SynergyType.Impregnable, "난공불락", 2, 4, 6, 8,
                    "피격 시 충격파를 발산하고 받는 피해를 감소시킵니다.\n방어구 체력 총합에 비례합니다.",
                    "충격파.  피해 감소 10%.  대미지 1000%",
                    "충격파 크기 ×2.  피해 감소 20%.  대미지 1000%",
                    "충격파 강화.  피해 감소 35%.  대미지 1300%",
                    "0.5초마다 자동 발동.  피해 감소 60%.  대미지 2000%"),

                // ── 정령술사 ────────────────────────────────────────
                Th(SynergyType.SpiritMage, "정령술사", 2, 4, 6, 8,
                    "전투에 함께하는 정령 골렘을 소환합니다.",
                    "정령 골렘 1기.  대미지 100%",
                    "정령 골렘 2기.  이동속도 30% 증가.",
                    "정령 골렘 4기.  대미지 200%",
                    "고대 정령 1기.  대미지 450%.  8초마다 광역 공격"),

                // ── 마왕 ────────────────────────────────────────────
                Th(SynergyType.DemonLord, "마왕", 2, 4, 6, 8,
                    "마왕의 스킬이 발동됩니다. 등급이 오를수록 스킬 형태가 강화됩니다.",
                    "지옥불 불씨.  2초당 1회.  대미지 100%",
                    "연옥의 파도.  초당 1회.  대미지 100%",
                    "마왕의 멸천참.  0.1초당 20% 대미지",
                    "전 스킬 합산 + 강화.  대미지 150%↑"),

                // ── 대부호 ──────────────────────────────────────────
                Th(SynergyType.Tycoon, "대부호", 2, 4, 6, 8,
                    "이동 시 골드를 뿌립니다. 2초 후 폭발하며 범위 피해를 줍니다.",
                    "1칸당 골드 3개.  골드당 70% 대미지",
                    "1칸당 골드 5개.",
                    "1칸당 골드 7개.",
                    "1칸당 골드 15개.  폭발 시간 1초로 단축"),

                // ── 과부화 ──────────────────────────────────────────
                Th(SynergyType.Overload, "과부화", 2, 4, 6, 8,
                    "왕귀형 콘셉트. 브~골드는 패널티만 있고 프리즘 달성 시 신이 됩니다.",
                    "10초마다 1초간 이동속도/피해량 50% 감소",
                    "10초마다 1초간 이동속도/피해량 50% 감소",
                    "10초마다 1초간 이동속도/피해량 50% 감소",
                    "초강력 스킬 3개 무한 난사.  대미지 1000%"),
            };

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
                (SynergyType.DemonLord,   SynergyGrade.Bronze, "SK_DEMON_1"),
                (SynergyType.DemonLord,   SynergyGrade.Silver, "SK_DEMON_2"),
                (SynergyType.DemonLord,   SynergyGrade.Gold,   "SK_DEMON_3"),
                (SynergyType.DemonLord,   SynergyGrade.Prism,  "SK_DEMON_4"),
                (SynergyType.Tycoon,      SynergyGrade.Bronze, "SK_GOLD_BOMB_1"),
                (SynergyType.Tycoon,      SynergyGrade.Silver, "SK_GOLD_BOMB_2"),
                (SynergyType.Tycoon,      SynergyGrade.Gold,   "SK_GOLD_BOMB_3"),
                (SynergyType.Tycoon,      SynergyGrade.Prism,  "SK_GOLD_FAST"),
                (SynergyType.Overload,    SynergyGrade.Bronze, "SK_OVERLOAD_PENALTY"),
                (SynergyType.Overload,    SynergyGrade.Silver, "SK_OVERLOAD_PENALTY"),
                (SynergyType.Overload,    SynergyGrade.Gold,   "SK_OVERLOAD_PENALTY"),
                (SynergyType.Overload,    SynergyGrade.Prism,  "SK_OVERLOAD_PRISM"),
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
                (SynergyType.Pinball,    SynergyGrade.Silver, "SUM_PINBALL_2", 1),
                (SynergyType.Fairy,      SynergyGrade.Bronze, "SUM_FAIRY_1",   1),
                (SynergyType.Fairy,      SynergyGrade.Silver, "SUM_FAIRY_2",   2),
                (SynergyType.Fairy,      SynergyGrade.Gold,   "SUM_FAIRY_3",   3),
                (SynergyType.HolyKnight, SynergyGrade.Bronze, "SUM_SANCTUARY_1", 1),
                (SynergyType.HolyKnight, SynergyGrade.Silver, "SUM_SANCTUARY_2", 1),
                (SynergyType.HolyKnight, SynergyGrade.Gold,   "SUM_SANCTUARY_3", 1),
                (SynergyType.SpiritMage, SynergyGrade.Bronze, "SUM_GOLEM_1",    1),
                (SynergyType.SpiritMage, SynergyGrade.Silver, "SUM_GOLEM_2",    2),
                (SynergyType.SpiritMage, SynergyGrade.Gold,   "SUM_GOLEM_3",    4),
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
            float duration = 0f)
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
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static SO_SummonData Sum(
            string id, string name,
            SummonAIType ai, ScalingStatType scaling,
            float atk, float spd, float atkCd, float atkRange, float dur,
            FixedEffectType fx = FixedEffectType.None, float fxVal = 0f,
            float uniqueSkillCd = 8f)
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
                { SynergyType.Assassin,    $"{SheetDir}/Shuriken_Sv.prefab.png"      },
                { SynergyType.SwordMaster, $"{SheetDir}/SwordWave.prefab.png"        },
                { SynergyType.HolyKnight,  $"{SheetDir}/Sanctuary_Sv.prefab.png"     },
                { SynergyType.DemonLord,   $"{SheetDir}/Scythe_BlackRed.prefab.png"  },
                { SynergyType.Titan,       $"{SheetDir}/Shockwave_RED.prefab.png"    },
                { SynergyType.Tycoon,      $"{SheetDir}/Gold_Coin1.prefab.png"       },
                { SynergyType.Executioner, $"{SheetDir}/Scythe_Black.prefab.png"     },
                { SynergyType.SpiritMage,  $"{SheetDir}/Spirit_Sv.prefab.png"        },
                { SynergyType.Fairy,       $"{SheetDir}/Fairy.prefab.png"            },
                { SynergyType.Pinball,     $"{SheetDir}/Pinball.prefab.png"          },
                { SynergyType.Impregnable, $"{SheetDir}/Shockwave_Common.prefab.png" },
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
    }
}
