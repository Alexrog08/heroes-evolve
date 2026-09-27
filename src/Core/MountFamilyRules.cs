using System.Collections.Generic;

namespace HeroesEvolve.Core
{
    /// <summary>
    /// Which kind of beast a lord may take up: the one he rides, if his people
    /// ride it, and otherwise one of theirs.
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
    /// A lord keeps the beast he rides. This was once "any beast his people
    /// ride", and for the Aserai, who field both, that meant swapping camel for
    /// horse and back whenever the other was a tier better. The saddle stayed
    /// behind each time, because the harness is its own slot, bought on its own:
    /// a subscriber reported Aserai on horses in camel saddles, and one lord in
    /// the log fitted a horse harness and bought a war camel in the same trip.
    /// A camel rider staying a camel rider is the same promise the rest of the
    /// market keeps -- a foot archer is still a foot archer in forty years --
    /// and it keeps the pair a pair.
    ///
    /// His people or his house: a family either his own culture or his clan's
    /// culture rides is one he may keep, so an Aserai serving a Vlandian house
    /// need not give up his camel. A lord sitting on a beast neither rides is
    /// never offered a better one of it, only one of theirs, so the odd Vlandian
    /// on a camel trades it for a horse in time.
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

            if (!Rides(offered, hisPeople, hisHouse)) return false;

            // His own beast, when it is one of theirs, is the only one on offer.
            // A lord on a foreign beast, or on none, may take any of theirs.
            bool ridesTheirs = riding != NoFamily && Rides(riding, hisPeople, hisHouse);
            return !ridesTheirs || offered == riding;
        }

        private static bool Rides(int family, ICollection<int> hisPeople, ICollection<int> hisHouse)
        {
            return (hisPeople != null && hisPeople.Contains(family))
                || (hisHouse != null && hisHouse.Contains(family));
        }
    }
}
