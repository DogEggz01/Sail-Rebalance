using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SailRebalance;

[HarmonyPatch(typeof(Mast), "UpdateControllerAttachments")]
internal static class LateenLowerBraceAttachmentPatch
{
	private static readonly HashSet<int> MissingWinchWarnings = new HashSet<int>();

	private static void Postfix(Mast __instance)
	{
		if (__instance.sails == null)
		{
			return;
		}
		for (int i = 0; i < __instance.sails.Count; i++)
		{
			GameObject gameObject = __instance.sails[i];
			if (gameObject == null)
			{
				continue;
			}
			Sail component = gameObject.GetComponent<Sail>();
			SailConnections component2 = gameObject.GetComponent<SailConnections>();
			if (component == null || component2 == null || component.category != SailCategory.lateen)
			{
				continue;
			}
			LateenYardRig lateenYardRig = gameObject.GetComponent<LateenYardRig>();
			GPButtonRopeWinch gpbuttonRopeWinch = ((lateenYardRig != null && lateenYardRig.LowerBrace != null) ? FindAttachedWinch(__instance, component.mastOrder, lateenYardRig.LowerBrace) : null);
			if (gpbuttonRopeWinch == null)
			{
				gpbuttonRopeWinch = FindUnusedWinch(__instance, component.mastOrder);
			}
			if (gpbuttonRopeWinch == null)
			{
				if (MissingWinchWarnings.Add(component.GetInstanceID()))
				{
					Plugin.Log?.LogWarning("No unused winch is available for the lower brace on " + component.name + ".");
				}
				continue;
			}
			if (lateenYardRig == null)
			{
				lateenYardRig = gameObject.AddComponent<LateenYardRig>();
			}
			if (lateenYardRig.Initialize(__instance, component, component2, gpbuttonRopeWinch) && !(lateenYardRig.LowerBrace == null))
			{
				gpbuttonRopeWinch.AttachToController(lateenYardRig.LowerBrace);
				gpbuttonRopeWinch.description = "lower brace winch";
				gpbuttonRopeWinch.gameObject.name = "lower brace winch";
				gpbuttonRopeWinch.ShowWinch(state: true);
			}
		}
	}

	private static GPButtonRopeWinch FindUnusedWinch(Mast mast, int order)
	{
		GPButtonRopeWinch unused = GetUnused(mast.leftAngleWinch, order);
		if (unused != null)
		{
			return unused;
		}
		unused = GetUnused(mast.rightAngleWinch, order);
		if (unused != null)
		{
			return unused;
		}
		return GetUnused(mast.midAngleWinch, order);
	}

	private static GPButtonRopeWinch FindAttachedWinch(Mast mast, int order, RopeController controller)
	{
		GPButtonRopeWinch attached = GetAttached(mast.leftAngleWinch, order, controller);
		if (attached != null)
		{
			return attached;
		}
		attached = GetAttached(mast.rightAngleWinch, order, controller);
		if (attached != null)
		{
			return attached;
		}
		return GetAttached(mast.midAngleWinch, order, controller);
	}

	private static GPButtonRopeWinch GetAttached(GPButtonRopeWinch[] winches, int index, RopeController controller)
	{
		if (winches == null || index < 0 || index >= winches.Length)
		{
			return null;
		}
		GPButtonRopeWinch gpbuttonRopeWinch = winches[index];
		if (!(gpbuttonRopeWinch != null) || !(gpbuttonRopeWinch.rope == controller))
		{
			return null;
		}
		return gpbuttonRopeWinch;
	}

	private static GPButtonRopeWinch GetUnused(GPButtonRopeWinch[] winches, int index)
	{
		if (winches == null || index < 0 || index >= winches.Length)
		{
			return null;
		}
		GPButtonRopeWinch gpbuttonRopeWinch = winches[index];
		if (!(gpbuttonRopeWinch != null) || !(gpbuttonRopeWinch.rope == null))
		{
			return null;
		}
		return gpbuttonRopeWinch;
	}
}
