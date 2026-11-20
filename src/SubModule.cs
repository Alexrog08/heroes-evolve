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

            // The configuration as the options screen has it now, because the
            // growth mode is settled here and nowhere later: see GrowthMode.
            // RegisterEvents reads both again for everything else.
            Settings.Load();
            McmBridge.Attach();
            GrowthMode.Apply(starter);

            starter.AddBehavior(new HeroLoadoutBehavior());
            PrisonerDialogue.Register(starter);
            ModLog.Info("HeroLoadoutBehavior registered.");
        }
    }
}
