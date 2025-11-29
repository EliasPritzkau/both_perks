using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace BothPerks
{
    public enum PerkApplicationScope
    {
        PlayerOnly = 0,
        PlayerFamilyAndCompanions = 1,
        CompanionsAndFamilyOnly = 2,
        AllHeroes = 3,
        Disabled = 4
    }

    // Settings class discovered by MCMv5.
    public sealed class BothPerksSettings : AttributeGlobalSettings<BothPerksSettings>
    {
        public override string Id => "BothPerksSettings_v1";
        public override string DisplayName => "Both Perks";
        public override string FolderName => "BothPerks";
        public override string FormatType => "json";

        [SettingPropertyGroup("General")]
        [SettingPropertyDropdown("Perk Application Scope", RequireRestart = true, Order = 0,
            HintText = "Choose which heroes receive both perks. Includes an option to disable the mod (no perks are assigned). Changes only take effect after restarting the game or reloading the campaign.")]
        public Dropdown<PerkApplicationScope> ScopeDropdown { get; set; } =
            new Dropdown<PerkApplicationScope>(
                new[]
                {
                    PerkApplicationScope.PlayerOnly,
                    PerkApplicationScope.PlayerFamilyAndCompanions,
                    PerkApplicationScope.CompanionsAndFamilyOnly,
                    PerkApplicationScope.AllHeroes,
                    PerkApplicationScope.Disabled
                },
                (int)PerkApplicationScope.PlayerFamilyAndCompanions);

        public PerkApplicationScope Scope => ScopeDropdown.SelectedValue;
    }
}
