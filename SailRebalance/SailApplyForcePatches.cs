using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SailRebalance;

[HarmonyPatch(typeof(Sail), "ApplyForce")]
internal static class LateenBadTackForcePatch
{
    private struct ForceState
    {
        internal bool Applied;
        internal float ForwardForce;
        internal float SideForce;
    }

    private static void Prefix(
        Sail __instance,
        ref float ___unamplifiedForwardForce,
        ref float ___unamplifiedSidewayForce,
        out ForceState __state)
    {
        __state = default;

        if (!Plugin.LateenBadTackPenaltyEnabled ||
            __instance.category != SailCategory.lateen ||
            !LateenControlRegistry.TryGetRig(__instance, out LateenYardRig rig) ||
            !rig.IsBadTack())
        {
            return;
        }

        __state.Applied = true;
        __state.ForwardForce = ___unamplifiedForwardForce;
        __state.SideForce = ___unamplifiedSidewayForce;

        ___unamplifiedForwardForce *= LateenYardRig.BadTackPowerMultiplier;
        ___unamplifiedSidewayForce *= LateenYardRig.BadTackPowerMultiplier;
    }

    private static void Postfix(
        ref float ___unamplifiedForwardForce,
        ref float ___unamplifiedSidewayForce,
        ForceState __state)
    {
        if (!__state.Applied)
            return;

        ___unamplifiedForwardForce = __state.ForwardForce;
        ___unamplifiedSidewayForce = __state.SideForce;
    }
}

[HarmonyPatch(typeof(Sail), "ApplyForce")]
internal static class SailCategoryPowerPatch
{
    private static readonly MethodInfo JunkMethod =
        AccessTools.Method(typeof(SailPower), nameof(SailPower.Junk));

    private static readonly MethodInfo GaffMethod =
        AccessTools.Method(typeof(SailPower), nameof(SailPower.Gaff));

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        bool replacedJunk = false;
        bool replacedGaff = false;

        foreach (CodeInstruction instruction in instructions)
        {
            MethodInfo replacement = null;

            if (instruction.opcode == OpCodes.Ldc_R4 &&
                instruction.operand is float value)
            {
                if (!replacedJunk && value == SailPower.JunkDefault)
                {
                    replacement = JunkMethod;
                    replacedJunk = true;
                }
                else if (!replacedGaff && value == SailPower.GaffDefault)
                {
                    replacement = GaffMethod;
                    replacedGaff = true;
                }
            }

            if (replacement == null)
            {
                yield return instruction;
                continue;
            }

            instruction.opcode = OpCodes.Ldarg_0;
            instruction.operand = null;
            yield return instruction;
            yield return new CodeInstruction(OpCodes.Call, replacement);
        }

        if (!replacedJunk || !replacedGaff)
        {
            throw new InvalidOperationException(
                "Could not locate Sail.ApplyForce's junk and gaff multipliers. The game may have updated.");
        }
    }
}
