using System;
using System.Collections.Generic;
using System.Reflection;
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
        private static FieldInfo? _developerField;
        private static AccessTools.FieldRef<PerkVM, Func<PerkObject, bool>>? _getIsPerkSelectedRef;
        private static AccessTools.FieldRef<PerkVM, Func<PerkObject, bool>>? _getIsPreviousPerkSelectedRef;
        private static AccessTools.FieldRef<PerkVM, bool>? _isAvailableRef;
        private static Action<PerkVM, PerkVM.PerkStates>? _setCurrentStateRef;

        internal static bool TryGetDeveloper(PerkSelectionVM instance, out object? developer)
        {
            developer = null;
            if (!EnsureSelectionRefs())
            {
                return false;
            }

            developer = _developerField?.GetValue(instance);
            return developer != null;
        }

        internal static Hero? GetDeveloperHero(object? developer)
        {
            return developer?.GetType().GetProperty("Hero")?.GetValue(developer, null) as Hero;
        }

        internal static void AddPerk(object developer, PerkObject perk)
        {
            developer.GetType().GetMethod("AddPerk", new[] { typeof(PerkObject) })?.Invoke(developer, new object[] { perk });
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

        internal static Hero? GetSelectorHero(Func<PerkObject, bool>? getIsPerkSelected)
        {
            object? target = getIsPerkSelected?.Target;
            if (target == null)
            {
                return null;
            }

            Hero? directHero = target.GetType().GetProperty("Hero", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target, null) as Hero;
            if (directHero != null)
            {
                return directHero;
            }

            Hero? developerHero = GetDeveloperHero(target);
            if (developerHero != null)
            {
                return developerHero;
            }

            object? heroItem = target.GetType().GetField("_heroItem", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
            return heroItem?.GetType().GetProperty("Hero", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(heroItem, null) as Hero;
        }

        private static bool EnsureSelectionRefs()
        {
            if (_selectionRefsUnavailable)
            {
                return false;
            }

            if (_selectedPerksRef != null && _refreshPerksOfRef != null && _developerField != null)
            {
                return true;
            }

            try
            {
                _selectedPerksRef = AccessTools.FieldRefAccess<PerkSelectionVM, List<PerkObject>>("_selectedPerks");
                _refreshPerksOfRef = AccessTools.FieldRefAccess<PerkSelectionVM, Action<SkillObject>>("_refreshPerksOf");
                _developerField = AccessTools.Field(typeof(PerkSelectionVM), "_developer");
                if (_developerField == null)
                {
                    throw new MissingFieldException(typeof(PerkSelectionVM).FullName, "_developer");
                }
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

    [HarmonyPatch(typeof(PerkSelectionVM), "OnSelectPerk", new Type[] { typeof(PerkSelectionItemVM) })]
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

        internal static void BeforeSelect(PerkObject? selectedPerk, PerkSelectionVM instance, out SelectionState state)
        {
            state = default;

            try
            {
                BothPerksSettings? settings = BothPerksSettings.Instance;
                if (settings == null)
                {
                    return;
                }

                if (settings.BehaviorMode != PerkBehaviorMode.Freedom || selectedPerk == null)
                {
                    return;
                }

                if (!PerkUiAccess.TryGetDeveloper(instance, out object? developer))
                {
                    return;
                }

                Hero? hero = PerkUiAccess.GetDeveloperHero(developer);
                if (hero == null || !IsHeroInScope(hero, settings.Scope))
                {
                    return;
                }

                PerkObject? alternative = selectedPerk.AlternativePerk;
                if (alternative == null)
                {
                    return;
                }

                bool alternativeIsPending = PerkUiAccess.TryGetSelectedPerks(instance, out List<PerkObject>? selectedPerks) &&
                                            selectedPerks != null &&
                                            selectedPerks.Contains(alternative);

                state.IsFreedomMode = true;
                state.Alternative = alternative;
                state.AlternativeWasSelected = hero.GetPerkValue(alternative) || alternativeIsPending;
            }
            catch (Exception ex)
            {
                Debug.Print($"[BothPerks] PerkSelectionVM.OnSelectPerk prefix failed: {ex}");
            }
        }

        internal static void AfterSelect(PerkObject? selectedPerk, PerkSelectionVM instance, SelectionState state)
        {
            try
            {
                BothPerksSettings? settings = BothPerksSettings.Instance;
                if (settings == null || selectedPerk == null)
                {
                    return;
                }

                if (state.IsFreedomMode)
                {
                    // If the game unselected the alternative during this pick, restore it so both stay selected.
                    PerkObject? alternative = state.Alternative;
                    if (alternative == null)
                    {
                        return;
                    }

                    if (!PerkUiAccess.TryGetDeveloper(instance, out object? developer))
                    {
                        return;
                    }

                    Hero? hero = PerkUiAccess.GetDeveloperHero(developer);
                    if (hero == null || !IsHeroInScope(hero, settings.Scope))
                    {
                        return;
                    }

                    if (developer == null)
                    {
                        return;
                    }

                    if (!state.AlternativeWasSelected)
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

                    PerkUiAccess.AddPerk(developer, alternative);
                    Action<SkillObject>? refreshAlt = PerkUiAccess.GetRefreshPerksOf(instance);
                    refreshAlt?.Invoke(alternative.Skill);
                    return;
                }

                if (!IsModeManualDoublePick())
                {
                    return;
                }

                PerkObject? manualAlternative = selectedPerk.AlternativePerk;
                if (manualAlternative == null)
                {
                    return;
                }

                if (settings.SkipDoctorsOath && (IsDoctorsOath(selectedPerk) || IsDoctorsOath(manualAlternative)))
                {
                    return;
                }

                if (!PerkUiAccess.TryGetDeveloper(instance, out object? manualDeveloper))
                {
                    return;
                }

                Hero? manualHero = PerkUiAccess.GetDeveloperHero(manualDeveloper);
                if (manualHero == null || !IsHeroInScope(manualHero, settings.Scope))
                {
                    return;
                }

                if (manualHero.GetPerkValue(manualAlternative))
                {
                    return;
                }

                if (!PerkUiAccess.TryGetSelectedPerks(instance, out List<PerkObject>? selectedPerks))
                {
                    return;
                }

                if (selectedPerks == null)
                {
                    return;
                }

                if (selectedPerks.Contains(manualAlternative))
                {
                    return;
                }

                selectedPerks.Add(manualAlternative);

                // Refresh the UI for this skill so both perks show as selected immediately.
                Action<SkillObject>? refresh = PerkUiAccess.GetRefreshPerksOf(instance);
                refresh?.Invoke(selectedPerk.Skill);
            }
            catch (Exception ex)
            {
                Debug.Print($"[BothPerks] PerkSelectionVM.OnSelectPerk postfix failed: {ex}");
            }
        }

        public static void Prefix(PerkSelectionItemVM selectedPerk, PerkSelectionVM __instance, out SelectionState __state)
        {
            BeforeSelect(selectedPerk?.Perk, __instance, out __state);
        }

        public static void Postfix(PerkSelectionItemVM selectedPerk, PerkSelectionVM __instance, SelectionState __state)
        {
            AfterSelect(selectedPerk?.Perk, __instance, __state);
        }
    }

    [HarmonyPatch]
    internal static class PerkSelectionVm_OnSelectPerkVm_Patch
    {
        public static bool Prepare()
        {
            return TargetMethod() != null;
        }

        public static MethodBase? TargetMethod()
        {
            return AccessTools.Method(typeof(PerkSelectionVM), "OnSelectPerk", new Type[] { typeof(PerkVM) });
        }

        public static void Prefix(PerkVM selectedPerk, PerkSelectionVM __instance, out PerkSelectionVm_OnSelectPerk_Patch.SelectionState __state)
        {
            PerkSelectionVm_OnSelectPerk_Patch.BeforeSelect(selectedPerk?.Perk, __instance, out __state);
        }

        public static void Postfix(PerkVM selectedPerk, PerkSelectionVM __instance, PerkSelectionVm_OnSelectPerk_Patch.SelectionState __state)
        {
            PerkSelectionVm_OnSelectPerk_Patch.AfterSelect(selectedPerk?.Perk, __instance, __state);
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
                if (settings == null)
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

                if (getIsPerkSelected == null || getIsPreviousSelected == null || setCurrentState == null)
                {
                    return true;
                }

                if (!PerkVm_HasAlternativeSelected_Patch.ShouldIgnoreAlternativeSelection(
                        __instance,
                        settings,
                        getIsPerkSelected))
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
        private const string DoctorsOathStringId = "MedicineDoctorsOath";

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

        internal static bool ShouldIgnoreAlternativeSelection(
            PerkVM instance,
            BothPerksSettings settings,
            Func<PerkObject, bool>? getIsPerkSelected)
        {
            if (settings == null || getIsPerkSelected == null)
            {
                return false;
            }

            PerkBehaviorMode mode = settings.BehaviorMode;
            if (mode != PerkBehaviorMode.Freedom && mode != PerkBehaviorMode.Manual)
            {
                return false;
            }

            Hero? hero = PerkUiAccess.GetSelectorHero(getIsPerkSelected);
            if (hero != null && !IsHeroInScope(hero, settings.Scope))
            {
                return false;
            }

            if (mode == PerkBehaviorMode.Freedom)
            {
                return true;
            }

            PerkObject? alternative = instance.Perk.AlternativePerk;
            if (alternative == null)
            {
                return false;
            }

            if (settings.SkipDoctorsOath && (IsDoctorsOath(instance.Perk) || IsDoctorsOath(alternative)))
            {
                return false;
            }

            return getIsPerkSelected(alternative);
        }

        public static bool Prefix(PerkVM __instance, ref bool __result)
        {
            try
            {
                BothPerksSettings? settings = BothPerksSettings.Instance;
                if (settings == null)
                {
                    return true;
                }

                if (!PerkUiAccess.TryGetIsPerkSelected(__instance, out Func<PerkObject, bool>? getIsPerkSelected))
                {
                    return true;
                }

                if (!ShouldIgnoreAlternativeSelection(__instance, settings, getIsPerkSelected))
                {
                    return true;
                }

                // In unlocked modes and in-scope: never report "alternative already selected".
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
