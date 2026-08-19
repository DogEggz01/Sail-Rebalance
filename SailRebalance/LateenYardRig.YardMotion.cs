using System;
using System.Collections.Generic;
using UnityEngine;

namespace SailRebalance;

internal sealed partial class LateenYardRig
{
    private const float MastPassMovementStart = 0.85f;
    private const float NonVanillaClearanceCompensation = 0.15f;
    private const float GeometryChangeEpsilon = 0.000001f;
    private const float PlaneIntersectionEpsilon = 0.0001f;
    private const float SurfaceClearanceTolerance = 0.0005f;
    private const float SurfaceSearchStep = 0.025f;
    private const float SurfaceSearchLimit = 5f;
    private const float SurfaceSearchMinimumRadius = 0.001f;
    private const float AxisMagnitudeEpsilon = 0.001f;
    private const float MinimumTipTolerance = 0.001f;
    private const float ClearanceCachePoseEpsilon = 0.00001f;
    private const float ClearanceCacheRotationDotEpsilon = 0.000001f;
    private const int SurfaceSearchIterations = 12;
    private const int PrincipalAxisIterations = 12;
    private const float TipSampleFraction = 0.01f;

    private MeshFilter yardMesh;
    private Transform visualRoot;
    private Transform proceduralPivot;
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
    private ClearancePoseSnapshot cachedClearancePose;
    private Vector2 cachedClearancePosition;

    private readonly struct ClearancePoseSnapshot
    {
        internal readonly Quaternion YardRotation;
        internal readonly Vector2 CurrentRingPosition;
        internal readonly Vector2 MirroredRingPosition;
        internal readonly Matrix4x4 RigToMastLocal;
        internal readonly Vector3 MastLossyScale;
        internal readonly Vector3 ColliderCenter;
        internal readonly float ColliderRadius;
        internal readonly float ColliderHeight;
        internal readonly int ColliderDirection;

        internal ClearancePoseSnapshot(
            Quaternion yardRotation,
            Vector2 currentRingPosition,
            Vector2 mirroredRingPosition,
            Matrix4x4 rigToMastLocal,
            Vector3 mastLossyScale,
            Vector3 colliderCenter,
            float colliderRadius,
            float colliderHeight,
            int colliderDirection)
        {
            YardRotation = yardRotation;
            CurrentRingPosition = currentRingPosition;
            MirroredRingPosition = mirroredRingPosition;
            RigToMastLocal = rigToMastLocal;
            MastLossyScale = mastLossyScale;
            ColliderCenter = colliderCenter;
            ColliderRadius = colliderRadius;
            ColliderHeight = colliderHeight;
            ColliderDirection = colliderDirection;
        }

        internal bool ApproximatelyEquals(ClearancePoseSnapshot other)
        {
            float positionEpsilonSquared =
                ClearanceCachePoseEpsilon * ClearanceCachePoseEpsilon;

            return Mathf.Abs(Quaternion.Dot(YardRotation, other.YardRotation)) >=
                    1f - ClearanceCacheRotationDotEpsilon &&
                (CurrentRingPosition - other.CurrentRingPosition).sqrMagnitude <=
                    positionEpsilonSquared &&
                (MirroredRingPosition - other.MirroredRingPosition).sqrMagnitude <=
                    positionEpsilonSquared &&
                MatrixApproximatelyEquals(RigToMastLocal, other.RigToMastLocal) &&
                (MastLossyScale - other.MastLossyScale).sqrMagnitude <=
                    positionEpsilonSquared &&
                (ColliderCenter - other.ColliderCenter).sqrMagnitude <=
                    positionEpsilonSquared &&
                Mathf.Abs(ColliderRadius - other.ColliderRadius) <=
                    ClearanceCachePoseEpsilon &&
                Mathf.Abs(ColliderHeight - other.ColliderHeight) <=
                    ClearanceCachePoseEpsilon &&
                ColliderDirection == other.ColliderDirection;
        }
    }

    private readonly struct CapsuleWorldGeometry
    {
        internal readonly Vector3 Center;
        internal readonly Vector3 Axis;
        internal readonly float HalfSegment;
        internal readonly float Radius;

        internal CapsuleWorldGeometry(
            Vector3 center,
            Vector3 axis,
            float halfSegment,
            float radius)
        {
            Center = center;
            Axis = axis;
            HalfSegment = halfSegment;
            Radius = radius;
        }

        internal Vector3 GetCenterlinePoint(Vector3 point)
        {
            float distance = Mathf.Clamp(
                Vector3.Dot(point - Center, Axis),
                -HalfSegment,
                HalfSegment);

            return Center + Axis * distance;
        }
    }

    private void ConfigureAxes()
    {
        Rigidbody ship = sail.shipRigidbody != null
            ? sail.shipRigidbody
            : mast.shipRigidbody;

        if (ship != null)
        {
            starboardAxisLocal = transform
                .InverseTransformDirection(ship.transform.right)
                .normalized;

            aftAxisLocal = transform
                .InverseTransformDirection(-ship.transform.forward)
                .normalized;
            return;
        }

        starboardAxisLocal = Vector3.up;
        aftAxisLocal = -Vector3.right;
    }

    private void ConfigureYardGeometry(bool initializeSide)
    {
        InvalidateClearanceCache();

        Vector3 mastAxisLocal = hinge.axis.sqrMagnitude > AxisMagnitudeEpsilon
            ? hinge.axis.normalized
            : Vector3.forward;

        Vector3 mastAxisWorld = transform
            .TransformDirection(mastAxisLocal)
            .normalized;

        mastPivotLocal = hinge.anchor;
        AnalyzeYardGeometry(mastAxisLocal, out Vector3 yardAxisLocal);

        if (Vector3.Dot(yardAxisLocal, mastAxisLocal) < 0f)
            yardAxisLocal = -yardAxisLocal;

        verticalRotation = Quaternion.FromToRotation(
            yardAxisLocal,
            mastAxisLocal);

        Vector3 contactWorld = transform.TransformPoint(yardContactCenterLocal);
        Vector3 mastCenterWorld = FindMastCenterlinePoint(
            contactWorld,
            mastAxisWorld,
            out mastContactCollider);

        mastCenterLocal = transform.InverseTransformPoint(mastCenterWorld);

        Vector3 vanillaRingOffset = yardContactCenterLocal - mastCenterLocal;
        vanillaRingSide = Vector3.Dot(vanillaRingOffset, starboardAxisLocal);
        vanillaRingAft = Vector3.Dot(vanillaRingOffset, aftAxisLocal);

        hasSurfaceMeasurement =
            mastContactCollider != null &&
            yardContactSurfacePointsLocal.Length != 0;

        vanillaSurfaceClearance = hasSurfaceMeasurement
            ? MeasureSurfaceClearance(Quaternion.identity, Vector2.zero)
            : 0f;

        originalYardSide = vanillaRingSide >= 0f ? 1 : -1;
        if (initializeSide)
            yardSide = originalYardSide;

        UpdateYardHeelAttachment();
    }

    private void RefreshGeometryIfNeeded()
    {
        if (!YardGeometryChanged())
            return;

        MeshFilter currentYardMesh = FindYardMesh(visualRoot);
        if (currentYardMesh == null || currentYardMesh.sharedMesh == null)
            return;

        yardMesh = currentYardMesh;
        proceduralPivot.localPosition = hinge.anchor;
        proceduralPivot.localRotation = Quaternion.identity;

        ConfigureAxes();
        ConfigureYardGeometry(initializeSide: false);
        CaptureGeometrySignature();
        ApplyProceduralPose(Mathf.Clamp01(BracePull));
    }

    private bool YardGeometryChanged()
    {
        if (yardMesh == null || yardMesh.sharedMesh == null)
            return true;

        if (yardMesh.sharedMesh != measuredYardMesh)
            return true;

        if ((yardMesh.transform.lossyScale - measuredYardScale).sqrMagnitude >
            GeometryChangeEpsilon)
        {
            return true;
        }

        return !Mathf.Approximately(sail.installHeight, measuredInstallHeight);
    }

    private void CaptureGeometrySignature()
    {
        measuredYardMesh = yardMesh.sharedMesh;
        measuredYardScale = yardMesh.transform.lossyScale;
        measuredInstallHeight = sail.installHeight;
    }

    private void CreateProceduralPivot()
    {
        var pivotObject = new GameObject("lateen brace pivot");
        proceduralPivot = pivotObject.transform;
        proceduralPivot.SetParent(transform, false);
        proceduralPivot.localPosition = mastPivotLocal;
        proceduralPivot.localRotation = Quaternion.identity;
        proceduralPivot.localScale = Vector3.one;
        visualRoot.SetParent(proceduralPivot, true);
    }

    private void ApplyProceduralPose(float bracePull)
    {
        float rotationAmount = Mathf.SmoothStep(0f, 1f, bracePull);
        Quaternion rotation = Quaternion.Slerp(
            Quaternion.identity,
            verticalRotation,
            rotationAmount);

        proceduralPivot.localRotation = rotation;

        Vector2 currentRingPosition = GetCurrentRingPosition(rotation);
        float transferAmount = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(
                MastPassMovementStart,
                1f,
                bracePull));

        int startSide = sideTransferActive ? transferStartSide : yardSide;
        int targetSide = sideTransferActive ? transferTargetSide : yardSide;
        Vector2 desiredRingPosition;

        if (sideTransferActive)
        {
            Vector2 startPosition = GetRingPosition(
                startSide,
                rotation,
                currentRingPosition);

            Vector2 targetPosition = GetRingPosition(
                targetSide,
                rotation,
                currentRingPosition);

            float arcBlend = 0.5f -
                0.5f * Mathf.Cos(transferAmount * Mathf.PI);

            float startAngle = Mathf.Atan2(
                startPosition.x,
                startPosition.y) * Mathf.Rad2Deg;

            float targetAngle = Mathf.Atan2(
                targetPosition.x,
                targetPosition.y) * Mathf.Rad2Deg;

            float angle = Mathf.LerpAngle(
                startAngle,
                targetAngle,
                arcBlend) * Mathf.Deg2Rad;

            float radius = Mathf.Lerp(
                startPosition.magnitude,
                targetPosition.magnitude,
                arcBlend);

            desiredRingPosition = new Vector2(
                Mathf.Sin(angle) * radius,
                Mathf.Cos(angle) * radius);
        }
        else
        {
            desiredRingPosition = GetRingPosition(
                targetSide,
                rotation,
                currentRingPosition);
        }

        float sideOffset = desiredRingPosition.x - currentRingPosition.x;
        float aftOffset = desiredRingPosition.y - currentRingPosition.y;

        proceduralPivot.localPosition =
            mastPivotLocal +
            starboardAxisLocal * sideOffset +
            aftAxisLocal * aftOffset;
    }

    private Vector2 GetCurrentRingPosition(Quaternion rotation)
    {
        Vector3 nativeRingOffset = GetNativeRingOffset(rotation);
        return new Vector2(
            Vector3.Dot(nativeRingOffset, starboardAxisLocal),
            Vector3.Dot(nativeRingOffset, aftAxisLocal));
    }

    private Vector2 GetRingPosition(
        int side,
        Quaternion rotation,
        Vector2 currentRingPosition)
    {
        if (side == originalYardSide)
            return currentRingPosition;

        var mirroredPosition = new Vector2(
            side * Mathf.Abs(vanillaRingSide),
            vanillaRingAft);

        return MatchVanillaSurfaceClearance(
            rotation,
            currentRingPosition,
            mirroredPosition);
    }

    private Vector2 MatchVanillaSurfaceClearance(
        Quaternion rotation,
        Vector2 currentRingPosition,
        Vector2 mirroredPosition)
    {
        if (!hasSurfaceMeasurement)
            return AddClearanceCompensation(mirroredPosition);

        ClearancePoseSnapshot currentPose = CaptureClearancePose(
            rotation,
            currentRingPosition,
            mirroredPosition);

        if (hasCachedClearancePosition &&
            cachedClearancePose.ApproximatelyEquals(currentPose))
        {
            return cachedClearancePosition;
        }

        Vector2 result = CalculateSurfaceClearancePosition(
            rotation,
            currentRingPosition,
            mirroredPosition);

        cachedClearancePose = currentPose;
        cachedClearancePosition = result;
        hasCachedClearancePosition = true;
        return result;
    }

    private ClearancePoseSnapshot CaptureClearancePose(
        Quaternion rotation,
        Vector2 currentRingPosition,
        Vector2 mirroredPosition)
    {
        Transform mastTransform = mastContactCollider.transform;

        return new ClearancePoseSnapshot(
            rotation,
            currentRingPosition,
            mirroredPosition,
            mastTransform.worldToLocalMatrix * transform.localToWorldMatrix,
            mastTransform.lossyScale,
            mastContactCollider.center,
            mastContactCollider.radius,
            mastContactCollider.height,
            mastContactCollider.direction);
    }

    private static bool MatrixApproximatelyEquals(
        Matrix4x4 first,
        Matrix4x4 second)
    {
        for (int i = 0; i < 16; i++)
        {
            if (Mathf.Abs(first[i] - second[i]) > ClearanceCachePoseEpsilon)
                return false;
        }

        return true;
    }

    private void InvalidateClearanceCache()
    {
        hasCachedClearancePosition = false;
    }

    private Vector2 CalculateSurfaceClearancePosition(
        Quaternion rotation,
        Vector2 currentRingPosition,
        Vector2 mirroredPosition)
    {
        Vector2 outward = mirroredPosition.sqrMagnitude > PlaneIntersectionEpsilon
            ? mirroredPosition.normalized
            : new Vector2(mirroredPosition.x >= 0f ? 1f : -1f, 0f);

        float baseline = GetSurfaceClearanceError(
            rotation,
            currentRingPosition,
            mirroredPosition);

        if (Mathf.Abs(baseline) <= SurfaceClearanceTolerance)
            return mirroredPosition;

        float lower;
        float upper;

        if (baseline < 0f)
        {
            lower = 0f;
            upper = SurfaceSearchStep;

            while (upper < SurfaceSearchLimit &&
                GetSurfaceClearanceError(
                    rotation,
                    currentRingPosition,
                    mirroredPosition + outward * upper) < 0f)
            {
                upper = Mathf.Min(upper * 2f, SurfaceSearchLimit);
            }
        }
        else
        {
            upper = 0f;
            float maximumInward = Mathf.Max(
                0f,
                mirroredPosition.magnitude - SurfaceSearchMinimumRadius);

            lower = -Mathf.Min(SurfaceSearchStep, maximumInward);

            while (-lower < maximumInward &&
                GetSurfaceClearanceError(
                    rotation,
                    currentRingPosition,
                    mirroredPosition + outward * lower) > 0f)
            {
                lower = -Mathf.Min(-lower * 2f, maximumInward);
            }

            if (GetSurfaceClearanceError(
                    rotation,
                    currentRingPosition,
                    mirroredPosition + outward * lower) > 0f)
            {
                return mirroredPosition;
            }
        }

        for (int i = 0; i < SurfaceSearchIterations; i++)
        {
            float middle = (lower + upper) * 0.5f;
            Vector2 candidate = mirroredPosition + outward * middle;

            if (GetSurfaceClearanceError(
                    rotation,
                    currentRingPosition,
                    candidate) < 0f)
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

    private float GetSurfaceClearanceError(
        Quaternion rotation,
        Vector2 currentRingPosition,
        Vector2 desiredRingPosition)
    {
        float targetClearance =
            vanillaSurfaceClearance + NonVanillaClearanceCompensation;

        return MeasureSurfaceClearance(
            rotation,
            desiredRingPosition - currentRingPosition) - targetClearance;
    }

    private static Vector2 AddClearanceCompensation(Vector2 mirroredPosition)
    {
        if (mirroredPosition.sqrMagnitude <= PlaneIntersectionEpsilon)
            return mirroredPosition;

        return mirroredPosition +
            mirroredPosition.normalized * NonVanillaClearanceCompensation;
    }

    private float MeasureSurfaceClearance(
        Quaternion rotation,
        Vector2 ringTranslation)
    {
        if (mastContactCollider == null ||
            yardContactSurfacePointsLocal.Length == 0)
        {
            return 0f;
        }

        Vector3 localTranslation =
            starboardAxisLocal * ringTranslation.x +
            aftAxisLocal * ringTranslation.y;

        CapsuleWorldGeometry mastGeometry =
            CreateCapsuleWorldGeometry(mastContactCollider);

        float clearance = float.PositiveInfinity;

        for (int i = 0; i < yardContactSurfacePointsLocal.Length; i++)
        {
            Vector3 pointLocal =
                mastPivotLocal +
                rotation * (yardContactSurfacePointsLocal[i] - mastPivotLocal) +
                localTranslation;

            Vector3 pointWorld = transform.TransformPoint(pointLocal);
            Vector3 mastCenterWorld =
                mastGeometry.GetCenterlinePoint(pointWorld);

            clearance = Mathf.Min(
                clearance,
                (pointWorld - mastCenterWorld).magnitude - mastGeometry.Radius);
        }

        return clearance;
    }

    private Vector3 GetNativeRingOffset(Quaternion rotation)
    {
        return mastPivotLocal +
            rotation * (yardContactCenterLocal - mastPivotLocal) -
            mastCenterLocal;
    }

    private Transform FindVisualRoot()
    {
        if (sail == null || sail.cloth == null)
            return null;

        Transform candidate = sail.cloth.transform.parent;
        return candidate != null && candidate != transform
            ? candidate
            : null;
    }

    private static MeshFilter FindYardMesh(Transform root)
    {
        if (root == null)
            return null;

        MeshFilter best = null;
        float bestLength = 0f;

        MeshFilter[] candidates =
            root.GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < candidates.Length; i++)
        {
            MeshFilter candidate = candidates[i];
            if (candidate.sharedMesh == null)
                continue;

            string lowerName = candidate.name.ToLowerInvariant();
            if (!lowerName.Contains("boom") && !lowerName.Contains("cylinder"))
                continue;

            Vector3 size = Vector3.Scale(
                candidate.sharedMesh.bounds.size,
                Abs(candidate.transform.lossyScale));

            float length = Mathf.Max(size.x, size.y, size.z);
            if (length <= bestLength)
                continue;

            best = candidate;
            bestLength = length;
        }

        return best;
    }

    private Vector3 FindMastCenterlinePoint(
        Vector3 contactWorld,
        Vector3 mastAxisWorld,
        out CapsuleCollider selectedCollider)
    {
        selectedCollider = null;

        Vector3 mastOriginWorld = mast != null
            ? mast.transform.position
            : transform.position;

        Vector3 result = mastOriginWorld +
            mastAxisWorld * Vector3.Dot(
                contactWorld - mastOriginWorld,
                mastAxisWorld);

        if (mast.mastCols == null)
            return result;

        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < mast.mastCols.Length; i++)
        {
            CapsuleCollider candidateCollider = mast.mastCols[i];
            if (candidateCollider == null)
                continue;

            CapsuleWorldGeometry candidateGeometry =
                CreateCapsuleWorldGeometry(candidateCollider);

            Vector3 candidate =
                candidateGeometry.GetCenterlinePoint(contactWorld);

            float distance = (candidate - contactWorld).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;

            result = candidate;
            selectedCollider = candidateCollider;
            nearestDistance = distance;
        }

        return result;
    }

    private static CapsuleWorldGeometry CreateCapsuleWorldGeometry(
        CapsuleCollider collider)
    {
        Vector3 localAxis = collider.direction switch
        {
            0 => Vector3.right,
            2 => Vector3.forward,
            _ => Vector3.up
        };

        Vector3 axisWorld = collider.transform
            .TransformDirection(localAxis)
            .normalized;

        Vector3 centerWorld = collider.transform.TransformPoint(collider.center);
        Vector3 scale = Abs(collider.transform.lossyScale);

        float axisScale = collider.direction switch
        {
            0 => scale.x,
            2 => scale.z,
            _ => scale.y
        };

        float halfSegment = Mathf.Max(
            0f,
            collider.height * 0.5f - collider.radius) * axisScale;

        float radialScale = collider.direction switch
        {
            0 => Mathf.Max(scale.y, scale.z),
            2 => Mathf.Max(scale.x, scale.y),
            _ => Mathf.Max(scale.x, scale.z)
        };

        return new CapsuleWorldGeometry(
            centerWorld,
            axisWorld,
            halfSegment,
            collider.radius * radialScale);
    }

    private void AnalyzeYardGeometry(
        Vector3 mastAxisLocal,
        out Vector3 yardAxisLocal)
    {
        Mesh mesh = yardMesh.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        var points = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPoint = yardMesh.transform.TransformPoint(vertices[i]);
            points[i] = transform.InverseTransformPoint(worldPoint);
        }

        Vector3 center = Average(points);
        yardAxisLocal = FindPrincipalAxis(points, center);

        float minimum = float.PositiveInfinity;
        float maximum = float.NegativeInfinity;

        for (int i = 0; i < points.Length; i++)
        {
            float projection = Vector3.Dot(points[i] - center, yardAxisLocal);
            minimum = Mathf.Min(minimum, projection);
            maximum = Mathf.Max(maximum, projection);
        }

        Vector3 firstTip = AverageTip(
            points,
            center,
            yardAxisLocal,
            minimum,
            maximum,
            first: true);

        Vector3 secondTip = AverageTip(
            points,
            center,
            yardAxisLocal,
            minimum,
            maximum,
            first: false);

        heelPositionLocal =
            Vector3.Dot(firstTip, mastAxisLocal) <=
            Vector3.Dot(secondTip, mastAxisLocal)
                ? firstTip
                : secondTip;

        float contactProjection = Vector3.Dot(
            mastPivotLocal - center,
            yardAxisLocal);

        yardContactCenterLocal = center +
            yardAxisLocal * Mathf.Clamp(contactProjection, minimum, maximum);

        yardContactSurfacePointsLocal = SliceMeshAtPlane(
            points,
            mesh.triangles,
            yardContactCenterLocal,
            yardAxisLocal);
    }

    private static Vector3[] SliceMeshAtPlane(
        Vector3[] points,
        int[] triangles,
        Vector3 planePoint,
        Vector3 planeNormal)
    {
        var intersections = new List<Vector3>();

        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            Vector3 first = points[triangles[i]];
            Vector3 second = points[triangles[i + 1]];
            Vector3 third = points[triangles[i + 2]];

            AddPlaneEdgeIntersections(
                intersections,
                first,
                second,
                planePoint,
                planeNormal);

            AddPlaneEdgeIntersections(
                intersections,
                second,
                third,
                planePoint,
                planeNormal);

            AddPlaneEdgeIntersections(
                intersections,
                third,
                first,
                planePoint,
                planeNormal);
        }

        if (intersections.Count == 0)
        {
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < points.Length; i++)
            {
                nearestDistance = Mathf.Min(
                    nearestDistance,
                    Mathf.Abs(Vector3.Dot(
                        points[i] - planePoint,
                        planeNormal)));
            }

            for (int i = 0; i < points.Length; i++)
            {
                float distance = Mathf.Abs(Vector3.Dot(
                    points[i] - planePoint,
                    planeNormal));

                if (distance <= nearestDistance + PlaneIntersectionEpsilon)
                    AddUniquePoint(intersections, points[i]);
            }
        }

        return intersections.ToArray();
    }

    private static void AddPlaneEdgeIntersections(
        List<Vector3> intersections,
        Vector3 first,
        Vector3 second,
        Vector3 planePoint,
        Vector3 planeNormal)
    {
        float firstDistance = Vector3.Dot(first - planePoint, planeNormal);
        float secondDistance = Vector3.Dot(second - planePoint, planeNormal);

        if (Mathf.Abs(firstDistance) <= PlaneIntersectionEpsilon)
            AddUniquePoint(intersections, first);

        if (Mathf.Abs(secondDistance) <= PlaneIntersectionEpsilon)
            AddUniquePoint(intersections, second);

        bool crossesPlane =
            firstDistance < -PlaneIntersectionEpsilon &&
            secondDistance > PlaneIntersectionEpsilon ||
            firstDistance > PlaneIntersectionEpsilon &&
            secondDistance < -PlaneIntersectionEpsilon;

        if (!crossesPlane)
            return;

        float amount = firstDistance / (firstDistance - secondDistance);
        AddUniquePoint(
            intersections,
            Vector3.Lerp(first, second, amount));
    }

    private static void AddUniquePoint(
        List<Vector3> points,
        Vector3 candidate)
    {
        float minimumSeparationSquared =
            PlaneIntersectionEpsilon * PlaneIntersectionEpsilon;

        for (int i = 0; i < points.Count; i++)
        {
            if ((points[i] - candidate).sqrMagnitude <= minimumSeparationSquared)
                return;
        }

        points.Add(candidate);
    }

    private static Vector3 Average(Vector3[] points)
    {
        Vector3 sum = Vector3.zero;

        for (int i = 0; i < points.Length; i++)
            sum += points[i];

        return sum / points.Length;
    }

    private static Vector3 FindPrincipalAxis(
        Vector3[] points,
        Vector3 center)
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

        Vector3 axis = xx >= yy && xx >= zz
            ? Vector3.right
            : yy >= zz
                ? Vector3.up
                : Vector3.forward;

        for (int i = 0; i < PrincipalAxisIterations; i++)
        {
            Vector3 next = new Vector3(
                xx * axis.x + xy * axis.y + xz * axis.z,
                xy * axis.x + yy * axis.y + yz * axis.z,
                xz * axis.x + yz * axis.y + zz * axis.z);

            if (next.sqrMagnitude <= GeometryChangeEpsilon)
                break;

            axis = next.normalized;
        }

        return axis;
    }

    private static Vector3 AverageTip(
        Vector3[] points,
        Vector3 center,
        Vector3 axis,
        float minimum,
        float maximum,
        bool first)
    {
        float extreme = first ? minimum : maximum;
        float tolerance = Mathf.Max(
            (maximum - minimum) * TipSampleFraction,
            MinimumTipTolerance);

        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < points.Length; i++)
        {
            float projection = Vector3.Dot(points[i] - center, axis);
            bool insideTip = first
                ? projection <= minimum + tolerance
                : projection >= maximum - tolerance;

            if (!insideTip)
                continue;

            sum += points[i];
            count++;
        }

        Vector3 tip = count > 0
            ? sum / count
            : center + axis * extreme;

        float tipProjection = Vector3.Dot(tip - center, axis);
        return tip + axis * (extreme - tipProjection);
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }
}
