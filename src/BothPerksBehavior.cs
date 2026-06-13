using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BothPerks
{
    internal sealed class BothPerksBehavior : CampaignBehaviorBase, IGameStateManagerListener
    {
        private const string DoctorsOathStringId = "MedicineDoctorsOath";

        private PerkApplicationScope _scope;
        private bool _grantAlternativeOnPick;
        private bool _autoAssignPerks;
        private bool _skipDoctorsOath;
        private PerkBehaviorMode _mode;
        private static Dictionary<SkillObject, PerkObject[]>? _perksBySkill;
        private static PerkObject? _doctorsOathPerk;
        private static Action<Hero, PerkObject, bool>? _setPerkValueInternal;

        public BothPerksBehavior()
        {
            BothPerksSettings? settings = BothPerksSettings.Instance;
            _scope = settings?.Scope ?? PerkApplicationScope.PlayerFamilyAndCompanions;
            _skipDoctorsOath = settings?.SkipDoctorsOath ?? false;
            SetMode(settings?.BehaviorMode ?? PerkBehaviorMode.Auto);
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HeroCreated.AddNonSerializedListener(this, OnHeroCreated);
            CampaignEvents.HeroGainedSkill.AddNonSerializedListener(this, OnHeroGainedSkill);
            CampaignEvents.NewCompanionAdded.AddNonSerializedListener(this, OnNewCompanionAdded);
            CampaignEvents.OnHeroChangedClanEvent.AddNonSerializedListener(this, OnHeroChangedClan);
            CampaignEvents.PerkOpenedEvent.AddNonSerializedListener(this, OnPerkOpened);

            // Refresh perks when character UI opens.
            Game.Current?.GameStateManager?.RegisterListener(this);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            SafeRun(() =>
            {
                RefreshSettings();

                if (!_autoAssignPerks)
                {
                    return;
                }

                SubModule.EnsureXpModel(starter);

                foreach (Hero hero in Hero.AllAliveHeroes)
                {
                    GrantAvailablePerks(hero, settingsRefreshed: true);
                }
            }, "OnSessionLaunched");
        }

        private void OnHeroCreated(Hero hero, bool isBornNaturally)
        {
            SafeRun(() =>
            {
                RefreshSettings();

                if (!_autoAssignPerks)
                {
                    return;
                }

                GrantAvailablePerks(hero, settingsRefreshed: true);
            }, "OnHeroCreated");
        }

        private void OnHeroGainedSkill(Hero hero, SkillObject skill, int change, bool shouldNotify)
        {
            SafeRun(() =>
            {
                RefreshSettings();

                if (!_autoAssignPerks)
                {
                    return;
                }

                GrantPerksForSkill(hero, skill, settingsRefreshed: true);
            }, "OnHeroGainedSkill");
        }

        private void OnNewCompanionAdded(Hero hero)
        {
            SafeRun(() =>
            {
                if (hero == null)
                {
                    return;
                }

                RefreshSettings();

                if (!_autoAssignPerks)
                {
                    return;
                }

                if (_scope != PerkApplicationScope.PlayerFamilyAndCompanions &&
                    _scope != PerkApplicationScope.CompanionsAndFamilyOnly)
                {
                    return;
                }

                if (!hero.IsPlayerCompanion)
                {
                    return;
                }

                GrantAvailablePerks(hero, settingsRefreshed: true);
            }, "OnNewCompanionAdded");
        }

        private void OnHeroChangedClan(Hero hero, Clan oldClan)
        {
            SafeRun(() =>
            {
                if (hero == null)
                {
                    return;
                }

                RefreshSettings();

                if (!_autoAssignPerks || _scope == PerkApplicationScope.PlayerOnly)
                {
                    return;
                }

                Clan newClan = hero.Clan;
                if (newClan == null || newClan != Clan.PlayerClan || oldClan == Clan.PlayerClan)
                {
                    return;
                }

                GrantAvailablePerks(hero, settingsRefreshed: true);
            }, "OnHeroChangedClan");
        }

        private void OnPerkOpened(Hero hero, PerkObject perk)
        {
            SafeRun(() =>
            {
                if (hero == null || perk == null)
                {
                    return;
                }

                RefreshSettings();

                if (!IsHeroInScope(hero))
                {
                    return;
                }

                if (ShouldSkipDoctorsOathNow() && IsDoctorsOath(perk))
                {
                    RemoveDoctorsOath(hero);
                    return;
                }

                if (!_grantAlternativeOnPick || !_autoAssignPerks)
                {
                    return;
                }

                PerkObject alternative = perk.AlternativePerk;
                if (ShouldSkipDoctorsOathNow() && IsDoctorsOath(alternative))
                {
                    return;
                }

                if (alternative == null || hero.GetPerkValue(alternative))
                {
                    return;
                }

                HeroDeveloper developer = hero.HeroDeveloper;
                if (developer == null)
                {
                    return;
                }

                developer.AddPerk(alternative);
                // developer.AddPerk(alternative) should handle the event firing implicitly.
                // CampaignEventDispatcher.Instance?.OnPerkOpened(hero, alternative);
            }, "OnPerkOpened");
        }

        public void OnPushState(GameState gameState, bool isTopGameState)
        {
            SafeRun(() =>
            {
                if (gameState is CharacterDeveloperState)
                {
                    RefreshSettings();
                    RefreshPlayerClanPerks(settingsRefreshed: true);
                }
            }, "OnPushState");
        }

        private void RefreshPlayerClanPerks(bool settingsRefreshed = false)
        {
            if (!_autoAssignPerks)
            {
                return;
            }

            Clan playerClan = Clan.PlayerClan;
            Hero mainHero = Hero.MainHero;
            var processed = new HashSet<Hero>();

            if (mainHero != null)
            {
                processed.Add(mainHero);
                GrantAvailablePerks(mainHero, settingsRefreshed);
            }

            if (playerClan == null)
            {
                return;
            }

            foreach (Hero hero in playerClan.Heroes)
            {
                if (hero == null || !hero.IsAlive)
                {
                    continue;
                }

                if (!processed.Add(hero))
                {
                    continue;
                }

                GrantAvailablePerks(hero, settingsRefreshed);
            }
        }

        // Unused IGameStateManagerListener members.
        public void OnCreateState(GameState gameState) { }
        public void OnPopState(GameState gameState) { }
        public void OnCleanStates() { }
        public void OnSavedGameLoadFinished() { }

        private static void SafeRun(Action action, string context)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.Print($"[BothPerks] {context} failed: {ex}");
            }
        }

        private void GrantAvailablePerks(Hero hero, bool settingsRefreshed = false)
        {
            if (hero == null || hero.HeroDeveloper == null || !hero.IsAlive)
            {
                return;
            }

            if (!_autoAssignPerks)
            {
                return;
            }

            if (!IsHeroInScope(hero))
            {
                return;
            }

            if (!settingsRefreshed)
            {
                RefreshSettings();
            }
            RemoveDoctorsOath(hero);

            HeroDeveloper developer = hero.HeroDeveloper;

            foreach (PerkObject perk in PerkObject.All)
            {
                if (ShouldSkipDoctorsOath(perk))
                {
                    continue;
                }

                if (perk.Skill == null)
                {
                    continue;
                }

                if (hero.GetPerkValue(perk))
                {
                    continue;
                }

                if (hero.GetSkillValue(perk.Skill) >= perk.RequiredSkillValue)
                {
                    developer.AddPerk(perk);
                }
            }
        }

        private void GrantPerksForSkill(Hero hero, SkillObject skill, bool settingsRefreshed = false)
        {
            if (hero == null || hero.HeroDeveloper == null || !hero.IsAlive || skill == null)
            {
                return;
            }

            if (!_autoAssignPerks)
            {
                return;
            }

            if (!IsHeroInScope(hero))
            {
                return;
            }

            if (!settingsRefreshed)
            {
                RefreshSettings();
            }
            RemoveDoctorsOath(hero);

            Dictionary<SkillObject, PerkObject[]> perksBySkill = GetPerksBySkill();
            if (!perksBySkill.TryGetValue(skill, out PerkObject[]? perksForSkill) || perksForSkill.Length == 0)
            {
                return;
            }

            HeroDeveloper developer = hero.HeroDeveloper;
            int heroSkillValue = hero.GetSkillValue(skill);

            foreach (PerkObject perk in perksForSkill)
            {
                if (ShouldSkipDoctorsOath(perk))
                {
                    continue;
                }

                if (hero.GetPerkValue(perk))
                {
                    continue;
                }

                if (heroSkillValue >= perk.RequiredSkillValue)
                {
                    developer.AddPerk(perk);
                }
            }
        }

        private static Dictionary<SkillObject, PerkObject[]> GetPerksBySkill()
        {
            if (_perksBySkill != null)
            {
                return _perksBySkill;
            }

            var dict = new Dictionary<SkillObject, List<PerkObject>>();

            foreach (PerkObject perk in PerkObject.All)
            {
                if (perk.Skill == null)
                {
                    continue;
                }

                if (!dict.TryGetValue(perk.Skill, out List<PerkObject>? list))
                {
                    list = new List<PerkObject>();
                    dict.Add(perk.Skill, list);
                }

                list.Add(perk);
            }

            _perksBySkill = new Dictionary<SkillObject, PerkObject[]>(dict.Count);
            foreach (KeyValuePair<SkillObject, List<PerkObject>> pair in dict)
            {
                _perksBySkill[pair.Key] = pair.Value.ToArray();
            }

            return _perksBySkill;
        }

        private void RefreshSettings()
        {
            BothPerksSettings? settings = BothPerksSettings.Instance;
            bool currentSkipDoctorsOath = settings?.SkipDoctorsOath ?? false;
            PerkBehaviorMode currentMode = settings?.BehaviorMode ?? PerkBehaviorMode.Auto;
            PerkApplicationScope currentScope = settings?.Scope ?? PerkApplicationScope.PlayerFamilyAndCompanions;

            bool skipChanged = currentSkipDoctorsOath != _skipDoctorsOath;
            _skipDoctorsOath = currentSkipDoctorsOath;
            SetMode(currentMode);
            _scope = currentScope;

            if (skipChanged && ShouldSkipDoctorsOathNow())
            {
                RemoveDoctorsOathFromAllInScopeHeroes();
            }
        }

        private void SetMode(PerkBehaviorMode mode)
        {
            _mode = mode;
            switch (mode)
            {
                case PerkBehaviorMode.Manual:
                    _autoAssignPerks = false;
                    _grantAlternativeOnPick = true;
                    break;
                case PerkBehaviorMode.Auto:
                    _autoAssignPerks = true;
                    _grantAlternativeOnPick = true;
                    break;
                case PerkBehaviorMode.Freedom:
                    _autoAssignPerks = false;
                    _grantAlternativeOnPick = false;
                    break;
                default:
                    _autoAssignPerks = true;
                    _grantAlternativeOnPick = true;
                    break;
            }
        }

        private void RemoveDoctorsOathFromAllInScopeHeroes()
        {
            if (!ShouldSkipDoctorsOathNow())
            {
                return;
            }

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (IsHeroInScope(hero))
                {
                    RemoveDoctorsOath(hero);
                }
            }
        }

        private void RemoveDoctorsOath(Hero hero)
        {
            if (!ShouldSkipDoctorsOathNow() || hero == null)
            {
                return;
            }

            PerkObject? doctorsOath = GetDoctorsOathPerk();
            if (doctorsOath == null || !hero.GetPerkValue(doctorsOath))
            {
                return;
            }

            Action<Hero, PerkObject, bool>? setter = GetPerkSetter();
            if (setter == null)
            {
                return;
            }

            try
            {
                setter(hero, doctorsOath, false);
            }
            catch (Exception ex)
            {
                Debug.Print($"[BothPerks] Failed to remove Doctor's Oath from {hero?.Name}: {ex}");
            }
        }

        private Action<Hero, PerkObject, bool>? GetPerkSetter()
        {
            if (_setPerkValueInternal != null)
            {
                return _setPerkValueInternal;
            }

            MethodInfo? method = typeof(Hero).GetMethod("SetPerkValueInternal", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                Debug.Print("[BothPerks] Unable to reflect Hero.SetPerkValueInternal; Doctor's Oath removal disabled.");
                return null;
            }

            try
            {
                _setPerkValueInternal = (Action<Hero, PerkObject, bool>)Delegate.CreateDelegate(
                    typeof(Action<Hero, PerkObject, bool>), null, method);
            }
            catch (Exception ex)
            {
                Debug.Print($"[BothPerks] Failed to bind Hero.SetPerkValueInternal delegate: {ex}");
                _setPerkValueInternal = null;
            }

            return _setPerkValueInternal;
        }

        private static PerkObject? GetDoctorsOathPerk()
        {
            if (_doctorsOathPerk != null)
            {
                return _doctorsOathPerk;
            }

            try
            {
                _doctorsOathPerk = DefaultPerks.Medicine.DoctorsOath;
                return _doctorsOathPerk;
            }
            catch (Exception)
            {
                // Fallback below.
            }

            foreach (PerkObject perk in PerkObject.All)
            {
                if (IsDoctorsOath(perk))
                {
                    _doctorsOathPerk = perk;
                    break;
                }
            }

            return _doctorsOathPerk;
        }

        private bool ShouldSkipDoctorsOath(PerkObject perk)
        {
            return ShouldSkipDoctorsOathNow() && IsDoctorsOath(perk);
        }

        private bool ShouldSkipDoctorsOathNow()
        {
            return _mode != PerkBehaviorMode.Freedom && _skipDoctorsOath;
        }

        private static bool IsDoctorsOath(PerkObject? perk)
        {
            return perk != null && perk.StringId == DoctorsOathStringId;
        }

        private bool IsHeroInScope(Hero hero)
        {
            if (hero == null)
            {
                return false;
            }

            switch (_scope)
            {
                case PerkApplicationScope.PlayerOnly:
                    return hero == Hero.MainHero;

                case PerkApplicationScope.PlayerFamilyAndCompanions:
                    return hero == Hero.MainHero ||
                           hero.IsPlayerCompanion ||
                           hero.Clan == Clan.PlayerClan;

                case PerkApplicationScope.CompanionsAndFamilyOnly:
                    return hero != Hero.MainHero &&
                           (hero.IsPlayerCompanion || hero.Clan == Clan.PlayerClan);

                case PerkApplicationScope.AllHeroes:
                    return true;

                default:
                    return false;
            }
        }
    }
}
