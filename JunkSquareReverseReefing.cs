using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SailRebalance
{
	internal static class JunkSquareReverseReefing
	{
		internal static bool IsTarget(Sail sail)
		{
			return sail != null && sail.squareSail && sail.category == SailCategory.square && JunkSquareIndices.Contains(sail.prefabIndex);
		}

		internal static void PrepareController(RopeControllerSailReef controller)
		{
			if (!Plugin.ReverseJunkSquareFurlingEnabled || controller == null || !IsTarget(controller.sail))
			{
				return;
			}

			if (!OriginalControllerDirections.ContainsKey(controller))
			{
				OriginalControllerDirections.Add(controller, controller.reverseReefing);
			}
			controller.reverseReefing = true;
		}

		internal static bool TakeOriginalControllerDirection(RopeControllerSailReef controller)
		{
			bool originalDirection;
			if (OriginalControllerDirections.TryGetValue(controller, out originalDirection))
			{
				OriginalControllerDirections.Remove(controller);
				return originalDirection;
			}
			return controller.reverseReefing;
		}

		internal static void RestorePreparedController(RopeControllerSailReef controller)
		{
			bool originalDirection;
			if (controller == null || !OriginalControllerDirections.TryGetValue(controller, out originalDirection))
			{
				return;
			}

			OriginalControllerDirections.Remove(controller);
			float currentUnroll = controller.sail != null ? Mathf.Clamp01(controller.sail.currentUnroll) : 1f;
			controller.reverseReefing = originalDirection;
			controller.currentLength = originalDirection ? 1f - currentUnroll : currentUnroll;
			controller.changed = true;
		}

		internal static void Register(JunkSquareReverseRig rig)
		{
			Rigs.Add(rig);
		}

		internal static void Unregister(JunkSquareReverseRig rig)
		{
			Rigs.Remove(rig);
		}

		internal static void PrepareMast(Mast mast)
		{
			JunkSquareReverseRig[] rigs = CopyRigs();
			for (int i = 0; i < rigs.Length; i++)
			{
				JunkSquareReverseRig rig = rigs[i];
				if (rig != null && rig.OwningMast == mast)
				{
					rig.PrepareForMastUpdate();
				}
			}
		}

		internal static void ApplyToMast(Mast mast)
		{
			if (mast == null || mast.sails == null)
			{
				return;
			}

			for (int i = 0; i < mast.sails.Count; i++)
			{
				GameObject sailObject = mast.sails[i];
				if (sailObject == null)
				{
					continue;
				}

				Sail sail = sailObject.GetComponent<Sail>();
				JunkSquareReverseRig rig = sailObject.GetComponent<JunkSquareReverseRig>();
				if (!Plugin.ReverseJunkSquareFurlingEnabled || !IsTarget(sail))
				{
					if (rig != null)
					{
						rig.RestoreAndDestroy();
					}
					continue;
				}

				SailConnections connections = sailObject.GetComponent<SailConnections>();
				if (connections == null)
				{
					WarnOnce(sail, "has no SailConnections component");
					continue;
				}

				if (rig == null)
				{
					rig = sailObject.AddComponent<JunkSquareReverseRig>();
				}
				if (rig.Initialize(mast, sail, connections))
				{
					rig.BindAssignedWinch();
					Warnings.Remove(sail.GetInstanceID());
				}
				else
				{
					RestorePreparedController(connections.reefController as RopeControllerSailReef);
					rig.RestoreAndDestroy();
				}
			}
		}

		internal static void ApplyToLoadedMasts()
		{
			Mast[] masts = Object.FindObjectsOfType<Mast>();
			for (int i = 0; i < masts.Length; i++)
			{
				ApplyToMast(masts[i]);
			}
		}

		internal static void SetEnabled(bool enabled)
		{
			if (enabled)
			{
				ApplyToLoadedMasts();
				return;
			}
			DisposeAllRigs();
		}

		internal static void FurlNewlyInstalledShipyardSail(GameObject sailObject)
		{
			if (!Plugin.ReverseJunkSquareFurlingEnabled || GameState.currentShipyard == null || sailObject == null)
			{
				return;
			}

			Sail sail = sailObject.GetComponent<Sail>();
			if (!IsTarget(sail) || !sail.IsInstalled())
			{
				return;
			}

			SailConnections connections = sailObject.GetComponent<SailConnections>();
			RopeControllerSailReef controller = connections != null ? connections.reefController as RopeControllerSailReef : null;
			if (controller == null)
			{
				WarnOnce(sail, "its newly installed reef controller was not found");
				return;
			}

			sail.currentUnroll = 0f;
			controller.reverseReefing = true;
			controller.currentLength = 1f;
			controller.changed = true;
			Warnings.Remove(sail.GetInstanceID());
		}

		internal static void DisposeAllRigs()
		{
			JunkSquareReverseRig[] rigs = CopyRigs();
			for (int i = 0; i < rigs.Length; i++)
			{
				if (rigs[i] != null)
				{
					rigs[i].RestoreAndDestroy();
				}
			}
			Rigs.Clear();
			RestoreAllPreparedControllers();
		}

		internal static void Reset()
		{
			DisposeAllRigs();
			OriginalControllerDirections.Clear();
			Warnings.Clear();
		}

		internal static void WarnOnce(Sail sail, string reason)
		{
			if (sail == null || !Warnings.Add(sail.GetInstanceID()))
			{
				return;
			}
			if (Plugin.Log != null)
			{
				Plugin.Log.LogWarning("Could not reverse Junk-square furling for " + sail.name + ": " + reason + ".");
			}
		}

		private static JunkSquareReverseRig[] CopyRigs()
		{
			JunkSquareReverseRig[] rigs = new JunkSquareReverseRig[Rigs.Count];
			Rigs.CopyTo(rigs);
			return rigs;
		}

		private static void RestoreAllPreparedControllers()
		{
			RopeControllerSailReef[] controllers = new RopeControllerSailReef[OriginalControllerDirections.Count];
			OriginalControllerDirections.Keys.CopyTo(controllers, 0);
			for (int i = 0; i < controllers.Length; i++)
			{
				RestorePreparedController(controllers[i]);
			}
			OriginalControllerDirections.Clear();
		}

		private static readonly HashSet<int> JunkSquareIndices = new HashSet<int>
		{
			27,
			28,
			29,
			100,
			101,
			102,
			103
		};

		private static readonly HashSet<JunkSquareReverseRig> Rigs = new HashSet<JunkSquareReverseRig>();

		private static readonly Dictionary<RopeControllerSailReef, bool> OriginalControllerDirections = new Dictionary<RopeControllerSailReef, bool>();

		private static readonly HashSet<int> Warnings = new HashSet<int>();
	}

	[HarmonyPatch(typeof(RopeControllerSailReef), "Awake")]
	internal static class JunkSquareReverseControllerAwakePatch
	{
		private static void Prefix(RopeControllerSailReef __instance)
		{
			JunkSquareReverseReefing.PrepareController(__instance);
		}
	}

	[HarmonyPatch(typeof(Mast), "UpdateControllerAttachments")]
	internal static class JunkSquareReverseMastAttachmentPatch
	{
		private static void Prefix(Mast __instance)
		{
			JunkSquareReverseReefing.PrepareMast(__instance);
		}

		private static void Postfix(Mast __instance)
		{
			JunkSquareReverseReefing.ApplyToMast(__instance);
		}
	}

	[HarmonyPatch(typeof(Mast), "AttachSailToMast")]
	internal static class JunkSquareReverseShipyardInstallPatch
	{
		private static void Prefix(GameObject sailObject, out bool __state)
		{
			Sail sail = sailObject != null ? sailObject.GetComponent<Sail>() : null;
			__state = GameState.currentShipyard != null && sail != null && !sail.IsInstalled() && JunkSquareReverseReefing.IsTarget(sail);
		}

		private static void Postfix(GameObject sailObject, bool __state)
		{
			if (__state)
			{
				JunkSquareReverseReefing.FurlNewlyInstalledShipyardSail(sailObject);
			}
		}
	}
}
