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
            return Plugin.ConfiguredJunkMultiplier;

        if (sail == null || sail.shipRigidbody == null)
            return JunkDefault;

        float apparentAngle = ApparentAngle(sail);
        bool strongWind = Wind.currentWind.magnitude > StrongWindThreshold;
        float baseMultiplier = strongWind ? GaffDefault : JunkDefault;

        if (apparentAngle <= 90f)
            return baseMultiplier;

        if (apparentAngle <= 150f)
        {
            return Mathf.Lerp(
                baseMultiplier,
                1f,
                Mathf.InverseLerp(90f, 150f, apparentAngle));
        }

        return Mathf.Lerp(
            1f,
            strongWind ? 0.9f : GaffDefault,
            Mathf.InverseLerp(150f, 180f, apparentAngle));
    }

    internal static float Gaff(Sail sail)
    {
        if (!Plugin.GaffCurveEnabled)
            return Plugin.ConfiguredGaffMultiplier;

        if (sail == null || sail.shipRigidbody == null)
            return GaffDefault;

        float apparentAngle = ApparentAngle(sail);

        if (apparentAngle <= 80f || apparentAngle >= 160f)
            return GaffDefault;

        if (apparentAngle <= 120f)
        {
            return Mathf.Lerp(
                GaffDefault,
                1f,
                Mathf.InverseLerp(80f, 120f, apparentAngle));
        }

        return Mathf.Lerp(
            1f,
            GaffDefault,
            Mathf.InverseLerp(120f, 160f, apparentAngle));
    }

    private static float ApparentAngle(Sail sail)
    {
        return Mathf.Abs(Vector3.SignedAngle(
            -sail.shipRigidbody.transform.forward,
            sail.apparentWind,
            Vector3.up));
    }
}
