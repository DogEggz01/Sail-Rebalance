using System;
using System.Collections.Generic;

namespace SailRebalance;

internal static class LateenYardPersistence
{
	private const string ModDataKey = "com.pro1p.sailwind.junkandgaffadjustment.lateen-yard-sides.v1";

	private static readonly Dictionary<string, int> LoadedSides = new Dictionary<string, int>();

	private static bool loaded;

	internal static void BeginLoad()
	{
		LoadedSides.Clear();
		loaded = false;
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
					list.Add(persistenceKey + "," + lateenYardRig.YardSideSign);
				}
			}
		}
		GameState.modData["com.pro1p.sailwind.junkandgaffadjustment.lateen-yard-sides.v1"] = string.Join("|", list.ToArray());
	}

	internal static void Load()
	{
		LoadedSides.Clear();
		loaded = true;
		if (GameState.modData == null || !GameState.modData.TryGetValue("com.pro1p.sailwind.junkandgaffadjustment.lateen-yard-sides.v1", out var text) || string.IsNullOrEmpty(text))
		{
			return;
		}
		string[] array = text.Split(new char[1] { '|' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].LastIndexOf(',');
			if (num > 0 && num < array[i].Length - 1)
			{
				string text2 = array[i].Substring(0, num);
				if (int.TryParse(array[i].Substring(num + 1), out var num2))
				{
					LoadedSides[text2] = ((num2 >= 0) ? 1 : (-1));
				}
			}
		}
		foreach (LateenYardRig activeRig in LateenControlRegistry.ActiveRigs)
		{
			ApplyLoadedSide(activeRig);
		}
	}

	internal static void ApplyLoadedSide(LateenYardRig rig)
	{
		if (loaded && !(rig == null))
		{
			string persistenceKey = rig.GetPersistenceKey();
			if (!string.IsNullOrEmpty(persistenceKey) && LoadedSides.TryGetValue(persistenceKey, out var savedSide))
			{
				rig.RestoreYardSide(savedSide);
			}
		}
	}

	internal static void Reset()
	{
		LoadedSides.Clear();
		loaded = false;
	}
}
