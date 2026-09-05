using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace HeroLoadoutFixer
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ModLog.Info("SubModule loaded.");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            CampaignGameStarter starter = gameStarterObject as CampaignGameStarter;
            if (starter == null) return;

            starter.AddBehavior(new HeroLoadoutBehavior());
            ModLog.Info("HeroLoadoutBehavior registered.");
        }
    }
}
