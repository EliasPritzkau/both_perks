both_perks

This is a Mount & Blade II: Bannerlord mod.

Relevant parts of this repo:
- `SubModule.xml` – module metadata.
- `src/` – C# source code and project file.

Build the mod by opening `src/BothPerks.csproj` in your C# IDE or building it with `dotnet build`, then use the resulting DLL in the module `bin` folder.
 
What the mod does:
- Gives heroes all perks they qualify for based on skill level, so they effectively get both perks in each skill tree.

Settings (in MCM):
- Perk handling mode (dropdown): Manual (double-pick), Auto (auto + manual fallback), or Freedom (manual picks stay unlocked; pick either/both if qualified).
- Perk Application Scope:
  - Player Only: only the main hero gets both perks.
  - Player Family And Companions: main hero, clan members, and player companions get both perks (default).
  - Companions And Family Only: clan members and player companions get both perks, but not the main hero.
  - All Heroes: every hero gets both perks.




  - Repository is a Bannerlord mod (both_perks solution at both_perks.sln) with source in both_perks/src and module assets in both_perks/SubModule.xml, both_perks/WorkshopUpdateBothPerks.xml, and both_perks/README.md.
  - Core behavior: both_perks/src/BothPerksBehavior.cs wires campaign events to auto-grant all eligible perks to in-scope heroes, optionally skipping Doctor’s Oath and granting the alternative perk when one is picked; scope and mode are refreshed from
    settings and integrated with character UI and clan changes.
  - Settings/MCM: both_perks/src/BothPerksSettings.cs defines MCM v5 settings (mode: manual/auto, scope options, skip Doctor’s Oath, skill XP multiplier). Dependencies pulled via BothPerks.csproj (Bannerlord assemblies, Harmony, MCM).
  - Harmony UI patch: both_perks/src/HarmonyPatches.cs patches PerkSelectionVM.OnSelectPerk so manual selection awards both perks in UI for in-scope heroes, honoring the skip-Doctor’s-Oath flag.
  - XP model: both_perks/src/BothPerksXpModel.cs multiplies skill XP for in-scope heroes based on settings; injected by both_perks/src/SubModule.cs, which also attaches the campaign behavior and applies Harmony patches at load.
  - Docs/meta: both_perks/src/DESCRIPTION.txt (mod summary), extra_info/.tasks.md and extra_info/AGENTS.md (repo guidelines: don’t edit MBSource/Archive; target BL 1.3.6; MCM allowed).

  Want me to check the compiled output in both_perks/bin, review the Backup folder, or run a build to verify it compiles?
