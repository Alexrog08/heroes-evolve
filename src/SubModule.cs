using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace HeroesEvolve
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // Before the log line, since it can switch logging off.
            Settings.Load();
            ModLog.Info("SubModule loaded.");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            CampaignGameStarter starter = gameStarterObject as CampaignGameStarter;
            if (starter == null) return;

            starter.AddBehavior(new HeroLoadoutBehavior());
            PrisonerDialogue.Register(starter);
            ModLog.Info("HeroLoadoutBehavior registered.");
        }
    }
}
