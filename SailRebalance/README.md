# Sail Rebalance

This BepInEx plugin replaces the original category constants in
`Sail.ApplyForce` with optional apparent-wind curves and adds a working
lower-brace control to lateen sails.

## Junk curve

- True wind at or below 21: 0.75 at 90 degrees, 1.00 at 150 degrees,
  and 0.85 at 180 degrees.
- True wind above 21: 0.85 at 90 degrees, 1.00 at 150 degrees, and
  0.90 at 180 degrees.
- When disabled, the configuration slider replaces the original 0.75 value.

## Gaff curve

- 0.85 at 80 degrees, 1.00 at 120 degrees, and 0.85 at 160 degrees.
- When disabled, the configuration slider replaces the original 0.85 value.

The threshold uses `Wind.currentWind.magnitude`. Angles remain apparent-wind
angles relative to the boat and work symmetrically on both sides.
Configuration changes take effect in real time.

## Lateen lower brace

Lateen sails receive a third winch using an otherwise unused mast winch slot.
The lower brace can move only while the sail is fully furled and every
configured sheet controller is fully loose. While the lower brace is away
from its normal position, the halyard and all sheet winches are locked,
including keyboard rotation and quick release. The lower-brace winch is
equally locked whenever its furling or loose-sheet checks are not met.

- Tightening the lower brace raises the yard vertically.
- During the final 15% of a pull, the yard follows a temporary clearance arc
  abaft and across the mast.
- Reaching 100% commits exactly one move to the opposite side of the mast.
- Mast and yard thickness determine the final clearance on the opposite side,
  with an additional 0.15 m clearance compensation.
- Yard thickness is measured where it crosses the mast, and the measurement is
  refreshed after shipyard scaling, install-height changes, or scaled-sail loading.
- A partial pull cancels if the lower brace returns to 70% or below.
- The winch must return to 70% or below before another full pull can change sides.
- Loosening the lower brace lowers the yard on its selected side.
- The lower-brace rope follows the vanilla sheet pattern through the mast's
  configured sheet fairlead position and attaches at the yard heel tip.

The selected yard side is stored in `GameState.modData`. Lower-brace tension is
reset to its normal 0% position on load, which keeps it compatible with
NANDTweaks ship-state restoration.

## Lateen bad tack

When enabled, a lateen on a bad tack produces 90% of its normal force. The
penalty can be toggled in the BepInEx configuration under `Lateen sails` and is
applied only inside `Sail.ApplyForce`.

Build with:

```powershell
dotnet build --configuration Release
```
