using TaleWorlds.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// One line of a town's stock, with the answers the scans keep asking for
    /// already worked out.
    ///
    /// The tiers are the reason this exists. ItemObject.Tier and Tierf look like
    /// fields and are not: Tierf falls through to
    /// ItemValueModel.CalculateTier(item) whenever an item carries no override,
    /// and Tier rounds and clamps that on top. A shopping trip scans eleven
    /// slots over a roster of several hundred, asking both of nearly every line
    /// every time -- some eleven thousand trips through the tier machinery for
    /// one lord walking through one gate, all of them recomputing values that
    /// cannot change while he is standing there.
    ///
    /// Working them out once per visit turns the eleven scans into integer
    /// comparisons. The item type is kept for the same reason, since it is the
    /// first thing every armour, mount and harness scan tests.
    /// </summary>
    public sealed class StockEntry
    {
        public EquipmentElement Element;
        public ItemObject Item;
        public ItemObject.ItemTypeEnum Type;

        /// <summary>1-based, the vocabulary the ceiling speaks.</summary>
        public int Tier;

        /// <summary>The game's fractional tier in hundredths, same 1-based scale.</summary>
        public int FineTier;
    }
}
