using System.Collections.Generic;

namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// The loadout a hero deserves, before anything is reconciled against what
    /// they already carry. Weapons are listed in priority order.
    /// </summary>
    public sealed class LoadoutTarget
    {
        public LoadoutTarget()
        {
            Weapons = new List<WeaponCategory>();
        }

        public List<WeaponCategory> Weapons { get; private set; }
        public bool WantsMount { get; set; }
    }
}
