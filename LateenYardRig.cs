using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace SailRebalance
{
	internal sealed partial class LateenYardRig : MonoBehaviour
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
				return this.sail != null && this.sail.currentUnroll <= ControlEpsilon && this.AreAllSheetsFullyLoose();
			}
		}

		internal bool CanOperateHalyardOrSheet
		{
			get
			{
				return this.BracePull >= FullyRaisedThreshold;
			}
		}

		private bool AreAllSheetsFullyLoose()
		{
			return this.Sheet != null && LateenYardRig.IsSheetFullyLoose(this.SheetLeft) && LateenYardRig.IsSheetFullyLoose(this.SheetMid) && LateenYardRig.IsSheetFullyLoose(this.SheetRight);
		}

		private static bool IsSheetFullyLoose(RopeController controller)
		{
			return controller == null || controller.currentLength >= FullyRaisedThreshold;
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
				if (log != null)
				{
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
			this.tackChangeArmed = (this.previousBracePull >= TackRearmPullThreshold);
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
			this.ApplyProceduralPose(bracePull);
			this.UpdateRopeTension(bracePull);
		}

		private void UpdateAutomaticSideTransfer(float bracePull)
		{
			bool releasing = bracePull < this.previousBracePull - ControlEpsilon;
			bool pullingIn = bracePull > this.previousBracePull + ControlEpsilon;
			if (!this.sideTransferActive && bracePull >= TackRearmPullThreshold)
			{
				this.tackChangeArmed = true;
			}
			if (this.tackChangeArmed && releasing)
			{
				this.transferStartSide = this.yardSide;
				this.transferTargetSide = -this.yardSide;
				this.sideTransferActive = true;
				this.tackChangeArmed = false;
			}
			if (this.sideTransferActive && bracePull <= ControlEpsilon)
			{
				this.yardSide = this.transferTargetSide;
				this.sideTransferActive = false;
			}
			else if (this.sideTransferActive && pullingIn && bracePull >= TackRearmPullThreshold)
			{
				this.sideTransferActive = false;
				this.tackChangeArmed = true;
			}
			this.previousBracePull = bracePull;
		}

		internal bool IsBadTack()
		{
			Rigidbody rigidbody = (this.sail != null) ? this.sail.shipRigidbody : null;
			if (rigidbody == null || this.sail.apparentWind.sqrMagnitude < WindMagnitudeEpsilon)
			{
				return false;
			}
			float num = Vector3.Dot(-this.sail.apparentWind.normalized, rigidbody.transform.right);
			return Mathf.Abs(num) >= WindSideDeadZone && ((num > 0f) ? 1 : -1) == this.yardSide;
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
			this.tackChangeArmed = (this.previousBracePull >= TackRearmPullThreshold);
			if (this.initialized && this.proceduralPivot != null)
			{
				this.ApplyProceduralPose(this.previousBracePull);
			}
		}

		internal void Dispose()
		{
			this.CleanupGeneratedObjects();
			UnityEngine.Object.Destroy(this);
		}

		private void OnDestroy()
		{
			this.CleanupGeneratedObjects();
		}

		private void CleanupGeneratedObjects()
		{
			if (this.cleanedUp)
			{
				return;
			}
			this.cleanedUp = true;
			this.initialized = false;
			LateenControlRegistry.Unregister(this);
			this.RestoreVisualHierarchy();
			this.DestroyRopeObjects();
			this.DestroyYardHeelAttachment();
			this.DestroyProceduralPivot();
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
	}
}
