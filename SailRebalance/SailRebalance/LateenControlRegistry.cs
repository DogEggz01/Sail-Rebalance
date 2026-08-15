using System.Collections.Generic;

namespace SailRebalance;

internal enum LateenControlRole
{
    LowerBrace,
    Halyard,
    Sheet
}

internal sealed class LateenControlBinding
{
    internal LateenYardRig Rig;
    internal LateenControlRole Role;
}

internal static class LateenControlRegistry
{
    private static readonly Dictionary<RopeController, LateenControlBinding> Controls =
        new Dictionary<RopeController, LateenControlBinding>();

    private static readonly Dictionary<Sail, LateenYardRig> SailRigs =
        new Dictionary<Sail, LateenYardRig>();

    private static readonly List<LateenYardRig> Rigs =
        new List<LateenYardRig>();

    internal static IEnumerable<LateenYardRig> ActiveRigs => Rigs;

    internal static void Register(LateenYardRig rig)
    {
        if (rig == null)
            return;

        UnregisterBindings(rig);

        Sail sail = rig.GetComponent<Sail>();
        if (sail != null)
            SailRigs[sail] = rig;

        if (!Rigs.Contains(rig))
            Rigs.Add(rig);

        Bind(rig.LowerBrace, rig, LateenControlRole.LowerBrace);
        Bind(rig.Halyard, rig, LateenControlRole.Halyard);
        Bind(rig.SheetLeft, rig, LateenControlRole.Sheet);
        Bind(rig.SheetMid, rig, LateenControlRole.Sheet);
        Bind(rig.SheetRight, rig, LateenControlRole.Sheet);

        LateenYardPersistence.ApplyLoadedSide(rig);
    }

    private static void Bind(
        RopeController controller,
        LateenYardRig rig,
        LateenControlRole role)
    {
        if (controller == null)
            return;

        Controls[controller] = new LateenControlBinding
        {
            Rig = rig,
            Role = role
        };
    }

    internal static bool TryGet(
        RopeController controller,
        out LateenControlBinding binding)
    {
        if (controller != null &&
            Controls.TryGetValue(controller, out binding) &&
            binding.Rig != null)
        {
            return true;
        }

        binding = null;
        return false;
    }

    internal static bool TryGetRig(Sail sail, out LateenYardRig rig)
    {
        if (sail != null &&
            SailRigs.TryGetValue(sail, out rig) &&
            rig != null)
        {
            return true;
        }

        rig = null;
        return false;
    }

    internal static void Unregister(LateenYardRig rig)
    {
        if (rig == null)
            return;

        UnregisterBindings(rig);
        Rigs.Remove(rig);

        Sail sail = rig.GetComponent<Sail>();
        if (sail != null)
            SailRigs.Remove(sail);
    }

    private static void UnregisterBindings(LateenYardRig rig)
    {
        var controllersToRemove = new List<RopeController>();

        foreach (KeyValuePair<RopeController, LateenControlBinding> pair in Controls)
        {
            if (pair.Value.Rig == rig)
                controllersToRemove.Add(pair.Key);
        }

        for (int i = 0; i < controllersToRemove.Count; i++)
            Controls.Remove(controllersToRemove[i]);
    }

    internal static void Clear()
    {
        Controls.Clear();
        SailRigs.Clear();
        Rigs.Clear();
    }
}
