using System;
using UnityEngine;

namespace SailRebalance
{
	internal sealed class LateenBraceController : RopeController
	{
		internal LateenYardRig Rig { get; private set; }

		internal void Initialize(LateenYardRig rig)
		{
			this.Rig = rig;
			this.currentLength = 0f;
			this.currentResistance = 0f;
			this.changed = true;
		}

		public override bool CanPull()
		{
			return this.Rig != null && this.Rig.CanOperateBrace;
		}

		private void Update()
		{
			this.currentLength = Mathf.Clamp01(this.currentLength);
			this.changed = false;
		}
	}
}
