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

    internal static class PerkUiAccess
    {
        private static bool _selectionRefsUnavailable;
        private static bool _perkVmRefsUnavailable;
        private static AccessTools.FieldRef<PerkSelectionVM, List<PerkObject>>? _selectedPerksRef;
        private static AccessTools.FieldRef<PerkSelectionVM, Action<SkillObject>>? _refreshPerksOfRef;
        private static AccessTools.FieldRef<PerkSelectionVM, HeroDeveloper>? _developerRef;
        private static AccessTools.FieldRef<PerkVM, Func<PerkObject, bool>>? _getIsPerkSelectedRef;
        private static AccessTools.FieldRef<PerkVM, Func<PerkObject, bool>>? _getIsPreviousPerkSelectedRef;
        private static AccessTools.FieldRef<PerkVM, bool>? _isAvailableRef;
        private static Action<PerkVM, PerkVM.PerkStates>? _setCurrentStateRef;

        internal static bool TryGetDeveloper(PerkSelectionVM instance, out HeroDeveloper? developer)
        {
            developer = null;
            if (!EnsureSelectionRefs())
            {
                return false;
            }

            developer = _developerRef?.Invoke(instance);
            return developer != null;
        }

        internal static bool TryGetSelectedPerks(PerkSelectionVM instance, out List<PerkObject>? selectedPerks)
        {
            selectedPerks = null;
            if (!EnsureSelectionRefs())
            {
                return false;
            }

            selectedPerks = _selectedPerksRef?.Invoke(instance);
            return selectedPerks != null;
        }

        internal static Action<SkillObject>? GetRefreshPerksOf(PerkSelectionVM instance)
        {
            if (!EnsureSelectionRefs())
            {
                return null;
            }

            return _refreshPerksOfRef?.Invoke(instance);
        }

        internal static bool TryGetPerkVmAccess(
            PerkVM instance,
            out Func<PerkObject, bool>? getIsPerkSelected,
            out Func<PerkObject, bool>? getIsPreviousPerkSelected,
            out bool isAvailable,
            out Action<PerkVM, PerkVM.PerkStates>? setCurrentState)
        {
            getIsPerkSelected = null;
            getIsPreviousPerkSelected = null;
            isAvailable = false;
            setCurrentState = null;

            if (!EnsurePerkVmRefs())
            {
                return false;
            }

            getIsPerkSelected = _getIsPerkSelectedRef?.Invoke(instance);
            getIsPreviousPerkSelected = _getIsPreviousPerkSelectedRef?.Invoke(instance);
            isAvailable = _isAvailableRef?.Invoke(instance) ?? false;
            setCurrentState = _setCurrentStateRef;
            return getIsPerkSelected != null && getIsPreviousPerkSelected != null && setCurrentState != null;
        }

        internal static bool TryGetIsPerkSelected(PerkVM instance, out Func<PerkObject, bool>? getIsPerkSelected)
        {
            getIsPerkSelected = null;
            if (!EnsurePerkVmRefs())
            {
                return false;
            }

            getIsPerkSelected = _getIsPerkSelectedRef?.Invoke(instance);
            return getIsPerkSelected != null;
        }

        private static bool EnsureSelectionRefs()
        {
            if (_selectionRefsUnavailable)
            {
                return false;
            }

            if (_selectedPerksRef != null && _refreshPerksOfRef != null && _developerRef != null)
            {
                return true;
            }

            try
            {
                _selectedPerksRef = AccessTools.FieldRefAccess<PerkSelectionVM, List<PerkObject>>("_selectedPerks");
                _refreshPerksOfRef = AccessTools.FieldRefAccess<PerkSelectionVM, Action<SkillObject>>("_refreshPerksOf");
                _developerRef = AccessTools.FieldRefAccess<PerkSelectionVM, HeroDeveloper>("_developer");
                return true;
            }
            catch (Exception ex)
            {
                _selectionRefsUnavailable = true;
                Debug.Print($"[BothPerks] PerkSelectionVM private field access unavailable; UI double-pick helpers disabled: {ex}");
                return false;
            }
        }

        private static bool EnsurePerkVmRefs()
        {
            if (_perkVmRefsUnavailable)
            {
                return false;
            }

            if (_getIsPerkSelectedRef != null &&
                _getIsPreviousPerkSelectedRef != null &&
                _isAvailableRef != null &&
                _setCurrentStateRef != null)
            {
                return true;
            }

            try
            {
                _getIsPerkSelectedRef = AccessTools.FieldRefAccess<PerkVM, Func<PerkObject, bool>>("_getIsPerkSelected");
                _getIsPreviousPerkSelectedRef = AccessTools.FieldRefAccess<PerkVM, Func<PerkObject, bool>>("_getIsPreviousPerkSelected");
                _isAvailableRef = AccessTools.FieldRefAccess<PerkVM, bool>("_isAvailable");
                _setCurrentStateRef = AccessTools.PropertySetter(typeof(PerkVM), "CurrentState")?
                    .CreateDelegate(typeof(Action<PerkVM, PerkVM.PerkStates>)) as Action<PerkVM, PerkVM.PerkStates>;

                if (_setCurrentStateRef == null)
                {
                    throw new MissingMemberException(typeof(PerkVM).FullName, "CurrentState");
                }

                return true;
            }
            catch (Exception ex)
            {
                _perkVmRefsUnavailable = true;
                Debug.Print($"[BothPerks] PerkVM private field access unavailable; freedom-mode UI helpers disabled: {ex}");
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(PerkSelectionVM), "OnSelectPerk")]
    internal static class PerkSelectionVm_OnSelectPerk_Patch
    {
        private const string DoctorsOathStringId = "MedicineDoctorsOath";

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

                if (!PerkUiAccess.TryGetDeveloper(__instance, out HeroDeveloper? developer))
                {
                    return;
                }

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

                    if (!PerkUiAccess.TryGetDeveloper(__instance, out HeroDeveloper? developer))
                    {
                        return;
                    }

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
                    Action<SkillObject>? refreshAlt = PerkUiAccess.GetRefreshPerksOf(__instance);
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

                if (!PerkUiAccess.TryGetDeveloper(__instance, out HeroDeveloper? manualDeveloper))
                {
                    return;
                }

                Hero? manualHero = manualDeveloper?.Hero;
                if (manualHero == null || !IsHeroInScope(manualHero, settings.Scope))
                {
                    return;
                }

                if (manualHero.GetPerkValue(manualAlternative))
                {
                    return;
                }

                if (!PerkUiAccess.TryGetSelectedPerks(__instance, out List<PerkObject>? selectedPerks))
                {
                    return;
                }

                if (selectedPerks.Contains(manualAlternative))
                {
                    return;
                }

                selectedPerks.Add(manualAlternative);

                // Refresh the UI for this skill so both perks show as selected immediately.
                Action<SkillObject>? refresh = PerkUiAccess.GetRefreshPerksOf(__instance);
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
        public static bool Prefix(PerkVM __instance)
        {
            try
            {
                BothPerksSettings? settings = BothPerksSettings.Instance;
                if (settings == null || settings.BehaviorMode != PerkBehaviorMode.Freedom)
                {
                    return true;
                }

                if (!PerkUiAccess.TryGetPerkVmAccess(
                        __instance,
                        out Func<PerkObject, bool>? getIsPerkSelected,
                        out Func<PerkObject, bool>? getIsPreviousSelected,
                        out bool isAvailable,
                        out Action<PerkVM, PerkVM.PerkStates>? setCurrentState))
                {
                    return true;
                }

                bool isSelected = getIsPerkSelected?.Invoke(__instance.Perk) ?? false;

                if (!isAvailable)
                {
                    setCurrentState(__instance, PerkVM.PerkStates.NotEarned);
                    return false;
                }

                if (isSelected)
                {
                    setCurrentState(__instance, PerkVM.PerkStates.EarnedAndActive);
                    return false;
                }

                bool previousSelected = getIsPreviousSelected?.Invoke(__instance.Perk) ?? false;
                setCurrentState(__instance, previousSelected
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

                if (!PerkUiAccess.TryGetIsPerkSelected(__instance, out Func<PerkObject, bool>? getIsPerkSelected))
                {
                    return true;
                }

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
