using HarmonyLib;

namespace SailRebalance;

[HarmonyPatch(typeof(RopeControllerSailReef), "Update")]
internal static class LateenHalyardKeyboardLockPatch
{
	private static bool Prefix(RopeControllerSailReef __instance)
	{
		if (LateenControlRegistry.TryGet(__instance, out var lateenControlBinding) && lateenControlBinding.Role == LateenControlRole.Halyard)
		{
			return lateenControlBinding.Rig.CanOperateHalyardOrSheet;
		}
		return true;
	}
}
