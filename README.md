both_perks

This is a Mount & Blade II: Bannerlord mod.

Relevant parts of this repo:
- `SubModule.xml` – module metadata.
- `src/` – C# source code and project file.

Build the mod by opening `src/BothPerks.csproj` in your C# IDE or building it with `dotnet build`, then use the resulting DLL in the module `bin` folder.
 
What the mod does:
- Gives heroes all perks they qualify for based on skill level, so they effectively get both perks in each skill tree.

Settings (in MCM):
- Perk Application Scope:
  - Player Only: only the main hero gets both perks.
  - Player Family And Companions: main hero, clan members, and player companions get both perks (default).
  - Companions And Family Only: clan members and player companions get both perks, but not the main hero.
  - All Heroes: every hero gets both perks.
  - Disabled: mod does nothing; no perks are added.
