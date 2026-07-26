# BothPerks Guidance

This mod is intentionally flattened: the source root is `mods\both_perks`, not `mods\both_perks\both_perks`.

Shared tooling and path config live in `..\personalTooling`.

Use:

```powershell
.\BuildBothPerks.ps1 -ReferenceVersion v1_4
..\personalTooling\BuildReleases.ps1 -GameVersion v1_4 -ModName BothPerks
..\personalTooling\StageLocalMods.ps1 -GameVersion v1_4 -ModName BothPerks
```

Do not recreate `ModPaths.local.psd1`, `ModPaths.example.psd1`, or `tools\ModPaths.psm1` in this folder.
