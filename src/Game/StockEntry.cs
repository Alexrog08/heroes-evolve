using TaleWorlds.Core;

namespace HeroesEvolve
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
    ///
    /// A struct rather than a class, and that is not incidental. A town roster
    /// runs to several hundred lines and this is built afresh for every lord who
    /// walks through a gate; as a class that is several hundred heap objects per
    /// visit for the collector to deal with, where the list it replaced held one
    /// array of values. Small enough to copy, so copied.
    /// </summary>
    public struct StockEntry
    {
        public EquipmentElement Element;
        public ItemObject Item;
        public ItemObject.ItemTypeEnum Type;

        /// <summary>1-based, the vocabulary the ceiling speaks.</summary>
        public int Tier;

        /// <summary>
        /// The piece's fractional tier in hundredths, same 1-based scale, with its
        /// quality counted in -- see QualityValue.
        /// </summary>
        public int FineTier;
    }
}
