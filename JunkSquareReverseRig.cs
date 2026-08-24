using UnityEngine;

namespace SailRebalance
{
	internal sealed class JunkSquareReverseRig : MonoBehaviour
	{
		internal Mast OwningMast
		{
			get { return this.mast; }
		}

		internal bool Initialize(Mast owningMast, Sail targetSail, SailConnections targetConnections)
		{
			this.mast = owningMast;
			if (this.initialized)
			{
				return true;
			}

			this.sail = targetSail;
			this.reefController = targetConnections.reefController as RopeControllerSailReef;
			if (this.reefController == null)
			{
				return this.Fail("its reef controller is not RopeControllerSailReef");
			}

			this.reefEffect = targetSail.GetComponentInChildren<ReefEffectAnimUniversal>(true);
			Animator targetAnimator = this.FindAnimator();
			if (this.reefEffect == null || targetAnimator == null)
			{
				return this.Fail("the Junk-square reef animator was not found");
			}

			this.visualRoot = FindDescendant(targetAnimator.transform, "SAIL_junk_square");
			this.boomLow = FindDescendant(this.visualRoot, "boom_010");
			Transform topCenter = FindDescendant(this.visualRoot, "sail_rope_att__top_");
			if (this.visualRoot == null || this.boomLow == null || topCenter == null)
			{
				return this.Fail("the Junk-square visual root, lower batten, or lifting-rope anchor was not found");
			}

			if (!this.CaptureUnfurledGeometry(targetAnimator))
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

			Transform boatRoot = this.mast.shipRigidbody != null ? this.mast.shipRigidbody.transform : this.mast.transform;
			GPButtonRopeWinch[] winches = boatRoot.GetComponentsInChildren<GPButtonRopeWinch>(true);
			for (int i = 0; i < winches.Length; i++)
			{
				if (winches[i] != null && winches[i].rope == this.reefController)
				{
					this.BindWinch(winches[i]);
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
			Animator found = this.reefEffect.GetComponent<Animator>();
			if (found == null)
			{
				found = this.reefEffect.GetComponentInParent<Animator>();
			}
			if (found == null)
			{
				found = this.sail.GetComponentInChildren<Animator>(true);
			}
			return found;
		}

		private bool CaptureUnfurledGeometry(Animator animator)
		{
			string clipName = string.IsNullOrEmpty(this.reefEffect.clipName) ? "ArmatureAction" : this.reefEffect.clipName;
			float currentAnimationTime = 1f - Mathf.Clamp01(this.sail.currentUnroll);
			System.Exception samplingException = null;
			System.Exception restorationException = null;
			try
			{
				this.SampleAnimator(animator, clipName, 0f);
				this.unfurledVisualRootLocalPosition = this.visualRoot.localPosition;
				this.unfurledBoomLowLocalPosition = this.visualRoot.InverseTransformPoint(this.boomLow.position);
			}
			catch (System.Exception exception)
			{
				samplingException = exception;
			}
			finally
			{
				try
				{
					this.SampleAnimator(animator, clipName, currentAnimationTime);
				}
				catch (System.Exception exception)
				{
					restorationException = exception;
				}
			}

			if (samplingException != null)
			{
				this.LogAnimationWarning("sample", samplingException);
				if (restorationException != null)
				{
					this.LogAnimationWarning("restore", restorationException);
				}
				return this.Fail("the Junk-square reef animation could not be sampled");
			}
			if (restorationException != null)
			{
				this.LogAnimationWarning("restore", restorationException);
				return this.Fail("the Junk-square reef animation state could not be restored");
			}
			return true;
		}

		private void SampleAnimator(Animator animator, string clipName, float normalizedTime)
		{
			animator.Play(clipName, this.reefEffect.layer, normalizedTime);
			animator.Update(0f);
		}

		private void LogAnimationWarning(string operation, System.Exception exception)
		{
			if (Plugin.Log != null)
			{
				Plugin.Log.LogWarning("Could not " + operation + " Junk-square reef animation for " + this.sail.name + ": " + exception.Message);
			}
		}

		private void ApplyBoomLowAnchorCorrection()
		{
			if (this.visualRoot == null || this.boomLow == null)
			{
				return;
			}

			// The Animator collapses boom_010 upward. Moving the visual group by the
			// opposite amount holds boom_010 down and makes the upper battens rise.
			this.visualRoot.localPosition = this.unfurledVisualRootLocalPosition;
			Vector3 targetPosition = this.visualRoot.TransformPoint(this.unfurledBoomLowLocalPosition);
			Vector3 correction = ParentVector(this.visualRoot.parent, targetPosition - this.boomLow.position);
			this.visualRoot.localPosition = this.unfurledVisualRootLocalPosition + correction;
		}

		private void ApplyControllerDirection(bool reverse)
		{
			if (this.reefController == null || this.sail == null)
			{
				return;
			}
			float currentUnroll = Mathf.Clamp01(this.sail.currentUnroll);
			this.reefController.reverseReefing = reverse;
			this.reefController.currentLength = reverse ? 1f - currentUnroll : currentUnroll;
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
			catch (System.Exception exception)
			{
				if (Plugin.Log != null)
				{
					Plugin.Log.LogWarning("Could not refresh Junk-square cloth for " + this.sail.name + ": " + exception.Message);
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
			Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
			for (int i = 0; i < descendants.Length; i++)
			{
				if (descendants[i].name == name)
				{
					return descendants[i];
				}
			}
			return null;
		}

		private static Vector3 ParentVector(Transform parent, Vector3 worldVector)
		{
			return parent != null ? parent.InverseTransformVector(worldVector) : worldVector;
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
}
