using System;
using System.Collections.Generic;

namespace SailRebalance
{
	internal static class LateenControlRegistry
	{
		internal static IEnumerable<LateenYardRig> ActiveRigs
		{
			get
			{
				return LateenControlRegistry.Rigs;
			}
		}

		internal static void Register(LateenYardRig rig)
		{
			if (rig == null)
			{
				return;
			}
			LateenControlRegistry.UnregisterBindings(rig);
			Sail component = rig.GetComponent<Sail>();
			if (component != null)
			{
				LateenControlRegistry.SailRigs[component] = rig;
			}
			if (!LateenControlRegistry.Rigs.Contains(rig))
			{
				LateenControlRegistry.Rigs.Add(rig);
			}
			LateenControlRegistry.Bind(rig.Brace, rig, LateenControlRole.Brace);
			LateenControlRegistry.Bind(rig.Halyard, rig, LateenControlRole.Halyard);
			LateenControlRegistry.Bind(rig.SheetLeft, rig, LateenControlRole.Sheet);
			LateenControlRegistry.Bind(rig.SheetMid, rig, LateenControlRole.Sheet);
			LateenControlRegistry.Bind(rig.SheetRight, rig, LateenControlRole.Sheet);
			LateenYardPersistence.ApplyLoadedSide(rig);
		}

		private static void Bind(RopeController controller, LateenYardRig rig, LateenControlRole role)
		{
			if (controller == null)
			{
				return;
			}
			LateenControlRegistry.Controls[controller] = new LateenControlBinding
			{
				Rig = rig,
				Role = role
			};
		}

		internal static bool TryGet(RopeController controller, out LateenControlBinding binding)
		{
			if (controller != null && LateenControlRegistry.Controls.TryGetValue(controller, out binding) && binding.Rig != null)
			{
				return true;
			}
			binding = null;
			return false;
		}

		internal static bool TryGetRig(Sail sail, out LateenYardRig rig)
		{
			if (sail != null && LateenControlRegistry.SailRigs.TryGetValue(sail, out rig) && rig != null)
			{
				return true;
			}
			rig = null;
			return false;
		}

		internal static void Unregister(LateenYardRig rig)
		{
			if (rig == null)
			{
				return;
			}
			LateenControlRegistry.UnregisterBindings(rig);
			LateenControlRegistry.Rigs.Remove(rig);
			Sail component = rig.GetComponent<Sail>();
			if (component != null)
			{
				LateenControlRegistry.SailRigs.Remove(component);
			}
		}

		private static void UnregisterBindings(LateenYardRig rig)
		{
			List<RopeController> list = new List<RopeController>();
			foreach (KeyValuePair<RopeController, LateenControlBinding> keyValuePair in LateenControlRegistry.Controls)
			{
				if (keyValuePair.Value.Rig == rig)
				{
					list.Add(keyValuePair.Key);
				}
			}
			for (int i = 0; i < list.Count; i++)
			{
				LateenControlRegistry.Controls.Remove(list[i]);
			}
		}

		internal static void DisposeAllRigs()
		{
			List<LateenYardRig> rigs = new List<LateenYardRig>(LateenControlRegistry.Rigs);
			for (int i = 0; i < rigs.Count; i++)
			{
				LateenYardRig rig = rigs[i];
				if (rig != null)
				{
					rig.Dispose();
				}
			}
			LateenControlRegistry.Controls.Clear();
			LateenControlRegistry.SailRigs.Clear();
			LateenControlRegistry.Rigs.Clear();
		}

		private static readonly Dictionary<RopeController, LateenControlBinding> Controls = new Dictionary<RopeController, LateenControlBinding>();

		private static readonly Dictionary<Sail, LateenYardRig> SailRigs = new Dictionary<Sail, LateenYardRig>();

		private static readonly List<LateenYardRig> Rigs = new List<LateenYardRig>();
	}
}
