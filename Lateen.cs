using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

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

	[HarmonyPatch(typeof(Mast), "UpdateControllerAttachments")]
	internal static class LateenBraceAttachmentPatch
	{
		private static void Postfix(Mast __instance)
		{
			Mast[] boatMasts = LateenBraceAttachmentPatch.GetBoatMasts(__instance);
			LateenBraceAttachmentPatch.ReleaseBraceAssignments(__instance);
			List<LateenBraceAttachmentPatch.LateenSailContext> lateens = LateenBraceAttachmentPatch.GetLateens(boatMasts);
			for (int i = 0; i < lateens.Count; i++)
			{
				LateenBraceAttachmentPatch.AttachBrace(lateens[i], boatMasts);
			}
		}

		private static void AttachBrace(LateenBraceAttachmentPatch.LateenSailContext context, Mast[] boatMasts)
		{
			LateenBraceWinchChoice lateenBraceWinchChoice = LateenBraceWinchSelector.Find(context.Mast, context.Sail.mastOrder, boatMasts);
			if (lateenBraceWinchChoice == null)
			{
				LateenBraceAttachmentPatch.WarnMissingWinch(context.Sail);
				return;
			}
			LateenYardRig lateenYardRig = context.SailObject.GetComponent<LateenYardRig>();
			if (lateenYardRig == null)
			{
				lateenYardRig = context.SailObject.AddComponent<LateenYardRig>();
			}
			lateenYardRig.SetBraceRopeHolder(lateenBraceWinchChoice.RopeHolder);
			if (!lateenYardRig.Initialize(context.Mast, context.Sail, context.Connections) || lateenYardRig.Brace == null)
			{
				return;
			}
			LateenBraceAttachmentPatch.RememberDescription(lateenBraceWinchChoice.Winch);
			lateenBraceWinchChoice.Winch.AttachToController(lateenYardRig.Brace);
			lateenBraceWinchChoice.Winch.description = LateenBraceAttachmentPatch.BraceWinchName;
			lateenBraceWinchChoice.Winch.ShowWinch(true);
			LateenBraceAttachmentPatch.MissingWinchWarnings.Remove(context.Sail.GetInstanceID());
		}

		private static Mast[] GetBoatMasts(Mast mast)
		{
			Mast[] componentsInChildren = LateenBraceAttachmentPatch.GetBoatRoot(mast).GetComponentsInChildren<Mast>(false);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] == mast)
				{
					return componentsInChildren;
				}
			}
			return new Mast[]
			{
				mast
			};
		}

		private static List<LateenBraceAttachmentPatch.LateenSailContext> GetLateens(Mast[] masts)
		{
			List<LateenBraceAttachmentPatch.LateenSailContext> list = new List<LateenBraceAttachmentPatch.LateenSailContext>();
			for (int i = 0; i < masts.Length; i++)
			{
				Mast mast = masts[i];
				if (!(mast == null) && mast.sails != null)
				{
					for (int j = 0; j < mast.sails.Count; j++)
					{
						GameObject gameObject = mast.sails[j];
						if (!(gameObject == null))
						{
							Sail component = gameObject.GetComponent<Sail>();
							SailConnections component2 = gameObject.GetComponent<SailConnections>();
							if (component != null && component2 != null && component.category == SailCategory.lateen)
							{
								list.Add(new LateenBraceAttachmentPatch.LateenSailContext
								{
									Mast = mast,
									Sail = component,
									Connections = component2,
									SailObject = gameObject
								});
							}
						}
					}
				}
			}
			list.Sort(LateenBraceAttachmentPatch.CompareLateens);
			return list;
		}

		private static int CompareLateens(LateenBraceAttachmentPatch.LateenSailContext left, LateenBraceAttachmentPatch.LateenSailContext right)
		{
			int num = left.Mast.orderIndex.CompareTo(right.Mast.orderIndex);
			if (num != 0)
			{
				return num;
			}
			num = left.Sail.mastOrder.CompareTo(right.Sail.mastOrder);
			if (num == 0)
			{
				return left.Sail.GetInstanceID().CompareTo(right.Sail.GetInstanceID());
			}
			return num;
		}

		private static Transform GetBoatRoot(Mast mast)
		{
			if (!(mast.shipRigidbody != null))
			{
				return mast.transform;
			}
			return mast.shipRigidbody.transform;
		}

		private static void ReleaseBraceAssignments(Mast mast)
		{
			foreach (GPButtonRopeWinch gpbuttonRopeWinch in LateenBraceAttachmentPatch.GetBoatRoot(mast).GetComponentsInChildren<GPButtonRopeWinch>(true))
			{
				string description;
				if (LateenBraceAttachmentPatch.OriginalDescriptions.TryGetValue(gpbuttonRopeWinch, out description))
				{
					gpbuttonRopeWinch.description = description;
					LateenBraceAttachmentPatch.OriginalDescriptions.Remove(gpbuttonRopeWinch);
				}
				if (gpbuttonRopeWinch.rope is LateenBraceController)
				{
					gpbuttonRopeWinch.rope = null;
					gpbuttonRopeWinch.ShowWinch(false);
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
			foreach (KeyValuePair<GPButtonRopeWinch, string> keyValuePair in LateenBraceAttachmentPatch.OriginalDescriptions)
			{
				GPButtonRopeWinch key = keyValuePair.Key;
				if (key != null)
				{
					key.description = keyValuePair.Value;
					if (key.rope is LateenBraceController)
					{
						key.rope = null;
						key.ShowWinch(false);
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

	internal sealed class LateenBraceController : RopeController
	{
		internal LateenYardRig Rig { get; private set; }

		internal void Initialize(LateenYardRig rig)
		{
			this.Rig = rig;
			this.currentLength = 0f;
			this.currentResistance = 0f;
			this.changed = true;
		}

		public override bool CanPull()
		{
			return this.Rig != null && this.Rig.CanOperateBrace;
		}

		private void Update()
		{
			this.currentLength = Mathf.Clamp01(this.currentLength);
			this.changed = false;
		}
	}

	internal sealed class LateenBraceWinchChoice
	{
		internal GPButtonRopeWinch Winch;

		internal Transform RopeHolder;
	}

	internal static class LateenBraceWinchSelector
	{
		internal static LateenBraceWinchChoice Find(Mast owningMast, int sailOrder, Mast[] boatMasts)
		{
			LateenBraceWinchChoice lateenBraceWinchChoice = LateenBraceWinchSelector.FindReefWinchOnMast(owningMast);
			if (lateenBraceWinchChoice != null)
			{
				return lateenBraceWinchChoice;
			}
			lateenBraceWinchChoice = LateenBraceWinchSelector.FindSpecialReefWinch(owningMast, boatMasts, false);
			if (lateenBraceWinchChoice != null)
			{
				return lateenBraceWinchChoice;
			}
			lateenBraceWinchChoice = LateenBraceWinchSelector.FindAssociatedStaysailAngleWinch(owningMast, owningMast, boatMasts);
			if (lateenBraceWinchChoice != null)
			{
				return lateenBraceWinchChoice;
			}
			lateenBraceWinchChoice = LateenBraceWinchSelector.FindAngleWinchOnMast(owningMast, sailOrder);
			if (lateenBraceWinchChoice != null)
			{
				return lateenBraceWinchChoice;
			}
			lateenBraceWinchChoice = LateenBraceWinchSelector.FindOtherStaysailAngleWinch(owningMast, boatMasts);
			if (lateenBraceWinchChoice != null)
			{
				return lateenBraceWinchChoice;
			}
			return LateenBraceWinchSelector.FindSpecialReefWinch(owningMast, boatMasts, true);
		}

		private static LateenBraceWinchChoice FindReefWinchOnMast(Mast mast)
		{
			if (mast == null || mast.reefWinch == null)
			{
				return null;
			}
			for (int i = 0; i < LateenBraceWinchSelector.PreferredReefSlots.Length; i++)
			{
				LateenBraceWinchChoice availableReefWinch = LateenBraceWinchSelector.GetAvailableReefWinch(mast, LateenBraceWinchSelector.PreferredReefSlots[i]);
				if (availableReefWinch != null)
				{
					return availableReefWinch;
				}
			}
			for (int j = 0; j < mast.reefWinch.Length; j++)
			{
				if (!LateenBraceWinchSelector.IsPreferredReefSlot(j))
				{
					LateenBraceWinchChoice availableReefWinch2 = LateenBraceWinchSelector.GetAvailableReefWinch(mast, j);
					if (availableReefWinch2 != null)
					{
						return availableReefWinch2;
					}
				}
			}
			return null;
		}

		private static LateenBraceWinchChoice FindSpecialReefWinch(Mast owningMast, Mast[] boatMasts, bool bowsprit)
		{
			LateenBraceWinchSelector.RankedReefChoice rankedReefChoice = null;
			for (int i = 0; i < boatMasts.Length; i++)
			{
				Mast mast = boatMasts[i];
				if (!(mast == null) && !(mast == owningMast) && LateenBraceWinchSelector.IsBowsprit(mast) == bowsprit && (bowsprit || mast.onlyStaysails) && mast.reefWinch != null)
				{
					for (int j = 0; j < mast.reefWinch.Length; j++)
					{
						LateenBraceWinchChoice availableReefWinch = LateenBraceWinchSelector.GetAvailableReefWinch(mast, j);
						if (availableReefWinch != null)
						{
							LateenBraceWinchSelector.RankedReefChoice rankedReefChoice2 = new LateenBraceWinchSelector.RankedReefChoice
							{
								Choice = availableReefWinch,
								SlotPriority = LateenBraceWinchSelector.GetSlotPriority(j),
								WinchDistance = (availableReefWinch.Winch.transform.position - owningMast.transform.position).sqrMagnitude,
								RopeHolderDistance = (availableReefWinch.RopeHolder.position - owningMast.transform.position).sqrMagnitude,
								SourceOrder = mast.orderIndex,
								SourceInstanceId = mast.GetInstanceID()
							};
							if (LateenBraceWinchSelector.IsBetterSpecialReefChoice(rankedReefChoice2, rankedReefChoice))
							{
								rankedReefChoice = rankedReefChoice2;
							}
						}
					}
				}
			}
			if (rankedReefChoice == null)
			{
				return null;
			}
			return rankedReefChoice.Choice;
		}

		private static bool IsBetterSpecialReefChoice(LateenBraceWinchSelector.RankedReefChoice candidate, LateenBraceWinchSelector.RankedReefChoice current)
		{
			if (current == null || candidate.SlotPriority != current.SlotPriority)
			{
				return current == null || candidate.SlotPriority < current.SlotPriority;
			}
			int num = candidate.WinchDistance.CompareTo(current.WinchDistance);
			if (num != 0)
			{
				return num < 0;
			}
			num = candidate.RopeHolderDistance.CompareTo(current.RopeHolderDistance);
			if (num != 0)
			{
				return num < 0;
			}
			num = candidate.SourceOrder.CompareTo(current.SourceOrder);
			if (num == 0)
			{
				return candidate.SourceInstanceId < current.SourceInstanceId;
			}
			return num < 0;
		}

		private static LateenBraceWinchChoice FindAssociatedStaysailAngleWinch(Mast owningMast, Mast associatedOrdinaryMast, Mast[] boatMasts)
		{
			Transform boatRoot = LateenBraceWinchSelector.GetBoatRoot(owningMast);
			List<Mast> list = new List<Mast>();
			for (int i = 0; i < boatMasts.Length; i++)
			{
				Mast mast = boatMasts[i];
				if (LateenBraceWinchSelector.IsStaysailMast(mast) && !(LateenBraceWinchSelector.FindNearestOrdinaryMast(mast, boatMasts, boatRoot) != associatedOrdinaryMast))
				{
					list.Add(mast);
				}
			}
			list.Sort(delegate(Mast left, Mast right)
			{
				float horizontalDistanceSquared = LateenBraceWinchSelector.GetHorizontalDistanceSquared(left.transform.position, associatedOrdinaryMast.transform.position, boatRoot.up);
				float horizontalDistanceSquared2 = LateenBraceWinchSelector.GetHorizontalDistanceSquared(right.transform.position, associatedOrdinaryMast.transform.position, boatRoot.up);
				int num = horizontalDistanceSquared.CompareTo(horizontalDistanceSquared2);
				if (num == 0)
				{
					return LateenBraceWinchSelector.CompareMastIdentity(left, right);
				}
				return num;
			});
			for (int j = 0; j < list.Count; j++)
			{
				LateenBraceWinchChoice lateenBraceWinchChoice = LateenBraceWinchSelector.FindAnyAngleWinchOnMast(list[j]);
				if (lateenBraceWinchChoice != null)
				{
					return lateenBraceWinchChoice;
				}
			}
			return null;
		}

		private static LateenBraceWinchChoice FindOtherStaysailAngleWinch(Mast owningMast, Mast[] boatMasts)
		{
			Transform boatRoot = LateenBraceWinchSelector.GetBoatRoot(owningMast);
			List<Mast> list = new List<Mast>();
			for (int i = 0; i < boatMasts.Length; i++)
			{
				Mast mast = boatMasts[i];
				if (mast != null && mast != owningMast && LateenBraceWinchSelector.IsOrdinaryMast(mast))
				{
					list.Add(mast);
				}
			}
			list.Sort(delegate(Mast left, Mast right)
			{
				float leftForward = Vector3.Dot(left.transform.position - boatRoot.position, boatRoot.forward);
				float rightForward = Vector3.Dot(right.transform.position - boatRoot.position, boatRoot.forward);
				int num = rightForward.CompareTo(leftForward);
				if (num == 0)
				{
					return LateenBraceWinchSelector.CompareMastIdentity(left, right);
				}
				return num;
			});
			for (int j = 0; j < list.Count; j++)
			{
				LateenBraceWinchChoice lateenBraceWinchChoice = LateenBraceWinchSelector.FindAssociatedStaysailAngleWinch(owningMast, list[j], boatMasts);
				if (lateenBraceWinchChoice != null)
				{
					return lateenBraceWinchChoice;
				}
			}
			return null;
		}

		private static Mast FindNearestOrdinaryMast(Mast staysailMast, Mast[] boatMasts, Transform boatRoot)
		{
			Mast mast = null;
			float num = float.PositiveInfinity;
			for (int i = 0; i < boatMasts.Length; i++)
			{
				Mast mast2 = boatMasts[i];
				if (LateenBraceWinchSelector.IsOrdinaryMast(mast2))
				{
					float horizontalDistanceSquared = LateenBraceWinchSelector.GetHorizontalDistanceSquared(staysailMast.transform.position, mast2.transform.position, boatRoot.up);
					if (mast == null || horizontalDistanceSquared < num || (Mathf.Approximately(horizontalDistanceSquared, num) && LateenBraceWinchSelector.CompareMastIdentity(mast2, mast) < 0))
					{
						mast = mast2;
						num = horizontalDistanceSquared;
					}
				}
			}
			return mast;
		}

		private static float GetHorizontalDistanceSquared(Vector3 first, Vector3 second, Vector3 boatUp)
		{
			Vector3 offset = first - second;
			offset -= Vector3.Project(offset, boatUp);
			return offset.sqrMagnitude;
		}

		private static int CompareMastIdentity(Mast left, Mast right)
		{
			int num = left.orderIndex.CompareTo(right.orderIndex);
			if (num == 0)
			{
				return left.GetInstanceID().CompareTo(right.GetInstanceID());
			}
			return num;
		}

		private static LateenBraceWinchChoice FindAngleWinchOnMast(Mast mast, int index)
		{
			LateenBraceWinchChoice availableWinch = LateenBraceWinchSelector.GetAvailableWinch(mast, mast.leftAngleWinch, index);
			if (availableWinch != null)
			{
				return availableWinch;
			}
			availableWinch = LateenBraceWinchSelector.GetAvailableWinch(mast, mast.rightAngleWinch, index);
			return availableWinch ?? LateenBraceWinchSelector.GetAvailableWinch(mast, mast.midAngleWinch, index);
		}

		private static LateenBraceWinchChoice FindAnyAngleWinchOnMast(Mast mast)
		{
			int maximumLength = LateenBraceWinchSelector.GetMaximumLength(new GPButtonRopeWinch[][]
			{
				mast.leftAngleWinch,
				mast.rightAngleWinch,
				mast.midAngleWinch
			});
			for (int i = 0; i < maximumLength; i++)
			{
				LateenBraceWinchChoice availableWinch = LateenBraceWinchSelector.GetAvailableWinch(mast, mast.leftAngleWinch, i);
				if (availableWinch != null)
				{
					return availableWinch;
				}
				availableWinch = LateenBraceWinchSelector.GetAvailableWinch(mast, mast.rightAngleWinch, i);
				if (availableWinch != null)
				{
					return availableWinch;
				}
				availableWinch = LateenBraceWinchSelector.GetAvailableWinch(mast, mast.midAngleWinch, i);
				if (availableWinch != null)
				{
					return availableWinch;
				}
			}
			return null;
		}

		private static LateenBraceWinchChoice GetAvailableReefWinch(Mast mast, int index)
		{
			if (!(mast == null))
			{
				return LateenBraceWinchSelector.GetAvailableWinch(mast, mast.reefWinch, index);
			}
			return null;
		}

		private static LateenBraceWinchChoice GetAvailableWinch(Mast ropeHolderMast, GPButtonRopeWinch[] winches, int index)
		{
			GPButtonRopeWinch winch = LateenBraceWinchSelector.GetWinch(winches, index);
			Transform transform = LateenBraceWinchSelector.GetTransform(ropeHolderMast.mastReefAtt, index);
			if (winch == null || winch.rope != null || transform == null)
			{
				return null;
			}
			return new LateenBraceWinchChoice
			{
				Winch = winch,
				RopeHolder = transform
			};
		}

		private static bool IsOrdinaryMast(Mast mast)
		{
			return mast != null && !mast.onlyStaysails && !LateenBraceWinchSelector.IsBowsprit(mast);
		}

		private static bool IsStaysailMast(Mast mast)
		{
			return mast != null && mast.onlyStaysails && !LateenBraceWinchSelector.IsBowsprit(mast);
		}

		private static bool IsBowsprit(Mast mast)
		{
			if (LateenBraceWinchSelector.ContainsIgnoreCase(mast.gameObject.name, "bowsprit"))
			{
				return true;
			}
			if (mast.reefWinch == null)
			{
				return false;
			}
			for (int i = 0; i < mast.reefWinch.Length; i++)
			{
				GPButtonRopeWinch gpbuttonRopeWinch = mast.reefWinch[i];
				if (gpbuttonRopeWinch != null && LateenBraceWinchSelector.ContainsIgnoreCase(gpbuttonRopeWinch.gameObject.name, "bowsprit"))
				{
					return true;
				}
			}
			return false;
		}

		private static bool ContainsIgnoreCase(string value, string search)
		{
			return value != null && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static int GetSlotPriority(int index)
		{
			switch (index)
			{
			case 1:
				return 0;
			case 2:
				return 1;
			case 3:
				return 2;
			default:
				return 3;
			}
		}

		private static bool IsPreferredReefSlot(int index)
		{
			return index == 1 || index == 2 || index == 3;
		}

		private static int GetMaximumLength(params GPButtonRopeWinch[][] arrays)
		{
			int num = 0;
			for (int i = 0; i < arrays.Length; i++)
			{
				if (arrays[i] != null)
				{
					num = Mathf.Max(num, arrays[i].Length);
				}
			}
			return num;
		}

		private static GPButtonRopeWinch GetWinch(GPButtonRopeWinch[] winches, int index)
		{
			if (winches == null || index < 0 || index >= winches.Length)
			{
				return null;
			}
			return winches[index];
		}

		private static Transform GetTransform(Transform[] transforms, int index)
		{
			if (transforms == null || index < 0 || index >= transforms.Length)
			{
				return null;
			}
			return transforms[index];
		}

		private static Transform GetBoatRoot(Mast mast)
		{
			if (!(mast.shipRigidbody != null))
			{
				return mast.transform;
			}
			return mast.shipRigidbody.transform;
		}

		private static readonly int[] PreferredReefSlots = new int[]
		{
			1,
			2,
			3
		};

		private sealed class RankedReefChoice
		{
			internal LateenBraceWinchChoice Choice;

			internal int SlotPriority;

			internal float WinchDistance;

			internal float RopeHolderDistance;

			internal int SourceOrder;

			internal int SourceInstanceId;
		}
	}

	internal sealed class LateenControlBinding
	{
		internal LateenYardRig Rig;

		internal LateenControlRole Role;
	}

	internal static class LateenControlRegistry
	{
		internal static IEnumerable<LateenYardRig> ActiveRigs
		{
			get
			{
				return LateenControlRegistry.Rigs;
			}
		}

		internal static void Register(LateenYardRig rig)
		{
			if (rig == null)
			{
				return;
			}
			LateenControlRegistry.UnregisterBindings(rig);
			Sail component = rig.GetComponent<Sail>();
			if (component != null)
			{
				LateenControlRegistry.SailRigs[component] = rig;
			}
			if (!LateenControlRegistry.Rigs.Contains(rig))
			{
				LateenControlRegistry.Rigs.Add(rig);
			}
			LateenControlRegistry.Bind(rig.Brace, rig, LateenControlRole.Brace);
			LateenControlRegistry.Bind(rig.Halyard, rig, LateenControlRole.Halyard);
			LateenControlRegistry.Bind(rig.SheetLeft, rig, LateenControlRole.Sheet);
			LateenControlRegistry.Bind(rig.SheetMid, rig, LateenControlRole.Sheet);
			LateenControlRegistry.Bind(rig.SheetRight, rig, LateenControlRole.Sheet);
			LateenYardPersistence.ApplyLoadedSide(rig);
		}

		private static void Bind(RopeController controller, LateenYardRig rig, LateenControlRole role)
		{
			if (controller == null)
			{
				return;
			}
			LateenControlRegistry.Controls[controller] = new LateenControlBinding
			{
				Rig = rig,
				Role = role
			};
		}

		internal static bool TryGet(RopeController controller, out LateenControlBinding binding)
		{
			if (controller != null && LateenControlRegistry.Controls.TryGetValue(controller, out binding) && binding.Rig != null)
			{
				return true;
			}
			binding = null;
			return false;
		}

		internal static bool TryGetRig(Sail sail, out LateenYardRig rig)
		{
			if (sail != null && LateenControlRegistry.SailRigs.TryGetValue(sail, out rig) && rig != null)
			{
				return true;
			}
			rig = null;
			return false;
		}

		internal static void Unregister(LateenYardRig rig)
		{
			if (rig == null)
			{
				return;
			}
			LateenControlRegistry.UnregisterBindings(rig);
			LateenControlRegistry.Rigs.Remove(rig);
			Sail component = rig.GetComponent<Sail>();
			if (component != null)
			{
				LateenControlRegistry.SailRigs.Remove(component);
			}
		}

		private static void UnregisterBindings(LateenYardRig rig)
		{
			List<RopeController> list = new List<RopeController>();
			foreach (KeyValuePair<RopeController, LateenControlBinding> keyValuePair in LateenControlRegistry.Controls)
			{
				if (keyValuePair.Value.Rig == rig)
				{
					list.Add(keyValuePair.Key);
				}
			}
			for (int i = 0; i < list.Count; i++)
			{
				LateenControlRegistry.Controls.Remove(list[i]);
			}
		}

		internal static void DisposeAllRigs()
		{
			List<LateenYardRig> list = new List<LateenYardRig>(LateenControlRegistry.Rigs);
			for (int i = 0; i < list.Count; i++)
			{
				LateenYardRig lateenYardRig = list[i];
				if (lateenYardRig != null)
				{
					lateenYardRig.Dispose();
				}
			}
			LateenControlRegistry.Controls.Clear();
			LateenControlRegistry.SailRigs.Clear();
			LateenControlRegistry.Rigs.Clear();
		}

		private static readonly Dictionary<RopeController, LateenControlBinding> Controls = new Dictionary<RopeController, LateenControlBinding>();

		private static readonly Dictionary<Sail, LateenYardRig> SailRigs = new Dictionary<Sail, LateenYardRig>();

		private static readonly List<LateenYardRig> Rigs = new List<LateenYardRig>();
	}

	internal enum LateenControlRole
	{
		Brace,
		Halyard,
		Sheet
	}

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

	[HarmonyPatch(typeof(SaveLoadManager), "LoadGame")]
	internal static class LateenYardBeginLoadPatch
	{
		private static void Prefix()
		{
			LateenYardPersistence.BeginLoad();
		}
	}

	[HarmonyPatch(typeof(SaveLoadManager), "LoadModData")]
	internal static class LateenYardLoadPatch
	{
		private static void Postfix()
		{
			LateenYardPersistence.Load();
		}
	}

	internal static class LateenYardPersistence
	{
		internal static void BeginLoad()
		{
			LateenYardPersistence.LoadedSides.Clear();
			LateenYardPersistence.loaded = false;
		}

		internal static void Save()
		{
			if (GameState.modData == null)
			{
				GameState.modData = new Dictionary<string, string>();
			}
			List<string> list = new List<string>();
			foreach (LateenYardRig lateenYardRig in LateenControlRegistry.ActiveRigs)
			{
				if (!(lateenYardRig == null))
				{
					string persistenceKey = lateenYardRig.GetPersistenceKey();
					if (!string.IsNullOrEmpty(persistenceKey))
					{
						list.Add(persistenceKey + "," + lateenYardRig.YardSideSign.ToString());
					}
				}
			}
			GameState.modData[LateenYardPersistence.ModDataKey] = string.Join("|", list.ToArray());
		}

		internal static void Load()
		{
			LateenYardPersistence.LoadedSides.Clear();
			LateenYardPersistence.loaded = true;
			string text;
			if (GameState.modData == null || !GameState.modData.TryGetValue(LateenYardPersistence.ModDataKey, out text) || string.IsNullOrEmpty(text))
			{
				return;
			}
			LateenYardPersistence.ParseSides(text, LateenYardPersistence.LoadedSides);
			foreach (LateenYardRig rig in LateenControlRegistry.ActiveRigs)
			{
				LateenYardPersistence.ApplyLoadedSide(rig);
			}
		}

		// Reads "key,side|key,side" into sides, each side as 1 or -1; entries it can't read are skipped.
		internal static void ParseSides(string text, Dictionary<string, int> sides)
		{
			string[] array = text.Split(new char[]
			{
				'|'
			}, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				int num = array[i].LastIndexOf(',');
				if (num > 0 && num < array[i].Length - 1)
				{
					string key = array[i].Substring(0, num);
					int num2;
					if (int.TryParse(array[i].Substring(num + 1), out num2))
					{
						sides[key] = ((num2 >= 0) ? 1 : -1);
					}
				}
			}
		}

		// LoadedSides holds each place's latest side: read at load, then updated at every side change, so a rig registering
		// again, or rebuilt in its place (a shipyard rebuilds every sail on release), takes the side the yard last had.
		internal static void ApplyLoadedSide(LateenYardRig rig)
		{
			if (!LateenYardPersistence.loaded || rig == null)
			{
				return;
			}
			string persistenceKey = rig.GetPersistenceKey();
			int savedSide;
			if (!string.IsNullOrEmpty(persistenceKey) && LateenYardPersistence.LoadedSides.TryGetValue(persistenceKey, out savedSide) && savedSide != rig.YardSideSign)
			{
				rig.RestoreYardSide(savedSide);
			}
		}

		internal static void RememberSide(LateenYardRig rig)
		{
			string persistenceKey = rig.GetPersistenceKey();
			if (string.IsNullOrEmpty(persistenceKey))
			{
				return;
			}
			LateenYardPersistence.LoadedSides[persistenceKey] = rig.YardSideSign;
			LateenYardPersistence.loaded = true;
		}

		internal static void Reset()
		{
			LateenYardPersistence.LoadedSides.Clear();
			LateenYardPersistence.loaded = false;
		}

		private const string ModDataKey = Plugin.PluginGuid + ".lateen-yard-sides.v1";

		private static readonly Dictionary<string, int> LoadedSides = new Dictionary<string, int>();

		private static bool loaded;
	}

	internal sealed class LateenYardRig : MonoBehaviour
	{
		internal LateenBraceController Brace { get; private set; }

		internal RopeController Halyard
		{
			get
			{
				if (!(this.connections != null))
				{
					return null;
				}
				return this.connections.reefController;
			}
		}

		internal RopeController SheetLeft
		{
			get
			{
				if (!(this.connections != null))
				{
					return null;
				}
				return this.connections.angleControllerLeft;
			}
		}

		internal RopeController SheetMid
		{
			get
			{
				if (!(this.connections != null))
				{
					return null;
				}
				return this.connections.angleControllerMid;
			}
		}

		internal RopeController SheetRight
		{
			get
			{
				if (!(this.connections != null))
				{
					return null;
				}
				return this.connections.angleControllerRight;
			}
		}

		internal RopeController Sheet
		{
			get
			{
				if (this.SheetMid != null)
				{
					return this.SheetMid;
				}
				if (!(this.SheetLeft != null))
				{
					return this.SheetRight;
				}
				return this.SheetLeft;
			}
		}

		internal float BracePull
		{
			get
			{
				if (!(this.Brace != null))
				{
					return 0f;
				}
				return 1f - Mathf.Clamp01(this.Brace.currentLength);
			}
		}

		internal int YardSideSign
		{
			get
			{
				return this.yardSide;
			}
		}
		internal bool CanOperateBrace
		{
			get
			{
				return this.sail != null && this.sail.currentUnroll <= LateenYardRig.ControlEpsilon && this.AreAllSheetsFullyLoose();
			}
		}

		internal bool CanOperateHalyardOrSheet
		{
			get
			{
				return this.BracePull >= LateenYardRig.FullyRaisedThreshold;
			}
		}

		private bool AreAllSheetsFullyLoose()
		{
			return this.Sheet != null && LateenYardRig.IsSheetFullyLoose(this.SheetLeft) && LateenYardRig.IsSheetFullyLoose(this.SheetMid) && LateenYardRig.IsSheetFullyLoose(this.SheetRight);
		}

		private static bool IsSheetFullyLoose(RopeController controller)
		{
			return controller == null || controller.currentLength >= LateenYardRig.FullyRaisedThreshold;
		}

		internal bool Initialize(Mast owningMast, Sail attachedSail, SailConnections attachedConnections)
		{
			if (this.cleanedUp)
			{
				return false;
			}
			this.mast = owningMast;
			this.sail = attachedSail;
			this.connections = attachedConnections;
			this.hinge = base.GetComponent<HingeJoint>();
			if (this.initialized)
			{
				this.RefreshGeometryIfNeeded();
				this.RefreshRopeHolderPosition();
				LateenControlRegistry.Register(this);
				return true;
			}
			if (this.mast == null || this.sail == null || this.connections == null || this.hinge == null)
			{
				return false;
			}
			this.visualRoot = this.FindVisualRoot();
			this.yardMesh = LateenYardRig.FindYardMesh(this.visualRoot);
			if (this.visualRoot == null || this.yardMesh == null || this.yardMesh.sharedMesh == null)
			{
				ManualLogSource log = Plugin.Log;
				if (log != null && !this.warnedMissingYard)
				{
					this.warnedMissingYard = true;
					log.LogWarning("Could not locate the lateen visual root or yard on " + base.name + ".");
				}
				return false;
			}
			this.ConfigureAxes();
			this.ConfigureYardGeometry(true);
			this.CreateProceduralPivot();
			this.CreateBraceRoute();
			if (this.Brace == null)
			{
				ManualLogSource log2 = Plugin.Log;
				if (log2 != null)
				{
					log2.LogWarning("Could not create a brace controller for " + base.name + ".");
				}
				return false;
			}
			this.CaptureGeometrySignature();
			this.initialized = true;
			this.previousBracePull = Mathf.Clamp01(this.BracePull);
			this.tackChangeArmed = (this.previousBracePull >= LateenYardRig.TackRearmPullThreshold);
			LateenControlRegistry.Register(this);
			return true;
		}

		private void LateUpdate()
		{
			if (!this.initialized || this.Brace == null || this.proceduralPivot == null)
			{
				return;
			}
			this.RefreshGeometryIfNeeded();
			float bracePull = Mathf.Clamp01(this.BracePull);
			this.UpdateAutomaticSideTransfer(bracePull);
			// On its own side and not crossing, the yard's pose depends on the brace alone; elsewhere it follows the sail's swing.
			if (!this.poseHoldsUntilBraceMoves || this.sideTransferActive || this.yardSide != this.originalYardSide || bracePull != this.appliedBracePull)
			{
				this.ApplyProceduralPose(bracePull);
			}
			if (bracePull != this.ropeTensionPull)
			{
				this.UpdateRopeTension(bracePull);
			}
		}

		private void UpdateAutomaticSideTransfer(float bracePull)
		{
			bool flag = bracePull < this.previousBracePull - LateenYardRig.ControlEpsilon;
			bool flag2 = bracePull > this.previousBracePull + LateenYardRig.ControlEpsilon;
			if (!this.sideTransferActive && bracePull >= LateenYardRig.TackRearmPullThreshold)
			{
				this.tackChangeArmed = true;
			}
			if (this.tackChangeArmed && flag)
			{
				this.transferStartSide = this.yardSide;
				this.transferTargetSide = -this.yardSide;
				this.sideTransferActive = true;
				this.tackChangeArmed = false;
			}
			if (this.sideTransferActive && bracePull <= LateenYardRig.ControlEpsilon)
			{
				this.yardSide = this.transferTargetSide;
				this.sideTransferActive = false;
				LateenYardPersistence.RememberSide(this);
			}
			else if (this.sideTransferActive && flag2 && bracePull >= LateenYardRig.TackRearmPullThreshold)
			{
				this.sideTransferActive = false;
				this.tackChangeArmed = true;
			}
			this.previousBracePull = bracePull;
		}

		internal bool IsBadTack()
		{
			Rigidbody rigidbody = (this.sail != null) ? this.sail.shipRigidbody : null;
			if (rigidbody == null || this.sail.apparentWind.sqrMagnitude < LateenYardRig.WindMagnitudeEpsilon)
			{
				return false;
			}
			float num = Vector3.Dot(-this.sail.apparentWind.normalized, rigidbody.transform.right);
			return Mathf.Abs(num) >= LateenYardRig.WindSideDeadZone && ((num > 0f) ? 1 : -1) == this.yardSide;
		}

		internal string GetPersistenceKey()
		{
			SaveableObject componentInParent = base.GetComponentInParent<SaveableObject>();
			if (componentInParent == null || this.mast == null || this.sail == null)
			{
				return null;
			}
			return string.Format("{0}:{1}:{2}:{3}", new object[]
			{
				componentInParent.sceneIndex,
				this.mast.orderIndex,
				this.sail.mastOrder,
				this.sail.prefabIndex
			});
		}

		internal void RestoreYardSide(int savedSide)
		{
			this.yardSide = ((savedSide >= 0) ? 1 : -1);
			this.sideTransferActive = false;
			this.previousBracePull = Mathf.Clamp01(this.BracePull);
			this.tackChangeArmed = (this.previousBracePull >= LateenYardRig.TackRearmPullThreshold);
			if (this.initialized && this.proceduralPivot != null)
			{
				this.ApplyProceduralPose(this.previousBracePull);
			}
		}

		internal void Dispose()
		{
			this.CleanupGeneratedObjects(true);
			Object.Destroy(this);
		}

		// Reached with the sail's own destruction: Unity refuses a new parent that is being destroyed,
		// and the pivot, yard visuals and heel attachment go with the sail, so only the brace objects outside it are removed.
		private void OnDestroy()
		{
			this.CleanupGeneratedObjects(false);
		}

		private void CleanupGeneratedObjects(bool restoreHierarchy)
		{
			if (this.cleanedUp)
			{
				return;
			}
			this.cleanedUp = true;
			this.initialized = false;
			LateenControlRegistry.Unregister(this);
			if (restoreHierarchy)
			{
				this.RestoreVisualHierarchy();
			}
			this.DestroyRopeObjects();
			if (restoreHierarchy)
			{
				this.DestroyYardHeelAttachment();
				this.DestroyProceduralPivot();
			}
		}

		internal void SetBraceRopeHolder(Transform ropeHolder)
		{
			this.braceRopeHolder = ropeHolder;
			if (this.braceWinchRope != null)
			{
				this.braceWinchRope.attachment = this.GetBraceRopeHolder();
			}
			this.RefreshRopeHolderPosition();
		}

		private void UpdateYardHeelAttachment()
		{
			if (this.yardHeelAttachment == null)
			{
				GameObject gameObject = new GameObject("lateen yard heel attachment");
				this.yardHeelAttachment = gameObject.transform;
			}
			if (this.yardHeelAttachment.parent != this.yardMesh.transform)
			{
				this.yardHeelAttachment.SetParent(this.yardMesh.transform, false);
			}
			this.yardHeelAttachment.position = base.transform.TransformPoint(this.heelPositionLocal);
		}

		private void CreateBraceRoute()
		{
			Transform midRopeAttachment = this.connections.midRopeAttachment;
			RopeEffect source = (this.Sheet != null) ? this.Sheet.GetComponent<RopeEffect>() : null;
			RopeEffect source2 = (midRopeAttachment != null) ? midRopeAttachment.GetComponent<RopeEffect>() : null;
			GameObject gameObject = new GameObject("lateen brace rope holder route");
			gameObject.SetActive(false);
			this.braceRopeHolderRoute = gameObject.transform;
			this.RefreshRopeHolderPosition();
			RopeEffect ropeEffect = this.CreateRopeEffect(gameObject, source2, this.yardHeelAttachment, true);
			if (ropeEffect != null)
			{
				this.braceRopes.Add(ropeEffect);
			}
			gameObject.SetActive(true);
			GameObject gameObject2 = new GameObject(base.name + " brace controller");
			gameObject2.SetActive(false);
			RopeEffect ropeEffect2 = this.CreateRopeEffect(gameObject2, source, this.GetBraceRopeHolder(), false);
			if (ropeEffect2 != null)
			{
				this.braceRopes.Add(ropeEffect2);
				this.braceWinchRope = ropeEffect2;
			}
			this.Brace = gameObject2.AddComponent<LateenBraceController>();
			this.Brace.Initialize(this);
			gameObject2.SetActive(true);
		}

		private RopeEffect CreateRopeEffect(GameObject owner, RopeEffect source, Transform attachment, bool sheetFallback)
		{
			if (source == null || attachment == null)
			{
				return null;
			}
			LineRenderer component = source.GetComponent<LineRenderer>();
			if (component == null)
			{
				return null;
			}
			LineRenderer destination = owner.AddComponent<LineRenderer>();
			LateenYardRig.CopyLineRenderer(component, destination);
			RopeEffect ropeEffect = owner.AddComponent<RopeEffect>();
			ropeEffect.attachment = attachment;
			ropeEffect.sheet = (source.sheet || sheetFallback);
			ropeEffect.jibSheet = false;
			ropeEffect.currentRopeLength = LateenYardRig.LooseRopeCurveLength;
			ropeEffect.ropeWidth = source.ropeWidth;
			ropeEffect.totalRopeLength = LateenYardRig.LooseRopeTension;
			ropeEffect.curveRate = source.curveRate;
			ropeEffect.attachedSailArea = this.sail.sailArea;
			ropeEffect.allRopeSections = new List<Vector3>();
			return ropeEffect;
		}

		private static void CopyLineRenderer(LineRenderer source, LineRenderer destination)
		{
			destination.sharedMaterial = source.sharedMaterial;
			destination.useWorldSpace = source.useWorldSpace;
			destination.startWidth = source.startWidth;
			destination.endWidth = source.endWidth;
			destination.startColor = source.startColor;
			destination.endColor = source.endColor;
			destination.positionCount = source.positionCount;
			destination.enabled = source.enabled;
		}

		private Transform GetBraceRopeHolder()
		{
			return this.braceRopeHolder;
		}

		private void RefreshRopeHolderPosition()
		{
			if (this.braceRopeHolderRoute == null)
			{
				return;
			}
			Transform transform = this.GetBraceRopeHolder();
			Transform parent = (transform != null && transform.parent != null) ? transform.parent : this.mast.transform;
			this.braceRopeHolderRoute.SetParent(parent, true);
			if (transform == null)
			{
				return;
			}
			this.braceRopeHolderRoute.position = transform.position;
			this.braceRopeHolderRoute.rotation = transform.rotation;
			if (this.braceWinchRope != null)
			{
				this.braceWinchRope.attachment = transform;
			}
		}

		private void UpdateRopeTension(float bracePull)
		{
			float currentRopeLength = Mathf.Lerp(LateenYardRig.LooseRopeCurveLength, LateenYardRig.TightRopeCurveLength, bracePull);
			float totalRopeLength = Mathf.Lerp(LateenYardRig.LooseRopeTension, LateenYardRig.TightRopeTension, bracePull);
			for (int i = 0; i < this.braceRopes.Count; i++)
			{
				RopeEffect ropeEffect = this.braceRopes[i];
				if (!(ropeEffect == null))
				{
					ropeEffect.currentRopeLength = currentRopeLength;
					ropeEffect.totalRopeLength = totalRopeLength;
				}
			}
			this.ropeTensionPull = bracePull;
		}

		private void DestroyRopeObjects()
		{
			if (this.Brace != null)
			{
				Object.Destroy(this.Brace.gameObject);
				this.Brace = null;
			}
			if (this.braceRopeHolderRoute != null)
			{
				Object.Destroy(this.braceRopeHolderRoute.gameObject);
				this.braceRopeHolderRoute = null;
			}
			this.braceWinchRope = null;
			this.braceRopeHolder = null;
			this.braceRopes.Clear();
		}

		private void DestroyYardHeelAttachment()
		{
			if (this.yardHeelAttachment != null)
			{
				Object.Destroy(this.yardHeelAttachment.gameObject);
				this.yardHeelAttachment = null;
			}
		}

		private void ConfigureAxes()
		{
			Rigidbody rigidbody = (this.sail.shipRigidbody != null) ? this.sail.shipRigidbody : this.mast.shipRigidbody;
			if (rigidbody != null)
			{
				this.starboardAxisLocal = base.transform.InverseTransformDirection(rigidbody.transform.right).normalized;
				this.aftAxisLocal = base.transform.InverseTransformDirection(-rigidbody.transform.forward).normalized;
				return;
			}
			this.starboardAxisLocal = Vector3.up;
			this.aftAxisLocal = -Vector3.right;
		}

		private void ConfigureYardGeometry(bool initializeSide)
		{
			this.InvalidateClearanceCache();
			Vector3 vector = (this.hinge.axis.sqrMagnitude > LateenYardRig.AxisMagnitudeEpsilon) ? this.hinge.axis.normalized : Vector3.forward;
			Vector3 normalized = base.transform.TransformDirection(vector).normalized;
			this.mastPivotLocal = this.hinge.anchor;
			Vector3 vector2;
			this.AnalyzeYardGeometry(vector, out vector2);
			if (Vector3.Dot(vector2, vector) < 0f)
			{
				vector2 = -vector2;
			}
			this.verticalRotation = Quaternion.FromToRotation(vector2, vector);
			Vector3 contactWorld = base.transform.TransformPoint(this.yardContactCenterLocal);
			Vector3 position = this.FindMastCenterlinePoint(contactWorld, normalized, out this.mastContactCollider);
			this.mastCenterLocal = base.transform.InverseTransformPoint(position);
			Vector3 lhs = this.yardContactCenterLocal - this.mastCenterLocal;
			this.vanillaRingSide = Vector3.Dot(lhs, this.starboardAxisLocal);
			this.vanillaRingAft = Vector3.Dot(lhs, this.aftAxisLocal);
			this.hasSurfaceMeasurement = (this.mastContactCollider != null && this.yardContactSurfacePointsLocal.Length != 0);
			this.vanillaSurfaceClearance = (this.hasSurfaceMeasurement ? this.MeasureSurfaceClearance(Quaternion.identity, Vector2.zero) : 0f);
			this.originalYardSide = ((this.vanillaRingSide >= 0f) ? 1 : -1);
			if (initializeSide)
			{
				this.yardSide = this.originalYardSide;
			}
			this.UpdateYardHeelAttachment();
		}

		private void RefreshGeometryIfNeeded()
		{
			if (!this.YardGeometryChanged())
			{
				return;
			}
			MeshFilter meshFilter = LateenYardRig.FindYardMesh(this.visualRoot);
			if (meshFilter == null || meshFilter.sharedMesh == null)
			{
				return;
			}
			this.yardMesh = meshFilter;
			this.proceduralPivot.localPosition = this.hinge.anchor;
			this.proceduralPivot.localRotation = Quaternion.identity;
			this.ConfigureAxes();
			this.ConfigureYardGeometry(false);
			this.CaptureGeometrySignature();
			this.ApplyProceduralPose(Mathf.Clamp01(this.BracePull));
		}

		private bool YardGeometryChanged()
		{
			if (this.yardMesh == null)
			{
				return true;
			}
			Mesh sharedMesh = this.yardMesh.sharedMesh;
			return sharedMesh == null || sharedMesh != this.measuredYardMesh || (this.yardMesh.transform.lossyScale - this.measuredYardScale).sqrMagnitude > LateenYardRig.GeometryChangeEpsilon || !Mathf.Approximately(this.sail.GetCurrentInstallHeight(), this.measuredInstallHeight);
		}

		private void CaptureGeometrySignature()
		{
			this.measuredYardMesh = this.yardMesh.sharedMesh;
			this.measuredYardScale = this.yardMesh.transform.lossyScale;
			// Sail.installHeight is the prefab's fixed default; the height the sail sits at is the current one.
			this.measuredInstallHeight = this.sail.GetCurrentInstallHeight();
		}

		private void CreateProceduralPivot()
		{
			this.originalVisualParent = this.visualRoot.parent;
			this.originalVisualSiblingIndex = this.visualRoot.GetSiblingIndex();
			this.hasOriginalVisualParent = true;
			GameObject gameObject = new GameObject("lateen brace pivot");
			this.proceduralPivot = gameObject.transform;
			this.proceduralPivot.SetParent(base.transform, false);
			this.proceduralPivot.localPosition = this.mastPivotLocal;
			this.proceduralPivot.localRotation = Quaternion.identity;
			this.proceduralPivot.localScale = Vector3.one;
			this.visualRoot.SetParent(this.proceduralPivot, true);
		}

		private void RestoreVisualHierarchy()
		{
			if (!this.hasOriginalVisualParent || this.visualRoot == null)
			{
				return;
			}
			if (this.proceduralPivot != null)
			{
				this.proceduralPivot.localPosition = this.mastPivotLocal;
				this.proceduralPivot.localRotation = Quaternion.identity;
				this.proceduralPivot.localScale = Vector3.one;
			}
			Transform transform = (this.originalVisualParent != null) ? this.originalVisualParent : base.transform;
			if (transform != null)
			{
				this.visualRoot.SetParent(transform, true);
				this.visualRoot.SetSiblingIndex(Mathf.Clamp(this.originalVisualSiblingIndex, 0, transform.childCount - 1));
			}
			else if (this.visualRoot.parent == this.proceduralPivot)
			{
				this.visualRoot.SetParent(null, true);
			}
			this.hasOriginalVisualParent = false;
		}

		private void DestroyProceduralPivot()
		{
			if (this.proceduralPivot != null)
			{
				Object.Destroy(this.proceduralPivot.gameObject);
				this.proceduralPivot = null;
			}
		}

		private void ApplyProceduralPose(float bracePull)
		{
			float num = 1f - bracePull;
			float t = Mathf.SmoothStep(0f, 1f, num);
			Quaternion quaternion = Quaternion.Slerp(Quaternion.identity, this.verticalRotation, t);
			this.proceduralPivot.localRotation = quaternion;
			Vector2 currentRingPosition = this.GetCurrentRingPosition(quaternion);
			float num2 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LateenYardRig.MastPassMovementStart, 1f, num));
			int side = this.sideTransferActive ? this.transferStartSide : this.yardSide;
			int side2 = this.sideTransferActive ? this.transferTargetSide : this.yardSide;
			Vector2 ringPosition3;
			if (this.sideTransferActive)
			{
				Vector2 ringPosition = this.GetRingPosition(side, quaternion, currentRingPosition);
				Vector2 ringPosition2 = this.GetRingPosition(side2, quaternion, currentRingPosition);
				float t2 = 0.5f - 0.5f * Mathf.Cos(num2 * Mathf.PI);
				float a = Mathf.Atan2(ringPosition.x, ringPosition.y) * Mathf.Rad2Deg;
				float b = Mathf.Atan2(ringPosition2.x, ringPosition2.y) * Mathf.Rad2Deg;
				float f = Mathf.LerpAngle(a, b, t2) * Mathf.Deg2Rad;
				float num3 = Mathf.Lerp(ringPosition.magnitude, ringPosition2.magnitude, t2);
				ringPosition3 = new Vector2(Mathf.Sin(f) * num3, Mathf.Cos(f) * num3);
			}
			else
			{
				ringPosition3 = this.GetRingPosition(side2, quaternion, currentRingPosition);
			}
			float d = ringPosition3.x - currentRingPosition.x;
			float d2 = ringPosition3.y - currentRingPosition.y;
			this.proceduralPivot.localPosition = this.mastPivotLocal + this.starboardAxisLocal * d + this.aftAxisLocal * d2;
			this.appliedBracePull = bracePull;
			this.poseHoldsUntilBraceMoves = (!this.sideTransferActive && this.yardSide == this.originalYardSide);
		}

		private Vector2 GetCurrentRingPosition(Quaternion rotation)
		{
			Vector3 nativeRingOffset = this.GetNativeRingOffset(rotation);
			return new Vector2(Vector3.Dot(nativeRingOffset, this.starboardAxisLocal), Vector3.Dot(nativeRingOffset, this.aftAxisLocal));
		}

		private Vector2 GetRingPosition(int side, Quaternion rotation, Vector2 currentRingPosition)
		{
			if (side == this.originalYardSide)
			{
				return currentRingPosition;
			}
			Vector2 mirroredPosition = new Vector2((float)side * Mathf.Abs(this.vanillaRingSide), this.vanillaRingAft);
			return this.MatchVanillaSurfaceClearance(rotation, currentRingPosition, mirroredPosition);
		}

		private Vector2 MatchVanillaSurfaceClearance(Quaternion rotation, Vector2 currentRingPosition, Vector2 mirroredPosition)
		{
			if (!this.hasSurfaceMeasurement)
			{
				return LateenYardRig.AddClearanceCompensation(mirroredPosition);
			}
			LateenYardRig.ClearancePoseSnapshot other = this.CaptureClearancePose(rotation, currentRingPosition, mirroredPosition);
			if (this.hasCachedClearancePosition && this.cachedClearancePose.ApproximatelyEquals(other))
			{
				return this.cachedClearancePosition;
			}
			Vector2 result = this.CalculateSurfaceClearancePosition(rotation, currentRingPosition, mirroredPosition);
			this.cachedClearancePose = other;
			this.cachedClearancePosition = result;
			this.hasCachedClearancePosition = true;
			return result;
		}

		private LateenYardRig.ClearancePoseSnapshot CaptureClearancePose(Quaternion rotation, Vector2 currentRingPosition, Vector2 mirroredPosition)
		{
			Transform transform = this.mastContactCollider.transform;
			return new LateenYardRig.ClearancePoseSnapshot(rotation, currentRingPosition, mirroredPosition, transform.worldToLocalMatrix * base.transform.localToWorldMatrix, transform.lossyScale, this.mastContactCollider.center, this.mastContactCollider.radius, this.mastContactCollider.height, this.mastContactCollider.direction);
		}

		private static bool MatrixApproximatelyEquals(Matrix4x4 first, Matrix4x4 second)
		{
			for (int i = 0; i < 16; i++)
			{
				if (Mathf.Abs(first[i] - second[i]) > LateenYardRig.ClearanceCachePoseEpsilon)
				{
					return false;
				}
			}
			return true;
		}

		private void InvalidateClearanceCache()
		{
			this.hasCachedClearancePosition = false;
		}

		private Vector2 CalculateSurfaceClearancePosition(Quaternion rotation, Vector2 currentRingPosition, Vector2 mirroredPosition)
		{
			if (!this.PrepareClearanceProbe(rotation))
			{
				return LateenYardRig.AddClearanceCompensation(mirroredPosition);
			}
			// The sail sways a little on its hinge, which misses the pose cache nearly every frame; the last result is kept
			// while it still meets the clearance within the tolerance the search itself accepts.
			if (this.hasCachedClearancePosition && this.cachedClearancePose.MirroredRingPosition == mirroredPosition && Mathf.Abs(this.GetSurfaceClearanceError(currentRingPosition, this.cachedClearancePosition)) <= LateenYardRig.SurfaceClearanceTolerance)
			{
				return this.cachedClearancePosition;
			}
			Vector2 a =(mirroredPosition.sqrMagnitude > 0.0001f) ? mirroredPosition.normalized : new Vector2((mirroredPosition.x >= 0f) ? 1f : -1f, 0f);
			float surfaceClearanceError = this.GetSurfaceClearanceError(currentRingPosition, mirroredPosition);
			if (Mathf.Abs(surfaceClearanceError) <= LateenYardRig.SurfaceClearanceTolerance)
			{
				return mirroredPosition;
			}
			float num;
			float num2;
			if (surfaceClearanceError < 0f)
			{
				num = 0f;
				for (num2 = LateenYardRig.SurfaceSearchStep; num2 < LateenYardRig.SurfaceSearchLimit; num2 = Mathf.Min(num2 * 2f, LateenYardRig.SurfaceSearchLimit))
				{
					if (this.GetSurfaceClearanceError(currentRingPosition, mirroredPosition + a * num2) >= 0f)
					{
						break;
					}
				}
			}
			else
			{
				num2 = 0f;
				float num3 = Mathf.Max(0f, mirroredPosition.magnitude - LateenYardRig.SurfaceSearchMinimumRadius);
				num = -Mathf.Min(LateenYardRig.SurfaceSearchStep, num3);
				while (-num < num3 && this.GetSurfaceClearanceError(currentRingPosition, mirroredPosition + a * num) > 0f)
				{
					num = -Mathf.Min(-num * 2f, num3);
				}
				if (this.GetSurfaceClearanceError(currentRingPosition, mirroredPosition + a * num) > 0f)
				{
					return mirroredPosition;
				}
			}
			for (int i = 0; i < LateenYardRig.SurfaceSearchIterations; i++)
			{
				float num4 = (num + num2) * 0.5f;
				Vector2 desiredRingPosition = mirroredPosition + a * num4;
				if (this.GetSurfaceClearanceError(currentRingPosition, desiredRingPosition) < 0f)
				{
					num = num4;
				}
				else
				{
					num2 = num4;
				}
			}
			return mirroredPosition + a * ((num + num2) * 0.5f);
		}

		private float GetSurfaceClearanceError(Vector2 currentRingPosition, Vector2 desiredRingPosition)
		{
			float num = this.vanillaSurfaceClearance + LateenYardRig.NonVanillaClearanceCompensation;
			return this.MeasurePreparedClearance(desiredRingPosition - currentRingPosition) - num;
		}

		private static Vector2 AddClearanceCompensation(Vector2 mirroredPosition)
		{
			if (mirroredPosition.sqrMagnitude <= 0.0001f)
			{
				return mirroredPosition;
			}
			return mirroredPosition + mirroredPosition.normalized * LateenYardRig.NonVanillaClearanceCompensation;
		}

		private float MeasureSurfaceClearance(Quaternion rotation, Vector2 ringTranslation)
		{
			if (!this.PrepareClearanceProbe(rotation))
			{
				return 0f;
			}
			return this.MeasurePreparedClearance(ringTranslation);
		}

		// Puts the yard's contact points, turned by the rotation, and the mast capsule in world space once,
		// so each measure of a search is plain arithmetic instead of engine calls per point.
		private bool PrepareClearanceProbe(Quaternion rotation)
		{
			int count = this.yardContactSurfacePointsLocal.Length;
			if (this.mastContactCollider == null || count == 0)
			{
				return false;
			}
			if (this.probeWorldPoints.Length != count)
			{
				this.probeWorldPoints = new Vector3[count];
			}
			Matrix4x4 localToWorldMatrix = base.transform.localToWorldMatrix;
			for (int i = 0; i < count; i++)
			{
				this.probeWorldPoints[i] = localToWorldMatrix.MultiplyPoint3x4(this.mastPivotLocal + rotation * (this.yardContactSurfacePointsLocal[i] - this.mastPivotLocal));
			}
			this.probeStarboardWorld = localToWorldMatrix.MultiplyVector(this.starboardAxisLocal);
			this.probeAftWorld = localToWorldMatrix.MultiplyVector(this.aftAxisLocal);
			this.probeCapsule = LateenYardRig.CreateCapsuleWorldGeometry(this.mastContactCollider);
			return true;
		}

		private float MeasurePreparedClearance(Vector2 ringTranslation)
		{
			Vector3 b = this.probeStarboardWorld * ringTranslation.x + this.probeAftWorld * ringTranslation.y;
			float num = float.PositiveInfinity;
			for (int i = 0; i < this.probeWorldPoints.Length; i++)
			{
				Vector3 vector = this.probeWorldPoints[i] + b;
				Vector3 centerlinePoint = this.probeCapsule.GetCenterlinePoint(vector);
				num = Mathf.Min(num, (vector - centerlinePoint).magnitude - this.probeCapsule.Radius);
			}
			return num;
		}

		private Vector3 GetNativeRingOffset(Quaternion rotation)
		{
			return this.mastPivotLocal + rotation * (this.yardContactCenterLocal - this.mastPivotLocal) - this.mastCenterLocal;
		}

		private Transform FindVisualRoot()
		{
			if (this.sail == null || this.sail.cloth == null)
			{
				return null;
			}
			Transform parent = this.sail.cloth.transform.parent;
			if (!(parent != null) || !(parent != base.transform))
			{
				return null;
			}
			return parent;
		}

		private static MeshFilter FindYardMesh(Transform root)
		{
			if (root == null)
			{
				return null;
			}
			MeshFilter result = null;
			float num = 0f;
			foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
			{
				if (!(meshFilter.sharedMesh == null))
				{
					string text = meshFilter.name.ToLowerInvariant();
					if (text.Contains("boom") || text.Contains("cylinder"))
					{
						Vector3 vector = Vector3.Scale(meshFilter.sharedMesh.bounds.size, LateenYardRig.Abs(meshFilter.transform.lossyScale));
						float num2 = Mathf.Max(new float[]
						{
							vector.x,
							vector.y,
							vector.z
						});
						if (num2 > num)
						{
							result = meshFilter;
							num = num2;
						}
					}
				}
			}
			return result;
		}

		private Vector3 FindMastCenterlinePoint(Vector3 contactWorld, Vector3 mastAxisWorld, out CapsuleCollider selectedCollider)
		{
			selectedCollider = null;
			Vector3 vector = (this.mast != null) ? this.mast.transform.position : base.transform.position;
			Vector3 result = vector + mastAxisWorld * Vector3.Dot(contactWorld - vector, mastAxisWorld);
			if (this.mast.mastCols == null)
			{
				return result;
			}
			float num = float.PositiveInfinity;
			for (int i = 0; i < this.mast.mastCols.Length; i++)
			{
				CapsuleCollider capsuleCollider = this.mast.mastCols[i];
				if (!(capsuleCollider == null))
				{
					Vector3 centerlinePoint = LateenYardRig.CreateCapsuleWorldGeometry(capsuleCollider).GetCenterlinePoint(contactWorld);
					float sqrMagnitude = (centerlinePoint - contactWorld).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						result = centerlinePoint;
						selectedCollider = capsuleCollider;
						num = sqrMagnitude;
					}
				}
			}
			return result;
		}

		private static LateenYardRig.CapsuleWorldGeometry CreateCapsuleWorldGeometry(CapsuleCollider collider)
		{
			int direction = collider.direction;
			Vector3 vector;
			if (direction != 0)
			{
				if (direction != 2)
				{
					vector = Vector3.up;
				}
				else
				{
					vector = Vector3.forward;
				}
			}
			else
			{
				vector = Vector3.right;
			}
			Vector3 direction2 = vector;
			Vector3 normalized = collider.transform.TransformDirection(direction2).normalized;
			Vector3 center = collider.transform.TransformPoint(collider.center);
			Vector3 vector2 = LateenYardRig.Abs(collider.transform.lossyScale);
			direction = collider.direction;
			float num;
			if (direction != 0)
			{
				if (direction != 2)
				{
					num = vector2.y;
				}
				else
				{
					num = vector2.z;
				}
			}
			else
			{
				num = vector2.x;
			}
			float num2 = num;
			float halfSegment = Mathf.Max(0f, collider.height * 0.5f - collider.radius) * num2;
			direction = collider.direction;
			if (direction != 0)
			{
				if (direction != 2)
				{
					num = Mathf.Max(vector2.x, vector2.z);
				}
				else
				{
					num = Mathf.Max(vector2.x, vector2.y);
				}
			}
			else
			{
				num = Mathf.Max(vector2.y, vector2.z);
			}
			float num3 = num;
			return new LateenYardRig.CapsuleWorldGeometry(center, normalized, halfSegment, collider.radius * num3);
		}

		private void AnalyzeYardGeometry(Vector3 mastAxisLocal, out Vector3 yardAxisLocal)
		{
			Mesh sharedMesh = this.yardMesh.sharedMesh;
			Vector3[] vertices = sharedMesh.vertices;
			Vector3[] array = new Vector3[vertices.Length];
			for (int i = 0; i < vertices.Length; i++)
			{
				Vector3 position = this.yardMesh.transform.TransformPoint(vertices[i]);
				array[i] = base.transform.InverseTransformPoint(position);
			}
			Vector3 vector = LateenYardRig.Average(array);
			yardAxisLocal = LateenYardRig.FindPrincipalAxis(array, vector);
			float num = float.PositiveInfinity;
			float num2 = float.NegativeInfinity;
			for (int j = 0; j < array.Length; j++)
			{
				float b = Vector3.Dot(array[j] - vector, yardAxisLocal);
				num = Mathf.Min(num, b);
				num2 = Mathf.Max(num2, b);
			}
			Vector3 vector2 = LateenYardRig.AverageTip(array, vector, yardAxisLocal, num, num2, true);
			Vector3 vector3 = LateenYardRig.AverageTip(array, vector, yardAxisLocal, num, num2, false);
			this.heelPositionLocal = ((Vector3.Dot(vector2, mastAxisLocal) <= Vector3.Dot(vector3, mastAxisLocal)) ? vector2 : vector3);
			float value = Vector3.Dot(this.mastPivotLocal - vector, yardAxisLocal);
			this.yardContactCenterLocal = vector + yardAxisLocal * Mathf.Clamp(value, num, num2);
			this.yardContactSurfacePointsLocal = LateenYardRig.SliceMeshAtPlane(array, sharedMesh.triangles, this.yardContactCenterLocal, yardAxisLocal);
		}

		private static Vector3[] SliceMeshAtPlane(Vector3[] points, int[] triangles, Vector3 planePoint, Vector3 planeNormal)
		{
			List<Vector3> list = new List<Vector3>();
			int num = 0;
			while (num + 2 < triangles.Length)
			{
				Vector3 vector = points[triangles[num]];
				Vector3 vector2 = points[triangles[num + 1]];
				Vector3 vector3 = points[triangles[num + 2]];
				LateenYardRig.AddPlaneEdgeIntersections(list, vector, vector2, planePoint, planeNormal);
				LateenYardRig.AddPlaneEdgeIntersections(list, vector2, vector3, planePoint, planeNormal);
				LateenYardRig.AddPlaneEdgeIntersections(list, vector3, vector, planePoint, planeNormal);
				num += 3;
			}
			if (list.Count == 0)
			{
				float num2 = float.PositiveInfinity;
				for (int i = 0; i < points.Length; i++)
				{
					num2 = Mathf.Min(num2, Mathf.Abs(Vector3.Dot(points[i] - planePoint, planeNormal)));
				}
				for (int j = 0; j < points.Length; j++)
				{
					if (Mathf.Abs(Vector3.Dot(points[j] - planePoint, planeNormal)) <= num2 + LateenYardRig.PlaneIntersectionEpsilon)
					{
						LateenYardRig.AddUniquePoint(list, points[j]);
					}
				}
			}
			return list.ToArray();
		}

		private static void AddPlaneEdgeIntersections(List<Vector3> intersections, Vector3 first, Vector3 second, Vector3 planePoint, Vector3 planeNormal)
		{
			float num = Vector3.Dot(first - planePoint, planeNormal);
			float num2 = Vector3.Dot(second - planePoint, planeNormal);
			if (Mathf.Abs(num) <= LateenYardRig.PlaneIntersectionEpsilon)
			{
				LateenYardRig.AddUniquePoint(intersections, first);
			}
			if (Mathf.Abs(num2) <= LateenYardRig.PlaneIntersectionEpsilon)
			{
				LateenYardRig.AddUniquePoint(intersections, second);
			}
			if ((num >= -LateenYardRig.PlaneIntersectionEpsilon || num2 <= LateenYardRig.PlaneIntersectionEpsilon) && (num <= LateenYardRig.PlaneIntersectionEpsilon || num2 >= -LateenYardRig.PlaneIntersectionEpsilon))
			{
				return;
			}
			float t = num / (num - num2);
			LateenYardRig.AddUniquePoint(intersections, Vector3.Lerp(first, second, t));
		}

		private static void AddUniquePoint(List<Vector3> points, Vector3 candidate)
		{
			float num = LateenYardRig.PlaneIntersectionEpsilon * LateenYardRig.PlaneIntersectionEpsilon;
			for (int i = 0; i < points.Count; i++)
			{
				if ((points[i] - candidate).sqrMagnitude <= num)
				{
					return;
				}
			}
			points.Add(candidate);
		}

		private static Vector3 Average(Vector3[] points)
		{
			Vector3 a = Vector3.zero;
			for (int i = 0; i < points.Length; i++)
			{
				a += points[i];
			}
			return a / (float)points.Length;
		}

		private static Vector3 FindPrincipalAxis(Vector3[] points, Vector3 center)
		{
			float num = 0f;
			float num2 = 0f;
			float num3 = 0f;
			float num4 = 0f;
			float num5 = 0f;
			float num6 = 0f;
			for (int i = 0; i < points.Length; i++)
			{
				Vector3 vector = points[i] - center;
				num += vector.x * vector.x;
				num2 += vector.x * vector.y;
				num3 += vector.x * vector.z;
				num4 += vector.y * vector.y;
				num5 += vector.y * vector.z;
				num6 += vector.z * vector.z;
			}
			Vector3 vector2 = (num >= num4 && num >= num6) ? Vector3.right : ((num4 >= num6) ? Vector3.up : Vector3.forward);
			for (int j = 0; j < LateenYardRig.PrincipalAxisIterations; j++)
			{
				Vector3 vector3 = new Vector3(num * vector2.x + num2 * vector2.y + num3 * vector2.z, num2 * vector2.x + num4 * vector2.y + num5 * vector2.z, num3 * vector2.x + num5 * vector2.y + num6 * vector2.z);
				if (vector3.sqrMagnitude <= 1E-06f)
				{
					break;
				}
				vector2 = vector3.normalized;
			}
			return vector2;
		}

		private static Vector3 AverageTip(Vector3[] points, Vector3 center, Vector3 axis, float minimum, float maximum, bool first)
		{
			float num = first ? minimum : maximum;
			float num2 = Mathf.Max((maximum - minimum) * LateenYardRig.TipSampleFraction, LateenYardRig.MinimumTipTolerance);
			Vector3 a = Vector3.zero;
			int num3 = 0;
			for (int i = 0; i < points.Length; i++)
			{
				float num4 = Vector3.Dot(points[i] - center, axis);
				if (first ? (num4 <= minimum + num2) : (num4 >= maximum - num2))
				{
					a += points[i];
					num3++;
				}
			}
			Vector3 a2 = (num3 > 0) ? (a / (float)num3) : (center + axis * num);
			float num5 = Vector3.Dot(a2 - center, axis);
			return a2 + axis * (num - num5);
		}

		private static Vector3 Abs(Vector3 value)
		{
			return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
		}

		internal const float ControlEpsilon = 0.0001f;

		internal const float FullyRaisedThreshold = 0.9999f;

		internal const float BadTackPowerMultiplier = 0.9f;

		private const float TackRearmPullThreshold = 0.7f;

		private const float WindMagnitudeEpsilon = 0.001f;

		private const float WindSideDeadZone = 0.05f;

		private Sail sail;

		private Mast mast;

		private SailConnections connections;

		private HingeJoint hinge;

		private int originalYardSide;

		private int yardSide;

		private float previousBracePull;

		private int transferStartSide;

		private int transferTargetSide;

		private bool sideTransferActive;

		private bool tackChangeArmed;

		private bool initialized;

		private bool cleanedUp;

		private bool warnedMissingYard;

		// The brace pull the pivot's pose was last set for, and whether that pose stays valid until the brace moves.
		private float appliedBracePull;

		private bool poseHoldsUntilBraceMoves;

		// The brace pull the ropes' lengths were last set for.
		private float ropeTensionPull = float.NaN;

		private const float LooseRopeTension = 0.86f;

		private const float TightRopeTension = 0.98f;

		private const float LooseRopeCurveLength = 0.45f;

		private const float TightRopeCurveLength = 0.18f;

		private Transform yardHeelAttachment;

		private Transform braceRopeHolder;

		private Transform braceRopeHolderRoute;

		private RopeEffect braceWinchRope;

		private readonly List<RopeEffect> braceRopes = new List<RopeEffect>(2);

		private const float MastPassMovementStart = 0.85f;

		private const float NonVanillaClearanceCompensation = 0.15f;

		private const float GeometryChangeEpsilon = 1E-06f;

		private const float PlaneIntersectionEpsilon = 0.0001f;

		private const float SurfaceClearanceTolerance = 0.0005f;

		private const float SurfaceSearchStep = 0.025f;

		private const float SurfaceSearchLimit = 5f;

		private const float SurfaceSearchMinimumRadius = 0.001f;

		private const float AxisMagnitudeEpsilon = 0.001f;

		private const float MinimumTipTolerance = 0.001f;

		private const float ClearanceCachePoseEpsilon = 1E-05f;

		private const float ClearanceCacheRotationDotEpsilon = 1E-06f;

		private const int SurfaceSearchIterations = 12;

		private const int PrincipalAxisIterations = 12;

		private const float TipSampleFraction = 0.01f;

		private MeshFilter yardMesh;

		private Transform visualRoot;

		private Transform proceduralPivot;

		private Transform originalVisualParent;

		private int originalVisualSiblingIndex;

		private bool hasOriginalVisualParent;

		private Quaternion verticalRotation;

		private Vector3 starboardAxisLocal;

		private Vector3 aftAxisLocal;

		private Vector3 mastCenterLocal;

		private Vector3 heelPositionLocal;

		private Vector3 mastPivotLocal;

		private Vector3 yardContactCenterLocal;

		private Vector3[] yardContactSurfacePointsLocal = Array.Empty<Vector3>();

		private CapsuleCollider mastContactCollider;

		private float vanillaRingSide;

		private float vanillaRingAft;

		private float vanillaSurfaceClearance;

		private bool hasSurfaceMeasurement;

		private Mesh measuredYardMesh;

		private Vector3 measuredYardScale;

		private float measuredInstallHeight;

		private bool hasCachedClearancePosition;

		private LateenYardRig.ClearancePoseSnapshot cachedClearancePose;

		private Vector2 cachedClearancePosition;

		private Vector3[] probeWorldPoints = Array.Empty<Vector3>();

		private Vector3 probeStarboardWorld;

		private Vector3 probeAftWorld;

		private LateenYardRig.CapsuleWorldGeometry probeCapsule;

		private readonly struct ClearancePoseSnapshot
		{
			internal ClearancePoseSnapshot(Quaternion yardRotation, Vector2 currentRingPosition, Vector2 mirroredRingPosition, Matrix4x4 rigToMastLocal, Vector3 mastLossyScale, Vector3 colliderCenter, float colliderRadius, float colliderHeight, int colliderDirection)
			{
				this.YardRotation = yardRotation;
				this.CurrentRingPosition = currentRingPosition;
				this.MirroredRingPosition = mirroredRingPosition;
				this.RigToMastLocal = rigToMastLocal;
				this.MastLossyScale = mastLossyScale;
				this.ColliderCenter = colliderCenter;
				this.ColliderRadius = colliderRadius;
				this.ColliderHeight = colliderHeight;
				this.ColliderDirection = colliderDirection;
			}

			internal bool ApproximatelyEquals(LateenYardRig.ClearancePoseSnapshot other)
			{
				float num = LateenYardRig.ClearanceCachePoseEpsilon * LateenYardRig.ClearanceCachePoseEpsilon;
				return Mathf.Abs(Quaternion.Dot(this.YardRotation, other.YardRotation)) >= 1f - LateenYardRig.ClearanceCacheRotationDotEpsilon && (this.CurrentRingPosition - other.CurrentRingPosition).sqrMagnitude <= num && (this.MirroredRingPosition - other.MirroredRingPosition).sqrMagnitude <= num && LateenYardRig.MatrixApproximatelyEquals(this.RigToMastLocal, other.RigToMastLocal) && (this.MastLossyScale - other.MastLossyScale).sqrMagnitude <= num && (this.ColliderCenter - other.ColliderCenter).sqrMagnitude <= num && Mathf.Abs(this.ColliderRadius - other.ColliderRadius) <= LateenYardRig.ClearanceCachePoseEpsilon && Mathf.Abs(this.ColliderHeight - other.ColliderHeight) <= LateenYardRig.ClearanceCachePoseEpsilon && this.ColliderDirection == other.ColliderDirection;
			}

			internal readonly Quaternion YardRotation;

			internal readonly Vector2 CurrentRingPosition;

			internal readonly Vector2 MirroredRingPosition;

			internal readonly Matrix4x4 RigToMastLocal;

			internal readonly Vector3 MastLossyScale;

			internal readonly Vector3 ColliderCenter;

			internal readonly float ColliderRadius;

			internal readonly float ColliderHeight;

			internal readonly int ColliderDirection;
		}

		private readonly struct CapsuleWorldGeometry
		{
			internal CapsuleWorldGeometry(Vector3 center, Vector3 axis, float halfSegment, float radius)
			{
				this.Center = center;
				this.Axis = axis;
				this.HalfSegment = halfSegment;
				this.Radius = radius;
			}

			internal Vector3 GetCenterlinePoint(Vector3 point)
			{
				float d = Mathf.Clamp(Vector3.Dot(point - this.Center, this.Axis), -this.HalfSegment, this.HalfSegment);
				return this.Center + this.Axis * d;
			}

			internal readonly Vector3 Center;

			internal readonly Vector3 Axis;

			internal readonly float HalfSegment;

			internal readonly float Radius;
		}
	}

	[HarmonyPatch(typeof(SaveLoadManager), "SaveModData")]
	internal static class LateenYardSavePatch
	{
		private static void Postfix()
		{
			LateenYardPersistence.Save();
		}
	}
}
