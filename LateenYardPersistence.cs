using System;
using System.Collections.Generic;

namespace SailRebalance
{
	internal static class LateenYardPersistence
	{
		internal static void BeginLoad()
		{
			LateenYardPersistence.LoadedSides.Clear();
			LateenYardPersistence.loaded = false;
		}

		internal static void Save()
		{
			if (GameState.modData == null)
			{
				GameState.modData = new Dictionary<string, string>();
			}
			List<string> list = new List<string>();
			foreach (LateenYardRig lateenYardRig in LateenControlRegistry.ActiveRigs)
			{
				if (!(lateenYardRig == null))
				{
					string persistenceKey = lateenYardRig.GetPersistenceKey();
					if (!string.IsNullOrEmpty(persistenceKey))
					{
						list.Add(persistenceKey + "," + lateenYardRig.YardSideSign.ToString());
					}
				}
			}
			GameState.modData[ModDataKey] = string.Join("|", list.ToArray());
		}

		internal static void Load()
		{
			LateenYardPersistence.LoadedSides.Clear();
			LateenYardPersistence.loaded = true;
			string text;
			if (GameState.modData == null || !GameState.modData.TryGetValue(ModDataKey, out text) || string.IsNullOrEmpty(text))
			{
				return;
			}
			string[] array = text.Split(new char[]
			{
				'|'
			}, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				int num = array[i].LastIndexOf(',');
				if (num > 0 && num < array[i].Length - 1)
				{
					string key = array[i].Substring(0, num);
					int num2;
					if (int.TryParse(array[i].Substring(num + 1), out num2))
					{
						LateenYardPersistence.LoadedSides[key] = ((num2 >= 0) ? 1 : -1);
					}
				}
			}
			foreach (LateenYardRig rig in LateenControlRegistry.ActiveRigs)
			{
				LateenYardPersistence.ApplyLoadedSide(rig);
			}
		}

		internal static void ApplyLoadedSide(LateenYardRig rig)
		{
			if (!LateenYardPersistence.loaded || rig == null)
			{
				return;
			}
			string persistenceKey = rig.GetPersistenceKey();
			int savedSide;
			if (!string.IsNullOrEmpty(persistenceKey) && LateenYardPersistence.LoadedSides.TryGetValue(persistenceKey, out savedSide))
			{
				rig.RestoreYardSide(savedSide);
			}
		}

		internal static void Reset()
		{
			LateenYardPersistence.LoadedSides.Clear();
			LateenYardPersistence.loaded = false;
		}

		private const string ModDataKey = "com.pro1p.sailwind.junkandgaffadjustment.lateen-yard-sides.v1";

		private static readonly Dictionary<string, int> LoadedSides = new Dictionary<string, int>();

		private static bool loaded;
	}
}
