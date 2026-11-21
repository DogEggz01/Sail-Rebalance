using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SailRebalance
{
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

	internal static class JunkSquareReverseReefing
	{
		internal static bool IsTarget(Sail sail)
		{
			return sail != null && sail.squareSail && sail.category == SailCategory.square && JunkSquareReverseReefing.JunkSquareIndices.Contains(sail.prefabIndex);
		}

		internal static void PrepareController(RopeControllerSailReef controller)
		{
			if (!Plugin.ReverseJunkSquareFurlingEnabled || controller == null || !JunkSquareReverseReefing.IsTarget(controller.sail))
			{
				return;
			}
			if (!JunkSquareReverseReefing.OriginalControllerDirections.ContainsKey(controller))
			{
				JunkSquareReverseReefing.OriginalControllerDirections.Add(controller, controller.reverseReefing);
			}
			controller.reverseReefing = true;
		}

		internal static bool TakeOriginalControllerDirection(RopeControllerSailReef controller)
		{
			bool result;
			if (JunkSquareReverseReefing.OriginalControllerDirections.TryGetValue(controller, out result))
			{
				JunkSquareReverseReefing.OriginalControllerDirections.Remove(controller);
				return result;
			}
			return controller.reverseReefing;
		}

		internal static void RestorePreparedController(RopeControllerSailReef controller)
		{
			bool flag;
			if (controller == null || !JunkSquareReverseReefing.OriginalControllerDirections.TryGetValue(controller, out flag))
			{
				return;
			}
			JunkSquareReverseReefing.OriginalControllerDirections.Remove(controller);
			float num = (controller.sail != null) ? Mathf.Clamp01(controller.sail.currentUnroll) : 1f;
			controller.reverseReefing = flag;
			controller.currentLength = (flag ? (1f - num) : num);
			controller.changed = true;
		}

		internal static void Register(JunkSquareReverseRig rig)
		{
			JunkSquareReverseReefing.Rigs.Add(rig);
		}

		internal static void Unregister(JunkSquareReverseRig rig)
		{
			JunkSquareReverseReefing.Rigs.Remove(rig);
		}

		internal static void PrepareMast(Mast mast)
		{
			foreach (JunkSquareReverseRig junkSquareReverseRig in JunkSquareReverseReefing.CopyRigs())
			{
				if (junkSquareReverseRig != null && junkSquareReverseRig.OwningMast == mast)
				{
					junkSquareReverseRig.PrepareForMastUpdate();
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
				GameObject gameObject = mast.sails[i];
				if (!(gameObject == null))
				{
					Sail component = gameObject.GetComponent<Sail>();
					JunkSquareReverseRig junkSquareReverseRig = gameObject.GetComponent<JunkSquareReverseRig>();
					if (!Plugin.ReverseJunkSquareFurlingEnabled || !JunkSquareReverseReefing.IsTarget(component))
					{
						if (junkSquareReverseRig != null)
						{
							junkSquareReverseRig.RestoreAndDestroy();
						}
					}
					else
					{
						SailConnections component2 = gameObject.GetComponent<SailConnections>();
						if (component2 == null)
						{
							JunkSquareReverseReefing.WarnOnce(component, "has no SailConnections component");
						}
						else
						{
							if (junkSquareReverseRig == null)
							{
								junkSquareReverseRig = gameObject.AddComponent<JunkSquareReverseRig>();
							}
							if (junkSquareReverseRig.Initialize(mast, component, component2))
							{
								junkSquareReverseRig.BindAssignedWinch();
								JunkSquareReverseReefing.Warnings.Remove(component.GetInstanceID());
							}
							else
							{
								JunkSquareReverseReefing.RestorePreparedController(component2.reefController as RopeControllerSailReef);
								junkSquareReverseRig.RestoreAndDestroy();
							}
						}
					}
				}
			}
		}

		internal static void ApplyToLoadedMasts()
		{
			Mast[] array = Object.FindObjectsOfType<Mast>();
			for (int i = 0; i < array.Length; i++)
			{
				JunkSquareReverseReefing.ApplyToMast(array[i]);
			}
		}

		internal static void SetEnabled(bool enabled)
		{
			if (enabled)
			{
				JunkSquareReverseReefing.ApplyToLoadedMasts();
				return;
			}
			JunkSquareReverseReefing.DisposeAllRigs();
		}

		internal static void FurlNewlyInstalledShipyardSail(GameObject sailObject)
		{
			if (!Plugin.ReverseJunkSquareFurlingEnabled || GameState.currentShipyard == null || sailObject == null)
			{
				return;
			}
			Sail component = sailObject.GetComponent<Sail>();
			if (!JunkSquareReverseReefing.IsTarget(component) || !component.IsInstalled())
			{
				return;
			}
			SailConnections component2 = sailObject.GetComponent<SailConnections>();
			RopeControllerSailReef ropeControllerSailReef = (component2 != null) ? (component2.reefController as RopeControllerSailReef) : null;
			if (ropeControllerSailReef == null)
			{
				JunkSquareReverseReefing.WarnOnce(component, "its newly installed reef controller was not found");
				return;
			}
			component.currentUnroll = 0f;
			ropeControllerSailReef.reverseReefing = true;
			ropeControllerSailReef.currentLength = 1f;
			ropeControllerSailReef.changed = true;
			JunkSquareReverseReefing.Warnings.Remove(component.GetInstanceID());
		}

		internal static void DisposeAllRigs()
		{
			JunkSquareReverseRig[] array = JunkSquareReverseReefing.CopyRigs();
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] != null)
				{
					array[i].RestoreAndDestroy();
				}
			}
			JunkSquareReverseReefing.Rigs.Clear();
			JunkSquareReverseReefing.RestoreAllPreparedControllers();
		}

		internal static void Reset()
		{
			JunkSquareReverseReefing.DisposeAllRigs();
			JunkSquareReverseReefing.OriginalControllerDirections.Clear();
			JunkSquareReverseReefing.Warnings.Clear();
		}

		internal static void WarnOnce(Sail sail, string reason)
		{
			if (sail == null || !JunkSquareReverseReefing.Warnings.Add(sail.GetInstanceID()))
			{
				return;
			}
			if (Plugin.Log != null)
			{
				Plugin.Log.LogWarning(string.Concat(new string[]
				{
					"Could not reverse Junk-square furling for ",
					sail.name,
					": ",
					reason,
					"."
				}));
			}
		}

		private static JunkSquareReverseRig[] CopyRigs()
		{
			JunkSquareReverseRig[] array = new JunkSquareReverseRig[JunkSquareReverseReefing.Rigs.Count];
			JunkSquareReverseReefing.Rigs.CopyTo(array);
			return array;
		}

		private static void RestoreAllPreparedControllers()
		{
			RopeControllerSailReef[] array = new RopeControllerSailReef[JunkSquareReverseReefing.OriginalControllerDirections.Count];
			JunkSquareReverseReefing.OriginalControllerDirections.Keys.CopyTo(array, 0);
			for (int i = 0; i < array.Length; i++)
			{
				JunkSquareReverseReefing.RestorePreparedController(array[i]);
			}
			JunkSquareReverseReefing.OriginalControllerDirections.Clear();
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

	internal sealed class JunkSquareReverseRig : MonoBehaviour
	{
		internal Mast OwningMast
		{
			get
			{
				return this.mast;
			}
		}

		internal bool Initialize(Mast owningMast, Sail targetSail, SailConnections targetConnections)
		{
			this.mast = owningMast;
			if (this.initialized)
			{
				return true;
			}
			this.sail = targetSail;
			this.reefController = (targetConnections.reefController as RopeControllerSailReef);
			if (this.reefController == null)
			{
				return this.Fail("its reef controller is not RopeControllerSailReef");
			}
			this.reefEffect = targetSail.GetComponentInChildren<ReefEffectAnimUniversal>(true);
			Animator animator = this.FindAnimator();
			if (this.reefEffect == null || animator == null)
			{
				return this.Fail("the Junk-square reef animator was not found");
			}
			this.visualRoot = JunkSquareReverseRig.FindDescendant(animator.transform, "SAIL_junk_square");
			this.boomLow = JunkSquareReverseRig.FindDescendant(this.visualRoot, "boom_010");
			Transform x = JunkSquareReverseRig.FindDescendant(this.visualRoot, "sail_rope_att__top_");
			if (this.visualRoot == null || this.boomLow == null || x == null)
			{
				return this.Fail("the Junk-square visual root, lower batten, or lifting-rope anchor was not found");
			}
			if (!this.CaptureUnfurledGeometry(animator))
			{
				return false;
			}
			this.originalControllerDirection = JunkSquareReverseReefing.TakeOriginalControllerDirection(this.reefController);
			this.ApplyControllerDirection(true);
			this.initialized = true;
			this.refreshClothPending = true;
			JunkSquareReverseReefing.Register(this);
			return true;
		}

		internal void PrepareForMastUpdate()
		{
			this.RestoreWinch();
		}

		internal void BindAssignedWinch()
		{
			if (!this.initialized || this.mast == null)
			{
				return;
			}
			GPButtonRopeWinch[] componentsInChildren = ((this.mast.shipRigidbody != null) ? this.mast.shipRigidbody.transform : this.mast.transform).GetComponentsInChildren<GPButtonRopeWinch>(true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null && componentsInChildren[i].rope == this.reefController)
				{
					this.BindWinch(componentsInChildren[i]);
					return;
				}
			}
			this.RestoreWinch();
		}

		internal void RestoreAndDestroy()
		{
			this.Restore();
			if (this != null)
			{
				Object.Destroy(this);
			}
		}

		private void LateUpdate()
		{
			if (!this.initialized)
			{
				return;
			}
			this.ApplyBoomLowAnchorCorrection();
			if (this.refreshClothPending)
			{
				this.refreshClothPending = false;
				this.RefreshCloth();
			}
		}

		private void OnDestroy()
		{
			this.Restore();
		}

		private Animator FindAnimator()
		{
			if (this.reefEffect == null)
			{
				return null;
			}
			if (this.reefEffect.overrideAnimator != null)
			{
				return this.reefEffect.overrideAnimator;
			}
			Animator animator = this.reefEffect.GetComponent<Animator>();
			if (animator == null)
			{
				animator = this.reefEffect.GetComponentInParent<Animator>();
			}
			if (animator == null)
			{
				animator = this.sail.GetComponentInChildren<Animator>(true);
			}
			return animator;
		}

		private bool CaptureUnfurledGeometry(Animator animator)
		{
			string clipName = string.IsNullOrEmpty(this.reefEffect.clipName) ? "ArmatureAction" : this.reefEffect.clipName;
			float normalizedTime = 1f - Mathf.Clamp01(this.sail.currentUnroll);
			Exception sampleError = null;
			Exception restoreError = null;
			try
			{
				this.SampleAnimator(animator, clipName, 0f);
				this.unfurledVisualRootLocalPosition = this.visualRoot.localPosition;
				this.unfurledBoomLowLocalPosition = this.visualRoot.InverseTransformPoint(this.boomLow.position);
			}
			catch (Exception exception)
			{
				sampleError = exception;
			}
			finally
			{
				try
				{
					this.SampleAnimator(animator, clipName, normalizedTime);
				}
				catch (Exception exception)
				{
					restoreError = exception;
				}
			}
			if (sampleError != null)
			{
				this.LogAnimationWarning("sample", sampleError);
				if (restoreError != null)
				{
					this.LogAnimationWarning("restore", restoreError);
				}
				return this.Fail("the Junk-square reef animation could not be sampled");
			}
			if (restoreError != null)
			{
				this.LogAnimationWarning("restore", restoreError);
				return this.Fail("the Junk-square reef animation state could not be restored");
			}
			return true;
		}

		private void SampleAnimator(Animator animator, string clipName, float normalizedTime)
		{
			animator.Play(clipName, this.reefEffect.layer, normalizedTime);
			animator.Update(0f);
		}

		private void LogAnimationWarning(string operation, Exception exception)
		{
			if (Plugin.Log != null)
			{
				Plugin.Log.LogWarning(string.Concat(new string[]
				{
					"Could not ",
					operation,
					" Junk-square reef animation for ",
					this.sail.name,
					": ",
					exception.Message
				}));
			}
		}

		private void ApplyBoomLowAnchorCorrection()
		{
			if (this.visualRoot == null || this.boomLow == null)
			{
				return;
			}
			this.visualRoot.localPosition = this.unfurledVisualRootLocalPosition;
			Vector3 a = this.visualRoot.TransformPoint(this.unfurledBoomLowLocalPosition);
			Vector3 b = JunkSquareReverseRig.ParentVector(this.visualRoot.parent, a - this.boomLow.position);
			this.visualRoot.localPosition = this.unfurledVisualRootLocalPosition + b;
		}

		private void ApplyControllerDirection(bool reverse)
		{
			if (this.reefController == null || this.sail == null)
			{
				return;
			}
			float num = Mathf.Clamp01(this.sail.currentUnroll);
			this.reefController.reverseReefing = reverse;
			this.reefController.currentLength = (reverse ? (1f - num) : num);
			this.reefController.changed = true;
		}

		private void BindWinch(GPButtonRopeWinch winch)
		{
			if (this.boundWinch == winch)
			{
				winch.reverseWindResistance = true;
				return;
			}
			this.RestoreWinch();
			this.boundWinch = winch;
			this.originalWinchReverseResistance = winch.reverseWindResistance;
			winch.reverseWindResistance = true;
		}

		private void RestoreWinch()
		{
			if (this.boundWinch != null)
			{
				this.boundWinch.reverseWindResistance = this.originalWinchReverseResistance;
			}
			this.boundWinch = null;
		}

		private void Restore()
		{
			if (this.restored)
			{
				return;
			}
			this.restored = true;
			this.RestoreWinch();
			if (this.initialized)
			{
				this.ApplyControllerDirection(this.originalControllerDirection);
				if (this.visualRoot != null)
				{
					this.visualRoot.localPosition = this.unfurledVisualRootLocalPosition;
				}
				this.RefreshCloth();
			}
			JunkSquareReverseReefing.Unregister(this);
		}

		private void RefreshCloth()
		{
			if (this.reefEffect == null || !this.reefEffect.isActiveAndEnabled)
			{
				return;
			}
			try
			{
				this.reefEffect.RefreshCloth();
			}
			catch (Exception ex)
			{
				if (Plugin.Log != null)
				{
					Plugin.Log.LogWarning("Could not refresh Junk-square cloth for " + this.sail.name + ": " + ex.Message);
				}
			}
		}

		private bool Fail(string reason)
		{
			JunkSquareReverseReefing.WarnOnce(this.sail, reason);
			return false;
		}

		private static Transform FindDescendant(Transform root, string name)
		{
			if (root == null)
			{
				return null;
			}
			Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i].name == name)
				{
					return componentsInChildren[i];
				}
			}
			return null;
		}

		private static Vector3 ParentVector(Transform parent, Vector3 worldVector)
		{
			if (!(parent != null))
			{
				return worldVector;
			}
			return parent.InverseTransformVector(worldVector);
		}

		private Mast mast;

		private Sail sail;

		private RopeControllerSailReef reefController;

		private ReefEffectAnimUniversal reefEffect;

		private Transform visualRoot;

		private Transform boomLow;

		private Vector3 unfurledVisualRootLocalPosition;

		private Vector3 unfurledBoomLowLocalPosition;

		private bool originalControllerDirection;

		private GPButtonRopeWinch boundWinch;

		private bool originalWinchReverseResistance;

		private bool initialized;

		private bool refreshClothPending;

		private bool restored;
	}

	[HarmonyPatch(typeof(Mast), "AttachSailToMast")]
	internal static class JunkSquareReverseShipyardInstallPatch
	{
		private static void Prefix(GameObject sailObject, out bool __state)
		{
			Sail sail = (sailObject != null) ? sailObject.GetComponent<Sail>() : null;
			__state = (GameState.currentShipyard != null && sail != null && !sail.IsInstalled() && JunkSquareReverseReefing.IsTarget(sail));
		}

		private static void Postfix(GameObject sailObject, bool __state)
		{
			if (__state)
			{
				JunkSquareReverseReefing.FurlNewlyInstalledShipyardSail(sailObject);
			}
		}
	}

	internal static class SailPower
	{
		internal static float Junk(Sail sail)
		{
			if (!Plugin.JunkCurveEnabled)
			{
				return Plugin.ConfiguredJunkMultiplier;
			}
			if (sail == null || sail.shipRigidbody == null)
			{
				return SailPower.JunkDefault;
			}
			return SailPower.JunkCurve(SailPower.ApparentAngle(sail), Wind.currentWind.magnitude > SailPower.StrongWindThreshold);
		}

		// apparentAngle in degrees: 0 is a head wind, 180 a wind from dead astern.
		internal static float JunkCurve(float apparentAngle, bool strongWind)
		{
			float num = strongWind ? 0.85f : SailPower.JunkDefault;
			if (apparentAngle <= 90f)
			{
				return num;
			}
			if (apparentAngle <= 150f)
			{
				return Mathf.Lerp(num, 1f, Mathf.InverseLerp(90f, 150f, apparentAngle));
			}
			return Mathf.Lerp(1f, strongWind ? 0.9f : 0.85f, Mathf.InverseLerp(150f, 180f, apparentAngle));
		}

		internal static float Gaff(Sail sail)
		{
			if (!Plugin.GaffCurveEnabled)
			{
				return Plugin.ConfiguredGaffMultiplier;
			}
			if (sail == null || sail.shipRigidbody == null)
			{
				return SailPower.GaffDefault;
			}
			return SailPower.GaffCurve(SailPower.ApparentAngle(sail));
		}

		internal static float GaffCurve(float apparentAngle)
		{
			if (apparentAngle <= 80f || apparentAngle >= 160f)
			{
				return SailPower.GaffDefault;
			}
			if (apparentAngle <= 120f)
			{
				return Mathf.Lerp(SailPower.GaffDefault, 1f, Mathf.InverseLerp(80f, 120f, apparentAngle));
			}
			return Mathf.Lerp(1f, SailPower.GaffDefault, Mathf.InverseLerp(120f, 160f, apparentAngle));
		}

		private static float ApparentAngle(Sail sail)
		{
			return Mathf.Abs(Vector3.SignedAngle(-sail.shipRigidbody.transform.forward, sail.apparentWind, Vector3.up));
		}

		internal const float JunkDefault = 0.75f;

		internal const float GaffDefault = 0.85f;

		internal const float StrongWindThreshold = 21f;
	}

	[HarmonyPatch(typeof(Sail), "ApplyForce")]
	internal static class SailCategoryPowerPatch
	{
		// Replaces the first 0.75 (junk) and first 0.85 (gaff) constant with SailPower.Junk(this) / SailPower.Gaff(this).
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
						replacement = SailCategoryPowerPatch.JunkMethod;
						replacedJunk = true;
					}
					else if (!replacedGaff && value == 0.85f)
					{
						replacement = SailCategoryPowerPatch.GaffMethod;
						replacedGaff = true;
					}
				}
				if (replacement == null)
				{
					yield return instruction;
					continue;
				}
				// The instruction object keeps its labels and blocks.
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

		private static readonly MethodInfo JunkMethod = AccessTools.Method(typeof(SailPower), "Junk", null, null);

		private static readonly MethodInfo GaffMethod = AccessTools.Method(typeof(SailPower), "Gaff", null, null);
	}
}
