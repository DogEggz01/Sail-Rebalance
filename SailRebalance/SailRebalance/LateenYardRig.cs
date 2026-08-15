using System;
using System.Collections.Generic;
using UnityEngine;

namespace SailRebalance;

internal sealed class LateenYardRig : MonoBehaviour
{
	internal const float ControlEpsilon = 0.0001f;

	internal const float FullyRaisedThreshold = 0.9999f;

	internal const float BadTackPowerMultiplier = 0.9f;

	private const float MastPassMovementStart = 0.85f;

	private const float TackResetPullThreshold = 0.7f;

	private const float NonVanillaClearanceCompensation = 0.15f;

	private const float GeometryChangeEpsilon = 1E-06f;

	private const float PlaneIntersectionEpsilon = 0.0001f;

	private const float SurfaceClearanceTolerance = 0.0005f;

	private const float SurfaceSearchStep = 0.025f;

	private const float SurfaceSearchLimit = 5f;

	private const float SurfaceSearchMinimumRadius = 0.001f;

	private const int SurfaceSearchIterations = 12;

	private const int PrincipalAxisIterations = 12;

	private const float TipSampleFraction = 0.01f;

	private const float LooseRopeTension = 0.86f;

	private const float TightRopeTension = 0.98f;

	private const float LooseRopeCurveLength = 0.45f;

	private const float TightRopeCurveLength = 0.18f;

	private Sail sail;

	private Mast mast;

	private SailConnections connections;

	private HingeJoint hinge;

	private MeshFilter yardMesh;

	private Transform visualRoot;

	private Transform proceduralPivot;

	private Transform yardHeelAttachment;

	private Transform lowerBraceFairlead;

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

	private int originalYardSide;

	private int yardSide;

	private float previousLowerBracePull;

	private int transferStartSide;

	private int transferTargetSide;

	private bool sideTransferActive;

	private bool tackChangeArmed;

	private bool initialized;

	private readonly List<RopeEffect> lowerBraceRopes = new List<RopeEffect>(2);

	internal LateenLowerBraceController LowerBrace { get; private set; }

	internal RopeController Halyard
	{
		get
		{
			if (!(connections != null))
			{
				return null;
			}
			return connections.reefController;
		}
	}

	internal RopeController SheetLeft
	{
		get
		{
			if (!(connections != null))
			{
				return null;
			}
			return connections.angleControllerLeft;
		}
	}

	internal RopeController SheetMid
	{
		get
		{
			if (!(connections != null))
			{
				return null;
			}
			return connections.angleControllerMid;
		}
	}

	internal RopeController SheetRight
	{
		get
		{
			if (!(connections != null))
			{
				return null;
			}
			return connections.angleControllerRight;
		}
	}

	internal RopeController Sheet
	{
		get
		{
			RopeController result;
			if ((result = SheetMid) == null)
			{
				result = SheetLeft ?? SheetRight;
			}
			return result;
		}
	}

	internal float LowerBracePull
	{
		get
		{
			if (!(LowerBrace != null))
			{
				return 0f;
			}
			return 1f - Mathf.Clamp01(LowerBrace.currentLength);
		}
	}

	internal int YardSideSign => yardSide;

	internal bool CanOperateLowerBrace
	{
		get
		{
			if (sail != null && sail.currentUnroll <= 0.0001f)
			{
				return AreAllSheetsFullyLoose();
			}
			return false;
		}
	}

	internal bool CanOperateHalyardOrSheet => LowerBracePull <= 0.0001f;

	private bool AreAllSheetsFullyLoose()
	{
		bool flag = false;
		return (IsSheetFullyLoose(SheetLeft, ref flag) && IsSheetFullyLoose(SheetMid, ref flag) && IsSheetFullyLoose(SheetRight, ref flag)) & flag;
	}

	private static bool IsSheetFullyLoose(RopeController controller, ref bool foundSheet)
	{
		if (controller == null)
		{
			return true;
		}
		foundSheet = true;
		return controller.currentLength >= 0.9999f;
	}

	internal bool Initialize(Mast owningMast, Sail attachedSail, SailConnections attachedConnections, GPButtonRopeWinch lowerBraceWinch)
	{
		mast = owningMast;
		sail = attachedSail;
		connections = attachedConnections;
		hinge = GetComponent<HingeJoint>();
		if (initialized)
		{
			RefreshGeometryIfNeeded();
			RefreshFairleadPosition();
			LateenControlRegistry.Register(this);
			return true;
		}
		if (mast == null || sail == null || connections == null || hinge == null || lowerBraceWinch == null)
		{
			return false;
		}
		visualRoot = FindVisualRoot();
		yardMesh = FindYardMesh(visualRoot);
		if (visualRoot == null || yardMesh == null || yardMesh.sharedMesh == null)
		{
			Plugin.Log?.LogWarning("Could not locate the lateen visual root or yard on " + base.name + ".");
			return false;
		}
		ConfigureAxes();
		ConfigureYardGeometry(initializeSide: true);
		CreateProceduralPivot();
		CreateLowerBraceRoute();
		if (LowerBrace == null)
		{
			Plugin.Log?.LogWarning("Could not create a lower-brace controller for " + base.name + ".");
			return false;
		}
		CaptureGeometrySignature();
		initialized = true;
		previousLowerBracePull = Mathf.Clamp01(LowerBracePull);
		tackChangeArmed = previousLowerBracePull <= 0.7f;
		LateenControlRegistry.Register(this);
		return true;
	}

	private void LateUpdate()
	{
		if (initialized && !(LowerBrace == null) && !(proceduralPivot == null))
		{
			RefreshGeometryIfNeeded();
			float lowerBracePull = Mathf.Clamp01(LowerBracePull);
			UpdateAutomaticSideTransfer(lowerBracePull);
			ApplyProceduralPose(lowerBracePull);
			UpdateRopeTension(lowerBracePull);
		}
	}

	private void ConfigureAxes()
	{
		Rigidbody rigidbody = ((sail.shipRigidbody != null) ? sail.shipRigidbody : mast.shipRigidbody);
		if (rigidbody != null)
		{
			starboardAxisLocal = base.transform.InverseTransformDirection(rigidbody.transform.right).normalized;
			aftAxisLocal = base.transform.InverseTransformDirection(-rigidbody.transform.forward).normalized;
		}
		else
		{
			starboardAxisLocal = Vector3.up;
			aftAxisLocal = -Vector3.right;
		}
	}

	private void ConfigureYardGeometry(bool initializeSide)
	{
		Vector3 vector = ((hinge.axis.sqrMagnitude > 0.001f) ? hinge.axis.normalized : Vector3.forward);
		Vector3 mastAxisWorld = base.transform.TransformDirection(vector).normalized;
		mastPivotLocal = hinge.anchor;
		AnalyzeYardGeometry(vector, out var vector2);
		if (Vector3.Dot(vector2, vector) < 0f)
		{
			vector2 = -vector2;
		}
		verticalRotation = Quaternion.FromToRotation(vector2, vector);
		Vector3 contactWorld = base.transform.TransformPoint(yardContactCenterLocal);
		Vector3 mastCenterWorld = FindMastCenterlinePoint(contactWorld, mastAxisWorld, out mastContactCollider);
		mastCenterLocal = base.transform.InverseTransformPoint(mastCenterWorld);
		Vector3 vanillaRingOffset = yardContactCenterLocal - mastCenterLocal;
		vanillaRingSide = Vector3.Dot(vanillaRingOffset, starboardAxisLocal);
		vanillaRingAft = Vector3.Dot(vanillaRingOffset, aftAxisLocal);
		hasSurfaceMeasurement = mastContactCollider != null && yardContactSurfacePointsLocal.Length != 0;
		vanillaSurfaceClearance = (hasSurfaceMeasurement ? MeasureSurfaceClearance(Quaternion.identity, Vector2.zero) : 0f);
		originalYardSide = ((vanillaRingSide >= 0f) ? 1 : (-1));
		if (initializeSide)
		{
			yardSide = originalYardSide;
		}
		if (yardHeelAttachment == null)
		{
			GameObject gameObject = new GameObject("lateen yard heel attachment");
			yardHeelAttachment = gameObject.transform;
		}
		if (yardHeelAttachment.parent != yardMesh.transform)
		{
			yardHeelAttachment.SetParent(yardMesh.transform, worldPositionStays: false);
		}
		yardHeelAttachment.position = base.transform.TransformPoint(heelPositionLocal);
	}

	private void RefreshGeometryIfNeeded()
	{
		if (YardGeometryChanged())
		{
			MeshFilter currentYardMesh = FindYardMesh(visualRoot);
			if (!(currentYardMesh == null) && !(currentYardMesh.sharedMesh == null))
			{
				yardMesh = currentYardMesh;
				proceduralPivot.localPosition = hinge.anchor;
				proceduralPivot.localRotation = Quaternion.identity;
				ConfigureAxes();
				ConfigureYardGeometry(initializeSide: false);
				CaptureGeometrySignature();
				ApplyProceduralPose(Mathf.Clamp01(LowerBracePull));
			}
		}
	}

	private bool YardGeometryChanged()
	{
		if (yardMesh == null || yardMesh.sharedMesh == null)
		{
			return true;
		}
		if (!(yardMesh.sharedMesh != measuredYardMesh) && !((yardMesh.transform.lossyScale - measuredYardScale).sqrMagnitude > 1E-06f))
		{
			return !Mathf.Approximately(sail.installHeight, measuredInstallHeight);
		}
		return true;
	}

	private void CaptureGeometrySignature()
	{
		measuredYardMesh = yardMesh.sharedMesh;
		measuredYardScale = yardMesh.transform.lossyScale;
		measuredInstallHeight = sail.installHeight;
	}

	private void CreateProceduralPivot()
	{
		GameObject gameObject = new GameObject("lateen lower-brace pivot");
		proceduralPivot = gameObject.transform;
		proceduralPivot.SetParent(base.transform, worldPositionStays: false);
		proceduralPivot.localPosition = mastPivotLocal;
		proceduralPivot.localRotation = Quaternion.identity;
		proceduralPivot.localScale = Vector3.one;
		visualRoot.SetParent(proceduralPivot, worldPositionStays: true);
	}

	private void CreateLowerBraceRoute()
	{
		Transform midRopeAttachment = connections.midRopeAttachment;
		RopeEffect source = ((Sheet != null) ? Sheet.GetComponent<RopeEffect>() : null);
		RopeEffect source2 = ((midRopeAttachment != null) ? midRopeAttachment.GetComponent<RopeEffect>() : null);
		GameObject gameObject = new GameObject("lateen lower-brace fairlead");
		gameObject.SetActive(value: false);
		lowerBraceFairlead = gameObject.transform;
		RefreshFairleadPosition();
		RopeEffect ropeEffect = CreateRopeEffect(gameObject, source2, yardHeelAttachment, sheetFallback: true);
		if (ropeEffect != null)
		{
			lowerBraceRopes.Add(ropeEffect);
		}
		gameObject.SetActive(value: true);
		GameObject gameObject2 = new GameObject(base.name + " lower-brace controller");
		gameObject2.SetActive(value: false);
		RopeEffect ropeEffect2 = CreateRopeEffect(gameObject2, source, lowerBraceFairlead, sheetFallback: false);
		if (ropeEffect2 != null)
		{
			lowerBraceRopes.Add(ropeEffect2);
		}
		LowerBrace = gameObject2.AddComponent<LateenLowerBraceController>();
		LowerBrace.Initialize(this);
		gameObject2.SetActive(value: true);
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
		CopyLineRenderer(component, destination);
		RopeEffect ropeEffect = owner.AddComponent<RopeEffect>();
		ropeEffect.attachment = attachment;
		ropeEffect.sheet = source.sheet | sheetFallback;
		ropeEffect.jibSheet = false;
		ropeEffect.currentRopeLength = 0.45f;
		ropeEffect.ropeWidth = source.ropeWidth;
		ropeEffect.totalRopeLength = 0.86f;
		ropeEffect.curveRate = source.curveRate;
		ropeEffect.attachedSailArea = sail.sailArea;
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

	private void RefreshFairleadPosition()
	{
		if (!(lowerBraceFairlead == null))
		{
			Transform transform = ((connections != null) ? connections.midRopeAttachment : null);
			Transform transform2 = ((transform != null && transform.parent != null) ? transform.parent : mast.transform);
			lowerBraceFairlead.SetParent(transform2, worldPositionStays: true);
			if (transform != null)
			{
				lowerBraceFairlead.position = transform.position;
				lowerBraceFairlead.rotation = transform.rotation;
			}
		}
	}

	private void UpdateRopeTension(float lowerBracePull)
	{
		float currentRopeLength = Mathf.Lerp(0.45f, 0.18f, lowerBracePull);
		float totalRopeLength = Mathf.Lerp(0.86f, 0.98f, lowerBracePull);
		for (int i = 0; i < lowerBraceRopes.Count; i++)
		{
			RopeEffect ropeEffect = lowerBraceRopes[i];
			if (!(ropeEffect == null))
			{
				ropeEffect.currentRopeLength = currentRopeLength;
				ropeEffect.totalRopeLength = totalRopeLength;
			}
		}
	}

	private void ApplyProceduralPose(float lowerBracePull)
	{
		float rotationAmount = Mathf.SmoothStep(0f, 1f, lowerBracePull);
		Quaternion rotation = Quaternion.Slerp(Quaternion.identity, verticalRotation, rotationAmount);
		proceduralPivot.localRotation = rotation;
		Vector2 currentRingPosition = GetCurrentRingPosition(rotation);
		float transferAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, lowerBracePull));
		int startSide = (sideTransferActive ? transferStartSide : yardSide);
		int targetSide = (sideTransferActive ? transferTargetSide : yardSide);
		Vector2 desiredRingPosition;
		if (sideTransferActive)
		{
			Vector2 startPosition = GetRingPosition(startSide, rotation, currentRingPosition);
			Vector2 targetPosition = GetRingPosition(targetSide, rotation, currentRingPosition);
			float arcBlend = 0.5f - 0.5f * Mathf.Cos(transferAmount * Mathf.PI);
			float a = Mathf.Atan2(startPosition.x, startPosition.y) * 57.29578f;
			float targetAngle = Mathf.Atan2(targetPosition.x, targetPosition.y) * 57.29578f;
			float angle = Mathf.LerpAngle(a, targetAngle, arcBlend) * (Mathf.PI / 180f);
			float radius = Mathf.Lerp(startPosition.magnitude, targetPosition.magnitude, arcBlend);
			desiredRingPosition = new Vector2(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
		}
		else
		{
			desiredRingPosition = GetRingPosition(targetSide, rotation, currentRingPosition);
		}
		float sideOffset = desiredRingPosition.x - currentRingPosition.x;
		float aftOffset = desiredRingPosition.y - currentRingPosition.y;
		proceduralPivot.localPosition = mastPivotLocal + starboardAxisLocal * sideOffset + aftAxisLocal * aftOffset;
	}

	private Vector2 GetCurrentRingPosition(Quaternion rotation)
	{
		Vector3 nativeRingOffset = GetNativeRingOffset(rotation);
		return new Vector2(Vector3.Dot(nativeRingOffset, starboardAxisLocal), Vector3.Dot(nativeRingOffset, aftAxisLocal));
	}

	private Vector2 GetRingPosition(int side, Quaternion rotation, Vector2 currentRingPosition)
	{
		if (side == originalYardSide)
		{
			return currentRingPosition;
		}
		Vector2 mirroredPosition = new Vector2((float)side * Mathf.Abs(vanillaRingSide), vanillaRingAft);
		return MatchVanillaSurfaceClearance(rotation, currentRingPosition, mirroredPosition);
	}

	private Vector2 MatchVanillaSurfaceClearance(Quaternion rotation, Vector2 currentRingPosition, Vector2 mirroredPosition)
	{
		if (!hasSurfaceMeasurement)
		{
			return AddClearanceCompensation(mirroredPosition);
		}
		Vector2 outward = ((mirroredPosition.sqrMagnitude > 0.0001f) ? mirroredPosition.normalized : new Vector2((mirroredPosition.x >= 0f) ? 1f : (-1f), 0f));
		float baseline = GetSurfaceClearanceError(rotation, currentRingPosition, mirroredPosition);
		if (Mathf.Abs(baseline) <= 0.0005f)
		{
			return mirroredPosition;
		}
		float lower;
		float upper;
		if (baseline < 0f)
		{
			lower = 0f;
			upper = 0.025f;
			while (upper < 5f && GetSurfaceClearanceError(rotation, currentRingPosition, mirroredPosition + outward * upper) < 0f)
			{
				upper = Mathf.Min(upper * 2f, 5f);
			}
		}
		else
		{
			upper = 0f;
			float maximumInward = Mathf.Max(0f, mirroredPosition.magnitude - 0.001f);
			lower = 0f - Mathf.Min(0.025f, maximumInward);
			while (0f - lower < maximumInward && GetSurfaceClearanceError(rotation, currentRingPosition, mirroredPosition + outward * lower) > 0f)
			{
				lower = 0f - Mathf.Min((0f - lower) * 2f, maximumInward);
			}
			if (GetSurfaceClearanceError(rotation, currentRingPosition, mirroredPosition + outward * lower) > 0f)
			{
				return mirroredPosition;
			}
		}
		for (int i = 0; i < 12; i++)
		{
			float middle = (lower + upper) * 0.5f;
			Vector2 candidate = mirroredPosition + outward * middle;
			if (GetSurfaceClearanceError(rotation, currentRingPosition, candidate) < 0f)
			{
				lower = middle;
			}
			else
			{
				upper = middle;
			}
		}
		return mirroredPosition + outward * ((lower + upper) * 0.5f);
	}

	private float GetSurfaceClearanceError(Quaternion rotation, Vector2 currentRingPosition, Vector2 desiredRingPosition)
	{
		float targetClearance = vanillaSurfaceClearance + 0.15f;
		return MeasureSurfaceClearance(rotation, desiredRingPosition - currentRingPosition) - targetClearance;
	}

	private static Vector2 AddClearanceCompensation(Vector2 mirroredPosition)
	{
		if (mirroredPosition.sqrMagnitude <= 0.0001f)
		{
			return mirroredPosition;
		}
		return mirroredPosition + mirroredPosition.normalized * 0.15f;
	}

	private float MeasureSurfaceClearance(Quaternion rotation, Vector2 ringTranslation)
	{
		if (mastContactCollider == null || yardContactSurfacePointsLocal.Length == 0)
		{
			return 0f;
		}
		Vector3 localTranslation = starboardAxisLocal * ringTranslation.x + aftAxisLocal * ringTranslation.y;
		float mastRadius = GetCapsuleRadialRadius(mastContactCollider);
		float clearance = float.PositiveInfinity;
		for (int i = 0; i < yardContactSurfacePointsLocal.Length; i++)
		{
			Vector3 pointLocal = mastPivotLocal + rotation * (yardContactSurfacePointsLocal[i] - mastPivotLocal) + localTranslation;
			Vector3 pointWorld = base.transform.TransformPoint(pointLocal);
			Vector3 mastCenterWorld = GetCapsuleCenterlinePoint(mastContactCollider, pointWorld);
			clearance = Mathf.Min(clearance, (pointWorld - mastCenterWorld).magnitude - mastRadius);
		}
		return clearance;
	}

	private Vector3 GetNativeRingOffset(Quaternion rotation)
	{
		return mastPivotLocal + rotation * (yardContactCenterLocal - mastPivotLocal) - mastCenterLocal;
	}

	private void UpdateAutomaticSideTransfer(float lowerBracePull)
	{
		if (lowerBracePull <= 0.7f)
		{
			tackChangeArmed = true;
			sideTransferActive = false;
		}
		if (tackChangeArmed && lowerBracePull > previousLowerBracePull + 0.0001f)
		{
			transferStartSide = yardSide;
			transferTargetSide = -yardSide;
			sideTransferActive = true;
			tackChangeArmed = false;
		}
		if (sideTransferActive && lowerBracePull >= 0.9999f)
		{
			yardSide = transferTargetSide;
			sideTransferActive = false;
		}
		previousLowerBracePull = lowerBracePull;
	}

	internal bool IsBadTack()
	{
		Rigidbody rigidbody = ((sail != null) ? sail.shipRigidbody : null);
		if (rigidbody == null || sail.apparentWind.sqrMagnitude < 0.001f)
		{
			return false;
		}
		float num = Vector3.Dot(-sail.apparentWind.normalized, rigidbody.transform.right);
		if (Mathf.Abs(num) >= 0.05f)
		{
			return ((num > 0f) ? 1 : (-1)) == yardSide;
		}
		return false;
	}

	internal string GetPersistenceKey()
	{
		SaveableObject componentInParent = GetComponentInParent<SaveableObject>();
		if (componentInParent == null || mast == null || sail == null)
		{
			return null;
		}
		return $"{componentInParent.sceneIndex}:{mast.orderIndex}:{sail.mastOrder}:{sail.prefabIndex}";
	}

	internal void RestoreYardSide(int savedSide)
	{
		yardSide = ((savedSide >= 0) ? 1 : (-1));
		sideTransferActive = false;
		previousLowerBracePull = Mathf.Clamp01(LowerBracePull);
		tackChangeArmed = previousLowerBracePull <= 0.7f;
		if (initialized && proceduralPivot != null)
		{
			ApplyProceduralPose(previousLowerBracePull);
		}
	}

	private Transform FindVisualRoot()
	{
		if (sail == null || sail.cloth == null)
		{
			return null;
		}
		Transform parent = sail.cloth.transform.parent;
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
		MeshFilter[] componentsInChildren = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			if (meshFilter.sharedMesh == null)
			{
				continue;
			}
			string text = meshFilter.name.ToLowerInvariant();
			if (text.Contains("boom") || text.Contains("cylinder"))
			{
				Vector3 vector = Vector3.Scale(meshFilter.sharedMesh.bounds.size, Abs(meshFilter.transform.lossyScale));
				float num2 = Mathf.Max(vector.x, vector.y, vector.z);
				if (num2 > num)
				{
					result = meshFilter;
					num = num2;
				}
			}
		}
		return result;
	}

	private Vector3 FindMastCenterlinePoint(Vector3 contactWorld, Vector3 mastAxisWorld, out CapsuleCollider selectedCollider)
	{
		selectedCollider = null;
		Vector3 mastOriginWorld = ((mast != null) ? mast.transform.position : base.transform.position);
		Vector3 result = mastOriginWorld + mastAxisWorld * Vector3.Dot(contactWorld - mastOriginWorld, mastAxisWorld);
		if (mast.mastCols == null)
		{
			return result;
		}
		float nearestDistance = float.PositiveInfinity;
		for (int i = 0; i < mast.mastCols.Length; i++)
		{
			CapsuleCollider capsuleCollider = mast.mastCols[i];
			if (!(capsuleCollider == null))
			{
				Vector3 candidate = GetCapsuleCenterlinePoint(capsuleCollider, contactWorld);
				float distance = (candidate - contactWorld).sqrMagnitude;
				if (distance < nearestDistance)
				{
					result = candidate;
					selectedCollider = capsuleCollider;
					nearestDistance = distance;
				}
			}
		}
		return result;
	}

	private static Vector3 GetCapsuleCenterlinePoint(CapsuleCollider collider, Vector3 contactWorld)
	{
		Vector3 localAxis = ((collider.direction == 0) ? Vector3.right : ((collider.direction == 2) ? Vector3.forward : Vector3.up));
		Vector3 axisWorld = collider.transform.TransformDirection(localAxis).normalized;
		Vector3 centerWorld = collider.transform.TransformPoint(collider.center);
		Vector3 scale = Abs(collider.transform.lossyScale);
		float axisScale = ((collider.direction == 0) ? scale.x : ((collider.direction == 2) ? scale.z : scale.y));
		float halfSegment = Mathf.Max(0f, collider.height * 0.5f - collider.radius) * axisScale;
		float distance = Mathf.Clamp(Vector3.Dot(contactWorld - centerWorld, axisWorld), 0f - halfSegment, halfSegment);
		return centerWorld + axisWorld * distance;
	}

	private void AnalyzeYardGeometry(Vector3 mastAxisLocal, out Vector3 yardAxisLocal)
	{
		Mesh mesh = yardMesh.sharedMesh;
		Vector3[] vertices = mesh.vertices;
		Vector3[] points = new Vector3[vertices.Length];
		for (int i = 0; i < vertices.Length; i++)
		{
			Vector3 world = yardMesh.transform.TransformPoint(vertices[i]);
			points[i] = base.transform.InverseTransformPoint(world);
		}
		Vector3 center = Average(points);
		yardAxisLocal = FindPrincipalAxis(points, center);
		float minimum = float.PositiveInfinity;
		float maximum = float.NegativeInfinity;
		for (int j = 0; j < points.Length; j++)
		{
			float projection = Vector3.Dot(points[j] - center, yardAxisLocal);
			minimum = Mathf.Min(minimum, projection);
			maximum = Mathf.Max(maximum, projection);
		}
		Vector3 firstTip = AverageTip(points, center, yardAxisLocal, minimum, maximum, first: true);
		Vector3 secondTip = AverageTip(points, center, yardAxisLocal, minimum, maximum, first: false);
		heelPositionLocal = ((Vector3.Dot(firstTip, mastAxisLocal) <= Vector3.Dot(secondTip, mastAxisLocal)) ? firstTip : secondTip);
		float contactProjection = Vector3.Dot(mastPivotLocal - center, yardAxisLocal);
		yardContactCenterLocal = center + yardAxisLocal * Mathf.Clamp(contactProjection, minimum, maximum);
		yardContactSurfacePointsLocal = SliceMeshAtPlane(points, mesh.triangles, yardContactCenterLocal, yardAxisLocal);
	}

	private static Vector3[] SliceMeshAtPlane(Vector3[] points, int[] triangles, Vector3 planePoint, Vector3 planeNormal)
	{
		List<Vector3> intersections = new List<Vector3>();
		for (int i = 0; i + 2 < triangles.Length; i += 3)
		{
			Vector3 first = points[triangles[i]];
			Vector3 second = points[triangles[i + 1]];
			Vector3 third = points[triangles[i + 2]];
			AddPlaneEdgeIntersections(intersections, first, second, planePoint, planeNormal);
			AddPlaneEdgeIntersections(intersections, second, third, planePoint, planeNormal);
			AddPlaneEdgeIntersections(intersections, third, first, planePoint, planeNormal);
		}
		if (intersections.Count == 0)
		{
			float nearestDistance = float.PositiveInfinity;
			for (int j = 0; j < points.Length; j++)
			{
				nearestDistance = Mathf.Min(nearestDistance, Mathf.Abs(Vector3.Dot(points[j] - planePoint, planeNormal)));
			}
			for (int k = 0; k < points.Length; k++)
			{
				if (Mathf.Abs(Vector3.Dot(points[k] - planePoint, planeNormal)) <= nearestDistance + 0.0001f)
				{
					AddUniquePoint(intersections, points[k]);
				}
			}
		}
		return intersections.ToArray();
	}

	private static void AddPlaneEdgeIntersections(List<Vector3> intersections, Vector3 first, Vector3 second, Vector3 planePoint, Vector3 planeNormal)
	{
		float firstDistance = Vector3.Dot(first - planePoint, planeNormal);
		float secondDistance = Vector3.Dot(second - planePoint, planeNormal);
		if (Mathf.Abs(firstDistance) <= 0.0001f)
		{
			AddUniquePoint(intersections, first);
		}
		if (Mathf.Abs(secondDistance) <= 0.0001f)
		{
			AddUniquePoint(intersections, second);
		}
		if ((firstDistance < -0.0001f && secondDistance > 0.0001f) || (firstDistance > 0.0001f && secondDistance < -0.0001f))
		{
			float amount = firstDistance / (firstDistance - secondDistance);
			AddUniquePoint(intersections, Vector3.Lerp(first, second, amount));
		}
	}

	private static void AddUniquePoint(List<Vector3> points, Vector3 candidate)
	{
		float minimumSeparation = 9.999999E-09f;
		for (int i = 0; i < points.Count; i++)
		{
			if ((points[i] - candidate).sqrMagnitude <= minimumSeparation)
			{
				return;
			}
		}
		points.Add(candidate);
	}

	private static Vector3 Average(Vector3[] points)
	{
		Vector3 sum = Vector3.zero;
		for (int i = 0; i < points.Length; i++)
		{
			sum += points[i];
		}
		return sum / points.Length;
	}

	private static Vector3 FindPrincipalAxis(Vector3[] points, Vector3 center)
	{
		float xx = 0f;
		float xy = 0f;
		float xz = 0f;
		float yy = 0f;
		float yz = 0f;
		float zz = 0f;
		for (int i = 0; i < points.Length; i++)
		{
			Vector3 offset = points[i] - center;
			xx += offset.x * offset.x;
			xy += offset.x * offset.y;
			xz += offset.x * offset.z;
			yy += offset.y * offset.y;
			yz += offset.y * offset.z;
			zz += offset.z * offset.z;
		}
		Vector3 axis = ((xx >= yy && xx >= zz) ? Vector3.right : ((yy >= zz) ? Vector3.up : Vector3.forward));
		for (int j = 0; j < 12; j++)
		{
			Vector3 next = new Vector3(xx * axis.x + xy * axis.y + xz * axis.z, xy * axis.x + yy * axis.y + yz * axis.z, xz * axis.x + yz * axis.y + zz * axis.z);
			if (next.sqrMagnitude <= 1E-06f)
			{
				break;
			}
			axis = next.normalized;
		}
		return axis;
	}

	private static Vector3 AverageTip(Vector3[] points, Vector3 center, Vector3 axis, float minimum, float maximum, bool first)
	{
		float extreme = (first ? minimum : maximum);
		float tolerance = Mathf.Max((maximum - minimum) * 0.01f, 0.001f);
		Vector3 sum = Vector3.zero;
		int count = 0;
		for (int i = 0; i < points.Length; i++)
		{
			float projection = Vector3.Dot(points[i] - center, axis);
			if ((first && projection <= minimum + tolerance) || (!first && projection >= maximum - tolerance))
			{
				sum += points[i];
				count++;
			}
		}
		Vector3 obj = ((count > 0) ? (sum / count) : (center + axis * extreme));
		float tipProjection = Vector3.Dot(obj - center, axis);
		return obj + axis * (extreme - tipProjection);
	}

	private static Vector3 Abs(Vector3 value)
	{
		return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
	}

	private static float GetCapsuleRadialRadius(CapsuleCollider collider)
	{
		Vector3 scale = Abs(collider.transform.lossyScale);
		return collider.direction switch
		{
			0 => collider.radius * Mathf.Max(scale.y, scale.z), 
			2 => collider.radius * Mathf.Max(scale.x, scale.y), 
			_ => collider.radius * Mathf.Max(scale.x, scale.z), 
		};
	}

	private void OnDestroy()
	{
		LateenControlRegistry.Unregister(this);
		if (LowerBrace != null)
		{
			UnityEngine.Object.Destroy(LowerBrace.gameObject);
		}
		if (lowerBraceFairlead != null)
		{
			UnityEngine.Object.Destroy(lowerBraceFairlead.gameObject);
		}
	}
}
