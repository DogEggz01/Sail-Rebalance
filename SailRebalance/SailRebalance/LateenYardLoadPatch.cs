using HarmonyLib;

namespace SailRebalance;

[HarmonyPatch(typeof(SaveLoadManager), "LoadModData")]
internal static class LateenYardLoadPatch
{
	private static void Postfix()
	{
		LateenYardPersistence.Load();
	}
}
