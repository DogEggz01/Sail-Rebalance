using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SailRebalance;

[BepInPlugin("com.pro1p.sailwind.junkandgaffadjustment", "Sail Rebalance", "1.1.10")]
public sealed class Plugin : BaseUnityPlugin
{
	public const string PluginGuid = "com.pro1p.sailwind.junkandgaffadjustment";

	public const string PluginName = "Sail Rebalance";

	public const string PluginVersion = "1.1.10";

	private Harmony harmony;

	internal static ConfigEntry<bool> EnableJunkCurve { get; private set; }

	internal static ConfigEntry<float> JunkMultiplier { get; private set; }

	internal static ConfigEntry<bool> EnableGaffCurve { get; private set; }

	internal static ConfigEntry<float> GaffMultiplier { get; private set; }

	internal static ConfigEntry<bool> EnableLateenBadTackPenalty { get; private set; }

	internal static ManualLogSource Log { get; private set; }

	internal static bool JunkCurveEnabled => EnableJunkCurve?.Value ?? true;

	internal static float ConfiguredJunkMultiplier => JunkMultiplier?.Value ?? 0.75f;

	internal static bool GaffCurveEnabled => EnableGaffCurve?.Value ?? true;

	internal static float ConfiguredGaffMultiplier => GaffMultiplier?.Value ?? 0.85f;

	internal static bool LateenBadTackPenaltyEnabled => EnableLateenBadTackPenalty?.Value ?? true;

	private void Awake()
	{
		Log = base.Logger;
		AcceptableValueRange<float> acceptableValueRange = new AcceptableValueRange<float>(0.5f, 1f);
		EnableJunkCurve = base.Config.Bind("Junk sails", "Enable junk curve", defaultValue: true, "Use the junk apparent-wind curve. When enabled, the junk multiplier slider is ignored.");
		JunkMultiplier = base.Config.Bind("Junk sails", "Junk multiplier", 0.75f, new ConfigDescription("Replaces the original 0.75 multiplier when the junk curve is disabled.", acceptableValueRange));
		EnableGaffCurve = base.Config.Bind("Gaff sails", "Enable gaff curve", defaultValue: true, "Use the gaff apparent-wind curve. When enabled, the gaff multiplier slider is ignored.");
		GaffMultiplier = base.Config.Bind("Gaff sails", "Gaff multiplier", 0.85f, new ConfigDescription("Replaces the original 0.85 multiplier when the gaff curve is disabled.", acceptableValueRange));
		EnableLateenBadTackPenalty = base.Config.Bind("Lateen sails", "Enable bad tack penalty", defaultValue: true, "Reduce lateen sail force by 10% when the yard is on the windward side of the mast.");
		harmony = new Harmony("com.pro1p.sailwind.junkandgaffadjustment");
		harmony.PatchAll(typeof(Plugin).Assembly);
		base.Logger.LogInfo("Sail Rebalance 1.1.10 loaded.");
	}

	private void OnDestroy()
	{
		LateenControlRegistry.Clear();
		LateenYardPersistence.Reset();
		this.harmony?.UnpatchSelf();
	}
}
