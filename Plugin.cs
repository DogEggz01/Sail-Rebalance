using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SailRebalance
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public sealed class Plugin : BaseUnityPlugin
	{
		internal static ConfigEntry<bool> EnableJunkCurve { get; private set; }

		internal static ConfigEntry<float> JunkMultiplier { get; private set; }

		internal static ConfigEntry<bool> EnableGaffCurve { get; private set; }

		internal static ConfigEntry<float> GaffMultiplier { get; private set; }

		internal static ConfigEntry<bool> EnableLateenBadTackPenalty { get; private set; }

		internal static ConfigEntry<bool> ReverseJunkSquareFurling { get; private set; }

		internal static ManualLogSource Log { get; private set; }

		internal static bool JunkCurveEnabled
		{
			get
			{
				ConfigEntry<bool> enableJunkCurve = Plugin.EnableJunkCurve;
				return enableJunkCurve == null || enableJunkCurve.Value;
			}
		}

		internal static float ConfiguredJunkMultiplier
		{
			get
			{
				ConfigEntry<float> junkMultiplier = Plugin.JunkMultiplier;
				if (junkMultiplier == null)
				{
					return SailPower.JunkDefault;
				}
				return junkMultiplier.Value;
			}
		}

		internal static bool GaffCurveEnabled
		{
			get
			{
				ConfigEntry<bool> enableGaffCurve = Plugin.EnableGaffCurve;
				return enableGaffCurve == null || enableGaffCurve.Value;
			}
		}

		internal static float ConfiguredGaffMultiplier
		{
			get
			{
				ConfigEntry<float> gaffMultiplier = Plugin.GaffMultiplier;
				if (gaffMultiplier == null)
				{
					return SailPower.GaffDefault;
				}
				return gaffMultiplier.Value;
			}
		}

		internal static bool LateenBadTackPenaltyEnabled
		{
			get
			{
				ConfigEntry<bool> enableLateenBadTackPenalty = Plugin.EnableLateenBadTackPenalty;
				return enableLateenBadTackPenalty == null || enableLateenBadTackPenalty.Value;
			}
		}

		internal static bool ReverseJunkSquareFurlingEnabled
		{
			get
			{
				ConfigEntry<bool> reverseJunkSquareFurling = Plugin.ReverseJunkSquareFurling;
				return reverseJunkSquareFurling == null || reverseJunkSquareFurling.Value;
			}
		}

		private void Awake()
		{
			Plugin.Log = base.Logger;
			AcceptableValueRange<float> acceptableValues = new AcceptableValueRange<float>(0.5f, 1f);
			Plugin.EnableJunkCurve = base.Config.Bind<bool>("Junk sails", "Enable junk curve", true, "Use the junk apparent-wind curve. When enabled, the junk multiplier slider is ignored.");
			Plugin.JunkMultiplier = base.Config.Bind<float>("Junk sails", "Junk multiplier", SailPower.JunkDefault, new ConfigDescription("Replaces the original 0.75 multiplier when the junk curve is disabled.", acceptableValues, Array.Empty<object>()));
			Plugin.EnableGaffCurve = base.Config.Bind<bool>("Gaff sails", "Enable gaff curve", true, "Use the gaff apparent-wind curve. When enabled, the gaff multiplier slider is ignored.");
			Plugin.GaffMultiplier = base.Config.Bind<float>("Gaff sails", "Gaff multiplier", SailPower.GaffDefault, new ConfigDescription("Replaces the original 0.85 multiplier when the gaff curve is disabled.", acceptableValues, Array.Empty<object>()));
			Plugin.EnableLateenBadTackPenalty = base.Config.Bind<bool>("Lateen sails", "Enable bad tack penalty", true, "Reduce lateen sail force by 10% when the yard is on the windward side of the mast.");
			Plugin.ReverseJunkSquareFurling = base.Config.Bind<bool>("Junk square sails", "Reverse furling", true, "Pulling the reef winch raises and unfurls the sail; releasing it lowers and furls the sail.");
			Plugin.ReverseJunkSquareFurling.SettingChanged += this.OnReverseJunkSquareFurlingChanged;
			this.harmony = new Harmony(PluginGuid);
			this.harmony.PatchAll(typeof(Plugin).Assembly);
			this.reverseJunkSquareRefreshPending = true;
			base.Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
		}

		private void Update()
		{
			if (!this.reverseJunkSquareRefreshPending)
			{
				return;
			}
			this.reverseJunkSquareRefreshPending = false;
			JunkSquareReverseReefing.SetEnabled(Plugin.ReverseJunkSquareFurlingEnabled);
		}

		private void OnReverseJunkSquareFurlingChanged(object sender, EventArgs eventArgs)
		{
			this.reverseJunkSquareRefreshPending = true;
		}

		private void OnDestroy()
		{
			if (Plugin.ReverseJunkSquareFurling != null)
			{
				Plugin.ReverseJunkSquareFurling.SettingChanged -= this.OnReverseJunkSquareFurlingChanged;
			}
			JunkSquareReverseReefing.Reset();
			LateenBraceAttachmentPatch.Reset();
			LateenControlRegistry.DisposeAllRigs();
			LateenYardPersistence.Reset();
			Harmony harmony = this.harmony;
			if (harmony == null)
			{
				return;
			}
			harmony.UnpatchSelf();
		}

		public const string PluginGuid = "com.pro1p.sailwind.junkandgaffadjustment";

		public const string PluginName = "Sail Rebalance";

		public const string PluginVersion = "1.2.0";

		private Harmony harmony;

		private volatile bool reverseJunkSquareRefreshPending;
	}
}
