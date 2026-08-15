using HarmonyLib;

namespace SailRebalance;

[HarmonyPatch(typeof(SaveLoadManager), "SaveModData")]
internal static class LateenYardSavePatch
{
	private static void Postfix()
	{
		LateenYardPersistence.Save();
	}
}
