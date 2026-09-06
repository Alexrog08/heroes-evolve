using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Library;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// The only two perks in the game that care which weapon of a category a
    /// hero swings.
    ///
    /// A census of 403 lords found 202 holding the one-handed one and 184 the
    /// two-handed one -- roughly half the map each. That is why the market pays
    /// attention: a lord who took Swift Strike has said he fights with an axe or
    /// a mace, and buying him another sword wastes the choice he made.
    ///
    /// Looked up by a fragment of the string id rather than through
    /// DefaultPerks, whose perk fields are not reachable by name from a mod, and
    /// cached because PerkObject.All is a linear scan of every perk in the game.
    /// </summary>
    public static class WeaponPerks
    {
        private static bool _resolved;
        private static PerkObject _oneHandedAxesAndMaces;
        private static PerkObject _twoHandedAxesAndMaces;

        /// <summary>Forgets the lookup, for a fresh campaign.</summary>
        public static void Reset()
        {
            _resolved = false;
            _oneHandedAxesAndMaces = null;
            _twoHandedAxesAndMaces = null;
        }

        /// <summary>
        /// True when this hero is rewarded for carrying an axe or a mace in the
        /// handedness the given category belongs to.
        ///
        /// Asked per handedness, never in general: the core enum collapses one-
        /// and two-handed maces, so a hero with only the two-handed perk must
        /// not have his one-handed slot steered by it.
        /// </summary>
        public static bool FavoursAxeOrMace(Hero hero, WeaponCategory category)
        {
            if (hero == null) return false;

            Resolve();

            PerkObject perk = null;
            if (category == WeaponCategory.OneHandedSword || category == WeaponCategory.OneHandedAxe)
            {
                perk = _oneHandedAxesAndMaces;
            }
            else if (category == WeaponCategory.TwoHandedSword || category == WeaponCategory.TwoHandedAxe)
            {
                perk = _twoHandedAxesAndMaces;
            }

            // Mace is deliberately absent: a hero already carrying one needs no
            // steering towards them, and the collapsed enum cannot say which
            // handedness his is.
            if (perk == null) return false;

            return hero.GetPerkValue(perk);
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _oneHandedAxesAndMaces = Find("SwiftStrike");
            _twoHandedAxesAndMaces = Find("OnTheEdge");
        }

        private static PerkObject Find(string fragment)
        {
            MBReadOnlyList<PerkObject> all = PerkObject.All;
            if (all == null) return null;

            for (int i = 0; i < all.Count; i++)
            {
                PerkObject perk = all[i];
                if (perk == null || perk.StringId == null) continue;
                if (perk.StringId.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0) return perk;
            }
            return null;
        }
    }
}
