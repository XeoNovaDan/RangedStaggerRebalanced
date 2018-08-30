using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using Harmony;
using Verse;
using RimWorld;

namespace RangedStaggerRebalanced
{
    [StaticConstructorOnStartup]
    static class HarmonyPatches
    {

        static readonly Type patchType = typeof(HarmonyPatches);

        static HarmonyPatches()
        {
            HarmonyInstance h = HarmonyInstance.Create("XeoNovaDan.RangedStaggerRebalanced");

            // HarmonyInstance.DEBUG = true;

            h.Patch(AccessTools.Property(typeof(ProjectileProperties), nameof(ProjectileProperties.StoppingPower)).GetGetMethod(), null,
                new HarmonyMethod(patchType, nameof(PostfixStoppingPower)));

             h.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.SpecialDisplayStats)), null,
                new HarmonyMethod(patchType, nameof(Thing_PostfixSpecialDisplayStats)));

            h.Patch(AccessTools.Method(typeof(ThingDef), nameof(ThingDef.SpecialDisplayStats)), null,
                new HarmonyMethod(patchType, nameof(ThingDef_PostfixSpecialDisplayStats)));

            h.Patch(AccessTools.Method(typeof(Bullet), "Impact"), null, null,
                new HarmonyMethod(patchType, nameof(TranspileImpact)));

        }

        #region PostfixStoppingPower
        private const float StoppingPowerPerDamageAmount = 0.1f;

        public static void PostfixStoppingPower(ProjectileProperties __instance, ref float __result)
        {
            if (__result == 0.5f)
                __result = __instance.GetDamageAmount(null) * StoppingPowerPerDamageAmount;
        }
        #endregion

        #region Thing_PostfixSpecialDisplayStats
        public static void Thing_PostfixSpecialDisplayStats(Thing __instance, ref IEnumerable<StatDrawEntry> __result)
        {
            if (Traverse.Create(__instance.def).Field("verbs").GetValue() is List<VerbProperties> verbs)
            {
                VerbProperties verb = verbs.First(x => x.isPrimary);
                if (verb.LaunchesProjectile && verb.defaultProjectile is ThingDef dP && dP.projectile is ProjectileProperties p)
                {
                    StatCategoryDef c = (__instance.def.category != ThingCategory.Pawn) ? StatCategoryDefOf.Weapon : StatCategoryDefOf.PawnCombat;
                    string vS = GetAdjustedStoppingPower(p.StoppingPower, __instance).ToString("F2");
                    string oRT = "StoppingPowerExplanation".Translate(); 
                    __result = __result.Add(new StatDrawEntry(c, "StoppingPower".Translate(), vS, overrideReportText: oRT));
                }
            }
        }
        #endregion

        #region ThingDef_PostfixSpecialDisplayStats
        public static void ThingDef_PostfixSpecialDisplayStats (ref IEnumerable<StatDrawEntry> __result)
        {
            __result = __result.Where(x => x.LabelCap != "StoppingPower".Translate().CapitalizeFirst());
        }
        #endregion

        #region TranspileImpact
        public static IEnumerable<CodeInstruction> TranspileImpact(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();

            MethodInfo get_stoppingPower = AccessTools.Property(typeof(ProjectileProperties), nameof(ProjectileProperties.StoppingPower)).GetGetMethod();
            MethodInfo getAdjustedStoppingPowerFromLauncher = AccessTools.Method(patchType, nameof(GetAdjustedStoppingPowerFromLauncher));
            bool done = false;

            MethodInfo getStaggerTicks = AccessTools.Method(patchType, nameof(GetStaggerTicks));
            bool done2 = false;

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                // Adjust effective stopping power based on the weapon's quality
                if (!done && instruction.opcode == OpCodes.Callvirt && instruction.operand == get_stoppingPower)
                {
                    yield return new CodeInstruction(instruction.opcode, instruction.operand);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 9);

                    instruction.opcode = OpCodes.Call;
                    instruction.operand = getAdjustedStoppingPowerFromLauncher;

                    done = true;
                }

                // Dynamic stagger duration based on stopping power and target's body size
                if (!done2 && instruction.opcode == OpCodes.Ldc_I4_S)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 11);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 9);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Projectile), nameof(Projectile.def)));
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ThingDef), nameof(ThingDef.projectile)));
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(ProjectileProperties), nameof(ProjectileProperties.StoppingPower)).GetGetMethod());

                    instruction.opcode = OpCodes.Call;
                    instruction.operand = getStaggerTicks;

                    done2 = true;
                }

                yield return instruction;
            }
        }

        public static int GetStaggerTicks(Pawn pawn, Thing launcher, float baseStoppingPower)
        {
            float stoppingPower = GetAdjustedStoppingPowerFromLauncher(baseStoppingPower, launcher);
            float bodySize = pawn.BodySize;
            float powerPct = (RangedStaggerRebalancedSettings.useExperimentalCurve) ? stoppingPower / bodySize : (stoppingPower - bodySize) / stoppingPower;

            int staggerDuration = Mathf.RoundToInt(BaseStaggerTicks * ((RangedStaggerRebalancedSettings.useExperimentalCurve) ?
                ExperimentalCurve.Evaluate(powerPct) : PowerPctCurve.Evaluate(powerPct)));

            if (Prefs.DevMode)
                Log.Message($"stagger duration: {Math.Round(staggerDuration.TicksToSeconds(), 2)}s");

            return staggerDuration;
        }

        private static SimpleCurve ExperimentalCurve = new SimpleCurve
        {
            new CurvePoint(1f, 0.2f),
            new CurvePoint(1.5f, 0.5f),
            new CurvePoint(3f, 1f)
        };

        private const int BaseStaggerTicks = 120;

        private static SimpleCurve PowerPctCurve = new SimpleCurve
        {
            new CurvePoint(1f, 1f),
            new CurvePoint(0.8f, 1f),
            new CurvePoint(0f, 0.2f)
        };

        private static float GetAdjustedStoppingPowerFromLauncher(float baseStoppingPower, Thing launcher)
        {
            Thing weapon = (!(launcher is Pawn)) ? ((launcher is Building_TurretGun) ? ((Building_TurretGun)launcher).gun : null) : ((Pawn)launcher).equipment.Primary;
            return GetAdjustedStoppingPower(baseStoppingPower, weapon);
        }

        private static float GetAdjustedStoppingPower(float baseStoppingPower, Thing weapon)
        {
            return baseStoppingPower * ((weapon != null) ? weapon.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) : 1f) *
                ((weapon != null && weapon.def.Verbs?.First() is VerbProperties verb && verb.defaultProjectile?.GetModExtension<StoppingPowerExtension>() is StoppingPowerExtension sPE) ?
                sPE.stoppingPowerFactor : StoppingPowerExtension.defaultValues.stoppingPowerFactor);
        }
        #endregion

    }
}
