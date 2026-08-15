using HarmonyLib;

namespace SailRebalance;

[HarmonyPatch(typeof(GPButtonRopeWinch), "Update")]
internal static class LateenWinchLockPatch
{
	private static bool Prefix(GPButtonRopeWinch __instance, ref float ___currentInput)
	{
		if (!LateenControlRegistry.TryGet(__instance.rope, out var lateenControlBinding))
		{
			return true;
		}
		if ((lateenControlBinding.Role == LateenControlRole.LowerBrace) ? lateenControlBinding.Rig.CanOperateLowerBrace : lateenControlBinding.Rig.CanOperateHalyardOrSheet)
		{
			return true;
		}
		___currentInput = 0f;
		return false;
	}
}
