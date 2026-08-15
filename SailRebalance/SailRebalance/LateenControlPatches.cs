using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SailRebalance;

[HarmonyPatch(typeof(Mast), nameof(Mast.UpdateControllerAttachments))]
internal static class LateenLowerBraceAttachmentPatch
{
    private const string LowerBraceWinchName = "lower brace winch";

    private static readonly HashSet<int> MissingWinchWarnings =
        new HashSet<int>();

    private static void Postfix(Mast __instance)
    {
        if (__instance.sails == null)
            return;

        for (int i = 0; i < __instance.sails.Count; i++)
        {
            GameObject sailObject = __instance.sails[i];
            if (sailObject == null)
                continue;

            Sail sail = sailObject.GetComponent<Sail>();
            SailConnections connections = sailObject.GetComponent<SailConnections>();

            if (sail == null ||
                connections == null ||
                sail.category != SailCategory.lateen)
            {
                continue;
            }

            LateenYardRig rig = sailObject.GetComponent<LateenYardRig>();
            GPButtonRopeWinch winch = rig != null && rig.LowerBrace != null
                ? FindAttachedWinch(__instance, sail.mastOrder, rig.LowerBrace)
                : null;

            if (winch == null)
                winch = FindUnusedWinch(__instance, sail.mastOrder);

            if (winch == null)
            {
                if (MissingWinchWarnings.Add(sail.GetInstanceID()))
                {
                    Plugin.Log?.LogWarning(
                        $"No unused winch is available for the lower brace on {sail.name}.");
                }

                continue;
            }

            if (rig == null)
                rig = sailObject.AddComponent<LateenYardRig>();

            if (!rig.Initialize(__instance, sail, connections) || rig.LowerBrace == null)
                continue;

            winch.AttachToController(rig.LowerBrace);
            winch.description = LowerBraceWinchName;
            winch.gameObject.name = LowerBraceWinchName;
            winch.ShowWinch(true);
        }
    }

    private static GPButtonRopeWinch FindUnusedWinch(Mast mast, int order)
    {
        return GetUnused(mast.leftAngleWinch, order)
            ?? GetUnused(mast.rightAngleWinch, order)
            ?? GetUnused(mast.midAngleWinch, order);
    }

    private static GPButtonRopeWinch FindAttachedWinch(
        Mast mast,
        int order,
        RopeController controller)
    {
        return GetAttached(mast.leftAngleWinch, order, controller)
            ?? GetAttached(mast.rightAngleWinch, order, controller)
            ?? GetAttached(mast.midAngleWinch, order, controller);
    }

    private static GPButtonRopeWinch GetAttached(
        GPButtonRopeWinch[] winches,
        int index,
        RopeController controller)
    {
        GPButtonRopeWinch winch = GetWinch(winches, index);
        return winch != null && winch.rope == controller ? winch : null;
    }

    private static GPButtonRopeWinch GetUnused(
        GPButtonRopeWinch[] winches,
        int index)
    {
        GPButtonRopeWinch winch = GetWinch(winches, index);
        return winch != null && winch.rope == null ? winch : null;
    }

    private static GPButtonRopeWinch GetWinch(
        GPButtonRopeWinch[] winches,
        int index)
    {
        if (winches == null || index < 0 || index >= winches.Length)
            return null;

        return winches[index];
    }
}

[HarmonyPatch(typeof(GPButtonRopeWinch), "Update")]
internal static class LateenWinchLockPatch
{
    private static bool Prefix(
        GPButtonRopeWinch __instance,
        ref float ___currentInput)
    {
        if (!LateenControlRegistry.TryGet(
                __instance.rope,
                out LateenControlBinding binding))
        {
            return true;
        }

        bool canOperate = binding.Role == LateenControlRole.LowerBrace
            ? binding.Rig.CanOperateLowerBrace
            : binding.Rig.CanOperateHalyardOrSheet;

        if (canOperate)
            return true;

        ___currentInput = 0f;
        return false;
    }
}
