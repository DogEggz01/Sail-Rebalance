using System;
using System.Collections.Generic;
using UnityEngine;

namespace SailRebalance
{
	internal static class LateenBraceWinchSelector
	{
		internal static LateenBraceWinchChoice Find(Mast owningMast, int sailOrder, Mast[] boatMasts)
		{
			LateenBraceWinchChoice choice = LateenBraceWinchSelector.FindReefWinchOnMast(owningMast);
			if (choice != null)
			{
				return choice;
			}
			choice = LateenBraceWinchSelector.FindSpecialReefWinch(owningMast, boatMasts, false);
			if (choice != null)
			{
				return choice;
			}
			choice = LateenBraceWinchSelector.FindAssociatedStaysailAngleWinch(owningMast, owningMast, boatMasts);
			if (choice != null)
			{
				return choice;
			}
			choice = LateenBraceWinchSelector.FindAngleWinchOnMast(owningMast, sailOrder);
			if (choice != null)
			{
				return choice;
			}
			choice = LateenBraceWinchSelector.FindOtherStaysailAngleWinch(owningMast, boatMasts);
			if (choice != null)
			{
				return choice;
			}
			return LateenBraceWinchSelector.FindSpecialReefWinch(owningMast, boatMasts, true);
		}

		private static LateenBraceWinchChoice FindReefWinchOnMast(Mast mast)
		{
			if (mast == null || mast.reefWinch == null)
			{
				return null;
			}
			for (int i = 0; i < PreferredReefSlots.Length; i++)
			{
				LateenBraceWinchChoice choice = LateenBraceWinchSelector.GetAvailableReefWinch(mast, PreferredReefSlots[i]);
				if (choice != null)
				{
					return choice;
				}
			}
			for (int i = 0; i < mast.reefWinch.Length; i++)
			{
				if (LateenBraceWinchSelector.IsPreferredReefSlot(i))
				{
					continue;
				}
				LateenBraceWinchChoice choice = LateenBraceWinchSelector.GetAvailableReefWinch(mast, i);
				if (choice != null)
				{
					return choice;
				}
			}
			return null;
		}

		private static LateenBraceWinchChoice FindSpecialReefWinch(Mast owningMast, Mast[] boatMasts, bool bowsprit)
		{
			RankedReefChoice best = null;
			for (int i = 0; i < boatMasts.Length; i++)
			{
				Mast mast = boatMasts[i];
				if (mast == null || mast == owningMast || LateenBraceWinchSelector.IsBowsprit(mast) != bowsprit)
				{
					continue;
				}
				if (!bowsprit && !mast.onlyStaysails)
				{
					continue;
				}
				if (mast.reefWinch == null)
				{
					continue;
				}
				for (int j = 0; j < mast.reefWinch.Length; j++)
				{
					LateenBraceWinchChoice candidate = LateenBraceWinchSelector.GetAvailableReefWinch(mast, j);
					if (candidate == null)
					{
						continue;
					}
					RankedReefChoice rankedCandidate = new RankedReefChoice
					{
						Choice = candidate,
						SlotPriority = LateenBraceWinchSelector.GetSlotPriority(j),
						WinchDistance = (candidate.Winch.transform.position - owningMast.transform.position).sqrMagnitude,
						RopeHolderDistance = (candidate.RopeHolder.position - owningMast.transform.position).sqrMagnitude,
						SourceOrder = mast.orderIndex,
						SourceInstanceId = mast.GetInstanceID()
					};
					if (LateenBraceWinchSelector.IsBetterSpecialReefChoice(rankedCandidate, best))
					{
						best = rankedCandidate;
					}
				}
			}
			return (best != null) ? best.Choice : null;
		}

		private static bool IsBetterSpecialReefChoice(RankedReefChoice candidate, RankedReefChoice current)
		{
			if (current == null || candidate.SlotPriority != current.SlotPriority)
			{
				return current == null || candidate.SlotPriority < current.SlotPriority;
			}
			int comparison = candidate.WinchDistance.CompareTo(current.WinchDistance);
			if (comparison != 0)
			{
				return comparison < 0;
			}
			comparison = candidate.RopeHolderDistance.CompareTo(current.RopeHolderDistance);
			if (comparison != 0)
			{
				return comparison < 0;
			}
			comparison = candidate.SourceOrder.CompareTo(current.SourceOrder);
			return (comparison != 0) ? comparison < 0 : candidate.SourceInstanceId < current.SourceInstanceId;
		}

		private static LateenBraceWinchChoice FindAssociatedStaysailAngleWinch(Mast owningMast, Mast associatedOrdinaryMast, Mast[] boatMasts)
		{
			Transform boatRoot = LateenBraceWinchSelector.GetBoatRoot(owningMast);
			List<Mast> associatedMasts = new List<Mast>();
			for (int i = 0; i < boatMasts.Length; i++)
			{
				Mast mast = boatMasts[i];
				if (!LateenBraceWinchSelector.IsStaysailMast(mast) || LateenBraceWinchSelector.FindNearestOrdinaryMast(mast, boatMasts, boatRoot) != associatedOrdinaryMast)
				{
					continue;
				}
				associatedMasts.Add(mast);
			}
			associatedMasts.Sort(delegate(Mast left, Mast right)
			{
				float leftDistance = LateenBraceWinchSelector.GetHorizontalDistanceSquared(left.transform.position, associatedOrdinaryMast.transform.position, boatRoot.up);
				float rightDistance = LateenBraceWinchSelector.GetHorizontalDistanceSquared(right.transform.position, associatedOrdinaryMast.transform.position, boatRoot.up);
				int comparison = leftDistance.CompareTo(rightDistance);
				return (comparison != 0) ? comparison : LateenBraceWinchSelector.CompareMastIdentity(left, right);
			});
			for (int j = 0; j < associatedMasts.Count; j++)
			{
				LateenBraceWinchChoice choice = LateenBraceWinchSelector.FindAnyAngleWinchOnMast(associatedMasts[j]);
				if (choice != null)
				{
					return choice;
				}
			}
			return null;
		}

		private static LateenBraceWinchChoice FindOtherStaysailAngleWinch(Mast owningMast, Mast[] boatMasts)
		{
			Transform boatRoot = LateenBraceWinchSelector.GetBoatRoot(owningMast);
			List<Mast> ordinaryMasts = new List<Mast>();
			for (int i = 0; i < boatMasts.Length; i++)
			{
				Mast mast = boatMasts[i];
				if (mast != null && mast != owningMast && LateenBraceWinchSelector.IsOrdinaryMast(mast))
				{
					ordinaryMasts.Add(mast);
				}
			}
			ordinaryMasts.Sort(delegate(Mast left, Mast right)
			{
				float leftForward = Vector3.Dot(left.transform.position - boatRoot.position, boatRoot.forward);
				float rightForward = Vector3.Dot(right.transform.position - boatRoot.position, boatRoot.forward);
				int comparison = rightForward.CompareTo(leftForward);
				return (comparison != 0) ? comparison : LateenBraceWinchSelector.CompareMastIdentity(left, right);
			});
			for (int j = 0; j < ordinaryMasts.Count; j++)
			{
				LateenBraceWinchChoice choice = LateenBraceWinchSelector.FindAssociatedStaysailAngleWinch(owningMast, ordinaryMasts[j], boatMasts);
				if (choice != null)
				{
					return choice;
				}
			}
			return null;
		}

		private static Mast FindNearestOrdinaryMast(Mast staysailMast, Mast[] boatMasts, Transform boatRoot)
		{
			Mast nearest = null;
			float nearestDistance = float.PositiveInfinity;
			for (int i = 0; i < boatMasts.Length; i++)
			{
				Mast candidate = boatMasts[i];
				if (!LateenBraceWinchSelector.IsOrdinaryMast(candidate))
				{
					continue;
				}
				float distance = LateenBraceWinchSelector.GetHorizontalDistanceSquared(staysailMast.transform.position, candidate.transform.position, boatRoot.up);
				if (nearest == null || distance < nearestDistance || (Mathf.Approximately(distance, nearestDistance) && LateenBraceWinchSelector.CompareMastIdentity(candidate, nearest) < 0))
				{
					nearest = candidate;
					nearestDistance = distance;
				}
			}
			return nearest;
		}

		private static float GetHorizontalDistanceSquared(Vector3 first, Vector3 second, Vector3 boatUp)
		{
			Vector3 offset = first - second;
			offset -= Vector3.Project(offset, boatUp);
			return offset.sqrMagnitude;
		}

		private static int CompareMastIdentity(Mast left, Mast right)
		{
			int comparison = left.orderIndex.CompareTo(right.orderIndex);
			return (comparison != 0) ? comparison : left.GetInstanceID().CompareTo(right.GetInstanceID());
		}

		private static LateenBraceWinchChoice FindAngleWinchOnMast(Mast mast, int index)
		{
			LateenBraceWinchChoice choice = LateenBraceWinchSelector.GetAvailableWinch(mast, mast.leftAngleWinch, index);
			if (choice != null)
			{
				return choice;
			}
			choice = LateenBraceWinchSelector.GetAvailableWinch(mast, mast.rightAngleWinch, index);
			return choice ?? LateenBraceWinchSelector.GetAvailableWinch(mast, mast.midAngleWinch, index);
		}

		private static LateenBraceWinchChoice FindAnyAngleWinchOnMast(Mast mast)
		{
			int slotCount = LateenBraceWinchSelector.GetMaximumLength(mast.leftAngleWinch, mast.rightAngleWinch, mast.midAngleWinch);
			for (int i = 0; i < slotCount; i++)
			{
				LateenBraceWinchChoice choice = LateenBraceWinchSelector.GetAvailableWinch(mast, mast.leftAngleWinch, i);
				if (choice != null)
				{
					return choice;
				}
				choice = LateenBraceWinchSelector.GetAvailableWinch(mast, mast.rightAngleWinch, i);
				if (choice != null)
				{
					return choice;
				}
				choice = LateenBraceWinchSelector.GetAvailableWinch(mast, mast.midAngleWinch, i);
				if (choice != null)
				{
					return choice;
				}
			}
			return null;
		}

		private static LateenBraceWinchChoice GetAvailableReefWinch(Mast mast, int index)
		{
			return (mast == null) ? null : LateenBraceWinchSelector.GetAvailableWinch(mast, mast.reefWinch, index);
		}

		private static LateenBraceWinchChoice GetAvailableWinch(Mast ropeHolderMast, GPButtonRopeWinch[] winches, int index)
		{
			GPButtonRopeWinch winch = LateenBraceWinchSelector.GetWinch(winches, index);
			Transform ropeHolder = LateenBraceWinchSelector.GetTransform(ropeHolderMast.mastReefAtt, index);
			if (winch == null || winch.rope != null || ropeHolder == null)
			{
				return null;
			}
			return new LateenBraceWinchChoice
			{
				Winch = winch,
				RopeHolder = ropeHolder
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
				GPButtonRopeWinch winch = mast.reefWinch[i];
				if (winch != null && LateenBraceWinchSelector.ContainsIgnoreCase(winch.gameObject.name, "bowsprit"))
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
			int maximum = 0;
			for (int i = 0; i < arrays.Length; i++)
			{
				if (arrays[i] != null)
				{
					maximum = Mathf.Max(maximum, arrays[i].Length);
				}
			}
			return maximum;
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
			return (mast.shipRigidbody != null) ? mast.shipRigidbody.transform : mast.transform;
		}

		private sealed class RankedReefChoice
		{
			internal LateenBraceWinchChoice Choice;

			internal int SlotPriority;

			internal float WinchDistance;

			internal float RopeHolderDistance;

			internal int SourceOrder;

			internal int SourceInstanceId;
		}

		private static readonly int[] PreferredReefSlots = new int[] { 1, 2, 3 };
	}

	internal sealed class LateenBraceWinchChoice
	{
		internal GPButtonRopeWinch Winch;

		internal Transform RopeHolder;

	}
}
