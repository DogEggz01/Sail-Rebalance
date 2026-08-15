using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SailRebalance;

[HarmonyPatch(typeof(Sail), "ApplyForce")]
internal static class SailApplyForcePatch
{
	private struct LateenForceState
	{
		internal bool Applied;

		internal float ForwardForce;

		internal float SideForce;
	}

	private static readonly MethodInfo JunkMethod = AccessTools.Method(typeof(SailPower), "Junk");

	private static readonly MethodInfo GaffMethod = AccessTools.Method(typeof(SailPower), "Gaff");

	private static void Prefix(Sail __instance, ref float ___unamplifiedForwardForce, ref float ___unamplifiedSidewayForce, out LateenForceState __state)
	{
		__state = default(LateenForceState);
		if (Plugin.LateenBadTackPenaltyEnabled && __instance.category == SailCategory.lateen && LateenControlRegistry.TryGetRig(__instance, out var lateenYardRig) && lateenYardRig.IsBadTack())
		{
			__state.Applied = true;
			__state.ForwardForce = ___unamplifiedForwardForce;
			__state.SideForce = ___unamplifiedSidewayForce;
			___unamplifiedForwardForce *= 0.9f;
			___unamplifiedSidewayForce *= 0.9f;
		}
	}

	private static void Postfix(ref float ___unamplifiedForwardForce, ref float ___unamplifiedSidewayForce, LateenForceState __state)
	{
		if (__state.Applied)
		{
			___unamplifiedForwardForce = __state.ForwardForce;
			___unamplifiedSidewayForce = __state.SideForce;
		}
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		bool replacedJunk = false;
		bool replacedGaff = false;
		foreach (CodeInstruction instruction in instructions)
		{
			MethodInfo replacement = null;
			if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float value)
			{
				if (!replacedJunk && value == 0.75f)
				{
					replacement = JunkMethod;
					replacedJunk = true;
				}
				else if (!replacedGaff && value == 0.85f)
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
			throw new InvalidOperationException("Could not locate Sail.ApplyForce's junk and gaff multipliers. The game may have updated.");
		}
	}
}
