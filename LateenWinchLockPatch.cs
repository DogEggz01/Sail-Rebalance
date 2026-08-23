using System;
using HarmonyLib;

namespace SailRebalance
{
	[HarmonyPatch(typeof(GPButtonRopeWinch), "Update")]
	internal static class LateenWinchLockPatch
	{
		private static bool Prefix(GPButtonRopeWinch __instance, ref float ___currentInput)
		{
			LateenControlBinding lateenControlBinding;
			if (!LateenControlRegistry.TryGet(__instance.rope, out lateenControlBinding))
			{
				return true;
			}
			if ((lateenControlBinding.Role == LateenControlRole.Brace) ? lateenControlBinding.Rig.CanOperateBrace : lateenControlBinding.Rig.CanOperateHalyardOrSheet)
			{
				return true;
			}
			___currentInput = 0f;
			return false;
		}
	}
}
