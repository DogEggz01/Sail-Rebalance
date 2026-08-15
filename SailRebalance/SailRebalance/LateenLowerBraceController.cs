using UnityEngine;

namespace SailRebalance;

internal sealed class LateenLowerBraceController : RopeController
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
		if (Rig != null)
		{
			return Rig.CanOperateLowerBrace;
		}
		return false;
	}

	public override void UpdateSailAttachment()
	{
	}

	private void Update()
	{
		currentLength = Mathf.Clamp01(currentLength);
		changed = false;
	}
}
