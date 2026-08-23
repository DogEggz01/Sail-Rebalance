using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SailRebalance
{
	[HarmonyPatch(typeof(Mast), "UpdateControllerAttachments")]
	internal static class LateenBraceAttachmentPatch
	{
		private static void Postfix(Mast __instance)
		{
			Mast[] boatMasts = LateenBraceAttachmentPatch.GetBoatMasts(__instance);
			LateenBraceAttachmentPatch.ReleaseBraceAssignments(__instance);
			List<LateenSailContext> lateens = LateenBraceAttachmentPatch.GetLateens(boatMasts);
			for (int i = 0; i < lateens.Count; i++)
			{
				LateenBraceAttachmentPatch.AttachBrace(lateens[i], boatMasts);
			}
		}

		private static void AttachBrace(LateenSailContext context, Mast[] boatMasts)
		{
			LateenBraceWinchChoice choice = LateenBraceWinchSelector.Find(context.Mast, context.Sail.mastOrder, boatMasts);
			if (choice == null)
			{
				LateenBraceAttachmentPatch.WarnMissingWinch(context.Sail);
				return;
			}

			LateenYardRig rig = context.SailObject.GetComponent<LateenYardRig>();
			if (rig == null)
			{
				rig = context.SailObject.AddComponent<LateenYardRig>();
			}
			rig.SetBraceRopeHolder(choice.RopeHolder);
			if (!rig.Initialize(context.Mast, context.Sail, context.Connections) || rig.Brace == null)
			{
				return;
			}

			LateenBraceAttachmentPatch.RememberDescription(choice.Winch);
			choice.Winch.AttachToController(rig.Brace);
			choice.Winch.description = BraceWinchName;
			choice.Winch.ShowWinch(true);
			LateenBraceAttachmentPatch.MissingWinchWarnings.Remove(context.Sail.GetInstanceID());
		}

		private static Mast[] GetBoatMasts(Mast mast)
		{
			Mast[] masts = LateenBraceAttachmentPatch.GetBoatRoot(mast).GetComponentsInChildren<Mast>(false);
			for (int i = 0; i < masts.Length; i++)
			{
				if (masts[i] == mast)
				{
					return masts;
				}
			}
			return new Mast[] { mast };
		}

		private static List<LateenSailContext> GetLateens(Mast[] masts)
		{
			List<LateenSailContext> lateens = new List<LateenSailContext>();
			for (int i = 0; i < masts.Length; i++)
			{
				Mast mast = masts[i];
				if (mast == null || mast.sails == null)
				{
					continue;
				}
				for (int j = 0; j < mast.sails.Count; j++)
				{
					GameObject sailObject = mast.sails[j];
					if (sailObject == null)
					{
						continue;
					}
					Sail sail = sailObject.GetComponent<Sail>();
					SailConnections connections = sailObject.GetComponent<SailConnections>();
					if (sail != null && connections != null && sail.category == SailCategory.lateen)
					{
						lateens.Add(new LateenSailContext
						{
							Mast = mast,
							Sail = sail,
							Connections = connections,
							SailObject = sailObject
						});
					}
				}
			}
			lateens.Sort(LateenBraceAttachmentPatch.CompareLateens);
			return lateens;
		}

		private static int CompareLateens(LateenSailContext left, LateenSailContext right)
		{
			int comparison = left.Mast.orderIndex.CompareTo(right.Mast.orderIndex);
			if (comparison != 0)
			{
				return comparison;
			}
			comparison = left.Sail.mastOrder.CompareTo(right.Sail.mastOrder);
			return (comparison != 0) ? comparison : left.Sail.GetInstanceID().CompareTo(right.Sail.GetInstanceID());
		}

		private static Transform GetBoatRoot(Mast mast)
		{
			return (mast.shipRigidbody != null) ? mast.shipRigidbody.transform : mast.transform;
		}

		private static void ReleaseBraceAssignments(Mast mast)
		{
			GPButtonRopeWinch[] winches = LateenBraceAttachmentPatch.GetBoatRoot(mast).GetComponentsInChildren<GPButtonRopeWinch>(true);
			for (int i = 0; i < winches.Length; i++)
			{
				GPButtonRopeWinch winch = winches[i];
				string description;
				if (LateenBraceAttachmentPatch.OriginalDescriptions.TryGetValue(winch, out description))
				{
					winch.description = description;
					LateenBraceAttachmentPatch.OriginalDescriptions.Remove(winch);
				}
				if (winch.rope is LateenBraceController)
				{
					winch.rope = null;
					winch.ShowWinch(false);
				}
			}
		}

		private static void RememberDescription(GPButtonRopeWinch winch)
		{
			if (!LateenBraceAttachmentPatch.OriginalDescriptions.ContainsKey(winch))
			{
				LateenBraceAttachmentPatch.OriginalDescriptions.Add(winch, winch.description);
			}
		}

		private static void WarnMissingWinch(Sail sail)
		{
			if (!LateenBraceAttachmentPatch.MissingWinchWarnings.Add(sail.GetInstanceID()))
			{
				return;
			}
			ManualLogSource log = Plugin.Log;
			if (log != null)
			{
				log.LogWarning("No unused winch is available for the brace on " + sail.name + ".");
			}
		}

		internal static void Reset()
		{
			foreach (KeyValuePair<GPButtonRopeWinch, string> entry in LateenBraceAttachmentPatch.OriginalDescriptions)
			{
				GPButtonRopeWinch winch = entry.Key;
				if (winch != null)
				{
					winch.description = entry.Value;
					if (winch.rope is LateenBraceController)
					{
						winch.rope = null;
						winch.ShowWinch(false);
					}
				}
			}
			LateenBraceAttachmentPatch.OriginalDescriptions.Clear();
			LateenBraceAttachmentPatch.MissingWinchWarnings.Clear();
		}

		private const string BraceWinchName = "brace winch";

		private static readonly Dictionary<GPButtonRopeWinch, string> OriginalDescriptions = new Dictionary<GPButtonRopeWinch, string>();

		private static readonly HashSet<int> MissingWinchWarnings = new HashSet<int>();

		private sealed class LateenSailContext
		{
			internal Mast Mast;

			internal Sail Sail;

			internal SailConnections Connections;

			internal GameObject SailObject;
		}
	}
}
