# Sail Rebalance v1.1.10 reconstruction

This source tree was reconstructed from the installed `SailRebalance.dll`
version 1.1.10 using its matching portable PDB. The installed binary is the
behavioral authority for this baseline.

Reference copies of the original DLL and PDB are stored in `ReferenceBinary`.
Normal builds do not overwrite the installed plugin. To install a deliberate
future build, use:

```powershell
dotnet build --configuration Release -p:InstallAfterBuild=true
```

Because this is decompiled source, comments and some original expression-level
style cannot be recovered. Type names, members, constants, control flow,
Harmony targets, plugin metadata, and runtime calls are reconstructed from the
compiled assembly.

Authoritative v1.1.10 DLL SHA-256:

```text
0A272DF4F6E5AFC94AFA536EFA420DA07B3048DD5F1C24FD06E4A584F05D317D
```

Validation performed during reconstruction:

- Release build completed with zero warnings and zero errors.
- Rebuilt and original assemblies have the same assembly/plugin version,
  plugin GUID, type count, method count, field count, constants, and Harmony
  targets.
- Re-decompiling the rebuilt DLL produced the same reconstructed source as the
  reference DLL except for an immaterial explicit `this.` qualifier.
- The rebuilt DLL is not expected to be byte-identical because recompilation
  regenerates assembly/debug metadata.
