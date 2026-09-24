namespace HeroesEvolve.Core
{
    /// <summary>
    /// What a robbery leaves a man standing in.
    ///
    /// Robbery used to leave a hero with nothing and let the repair dress him
    /// again the next day. That repair is built for a different problem -- the
    /// lord who came of age with no gear at all -- so it reads his skills and
    /// infers a loadout from them, and the inference is wrong exactly when it
    /// matters most. A companion handed a crossbow last week still has Bow 62
    /// against Crossbow 61, and the planner takes the highest with no margin:
    /// it gives him back a bow, and the player's choice is gone.
    ///
    /// So the robbery dresses him itself, in the same instant it strips him,
    /// from what it is in the act of taking. Nothing is remembered because
    /// nothing needs to be: the gear is in hand. He keeps the shape of the man
    /// he was -- the same weapon kinds, a mount if he rode one -- at the
    /// cheapest the world sells, and the market carries him back up from there.
    /// In the fiction he scavenged the captor's baggage, or was handed enough
    /// to get home on.
    ///
    /// Two tiers rather than one, and the split is the dress rule. Every
    /// garment in the game that would put a lord in a gown is tier 1 body
    /// armour -- khuzait_dress, nordic_civilian_dress, dress_norse_lady,
    /// burlap_sack_dress -- and there is no reliable way to tell them from the
    /// tunics beside them: the Civilian flag is on every horse in the game, and
    /// matching names breaks on the first mod. KitTier.Lowest already draws
    /// that line for the whole mod and this keeps it for the chest.
    ///
    /// The weapons are held up too, and for the same reason. Tier 1 of the
    /// weapon rack is the peasant's -- peasant_pitchfork_1_t1, pitchforks,
    /// hammers, sickles -- and a lord robbed of his lance was coming back with
    /// a pitchfork, which players noticed and reported. A man scavenging an
    /// army's baggage takes soldiers' weapons, not farm tools. The starting kit
    /// already refuses tier 1 for the same reason (GrantTier.Minimum), so this
    /// is the one floor the mod uses for weapons everywhere.
    ///
    /// Everywhere else takes tier 1, and that is coverage rather than flavour.
    /// Counted over what the game sells, Nord has four pairs of boots at tier 1
    /// and none at tier 2; Sturgia has six and one. A tier-2 floor would leave
    /// those men barefoot for ever, because a lord only ever buys a better
    /// version of what he already carries and can never fill an empty slot.
    /// Weapons do not have that hole: the search falls back to anyone's rack
    /// and then to his own people's up to the tier taken, and the
    /// seven-culture robbery sweep is the check that it always finds one.
    /// </summary>
    public static class RagTier
    {
        /// <summary>
        /// The chest, where the dresses are. The same floor the rest of the mod
        /// uses for a battle outfit.
        /// </summary>
        public const int Body = KitTier.Lowest;

        /// <summary>
        /// Weapons, shields and ammunition: out of the peasant's rack. The same
        /// floor the starting kit keeps.
        /// </summary>
        public const int Weapon = KitTier.Lowest;

        /// <summary>
        /// Head, legs, hands, cape and mounts: the cheapest the world has.
        /// Nothing in these slots is a gown or a farm tool, and some cultures
        /// sell nothing above it.
        /// </summary>
        public const int Everything = 1;

        /// <summary>
        /// The floor for one slot. The chest and the weapons are held up;
        /// everything else falls as far as the catalogue goes.
        /// </summary>
        public static int For(bool isBodyArmour, bool isWeapon)
        {
            if (isBodyArmour) return Body;
            if (isWeapon) return Weapon;
            return Everything;
        }
    }
}
