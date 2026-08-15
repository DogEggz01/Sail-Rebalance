using UnityEngine;

namespace SailRebalance;

internal sealed partial class LateenYardRig : MonoBehaviour
{
    internal const float ControlEpsilon = 0.0001f;
    internal const float FullyRaisedThreshold = 0.9999f;
    internal const float BadTackPowerMultiplier = 0.9f;

    private const float TackResetPullThreshold = 0.7f;
    private const float WindMagnitudeEpsilon = 0.001f;
    private const float WindSideDeadZone = 0.05f;

    private Sail sail;
    private Mast mast;
    private SailConnections connections;
    private HingeJoint hinge;

    private int originalYardSide;
    private int yardSide;
    private float previousLowerBracePull;
    private int transferStartSide;
    private int transferTargetSide;
    private bool sideTransferActive;
    private bool tackChangeArmed;
    private bool initialized;

    internal LateenLowerBraceController LowerBrace { get; private set; }

    internal RopeController Halyard =>
        connections != null ? connections.reefController : null;

    internal RopeController SheetLeft =>
        connections != null ? connections.angleControllerLeft : null;

    internal RopeController SheetMid =>
        connections != null ? connections.angleControllerMid : null;

    internal RopeController SheetRight =>
        connections != null ? connections.angleControllerRight : null;

    internal RopeController Sheet
    {
        get
        {
            if (SheetMid != null)
                return SheetMid;

            return SheetLeft != null ? SheetLeft : SheetRight;
        }
    }

    internal float LowerBracePull => LowerBrace != null
        ? 1f - Mathf.Clamp01(LowerBrace.currentLength)
        : 0f;

    internal int YardSideSign => yardSide;

    internal bool CanOperateLowerBrace =>
        sail != null &&
        sail.currentUnroll <= ControlEpsilon &&
        AreAllSheetsFullyLoose();

    internal bool CanOperateHalyardOrSheet =>
        LowerBracePull <= ControlEpsilon;

    private bool AreAllSheetsFullyLoose()
    {
        return Sheet != null &&
            IsSheetFullyLoose(SheetLeft) &&
            IsSheetFullyLoose(SheetMid) &&
            IsSheetFullyLoose(SheetRight);
    }

    private static bool IsSheetFullyLoose(RopeController controller)
    {
        return controller == null ||
            controller.currentLength >= FullyRaisedThreshold;
    }

    internal bool Initialize(
        Mast owningMast,
        Sail attachedSail,
        SailConnections attachedConnections)
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

        if (mast == null || sail == null || connections == null || hinge == null)
            return false;

        visualRoot = FindVisualRoot();
        yardMesh = FindYardMesh(visualRoot);

        if (visualRoot == null || yardMesh == null || yardMesh.sharedMesh == null)
        {
            Plugin.Log?.LogWarning(
                $"Could not locate the lateen visual root or yard on {name}.");
            return false;
        }

        ConfigureAxes();
        ConfigureYardGeometry(initializeSide: true);
        CreateProceduralPivot();
        CreateLowerBraceRoute();

        if (LowerBrace == null)
        {
            Plugin.Log?.LogWarning(
                $"Could not create a lower-brace controller for {name}.");
            return false;
        }

        CaptureGeometrySignature();
        initialized = true;
        previousLowerBracePull = Mathf.Clamp01(LowerBracePull);
        tackChangeArmed = previousLowerBracePull <= TackResetPullThreshold;
        LateenControlRegistry.Register(this);
        return true;
    }

    private void LateUpdate()
    {
        if (!initialized || LowerBrace == null || proceduralPivot == null)
            return;

        RefreshGeometryIfNeeded();

        float lowerBracePull = Mathf.Clamp01(LowerBracePull);
        UpdateAutomaticSideTransfer(lowerBracePull);
        ApplyProceduralPose(lowerBracePull);
        UpdateRopeTension(lowerBracePull);
    }

    private void UpdateAutomaticSideTransfer(float lowerBracePull)
    {
        if (lowerBracePull <= TackResetPullThreshold)
        {
            tackChangeArmed = true;
            sideTransferActive = false;
        }

        if (tackChangeArmed &&
            lowerBracePull > previousLowerBracePull + ControlEpsilon)
        {
            transferStartSide = yardSide;
            transferTargetSide = -yardSide;
            sideTransferActive = true;
            tackChangeArmed = false;
        }

        if (sideTransferActive && lowerBracePull >= FullyRaisedThreshold)
        {
            yardSide = transferTargetSide;
            sideTransferActive = false;
        }

        previousLowerBracePull = lowerBracePull;
    }

    internal bool IsBadTack()
    {
        Rigidbody ship = sail != null ? sail.shipRigidbody : null;
        if (ship == null || sail.apparentWind.sqrMagnitude < WindMagnitudeEpsilon)
            return false;

        float lateralWind = Vector3.Dot(
            -sail.apparentWind.normalized,
            ship.transform.right);

        if (Mathf.Abs(lateralWind) < WindSideDeadZone)
            return false;

        int windwardSide = lateralWind > 0f ? 1 : -1;
        return windwardSide == yardSide;
    }

    internal string GetPersistenceKey()
    {
        SaveableObject owner = GetComponentInParent<SaveableObject>();
        if (owner == null || mast == null || sail == null)
            return null;

        return $"{owner.sceneIndex}:{mast.orderIndex}:{sail.mastOrder}:{sail.prefabIndex}";
    }

    internal void RestoreYardSide(int savedSide)
    {
        yardSide = savedSide >= 0 ? 1 : -1;
        sideTransferActive = false;
        previousLowerBracePull = Mathf.Clamp01(LowerBracePull);
        tackChangeArmed = previousLowerBracePull <= TackResetPullThreshold;

        if (initialized && proceduralPivot != null)
            ApplyProceduralPose(previousLowerBracePull);
    }

    private void OnDestroy()
    {
        LateenControlRegistry.Unregister(this);
        DestroyRopeObjects();
    }
}
