using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace BothPerks
{
    internal sealed class BothPerksXpModel : DefaultGenericXpModel
    {
        public override float GetXpMultiplier(Hero hero)
        {
            float baseMultiplier = base.GetXpMultiplier(hero);

            BothPerksSettings? settings = BothPerksSettings.Instance;
            if (settings == null)
            {
                return baseMultiplier;
            }

            float multiplier = Math.Max(0.1f, settings.SkillXpMultiplier);
            if (Math.Abs(multiplier - 1f) < 0.0001f)
            {
                return baseMultiplier;
            }

            if (!IsHeroInScope(hero, settings.Scope))
            {
                return baseMultiplier;
            }

            return baseMultiplier * multiplier;
        }

        private static bool IsHeroInScope(Hero hero, PerkApplicationScope scope)
        {
            if (hero == null)
            {
                return false;
            }

            switch (scope)
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
