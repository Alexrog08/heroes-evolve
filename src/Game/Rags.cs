using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// What a man is left standing in when he is robbed.
    ///
    /// The rule and the reasoning are in RagTier. This is the half that needs
    /// the game: for one slot the robbery has just emptied, find the cheapest
    /// thing of the same kind his own people make, and put it there.
    ///
    /// Called from inside the strip itself, with the piece that was taken still
    /// in hand. That is the whole trick and the reason no loadout has to be
    /// remembered anywhere: the shape is not recalled, it is read off the thing
    /// being removed, one slot at a time.
    ///
    /// Deliberately not the repair. GrantService answers "this man has nothing
    /// and never did, what should he be?" by reading his skills and inventing a
    /// loadout, which is right for a lord whose gear never generated and wrong
    /// for one who had a loadout until a moment ago. Two different questions,
    /// two different answers; the repair is left alone.
    ///
    /// Items are conjured rather than moved, exactly as the repair conjures
    /// them. The captor keeps every real piece -- that is the point of robbing
    /// him -- and can be beaten to get it back.
    /// </summary>
    public static class Rags
    {
        private static int _handedOut;
        private static int _slotsLeftEmpty;

        /// <summary>Counts since the session began, for the census.</summary>
        public static int HandedOut { get { return _handedOut; } }

        /// <summary>
        /// Slots a robbery emptied and this could not refill, which matter more
        /// than they look: a lord only ever buys a better version of what he
        /// already carries, so a slot left empty here is empty for good.
        /// </summary>
        public static int SlotsLeftEmpty { get { return _slotsLeftEmpty; } }

        public static void ResetSession()
        {
            _handedOut = 0;
            _slotsLeftEmpty = 0;
        }

        /// <summary>
        /// Anything that goes in a weapon slot: arms, shields, arrows and bolts.
        /// Horses carry a HorseComponent and armour an ArmorComponent, so the
        /// weapon component alone sorts them.
        /// </summary>
        private static bool IsWeapon(ItemObject item)
        {
            return item != null && item.WeaponComponent != null;
        }

        /// <summary>
        /// Whether this piece is already the bottom of the world and not worth
        /// a captor's trouble.
        ///
        /// Closes a mill the rags would otherwise turn. A man robbed once now
        /// stands in tier-1 boots and a tier-2 coat; rob him again and he loses
        /// those and is handed the same thing back, which is motion without
        /// consequence -- a line in the log, a few denars, and a lord who keeps
        /// being reported as robbed. Nothing worth taking is not taken.
        ///
        /// Shared with Takeable, so the conversation stops offering a demand
        /// over a man who has nothing left of value, exactly as it already does
        /// for one stripped bare.
        ///
        /// It spares the genuinely poor as well as the recently robbed, and
        /// that is the right answer for both. Their gear is at the floor this
        /// hands out, so taking it and replacing it changes nothing about them
        /// either.
        /// </summary>
        public static bool IsRag(ItemObject item)
        {
            if (item == null) return false;

            // Every kind, not only armour. The first version asked this of the
            // armour slots alone, and a test robbery showed what that left
            // running: a lord already in rags kept his coat and boots, which
            // were spared, and lost his pitchfork, his arrows and his horse
            // every time. The log read empire_horse, then hunter, then hunter,
            // and barbed_arrows, then default_arrows, then default_arrows. It
            // settles once everything is at the bottom, but until then it is
            // churn nobody asked for.
            bool body = item.ItemType == ItemObject.ItemTypeEnum.BodyArmor;
            return (int)item.Tier + 1 <= RagTier.For(body, IsWeapon(item));
        }

        /// <summary>
        /// Puts the cheapest equivalent of <paramref name="taken"/> back into
        /// the slot it came out of. Returns true if something was found.
        /// </summary>
        public static bool Replace(Hero hero, EquipmentIndex slot, ItemObject taken)
        {
            if (hero == null || taken == null || hero.BattleEquipment == null) return false;

            try
            {
                // Hero and slot, so the same man robbed of the same piece
                // always scavenges the same thing and a test can be re-run.
                // See RagChoice.
                string seed = hero.StringId + ":" + (int)slot;

                ItemObject rag = Cheapest(hero, taken, seed);
                if (rag == null)
                {
                    _slotsLeftEmpty++;
                    return false;
                }

                hero.BattleEquipment[slot] = new EquipmentElement(rag, null, null, false);
                _handedOut++;
                return true;
            }
            catch
            {
                // A hero who cannot be dressed is left as the robbery found
                // him. Better a bare slot than a throw inside a capture.
                _slotsLeftEmpty++;
                return false;
            }
        }

        /// <summary>
        /// The cheapest item of the same kind, in his own colours.
        ///
        /// Same kind means the game's own ItemType, and for a weapon that is
        /// not enough on its own -- OneHandedWeapon covers a sword and a mace
        /// alike -- so weapons are matched on the mod's own category as well.
        /// A man robbed of an axe is handed an axe.
        /// </summary>
        private static ItemObject Cheapest(Hero hero, ItemObject taken, string seed)
        {
            CultureObject culture = hero.Culture;
            if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;

            bool body = taken.ItemType == ItemObject.ItemTypeEnum.BodyArmor;

            // The floor is the dress rule; the ceiling is what he just lost.
            //
            // These were the same number in the first version and four lords in
            // seven came out of a test robbery with a bare slot: passing the
            // floor as the ceiling too asks for "exactly tier 1", not "the
            // cheapest there is", and Aserai sells no boots at tier 1 while
            // Sturgia sells no gloves below tier 3. A slot left bare is bare for
            // life, so the search has to reach as far up as it must and the loop
            // below takes the lowest it finds.
            //
            // Never above what was taken, so rags cannot be an upgrade. A man
            // robbed of plated boots does not walk away in better ones because
            // his people make nothing cheap.
            int floor = RagTier.For(body, IsWeapon(taken));
            int ceiling = (int)taken.Tier + 1;
            if (ceiling < floor) ceiling = floor;

            // His own people first, at the rag tier and no higher.
            ItemObject own = Search(taken, culture, floor, floor, seed);
            if (own != null) return own;

            // Then anyone's, still at the rag tier. Some cultures simply make
            // nothing cheap: Sturgia's cheapest gloves are tier 3 and Aserai
            // sells no boots at tier 1 at all, so insisting on his own colours
            // here means handing a robbed man gloves barely worse than the ones
            // just taken off him. Foreign rags are the better answer and the
            // truer one -- what he is standing in was scavenged out of the
            // baggage of the army that held him, and none of that was his.
            ItemObject foreign = Search(taken, null, floor, floor, seed);
            if (foreign != null) return foreign;

            // And if the world sells nothing that cheap in this slot, his own
            // people at whatever it costs, up to what was taken. Late because
            // it is the one that can hand back something nearly as good; still
            // better than a slot left bare, which stays bare for life.
            ItemObject dearer = Search(taken, culture, ceiling, floor, seed);
            if (dearer != null) return dearer;

            // Then anyone's, over the same band.
            ItemObject dearerForeign = Search(taken, null, ceiling, floor, seed);
            if (dearerForeign != null) return dearerForeign;

            // Last of all, below the floor. The floor is a preference -- a lord
            // should not scavenge a pitchfork -- and a bare slot is permanent,
            // because a lord only ever buys a better version of what he carries
            // and never fills an empty slot. So when nothing at or above the
            // floor exists anywhere, the cheapest thing that does exist beats
            // nothing, and it is logged, because it should be rare enough to
            // look at.
            if (floor > RagTier.Everything)
            {
                ItemObject lastResort = Search(taken, null, ceiling, RagTier.Everything, seed);
                if (lastResort != null)
                {
                    ModLog.Info("RAGS belowFloor hero=" + hero.Name + " taken=" + taken.StringId
                                + " gave=" + lastResort.StringId + " floor=" + floor);
                    return lastResort;
                }
            }

            return null;
        }

        /// <summary>
        /// The cheapest item of the same kind within one tier band. A null
        /// culture means anybody's.
        /// </summary>
        private static ItemObject Search(ItemObject taken, CultureObject culture,
                                         int ceiling, int floor, string seed)
        {
            ItemObject.ItemTypeEnum type = taken.ItemType;
            bool mount = type == ItemObject.ItemTypeEnum.Horse;
            WeaponCategory wanted = ItemClassifier.Classify(taken);

            // Every item tied at the cheapest tier, not the first one found.
            // The object manager lists them in a fixed order, so taking the
            // first handed every robbed lord in the world the same pitchfork.
            // See RagChoice.
            List<ItemObject> cheapest = new List<ItemObject>();
            int bestTier = int.MaxValue;

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null || item.ItemType != type) continue;

                // A war mount, never a mule: ItemTypeEnum.Horse covers both, and
                // the pack animals are the only ones a hero with no riding can
                // sit on. See ItemCatalog.IsWarMount.
                if (mount && !ItemCatalog.IsWarMount(item)) continue;

                if (wanted != WeaponCategory.None
                    && ItemClassifier.Classify(item) != wanted) continue;

                if (!ItemCatalog.PassesCommonFilters(item, culture, ceiling, floor)) continue;

                // The floor, held here for every kind. PassesCommonFilters takes
                // it as minArmorTier and applies it to armour slots only, so for
                // weapons it was silently ignored: the search kept the cheapest
                // it saw, and that was the peasant's rack. The first test of the
                // tier-2 weapon floor came back with seven lords holding
                // pitchforks and a hammer.
                int tier = (int)item.Tier + 1;
                if (tier < floor) continue;
                if (tier > bestTier) continue;

                if (tier < bestTier)
                {
                    bestTier = tier;
                    cheapest.Clear();
                }

                cheapest.Add(item);
            }

            int pick = RagChoice.Pick(cheapest.Count, seed);
            return pick < 0 ? null : cheapest[pick];
        }
    }
}
