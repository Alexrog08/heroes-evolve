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

        public MarketOffer(EquipmentElement element, int price, int tier)
        {
            Element = element;
            Price = price;
            Tier = tier;
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
            return MarketRules.Compare(a.Tier, a.Price, b.Tier, b.Price);
        }
    }
}
