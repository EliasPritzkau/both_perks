using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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

    internal sealed class BothPerksBehavior : CampaignBehaviorBase
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
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            if (_scope == PerkApplicationScope.Disabled)
            {
                return;
            }

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                GrantAvailablePerks(hero);
            }
        }

        private void OnHeroCreated(Hero hero, bool isBornNaturally)
        {
            if (_scope == PerkApplicationScope.Disabled)
            {
                return;
            }

            GrantAvailablePerks(hero);
        }

        private void OnHeroGainedSkill(Hero hero, SkillObject skill, int change, bool shouldNotify)
        {
            if (_scope == PerkApplicationScope.Disabled)
            {
                return;
            }

            GrantPerksForSkill(hero, skill);
        }

        private void OnNewCompanionAdded(Hero hero)
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
        }

        private void OnHeroChangedClan(Hero hero, Clan oldClan)
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
