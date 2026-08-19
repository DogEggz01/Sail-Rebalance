using System.Collections.Generic;
using UnityEngine;

namespace SailRebalance;

internal sealed partial class LateenYardRig
{
    private const float LooseRopeTension = 0.86f;
    private const float TightRopeTension = 0.98f;
    private const float LooseRopeCurveLength = 0.45f;
    private const float TightRopeCurveLength = 0.18f;

    private Transform yardHeelAttachment;
    private Transform braceFairlead;

    private readonly List<RopeEffect> braceRopes =
        new List<RopeEffect>(2);

    private void UpdateYardHeelAttachment()
    {
        if (yardHeelAttachment == null)
        {
            var heelObject = new GameObject("lateen yard heel attachment");
            yardHeelAttachment = heelObject.transform;
        }

        if (yardHeelAttachment.parent != yardMesh.transform)
            yardHeelAttachment.SetParent(yardMesh.transform, false);

        yardHeelAttachment.position = transform.TransformPoint(heelPositionLocal);
    }

    private void CreateBraceRoute()
    {
        Transform sheetFairlead = connections.midRopeAttachment;
        RopeEffect sheetControllerRope = Sheet != null
            ? Sheet.GetComponent<RopeEffect>()
            : null;

        RopeEffect sheetFairleadRope = sheetFairlead != null
            ? sheetFairlead.GetComponent<RopeEffect>()
            : null;

        var fairleadObject = new GameObject("lateen brace fairlead");
        fairleadObject.SetActive(false);
        braceFairlead = fairleadObject.transform;
        RefreshFairleadPosition();

        RopeEffect fairleadToHeel = CreateRopeEffect(
            fairleadObject,
            sheetFairleadRope,
            yardHeelAttachment,
            sheetFallback: true);

        if (fairleadToHeel != null)
            braceRopes.Add(fairleadToHeel);

        fairleadObject.SetActive(true);

        var controllerObject = new GameObject($"{name} brace controller");
        controllerObject.SetActive(false);

        RopeEffect winchToFairlead = CreateRopeEffect(
            controllerObject,
            sheetControllerRope,
            braceFairlead,
            sheetFallback: false);

        if (winchToFairlead != null)
            braceRopes.Add(winchToFairlead);

        Brace = controllerObject.AddComponent<LateenBraceController>();
        Brace.Initialize(this);
        controllerObject.SetActive(true);
    }

    private RopeEffect CreateRopeEffect(
        GameObject owner,
        RopeEffect source,
        Transform attachment,
        bool sheetFallback)
    {
        if (source == null || attachment == null)
            return null;

        LineRenderer sourceLine = source.GetComponent<LineRenderer>();
        if (sourceLine == null)
            return null;

        LineRenderer line = owner.AddComponent<LineRenderer>();
        CopyLineRenderer(sourceLine, line);

        RopeEffect effect = owner.AddComponent<RopeEffect>();
        effect.attachment = attachment;
        effect.sheet = source.sheet || sheetFallback;
        effect.jibSheet = false;
        effect.currentRopeLength = LooseRopeCurveLength;
        effect.ropeWidth = source.ropeWidth;
        effect.totalRopeLength = LooseRopeTension;
        effect.curveRate = source.curveRate;
        effect.attachedSailArea = sail.sailArea;
        effect.allRopeSections = new List<Vector3>();
        return effect;
    }

    private static void CopyLineRenderer(
        LineRenderer source,
        LineRenderer destination)
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
        if (braceFairlead == null)
            return;

        Transform sheetFairlead = connections != null
            ? connections.midRopeAttachment
            : null;

        Transform parent = sheetFairlead != null && sheetFairlead.parent != null
            ? sheetFairlead.parent
            : mast.transform;

        braceFairlead.SetParent(parent, true);

        if (sheetFairlead == null)
            return;

        braceFairlead.position = sheetFairlead.position;
        braceFairlead.rotation = sheetFairlead.rotation;
    }

    private void UpdateRopeTension(float bracePull)
    {
        float currentRopeLength = Mathf.Lerp(
            LooseRopeCurveLength,
            TightRopeCurveLength,
            bracePull);

        float totalRopeLength = Mathf.Lerp(
            LooseRopeTension,
            TightRopeTension,
            bracePull);

        for (int i = 0; i < braceRopes.Count; i++)
        {
            RopeEffect rope = braceRopes[i];
            if (rope == null)
                continue;

            rope.currentRopeLength = currentRopeLength;
            rope.totalRopeLength = totalRopeLength;
        }
    }

    private void DestroyRopeObjects()
    {
        if (Brace != null)
            Destroy(Brace.gameObject);

        if (braceFairlead != null)
            Destroy(braceFairlead.gameObject);
    }
}
