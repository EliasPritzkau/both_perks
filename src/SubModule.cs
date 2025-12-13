using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BothPerks
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            HarmonyPatcher.ApplyPatches();
        }

        internal static void EnsureXpModel(CampaignGameStarter starter)
        {
            if (starter == null)
            {
                return;
            }

            if (starter.Models != null)
            {
                foreach (GameModel model in starter.Models)
                {
                    if (model is BothPerksXpModel)
                    {
                        return;
                    }
                }
            }

            starter.AddModel(new BothPerksXpModel());
        }

        private static void TryAddBehavior(Game game, object starterObject, string context)
        {
            if (!(game.GameType is Campaign))
            {
                return;
            }

            if (starterObject is CampaignGameStarter campaignStarter)
            {
                // Ensure we only have a single instance wired into the campaign.
                campaignStarter.RemoveBehaviors<BothPerksBehavior>();
                campaignStarter.AddBehavior(new BothPerksBehavior());
                Debug.Print($"[BothPerks] Behavior attached in {context}.");
                return;
            }

            // Fallback: attach directly to the live campaign behavior manager (e.g., saved-game load paths).
            ICampaignBehaviorManager? manager = Campaign.Current?.CampaignBehaviorManager;
            if (manager != null)
            {
                manager.RemoveBehavior<BothPerksBehavior>();
                manager.AddBehavior(new BothPerksBehavior());
                Debug.Print($"[BothPerks] Behavior attached via manager fallback in {context}.");
            }
        }

        public override void OnNewGameCreated(Game game, object initializerObject)
        {
            base.OnNewGameCreated(game, initializerObject);
            TryAddBehavior(game, initializerObject, nameof(OnNewGameCreated));
        }

        public override void OnGameLoaded(Game game, object initializerObject)
        {
            base.OnGameLoaded(game, initializerObject);
            TryAddBehavior(game, initializerObject, nameof(OnGameLoaded));
            if (initializerObject is CampaignGameStarter starter)
            {
                try
                {
                    EnsureXpModel(starter);
                }
                catch (Exception ex)
                {
                    Debug.Print($"[BothPerks] OnGameLoaded.EnsureXpModel failed: {ex}");
                }
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);

            TryAddBehavior(game, gameStarter, nameof(OnGameStart));
        }
    }
}
