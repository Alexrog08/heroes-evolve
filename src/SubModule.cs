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
    }
}
