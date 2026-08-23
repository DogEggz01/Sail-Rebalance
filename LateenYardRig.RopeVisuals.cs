using System.Collections.Generic;
using UnityEngine;

namespace SailRebalance
{
	internal sealed partial class LateenYardRig : MonoBehaviour
	{
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
			ropeEffect.currentRopeLength = LooseRopeCurveLength;
			ropeEffect.ropeWidth = source.ropeWidth;
			ropeEffect.totalRopeLength = LooseRopeTension;
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
			float currentRopeLength = Mathf.Lerp(LooseRopeCurveLength, TightRopeCurveLength, bracePull);
			float totalRopeLength = Mathf.Lerp(LooseRopeTension, TightRopeTension, bracePull);
			for (int i = 0; i < this.braceRopes.Count; i++)
			{
				RopeEffect ropeEffect = this.braceRopes[i];
				if (!(ropeEffect == null))
				{
					ropeEffect.currentRopeLength = currentRopeLength;
					ropeEffect.totalRopeLength = totalRopeLength;
				}
			}
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
		private const float LooseRopeTension = 0.86f;

		private const float TightRopeTension = 0.98f;

		private const float LooseRopeCurveLength = 0.45f;

		private const float TightRopeCurveLength = 0.18f;

		private Transform yardHeelAttachment;

		private Transform braceRopeHolder;

		private Transform braceRopeHolderRoute;

		private RopeEffect braceWinchRope;

		private readonly List<RopeEffect> braceRopes = new List<RopeEffect>(2);

	}
}
