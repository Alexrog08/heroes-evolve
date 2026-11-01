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
        private static int _civilianChests;
        private static int _openChests;
        private static readonly Dictionary<ItemObject.ItemTypeEnum, Dictionary<string, int>> _given =
            new Dictionary<ItemObject.ItemTypeEnum, Dictionary<string, int>>();

        /// <summary>Counts since the session began, for the census.</summary>
        public static int HandedOut { get { return _handedOut; } }

        /// <summary>
        /// Chests that had to come off the clothes rack because the battle rack
        /// had nothing for him anywhere. Nought in the base game, which sells
        /// five tier-1 battle rags to anyone; see RagTier.Body.
        /// </summary>
        public static int CivilianChests { get { return _civilianChests; } }

        /// <summary>
        /// Chests passed over because they leave the body bare, counted each
        /// time one would otherwise have been in the running. Nought in the
        /// base game, whose chest rags all cover him; see CoversBody.
        /// </summary>
        public static int OpenChests { get { return _openChests; } }

        /// <summary>What robbed men were handed of one kind, as the census reads it.</summary>
        public static string GivenSpread(ItemObject.ItemTypeEnum type)
        {
            Dictionary<string, int> given;
            return PurchaseWatch.Spread(_given.TryGetValue(type, out given) ? given : new Dictionary<string, int>());
        }

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
            _civilianChests = 0;
            _openChests = 0;
            _given.Clear();
        }

        /// <summary>
        /// Whether this piece is already the bottom of the world and not worth
        /// a captor's trouble.
        ///
        /// Closes a mill the rags would otherwise turn. A man robbed once now
        /// stands in tier-1 boots and a tier-1 coat; rob him again and he loses
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
            // A weapon component is what arms, shields, arrows and bolts have
            // and horses and armour do not, so it alone sorts the weapon slots.
            bool body = item.ItemType == ItemObject.ItemTypeEnum.BodyArmor;
            bool cape = item.ItemType == ItemObject.ItemTypeEnum.Cape;
            return (int)item.Tier + 1 <= RagTier.For(body, item.HasWeaponComponent, cape);
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

                Dictionary<string, int> given;
                if (!_given.TryGetValue(rag.ItemType, out given))
                {
                    given = new Dictionary<string, int>();
                    _given[rag.ItemType] = given;
                }
                int n;
                given.TryGetValue(rag.StringId, out n);
                given[rag.StringId] = n + 1;
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
        ///
        /// And a mount or a harness keeps its beast: a camel for a camel, a
        /// camel saddle for a camel saddle. The two are separate slots,
        /// scavenged one at a time, and ItemTypeEnum.Horse covers both beasts,
        /// so without this a camel rider could come out on a horse in a camel
        /// saddle. Each keeping its own beast keeps the pair a pair. Any beast
        /// at all only when the world has none of his at any tier: a saddle
        /// that does not fit is replaced at his first market (see
        /// ShoppingTrip), and a bare slot never is.
        /// </summary>
        private static ItemObject Cheapest(Hero hero, ItemObject taken, string seed)
        {
            // The chest from what fighting men wear, and only when the world
            // sells none of that at any price he could be handed, from the
            // clothes rack -- still never anything the game bars him from, and
            // on both racks only what covers him (CoversBody). A bare chest is
            // the one outcome worse than a tunic. See RagTier.Body.
            if (taken.ItemType == ItemObject.ItemTypeEnum.BodyArmor)
            {
                ItemObject chest = Cheapest(hero, taken, seed, MountFamilyRules.NoFamily, null, true);
                if (chest == null)
                {
                    chest = Cheapest(hero, taken, seed, MountFamilyRules.NoFamily, null, false);
                    if (chest != null)
                    {
                        _civilianChests++;
                        ModLog.Info("RAGS civilianChest hero=" + hero.Name + " taken=" + taken.StringId
                                    + " gave=" + chest.StringId);
                    }
                }
                return chest;
            }

            int family = FamilyOf(taken);

            // A rider scavenges what he can use from the saddle. The market has
            // always asked this (ItemCatalog.IsEligible) and the rags did not,
            // so a mounted caravan master robbed of his composite bow came away
            // with a long bow he could not draw on horseback. Asked of the horse
            // he has now: the robbery takes the weapons before the horse, and
            // a horse taken is replaced by another, so a man who rode in rides
            // out. Anything at all only when nothing he could use from the
            // saddle exists: the market replaces a weapon he cannot use, and a
            // bare slot is never filled.
            Hero rider = taken.HasWeaponComponent && hero.BattleEquipment[EquipmentIndex.Horse].Item != null
                ? hero : null;

            ItemObject rag = Cheapest(hero, taken, seed, family, rider, false);
            if (rag == null && rider != null)
            {
                rag = Cheapest(hero, taken, seed, family, null, false);
                if (rag != null)
                {
                    ModLog.Info("RAGS notMounted hero=" + hero.Name + " taken=" + taken.StringId
                                + " gave=" + rag.StringId);
                }
            }
            if (rag == null && family != MountFamilyRules.NoFamily)
            {
                rag = Cheapest(hero, taken, seed, MountFamilyRules.NoFamily, null, false);
            }
            return rag;
        }

        /// <summary>
        /// The beast a mount is, or the beast a harness is cut for.
        /// MountFamilyRules.NoFamily for everything else.
        /// </summary>
        private static int FamilyOf(ItemObject item)
        {
            if (item == null) return MountFamilyRules.NoFamily;
            if (item.ItemType == ItemObject.ItemTypeEnum.Horse) return MarketScanner.FamilyOf(item);
            if (item.ItemType == ItemObject.ItemTypeEnum.HorseHarness) return MarketScanner.HarnessFamilyOf(item);
            return MountFamilyRules.NoFamily;
        }

        /// <summary>
        /// The passes, for one beast or, with NoFamily, for any; for a rider,
        /// only what he can use mounted; and with battleOnly, nothing the game
        /// calls civilian.
        /// </summary>
        private static ItemObject Cheapest(Hero hero, ItemObject taken, string seed, int family, Hero rider,
                                           bool battleOnly)
        {
            CultureObject culture = hero.Culture;
            if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;

            bool body = taken.ItemType == ItemObject.ItemTypeEnum.BodyArmor;

            // The floor keeps the weapons off the peasant's rack (RagTier); the
            // ceiling is what he just lost.
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
            int floor = RagTier.For(body, taken.HasWeaponComponent,
                                    taken.ItemType == ItemObject.ItemTypeEnum.Cape);
            int ceiling = (int)taken.Tier + 1;
            if (ceiling < floor) ceiling = floor;

            // His own people first, at the rag tier and no higher.
            ItemObject own = Search(hero, taken, culture, floor, floor, seed, family, rider, battleOnly);
            if (own != null) return own;

            // Then anyone's, still at the rag tier. Some cultures simply make
            // nothing cheap: Sturgia's cheapest gloves are tier 3 and Aserai
            // sells no boots at tier 1 at all, so insisting on his own colours
            // here means handing a robbed man gloves barely worse than the ones
            // just taken off him. Foreign rags are the better answer and the
            // truer one -- what he is standing in was scavenged out of the
            // baggage of the army that held him, and none of that was his.
            ItemObject foreign = Search(hero, taken, null, floor, floor, seed, family, rider, battleOnly);
            if (foreign != null) return foreign;

            // And if the world sells nothing that cheap in this slot, his own
            // people at whatever it costs, up to what was taken. Late because
            // it is the one that can hand back something nearly as good; still
            // better than a slot left bare, which stays bare for life.
            ItemObject dearer = Search(hero, taken, culture, ceiling, floor, seed, family, rider, battleOnly);
            if (dearer != null) return dearer;

            // Then anyone's, over the same band.
            ItemObject dearerForeign = Search(hero, taken, null, ceiling, floor, seed, family, rider, battleOnly);
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
                ItemObject lastResort = Search(hero, taken, null, ceiling, RagTier.Everything, seed, family, rider,
                                                       battleOnly);
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
        /// The cheapest item of the same kind within one tier band, that the
        /// game lets this hero wear. A null culture means anybody's, NoFamily
        /// any beast, a null rider a man on foot, and battleOnly nothing the
        /// game marks civilian.
        /// </summary>
        private static ItemObject Search(Hero hero, ItemObject taken, CultureObject culture,
                                         int ceiling, int floor, string seed, int family, Hero rider,
                                         bool battleOnly)
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
                if (battleOnly && item.IsCivilian) continue;

                // A war mount, never a mule: ItemTypeEnum.Horse covers both, and
                // the pack animals are the only ones a hero with no riding can
                // sit on. See ItemCatalog.IsWarMount.
                if (mount && !ItemCatalog.IsWarMount(item)) continue;
                if (family != MountFamilyRules.NoFamily && FamilyOf(item) != family) continue;
                if (rider != null && !ItemClassifier.IsUsableMounted(item, rider)) continue;

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

                // Last, so they count only pieces that would otherwise have been
                // in the running. See CoversBody and ItemCatalog.IsWearableBy.
                if (type == ItemObject.ItemTypeEnum.BodyArmor && !CoversBody(item))
                {
                    _openChests++;
                    continue;
                }
                if (!ItemCatalog.IsWearableBy(item, hero))
                {
                    Diagnostics.NoteBarredBySex("rags");
                    continue;
                }

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

        /// <summary>
        /// Whether a chest covers the man inside it, as its author says with
        /// covers_body. The game reads the attribute into the armour's mesh
        /// mask, showing the body's own skin wherever the piece says it does not
        /// cover, and every chest in the base game and the gear mods tested
        /// states it outright.
        ///
        /// For the bottom of the world in a modded game. The base game's five
        /// battle rags all cover him; Open Source Armory adds two tier-1 Khuzait
        /// pieces that are a harness over the bare chest, and the first robbery
        /// test with it put five lords in seven into them -- a robbed man, who
        /// should look poor, looking undressed instead.
        /// </summary>
        private static bool CoversBody(ItemObject item)
        {
            ArmorComponent armour = item.ArmorComponent;
            return armour == null || (armour.MeshesMask & SkinMask.BodyVisible) == 0;
        }
    }
}
