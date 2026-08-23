using System;
using System.Collections.Generic;
using UnityEngine;

namespace SailRebalance
{
	internal sealed partial class LateenYardRig : MonoBehaviour
	{
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
			Vector3 vector = (this.hinge.axis.sqrMagnitude > AxisMagnitudeEpsilon) ? this.hinge.axis.normalized : Vector3.forward;
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
			return this.yardMesh == null || this.yardMesh.sharedMesh == null || this.yardMesh.sharedMesh != this.measuredYardMesh || (this.yardMesh.transform.lossyScale - this.measuredYardScale).sqrMagnitude > GeometryChangeEpsilon || !Mathf.Approximately(this.sail.installHeight, this.measuredInstallHeight);
		}

		private void CaptureGeometrySignature()
		{
			this.measuredYardMesh = this.yardMesh.sharedMesh;
			this.measuredYardScale = this.yardMesh.transform.lossyScale;
			this.measuredInstallHeight = this.sail.installHeight;
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
			// Preserve shipyard scaling while removing only the procedural yard pose.
			if (this.proceduralPivot != null)
			{
				this.proceduralPivot.localPosition = this.mastPivotLocal;
				this.proceduralPivot.localRotation = Quaternion.identity;
				this.proceduralPivot.localScale = Vector3.one;
			}
			Transform parent = (this.originalVisualParent != null) ? this.originalVisualParent : base.transform;
			if (parent != null)
			{
				this.visualRoot.SetParent(parent, true);
				this.visualRoot.SetSiblingIndex(Mathf.Clamp(this.originalVisualSiblingIndex, 0, parent.childCount - 1));
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
				UnityEngine.Object.Destroy(this.proceduralPivot.gameObject);
				this.proceduralPivot = null;
			}
		}

		private void ApplyProceduralPose(float bracePull)
		{
			float yardApproach = 1f - bracePull;
			float t = Mathf.SmoothStep(0f, 1f, yardApproach);
			Quaternion quaternion = Quaternion.Slerp(Quaternion.identity, this.verticalRotation, t);
			this.proceduralPivot.localRotation = quaternion;
			Vector2 currentRingPosition = this.GetCurrentRingPosition(quaternion);
			float transferProgress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MastPassMovementStart, 1f, yardApproach));
			int side = this.sideTransferActive ? this.transferStartSide : this.yardSide;
			int side2 = this.sideTransferActive ? this.transferTargetSide : this.yardSide;
			Vector2 ringPosition3;
			if (this.sideTransferActive)
			{
				Vector2 ringPosition = this.GetRingPosition(side, quaternion, currentRingPosition);
				Vector2 ringPosition2 = this.GetRingPosition(side2, quaternion, currentRingPosition);
				float t2 = 0.5f - 0.5f * Mathf.Cos(transferProgress * Mathf.PI);
				float a = Mathf.Atan2(ringPosition.x, ringPosition.y) * Mathf.Rad2Deg;
				float b = Mathf.Atan2(ringPosition2.x, ringPosition2.y) * Mathf.Rad2Deg;
				float f = Mathf.LerpAngle(a, b, t2) * Mathf.Deg2Rad;
				float num2 = Mathf.Lerp(ringPosition.magnitude, ringPosition2.magnitude, t2);
				ringPosition3 = new Vector2(Mathf.Sin(f) * num2, Mathf.Cos(f) * num2);
			}
			else
			{
				ringPosition3 = this.GetRingPosition(side2, quaternion, currentRingPosition);
			}
			float d = ringPosition3.x - currentRingPosition.x;
			float d2 = ringPosition3.y - currentRingPosition.y;
			this.proceduralPivot.localPosition = this.mastPivotLocal + this.starboardAxisLocal * d + this.aftAxisLocal * d2;
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
				if (Mathf.Abs(first[i] - second[i]) > ClearanceCachePoseEpsilon)
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
			Vector2 a = (mirroredPosition.sqrMagnitude > ControlEpsilon) ? mirroredPosition.normalized : new Vector2((mirroredPosition.x >= 0f) ? 1f : -1f, 0f);
			float surfaceClearanceError = this.GetSurfaceClearanceError(rotation, currentRingPosition, mirroredPosition);
			if (Mathf.Abs(surfaceClearanceError) <= SurfaceClearanceTolerance)
			{
				return mirroredPosition;
			}
			float num;
			float num2;
			if (surfaceClearanceError < 0f)
			{
				num = 0f;
				for (num2 = SurfaceSearchStep; num2 < SurfaceSearchLimit; num2 = Mathf.Min(num2 * 2f, SurfaceSearchLimit))
				{
					if (this.GetSurfaceClearanceError(rotation, currentRingPosition, mirroredPosition + a * num2) >= 0f)
					{
						break;
					}
				}
			}
			else
			{
				num2 = 0f;
				float num3 = Mathf.Max(0f, mirroredPosition.magnitude - SurfaceSearchMinimumRadius);
				num = -Mathf.Min(SurfaceSearchStep, num3);
				while (-num < num3 && this.GetSurfaceClearanceError(rotation, currentRingPosition, mirroredPosition + a * num) > 0f)
				{
					num = -Mathf.Min(-num * 2f, num3);
				}
				if (this.GetSurfaceClearanceError(rotation, currentRingPosition, mirroredPosition + a * num) > 0f)
				{
					return mirroredPosition;
				}
			}
			for (int i = 0; i < SurfaceSearchIterations; i++)
			{
				float num4 = (num + num2) * 0.5f;
				Vector2 desiredRingPosition = mirroredPosition + a * num4;
				if (this.GetSurfaceClearanceError(rotation, currentRingPosition, desiredRingPosition) < 0f)
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

		private float GetSurfaceClearanceError(Quaternion rotation, Vector2 currentRingPosition, Vector2 desiredRingPosition)
		{
			float num = this.vanillaSurfaceClearance + NonVanillaClearanceCompensation;
			return this.MeasureSurfaceClearance(rotation, desiredRingPosition - currentRingPosition) - num;
		}

		private static Vector2 AddClearanceCompensation(Vector2 mirroredPosition)
		{
			if (mirroredPosition.sqrMagnitude <= ControlEpsilon)
			{
				return mirroredPosition;
			}
			return mirroredPosition + mirroredPosition.normalized * NonVanillaClearanceCompensation;
		}

		private float MeasureSurfaceClearance(Quaternion rotation, Vector2 ringTranslation)
		{
			if (this.mastContactCollider == null || this.yardContactSurfacePointsLocal.Length == 0)
			{
				return 0f;
			}
			Vector3 b = this.starboardAxisLocal * ringTranslation.x + this.aftAxisLocal * ringTranslation.y;
			LateenYardRig.CapsuleWorldGeometry capsuleWorldGeometry = LateenYardRig.CreateCapsuleWorldGeometry(this.mastContactCollider);
			float num = float.PositiveInfinity;
			for (int i = 0; i < this.yardContactSurfacePointsLocal.Length; i++)
			{
				Vector3 position = this.mastPivotLocal + rotation * (this.yardContactSurfacePointsLocal[i] - this.mastPivotLocal) + b;
				Vector3 vector = base.transform.TransformPoint(position);
				Vector3 centerlinePoint = capsuleWorldGeometry.GetCenterlinePoint(vector);
				num = Mathf.Min(num, (vector - centerlinePoint).magnitude - capsuleWorldGeometry.Radius);
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
					if (Mathf.Abs(Vector3.Dot(points[j] - planePoint, planeNormal)) <= num2 + PlaneIntersectionEpsilon)
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
			if (Mathf.Abs(num) <= PlaneIntersectionEpsilon)
			{
				LateenYardRig.AddUniquePoint(intersections, first);
			}
			if (Mathf.Abs(num2) <= PlaneIntersectionEpsilon)
			{
				LateenYardRig.AddUniquePoint(intersections, second);
			}
			if ((num >= -PlaneIntersectionEpsilon || num2 <= PlaneIntersectionEpsilon) && (num <= PlaneIntersectionEpsilon || num2 >= -PlaneIntersectionEpsilon))
			{
				return;
			}
			float t = num / (num - num2);
			LateenYardRig.AddUniquePoint(intersections, Vector3.Lerp(first, second, t));
		}

		private static void AddUniquePoint(List<Vector3> points, Vector3 candidate)
		{
			float num = PlaneIntersectionEpsilon * PlaneIntersectionEpsilon;
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
			for (int j = 0; j < PrincipalAxisIterations; j++)
			{
				Vector3 vector3 = new Vector3(num * vector2.x + num2 * vector2.y + num3 * vector2.z, num2 * vector2.x + num4 * vector2.y + num5 * vector2.z, num3 * vector2.x + num5 * vector2.y + num6 * vector2.z);
				if (vector3.sqrMagnitude <= GeometryChangeEpsilon)
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
			float num2 = Mathf.Max((maximum - minimum) * TipSampleFraction, MinimumTipTolerance);
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
				float num = ClearanceCachePoseEpsilon * ClearanceCachePoseEpsilon;
				return Mathf.Abs(Quaternion.Dot(this.YardRotation, other.YardRotation)) >= 1f - ClearanceCacheRotationDotEpsilon && (this.CurrentRingPosition - other.CurrentRingPosition).sqrMagnitude <= num && (this.MirroredRingPosition - other.MirroredRingPosition).sqrMagnitude <= num && LateenYardRig.MatrixApproximatelyEquals(this.RigToMastLocal, other.RigToMastLocal) && (this.MastLossyScale - other.MastLossyScale).sqrMagnitude <= num && (this.ColliderCenter - other.ColliderCenter).sqrMagnitude <= num && Mathf.Abs(this.ColliderRadius - other.ColliderRadius) <= ClearanceCachePoseEpsilon && Mathf.Abs(this.ColliderHeight - other.ColliderHeight) <= ClearanceCachePoseEpsilon && this.ColliderDirection == other.ColliderDirection;
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
}
