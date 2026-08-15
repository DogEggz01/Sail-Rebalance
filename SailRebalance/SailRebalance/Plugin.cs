using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SailRebalance;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.pro1p.sailwind.junkandgaffadjustment";

    public const string PluginName = "Sail Rebalance";

    public const string PluginVersion = "1.1.11";

    private Harmony harmony;

    internal static ConfigEntry<bool> EnableJunkCurve { get; private set; }

    internal static ConfigEntry<float> JunkMultiplier { get; private set; }

    internal static ConfigEntry<bool> EnableGaffCurve { get; private set; }

    internal static ConfigEntry<float> GaffMultiplier { get; private set; }

    internal static ConfigEntry<bool> EnableLateenBadTackPenalty { get; private set; }

    internal static ManualLogSource Log { get; private set; }

    internal static bool JunkCurveEnabled => EnableJunkCurve?.Value ?? true;

    internal static float ConfiguredJunkMultiplier =>
        JunkMultiplier?.Value ?? SailPower.JunkDefault;

    internal static bool GaffCurveEnabled => EnableGaffCurve?.Value ?? true;

    internal static float ConfiguredGaffMultiplier =>
        GaffMultiplier?.Value ?? SailPower.GaffDefault;

    internal static bool LateenBadTackPenaltyEnabled => EnableLateenBadTackPenalty?.Value ?? true;

    private void Awake()
    {
        Log = Logger;
        var multiplierRange = new AcceptableValueRange<float>(0.5f, 1f);

        EnableJunkCurve = Config.Bind(
            "Junk sails",
            "Enable junk curve",
            true,
            "Use the junk apparent-wind curve. When enabled, the junk multiplier slider is ignored.");

        JunkMultiplier = Config.Bind(
            "Junk sails",
            "Junk multiplier",
            SailPower.JunkDefault,
            new ConfigDescription(
                "Replaces the original 0.75 multiplier when the junk curve is disabled.",
                multiplierRange));

        EnableGaffCurve = Config.Bind(
            "Gaff sails",
            "Enable gaff curve",
            true,
            "Use the gaff apparent-wind curve. When enabled, the gaff multiplier slider is ignored.");

        GaffMultiplier = Config.Bind(
            "Gaff sails",
            "Gaff multiplier",
            SailPower.GaffDefault,
            new ConfigDescription(
                "Replaces the original 0.85 multiplier when the gaff curve is disabled.",
                multiplierRange));

        EnableLateenBadTackPenalty = Config.Bind(
            "Lateen sails",
            "Enable bad tack penalty",
            true,
            "Reduce lateen sail force by 10% when the yard is on the windward side of the mast.");

        harmony = new Harmony(PluginGuid);
        harmony.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void OnDestroy()
    {
        LateenControlRegistry.Clear();
        LateenYardPersistence.Reset();
        harmony?.UnpatchSelf();
    }
}
