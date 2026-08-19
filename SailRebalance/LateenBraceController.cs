using UnityEngine;

namespace SailRebalance;

internal sealed class LateenBraceController : RopeController
{
    internal LateenYardRig Rig { get; private set; }

    internal void Initialize(LateenYardRig rig)
    {
        Rig = rig;
        currentLength = 1f;
        currentResistance = 0f;
        changed = true;
    }

    public override bool CanPull()
    {
        return Rig != null && Rig.CanOperateBrace;
    }

    private void Update()
    {
        currentLength = Mathf.Clamp01(currentLength);
        changed = false;
    }
}
