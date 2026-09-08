using System.Collections.Generic;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// One thing a town has in stock that a hero could actually buy: the exact
    /// roster entry, what this town charges him for it, and its tier.
    ///
    /// It carries the EquipmentElement and not just the ItemObject because a
    /// roster entry can hold a modifier -- a fine sword and a rusty one are the
    /// same item at different prices, and the hero must be handed the one he
    /// paid for.
    /// </summary>
    public struct MarketOffer
    {
        public EquipmentElement Element;

        /// <summary>What this settlement charges, modifier included.</summary>
        public int Price;

        /// <summary>1-based, the vocabulary the ceiling speaks.</summary>
        public int Tier;

        /// <summary>
        /// The game's own fractional tier in hundredths, on the same 1-based
        /// scale (the whole tier is round(Tierf)). This is what decides whether
        /// an offer is a real improvement; Tier above is the coarse bucket that
        /// keeps the ordering's character rule working.
        /// </summary>
        public int FineTier;

        /// <summary>
        /// True when the item is the same weapon class the hero already
        /// carries, rather than merely the same family. Only the ordering reads
        /// it -- see MarketRules.Compare. Always true for armour, mounts and
        /// harnesses, which have no family to drift within.
        /// </summary>
        public bool OwnClass;

        /// <summary>
        /// True when the item is dressed in the hero's own culture's colours.
        /// Both the upgrade gate and the ordering read it -- see
        /// MarketRules.CulturePreference.
        /// </summary>
        public bool OwnCulture;

        public MarketOffer(EquipmentElement element, int price, int tier, int fineTier, bool ownClass)
        {
            Element = element;
            Price = price;
            Tier = tier;
            FineTier = fineTier;
            OwnClass = ownClass;
        }

        public ItemObject Item
        {
            get { return Element.Item; }
        }
    }

    /// <summary>
    /// Sorts offers best-first. A named class rather than a lambda because the
    /// ordering itself lives in the pure core, where it is tested; this is only
    /// the adapter that lets List.Sort reach it.
    /// </summary>
    public sealed class MarketOfferOrder : IComparer<MarketOffer>
    {
        public static readonly MarketOfferOrder Instance = new MarketOfferOrder();

        public int Compare(MarketOffer a, MarketOffer b)
        {
            return MarketRules.Compare(a.Tier, a.OwnClass, a.FineTier, a.Price,
                                       b.Tier, b.OwnClass, b.FineTier, b.Price);
        }
    }
}
