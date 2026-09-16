using System.Collections.Generic;

namespace HeroesEvolve.Core
{
    /// <summary>
    /// Which kind of beast a lord may take up: only one his own people ride.
    ///
    /// Mounts come in families -- horses, camels, whatever a mod adds -- and the
    /// market used to judge a mount by its tier alone, so a Vlandian lord with
    /// money in Aserai country could ride home on a war camel. Culture was only
    /// a preference there, and a camel a tier better beat it.
    ///
    /// The families a people ride are read from its own soldiers (see
    /// CultureProfile.MountFamilies), which is how the mod already decides who
    /// fights with what, and it gets modded cultures right for free: in the base
    /// game only the Aserai field camel riders, alongside their horsemen.
    ///
    /// His people or his house: a family either his own culture or his clan's
    /// culture rides is open to him, so a lord married into a desert clan may
    /// take to camels and an Aserai serving a Vlandian house need not give them
    /// up. A lord already sitting on a beast his people do not ride is never
    /// offered a better one of it, only one of theirs, so the odd Vlandian on a
    /// camel from before this rule trades it for a horse in time.
    /// </summary>
    public static class MountFamilyRules
    {
        /// <summary>No family: nothing ridden, or a mount the game gives no monster.</summary>
        public const int NoFamily = -1;

        /// <summary>
        /// Whether a mount of <paramref name="offered"/> family is one this lord
        /// may take, while riding a mount of <paramref name="riding"/> family.
        ///
        /// When neither his culture nor his clan's shows any mounted soldier, the
        /// mod knows nothing about what they ride, and he keeps the family he
        /// already has rather than switching on no evidence.
        /// </summary>
        public static bool MayRide(int offered, int riding, ICollection<int> hisPeople, ICollection<int> hisHouse)
        {
            bool known = (hisPeople != null && hisPeople.Count > 0) || (hisHouse != null && hisHouse.Count > 0);
            if (!known) return offered != NoFamily && offered == riding;

            return (hisPeople != null && hisPeople.Contains(offered))
                || (hisHouse != null && hisHouse.Contains(offered));
        }
    }
}
