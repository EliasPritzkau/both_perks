using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BothPerks
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);

            if (game.GameType is Campaign && gameStarter is CampaignGameStarter campaignStarter)
            {
                campaignStarter.AddBehavior(new BothPerksBehavior());
            }
        }
    }

    internal sealed class BothPerksBehavior : CampaignBehaviorBase, IGameStateManagerListener
    {
        private readonly PerkApplicationScope _scope;

        private static Dictionary<SkillObject, PerkObject[]>? _perksBySkill;

        public BothPerksBehavior()
        {
            BothPerksSettings? settings = BothPerksSettings.Instance;
            _scope = settings?.Scope ?? PerkApplicationScope.PlayerFamilyAndCompanions;
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
                if (_scope == PerkApplicationScope.Disabled)
                {
                    return;
                }

                foreach (Hero hero in Hero.AllAliveHeroes)
                {
                    GrantAvailablePerks(hero);
                }
            }, "OnSessionLaunched");
        }

        private void OnHeroCreated(Hero hero, bool isBornNaturally)
        {
            SafeRun(() =>
            {
                if (_scope == PerkApplicationScope.Disabled)
                {
                    return;
                }

                GrantAvailablePerks(hero);
            }, "OnHeroCreated");
        }

        private void OnHeroGainedSkill(Hero hero, SkillObject skill, int change, bool shouldNotify)
        {
            SafeRun(() =>
            {
                if (_scope == PerkApplicationScope.Disabled)
                {
                    return;
                }

                GrantPerksForSkill(hero, skill);
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

                if (_scope != PerkApplicationScope.PlayerFamilyAndCompanions &&
                    _scope != PerkApplicationScope.CompanionsAndFamilyOnly)
                {
                    return;
                }

                if (!hero.IsPlayerCompanion)
                {
                    return;
                }

                GrantAvailablePerks(hero);
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

                if (_scope == PerkApplicationScope.Disabled ||
                    _scope == PerkApplicationScope.PlayerOnly ||
                    _scope == PerkApplicationScope.AllHeroes)
                {
                    return;
                }

                Clan newClan = hero.Clan;
                if (newClan == null || newClan != Clan.PlayerClan || oldClan == Clan.PlayerClan)
                {
                    return;
                }

                GrantAvailablePerks(hero);
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

                if (_scope == PerkApplicationScope.Disabled || !IsHeroInScope(hero))
                {
                    return;
                }

                PerkObject alternative = perk.AlternativePerk;
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
            }, "OnPerkOpened");
        }

        public void OnPushState(GameState gameState, bool isTopGameState)
        {
            SafeRun(() =>
            {
                if (gameState is CharacterDeveloperState)
                {
                    RefreshPlayerClanPerks();
                }
            }, "OnPushState");
        }

        private void RefreshPlayerClanPerks()
        {
            if (_scope == PerkApplicationScope.Disabled)
            {
                return;
            }

            Clan playerClan = Clan.PlayerClan;
            Hero mainHero = Hero.MainHero;
            var processed = new HashSet<Hero>();

            if (mainHero != null)
            {
                processed.Add(mainHero);
                GrantAvailablePerks(mainHero);
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

                GrantAvailablePerks(hero);
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

        private void GrantAvailablePerks(Hero hero)
        {
            if (hero == null || hero.HeroDeveloper == null || !hero.IsAlive)
            {
                return;
            }

            if (!IsHeroInScope(hero))
            {
                return;
            }

            HeroDeveloper developer = hero.HeroDeveloper;

            foreach (PerkObject perk in PerkObject.All)
            {
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

        private void GrantPerksForSkill(Hero hero, SkillObject skill)
        {
            if (hero == null || hero.HeroDeveloper == null || !hero.IsAlive || skill == null)
            {
                return;
            }

            if (!IsHeroInScope(hero))
            {
                return;
            }

            Dictionary<SkillObject, PerkObject[]> perksBySkill = GetPerksBySkill();
            if (!perksBySkill.TryGetValue(skill, out PerkObject[]? perksForSkill) || perksForSkill.Length == 0)
            {
                return;
            }

            HeroDeveloper developer = hero.HeroDeveloper;
            int heroSkillValue = hero.GetSkillValue(skill);

            foreach (PerkObject perk in perksForSkill)
            {
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

        private bool IsHeroInScope(Hero hero)
        {
            if (hero == null)
            {
                return false;
            }

            if (_scope == PerkApplicationScope.Disabled)
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
