using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SailRebalance
{
	[HarmonyPatch(typeof(Sail), "ApplyForce")]
	internal static class SailCategoryPowerPatch
	{
		private static readonly MethodInfo JunkMethod = AccessTools.Method(typeof(SailPower), "Junk", null, null);

		private static readonly MethodInfo GaffMethod = AccessTools.Method(typeof(SailPower), "Gaff", null, null);

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
}
