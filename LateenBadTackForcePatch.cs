using System;
using HarmonyLib;

namespace SailRebalance
{
	[HarmonyPatch(typeof(Sail), "ApplyForce")]
	internal static class LateenBadTackForcePatch
	{
		private static void Prefix(Sail __instance, ref float ___unamplifiedForwardForce, ref float ___unamplifiedSidewayForce, out LateenBadTackForcePatch.ForceState __state)
		{
			__state = default(LateenBadTackForcePatch.ForceState);
			LateenYardRig lateenYardRig;
			if (!Plugin.LateenBadTackPenaltyEnabled || __instance.category != SailCategory.lateen || !LateenControlRegistry.TryGetRig(__instance, out lateenYardRig) || !lateenYardRig.IsBadTack())
			{
				return;
			}
			__state.Applied = true;
			__state.ForwardForce = ___unamplifiedForwardForce;
			__state.SideForce = ___unamplifiedSidewayForce;
			___unamplifiedForwardForce *= LateenYardRig.BadTackPowerMultiplier;
			___unamplifiedSidewayForce *= LateenYardRig.BadTackPowerMultiplier;
		}

		private static void Postfix(ref float ___unamplifiedForwardForce, ref float ___unamplifiedSidewayForce, LateenBadTackForcePatch.ForceState __state)
		{
			if (!__state.Applied)
			{
				return;
			}
			___unamplifiedForwardForce = __state.ForwardForce;
			___unamplifiedSidewayForce = __state.SideForce;
		}

		private struct ForceState
		{
			internal bool Applied;

			internal float ForwardForce;

			internal float SideForce;
		}
	}
}
