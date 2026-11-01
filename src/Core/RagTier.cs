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
    /// The chest falls to the bottom with the rest of him, because a robbed
    /// man should look robbed. It used to be held at KitTier.Lowest, and that
    /// was the dress rule: every gown in the game is tier 1 body armour --
    /// khuzait_dress, nordic_civilian_dress, dress_norse_lady -- and the tier
    /// was the one line the whole mod could draw. On the chest alone two
    /// things the game says about a piece draw it better. The chest rag must
    /// not carry the Civilian flag, which rules out every gown and robe and
    /// cannot be used for the whole mod only because every horse and most
    /// boots carry it too (Rags.Cheapest). And no rag is anything the game
    /// bars his sex from (ItemCatalog.IsWearableBy), which covers what the
    /// first misses. Neither does it alone: two Nord dresses carry no sex flag,
    /// and the Civilian flag says nothing about another mod's bikini mail.
    ///
    /// What is left at tier 1 is what bandits and raiders fight in -- a fur
    /// waistcoat over a tunic, kaunakes, raider clothing, a studded leather
    /// coat, ring mail -- and only the Aserai sell any of it in their own
    /// colours, so most robbed men come away in something out of the bandits'
    /// end of the baggage. Alike, and the designer accepted that. It must also
    /// cover him, as the piece's own covers_body says: gear mods put open
    /// harnesses down here too, and a robbed man should look poor, not
    /// undressed (Rags.CoversBody).
    ///
    /// And the starting kit leaves it alone. It judges a lord once, the day
    /// after the game makes him (HeroLoadoutBehavior, _freshlyMade), and reads
    /// a cheap chest as the come-of-age failure only when the game also calls
    /// it civilian (GrantService.IsCivilian), which a chest rag never is. So
    /// neither the kit, for a lord robbed on the day he was made, nor the
    /// census mistakes a robbed man for a broken one.
    ///
    /// The weapons are held up, as the chest once was. Tier 1 of the weapon
    /// rack is the peasant's -- peasant_pitchfork_1_t1, pitchforks, hammers,
    /// sickles -- and a lord robbed of his lance was coming back with a
    /// pitchfork, which players noticed and reported. A man scavenging an
    /// army's baggage takes soldiers' weapons, not farm tools. The starting kit
    /// keeps the same floor for the same reason (GrantTier.Minimum), and goes
    /// below it only when a culture offers nothing else (GrantTier.Fallback),
    /// so this is the one floor the mod uses for weapons everywhere.
    ///
    /// The cape goes lower still, to tier 0; see Cape.
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
        /// The chest, at the bottom with everything else. The dresses are kept
        /// off it by flags rather than by tier; see the class comment.
        /// </summary>
        public const int Body = 1;

        /// <summary>
        /// Weapons, shields and ammunition: out of the peasant's rack. The same
        /// floor the starting kit keeps.
        /// </summary>
        public const int Weapon = KitTier.Lowest;

        /// <summary>
        /// Head, legs, hands and mounts: tier 1. Nothing there is a farm tool,
        /// and some cultures sell nothing above it.
        ///
        /// Not tier 0, which is a real rung of the game's scale -- the lowest,
        /// where the piece scores under half a tier -- and not an unranked one.
        /// Tier 0 of the head rack is mostly women's headscarves and of the leg
        /// rack holds the ladies' shoes, and the game bars a man from neither,
        /// so ItemCatalog.IsWearableBy cannot keep them off him.
        /// </summary>
        public const int Everything = 1;

        /// <summary>
        /// The cape, the one slot that goes to tier 0, where the plain cloth
        /// is: hoods, scarves, a short cloak, a tartan. Tier 1 of the cape rack
        /// is pauldrons and fur for most peoples -- every imperial and Khuzait
        /// piece there is shoulder armour, and one of Nord's two is a fur cape
        /// -- so a robbed man came away better dressed at the shoulders than
        /// anywhere else on him. The market counts tier 0 as the furthest
        /// behind and replaces it like any other piece (GearBalance.Shortfall,
        /// MarketRules.WornArmourFine), so the scarf is the first thing he buys
        /// his way out of.
        /// </summary>
        public const int Cape = 0;

        /// <summary>
        /// The floor for one slot. The weapons are held up, the cape goes
        /// lowest, and everything else falls as far as tier 1.
        /// </summary>
        public static int For(bool isBodyArmour, bool isWeapon, bool isCape)
        {
            if (isCape) return Cape;
            if (isBodyArmour) return Body;
            if (isWeapon) return Weapon;
            return Everything;
        }
    }
}
