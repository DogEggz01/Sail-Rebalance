using UnityEngine;

namespace SailRebalance;

internal static class SailPower
{
	internal const float JunkDefault = 0.75f;

	internal const float GaffDefault = 0.85f;

	internal const float StrongWindThreshold = 21f;

	internal static float Junk(Sail sail)
	{
		if (!Plugin.JunkCurveEnabled)
		{
			return Plugin.ConfiguredJunkMultiplier;
		}
		if (sail == null || sail.shipRigidbody == null)
		{
			return 0.75f;
		}
		float num = ApparentAngle(sail);
		bool flag = Wind.currentWind.magnitude > 21f;
		float num2 = (flag ? 0.85f : 0.75f);
		if (num <= 90f)
		{
			return num2;
		}
		if (num <= 150f)
		{
			return Mathf.Lerp(num2, 1f, Mathf.InverseLerp(90f, 150f, num));
		}
		return Mathf.Lerp(1f, flag ? 0.9f : 0.85f, Mathf.InverseLerp(150f, 180f, num));
	}

	internal static float Gaff(Sail sail)
	{
		if (!Plugin.GaffCurveEnabled)
		{
			return Plugin.ConfiguredGaffMultiplier;
		}
		if (sail == null || sail.shipRigidbody == null)
		{
			return 0.85f;
		}
		float num = ApparentAngle(sail);
		if (num <= 80f || num >= 160f)
		{
			return 0.85f;
		}
		if (num <= 120f)
		{
			return Mathf.Lerp(0.85f, 1f, Mathf.InverseLerp(80f, 120f, num));
		}
		return Mathf.Lerp(1f, 0.85f, Mathf.InverseLerp(120f, 160f, num));
	}

	private static float ApparentAngle(Sail sail)
	{
		return Mathf.Abs(Vector3.SignedAngle(-sail.shipRigidbody.transform.forward, sail.apparentWind, Vector3.up));
	}
}
