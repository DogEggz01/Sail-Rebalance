# Sail Rebalance v1.1.11 source

This source tree is version 1.1.11. It was reconstructed from the installed
`SailRebalance.dll` version 1.1.10 using its matching portable PDB, then cleaned
and restructured. The installed v1.1.10 binary remains the behavioral authority
for the original baseline.

Reference copies of the original DLL and PDB are stored in `ReferenceBinary`.
Normal builds do not overwrite the installed plugin. To install a deliberate
future build, use:

```powershell
dotnet build --configuration Release -p:InstallAfterBuild=true
```

Because this is decompiled source, comments and some original expression-level
style cannot be recovered. The reference binary remains available when exact
v1.1.10 behavior or assembly structure needs to be checked.

Authoritative v1.1.10 DLL SHA-256:

```text
0A272DF4F6E5AFC94AFA536EFA420DA07B3048DD5F1C24FD06E4A584F05D317D
```

The reconstructed source was subsequently cleaned for readability:

- `LateenYardRig` is now a partial class split into core state/control,
  yard motion and geometry, and rope visuals.
- Related control patches, registry types, persistence patches, and force
  patches are grouped by responsibility.
- Decompiled temporary names and repeated numeric literals were replaced with
  descriptive names and constants. Angle conversion uses `Mathf.Rad2Deg`.
- The empty `LateenLowerBraceController.UpdateSailAttachment` override and the
  redundant `RopeControllerSailReef.Update` guard were removed. The
  `GPButtonRopeWinch.Update` patch remains responsible for blocking mouse,
  controller, keyboard rotation, and quick release on locked winches.

Validation performed after the readability cleanup:

- Release build completed with zero warnings and zero errors.
- The rebuilt assembly and plugin metadata report version 1.1.11. The plugin
  name and GUID remain compatible with the v1.1.10 baseline.
- The expected Harmony targets remain, except for the deliberately removed
  `RopeControllerSailReef.Update` guard. The two independent `Sail.ApplyForce`
  responsibilities are now represented by separate patch classes targeting
  the same vanilla method.
- Normal validation builds leave the installed v1.1.10 DLL unchanged.
- This cleanup has not yet been exercised in a running game session.
- The rebuilt DLL is not expected to be byte-identical because recompilation
  regenerates assembly/debug metadata and the source structure has changed.
