using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper.PerkSelection;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace BothPerks
{
    internal static class HarmonyPatcher
    {
        private static bool _patched;

        internal static void ApplyPatches()
        {
            if (_patched)
            {
                return;
            }

            try
            {
                var harmony = new Harmony("bothperks.patches");
                harmony.PatchAll();
                _patched = true;
                Debug.Print("[BothPerks] Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Debug.Print($"[BothPerks] Harmony patching failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(PerkSelectionVM), "OnSelectPerk")]
    internal static class PerkSelectionVm_OnSelectPerk_Patch
    {
        private const string DoctorsOathStringId = "MedicineDoctorsOath";

        private static readonly AccessTools.FieldRef<PerkSelectionVM, List<PerkObject>> SelectedPerksRef =
            AccessTools.FieldRefAccess<PerkSelectionVM, List<PerkObject>>("_selectedPerks");

        private static readonly AccessTools.FieldRef<PerkSelectionVM, Action<SkillObject>> RefreshPerksOfRef =
            AccessTools.FieldRefAccess<PerkSelectionVM, Action<SkillObject>>("_refreshPerksOf");

        private static readonly AccessTools.FieldRef<PerkSelectionVM, HeroDeveloper> DeveloperRef =
            AccessTools.FieldRefAccess<PerkSelectionVM, HeroDeveloper>("_developer");

        internal struct SelectionState
        {
            public bool IsFreedomMode;
            public bool AlternativeWasSelected;
            public PerkObject? Alternative;
        }

        private static bool IsHeroInScope(Hero hero, PerkApplicationScope scope)
        {
            if (hero == null)
            {
                return false;
            }

            return scope switch
            {
                PerkApplicationScope.PlayerOnly => hero == Hero.MainHero,
                PerkApplicationScope.PlayerFamilyAndCompanions => hero == Hero.MainHero ||
                                                                  hero.IsPlayerCompanion ||
                                                                  hero.Clan == Clan.PlayerClan,
                PerkApplicationScope.CompanionsAndFamilyOnly => hero != Hero.MainHero &&
                                                                (hero.IsPlayerCompanion ||
                                                                 hero.Clan == Clan.PlayerClan),
                PerkApplicationScope.AllHeroes => true,
                _ => false
            };
        }

        private static bool IsDoctorsOath(PerkObject? perk)
        {
            return perk != null && perk.StringId == DoctorsOathStringId;
        }

        private static bool IsModeManualDoublePick()
        {
            BothPerksSettings? settings = BothPerksSettings.Instance;
            if (settings == null)
            {
                return false;
            }

            return settings.BehaviorMode switch
            {
                PerkBehaviorMode.Manual => true,
                PerkBehaviorMode.Auto => true,
                _ => false
            };
        }

        public static void Prefix(PerkSelectionItemVM selectedPerk, PerkSelectionVM __instance, out SelectionState __state)
        {
            __state = default;

            try
            {
                BothPerksSettings? settings = BothPerksSettings.Instance;
                if (settings == null)
                {
                    return;
                }

                if (settings.BehaviorMode != PerkBehaviorMode.Freedom || selectedPerk?.Perk == null)
                {
                    return;
                }

                HeroDeveloper developer = DeveloperRef(__instance);
                Hero? hero = developer?.Hero;
                if (hero == null || !IsHeroInScope(hero, settings.Scope))
                {
                    return;
                }

                PerkObject? alternative = selectedPerk.Perk.AlternativePerk;
                if (alternative == null)
                {
                    return;
                }

                __state.IsFreedomMode = true;
                __state.Alternative = alternative;
                __state.AlternativeWasSelected = hero.GetPerkValue(alternative);
            }
            catch (Exception ex)
            {
                Debug.Print($"[BothPerks] PerkSelectionVM.OnSelectPerk prefix failed: {ex}");
            }
        }

        public static void Postfix(PerkSelectionItemVM selectedPerk, PerkSelectionVM __instance, SelectionState __state)
        {
            try
            {
                BothPerksSettings? settings = BothPerksSettings.Instance;
                if (settings == null || selectedPerk?.Perk == null)
                {
                    return;
                }

                if (__state.IsFreedomMode)
                {
                    // If the game unselected the alternative during this pick, restore it so both stay selected.
                    PerkObject? alternative = __state.Alternative;
                    if (alternative == null)
                    {
                        return;
                    }

                    HeroDeveloper developer = DeveloperRef(__instance);
                    Hero? hero = developer?.Hero;
                    if (hero == null || !IsHeroInScope(hero, settings.Scope))
                    {
                        return;
                    }

                    if (developer == null)
                    {
                        return;
                    }

                    if (!__state.AlternativeWasSelected)
                    {
                        return;
                    }

                    if (hero.GetPerkValue(alternative))
                    {
                        return;
                    }

                    SkillObject skill = alternative.Skill;
                    if (skill == null || hero.GetSkillValue(skill) < alternative.RequiredSkillValue)
                    {
                        return;
                    }

                    developer.AddPerk(alternative);
                    Action<SkillObject> refreshAlt = RefreshPerksOfRef(__instance);
                    refreshAlt?.Invoke(alternative.Skill);
                    return;
                }

                if (!IsModeManualDoublePick())
                {
                    return;
                }

                if (selectedPerk?.Perk == null)
                {
                    return;
                }

                PerkObject? manualAlternative = selectedPerk.Perk.AlternativePerk;
                if (manualAlternative == null)
                {
                    return;
                }

                if (settings.SkipDoctorsOath && (IsDoctorsOath(selectedPerk.Perk) || IsDoctorsOath(manualAlternative)))
                {
                    return;
                }

                HeroDeveloper manualDeveloper = DeveloperRef(__instance);
                Hero? manualHero = manualDeveloper?.Hero;
                if (manualHero == null || !IsHeroInScope(manualHero, settings.Scope))
                {
                    return;
                }

                if (manualHero.GetPerkValue(manualAlternative))
                {
                    return;
                }

                List<PerkObject> selectedPerks = SelectedPerksRef(__instance);
                if (selectedPerks.Contains(manualAlternative))
                {
                    return;
                }

                selectedPerks.Add(manualAlternative);

                // Refresh the UI for this skill so both perks show as selected immediately.
                Action<SkillObject> refresh = RefreshPerksOfRef(__instance);
                refresh?.Invoke(selectedPerk.Perk.Skill);
            }
            catch (Exception ex)
            {
                Debug.Print($"[BothPerks] PerkSelectionVM.OnSelectPerk postfix failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(PerkVM), nameof(PerkVM.RefreshState))]
    internal static class PerkVm_RefreshState_Patch
    {
        private static readonly AccessTools.FieldRef<PerkVM, Func<PerkObject, bool>> GetIsPerkSelectedRef =
            AccessTools.FieldRefAccess<PerkVM, Func<PerkObject, bool>>("_getIsPerkSelected");

        private static readonly AccessTools.FieldRef<PerkVM, Func<PerkObject, bool>> GetIsPreviousPerkSelectedRef =
            AccessTools.FieldRefAccess<PerkVM, Func<PerkObject, bool>>("_getIsPreviousPerkSelected");

        private static readonly AccessTools.FieldRef<PerkVM, bool> IsAvailableRef =
            AccessTools.FieldRefAccess<PerkVM, bool>("_isAvailable");

        private static readonly AccessTools.FieldRef<PerkVM, bool> IsInSelectionRef =
            AccessTools.FieldRefAccess<PerkVM, bool>("_isInSelection");

        private static readonly Action<PerkVM, PerkVM.PerkStates>? SetCurrentStateRef =
            AccessTools.PropertySetter(typeof(PerkVM), "CurrentState")?
                .CreateDelegate(typeof(Action<PerkVM, PerkVM.PerkStates>)) as Action<PerkVM, PerkVM.PerkStates>;

        public static bool Prefix(PerkVM __instance)
        {
            try
            {
                BothPerksSettings? settings = BothPerksSettings.Instance;
                if (settings == null || settings.BehaviorMode != PerkBehaviorMode.Freedom)
                {
                    return true;
                }

                if (SetCurrentStateRef == null)
                {
                    return true;
                }

                Func<PerkObject, bool>? getIsPerkSelected = GetIsPerkSelectedRef(__instance);
                Func<PerkObject, bool>? getIsPreviousSelected = GetIsPreviousPerkSelectedRef(__instance);

                bool isAvailable = IsAvailableRef(__instance);
                bool isInSelection = IsInSelectionRef(__instance);
                bool isSelected = getIsPerkSelected?.Invoke(__instance.Perk) ?? false;

                if (!isAvailable)
                {
                    SetCurrentStateRef(__instance, PerkVM.PerkStates.NotEarned);
                    return false;
                }

                if (isInSelection)
                {
                    SetCurrentStateRef(__instance, PerkVM.PerkStates.InSelection);
                    return false;
                }

                if (isSelected)
                {
                    SetCurrentStateRef(__instance, PerkVM.PerkStates.EarnedAndActive);
                    return false;
                }

                bool previousSelected = getIsPreviousSelected?.Invoke(__instance.Perk) ?? false;
                SetCurrentStateRef(__instance, previousSelected
                    ? PerkVM.PerkStates.EarnedButNotSelected
                    : PerkVM.PerkStates.EarnedPreviousPerkNotSelected);

                return false;
            }
            catch (Exception ex)
            {
                Debug.Print($"[BothPerks] PerkVM.RefreshState prefix failed: {ex}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(PerkVM), "get__hasAlternativeAndSelected")]
    internal static class PerkVm_HasAlternativeSelected_Patch
    {
        private static readonly AccessTools.FieldRef<PerkVM, Func<PerkObject, bool>> GetIsPerkSelectedRef =
            AccessTools.FieldRefAccess<PerkVM, Func<PerkObject, bool>>("_getIsPerkSelected");

        private static bool IsHeroInScope(Hero hero, PerkApplicationScope scope)
        {
            if (hero == null)
            {
                return false;
            }

            return scope switch
            {
                PerkApplicationScope.PlayerOnly => hero == Hero.MainHero,
                PerkApplicationScope.PlayerFamilyAndCompanions => hero == Hero.MainHero ||
                                                                  hero.IsPlayerCompanion ||
                                                                  hero.Clan == Clan.PlayerClan,
                PerkApplicationScope.CompanionsAndFamilyOnly => hero != Hero.MainHero &&
                                                                (hero.IsPlayerCompanion ||
                                                                 hero.Clan == Clan.PlayerClan),
                PerkApplicationScope.AllHeroes => true,
                _ => false
            };
        }

        public static bool Prefix(PerkVM __instance, ref bool __result)
        {
            try
            {
                BothPerksSettings? settings = BothPerksSettings.Instance;
                if (settings == null || settings.BehaviorMode != PerkBehaviorMode.Freedom)
                {
                    return true;
                }

                Func<PerkObject, bool>? getIsPerkSelected = GetIsPerkSelectedRef(__instance);
                object? target = getIsPerkSelected?.Target;
                Hero? hero = (target as HeroDeveloper)?.Hero;

                if (hero != null && !IsHeroInScope(hero, settings.Scope))
                {
                    return true;
                }

                // In freedom mode and in-scope: never report "alternative already selected".
                __result = false;
                return false;
            }
            catch (Exception ex)
            {
                Debug.Print($"[BothPerks] PerkVM._hasAlternativeAndSelected getter prefix failed: {ex}");
                return true;
            }
        }
    }
}
