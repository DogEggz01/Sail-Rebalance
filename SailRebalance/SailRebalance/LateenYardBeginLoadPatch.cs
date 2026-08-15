using HarmonyLib;

namespace SailRebalance;

[HarmonyPatch(typeof(SaveLoadManager), "LoadGame")]
internal static class LateenYardBeginLoadPatch
{
	private static void Prefix()
	{
		LateenYardPersistence.BeginLoad();
	}
}
