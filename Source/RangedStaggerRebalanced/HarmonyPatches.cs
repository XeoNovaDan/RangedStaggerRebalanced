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

            h.Patch(AccessTools.Method(typeof(Bullet), "Impact"), null, null,
                new HarmonyMethod(patchType, nameof(TranspileImpact)));

        }

        public static IEnumerable<CodeInstruction> TranspileImpact(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();

            MethodInfo getStaggerTicks = AccessTools.Method(patchType, nameof(GetStaggerTicks));
            bool done = false;

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if (!done && instruction.opcode == OpCodes.Ldc_I4_S)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 11);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Projectile), nameof(Projectile.def)));
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ThingDef), nameof(ThingDef.projectile)));
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(ProjectileProperties), nameof(ProjectileProperties.StoppingPower)).GetGetMethod());

                    instruction.opcode = OpCodes.Call;
                    instruction.operand = getStaggerTicks;

                    done = true;
                }

                yield return instruction;
            }
        }

        public static int GetStaggerTicks(Pawn pawn, float stoppingPower)
        {
            float bodySize = pawn.BodySize;
            float powerPct = (stoppingPower - bodySize) / stoppingPower;

            SimpleCurve powerPctCurve = new SimpleCurve
            {
                { new CurvePoint(1f, 1f), true },
                { new CurvePoint(0.8f, 1f), true },
                { new CurvePoint(0f, 0.2f), true }
            };  

            return Mathf.RoundToInt(BaseStaggerTicks * powerPctCurve.Evaluate(powerPct));
        }

        private const int BaseStaggerTicks = 120;

    }
}
