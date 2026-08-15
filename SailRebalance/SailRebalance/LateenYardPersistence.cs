using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SailRebalance;

internal static class LateenYardPersistence
{
    private const string ModDataKey =
        Plugin.PluginGuid + ".lateen-yard-sides.v1";

    private static readonly Dictionary<string, int> LoadedSides =
        new Dictionary<string, int>();

    private static bool loaded;

    internal static void BeginLoad()
    {
        LoadedSides.Clear();
        loaded = false;
    }

    internal static void Save()
    {
        if (GameState.modData == null)
            GameState.modData = new Dictionary<string, string>();

        var entries = new List<string>();

        foreach (LateenYardRig rig in LateenControlRegistry.ActiveRigs)
        {
            if (rig == null)
                continue;

            string persistenceKey = rig.GetPersistenceKey();
            if (!string.IsNullOrEmpty(persistenceKey))
                entries.Add(persistenceKey + "," + rig.YardSideSign);
        }

        GameState.modData[ModDataKey] = string.Join("|", entries.ToArray());
    }

    internal static void Load()
    {
        LoadedSides.Clear();
        loaded = true;

        if (GameState.modData == null ||
            !GameState.modData.TryGetValue(ModDataKey, out string data) ||
            string.IsNullOrEmpty(data))
        {
            return;
        }

        string[] entries = data.Split(
            new[] { '|' },
            StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < entries.Length; i++)
        {
            int separator = entries[i].LastIndexOf(',');
            if (separator <= 0 || separator >= entries[i].Length - 1)
                continue;

            string key = entries[i].Substring(0, separator);
            string sideText = entries[i].Substring(separator + 1);

            if (int.TryParse(sideText, out int side))
                LoadedSides[key] = side >= 0 ? 1 : -1;
        }

        foreach (LateenYardRig rig in LateenControlRegistry.ActiveRigs)
            ApplyLoadedSide(rig);
    }

    internal static void ApplyLoadedSide(LateenYardRig rig)
    {
        if (!loaded || rig == null)
            return;

        string persistenceKey = rig.GetPersistenceKey();
        if (!string.IsNullOrEmpty(persistenceKey) &&
            LoadedSides.TryGetValue(persistenceKey, out int savedSide))
        {
            rig.RestoreYardSide(savedSide);
        }
    }

    internal static void Reset()
    {
        LoadedSides.Clear();
        loaded = false;
    }
}

[HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.LoadGame))]
internal static class LateenYardBeginLoadPatch
{
    private static void Prefix()
    {
        LateenYardPersistence.BeginLoad();
    }
}

[HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.SaveModData))]
internal static class LateenYardSavePatch
{
    private static void Postfix()
    {
        LateenYardPersistence.Save();
    }
}

[HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.LoadModData))]
internal static class LateenYardLoadPatch
{
    private static void Postfix()
    {
        LateenYardPersistence.Load();
    }
}
