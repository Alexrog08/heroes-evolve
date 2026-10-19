using System.Collections.Generic;
using System.Collections.ObjectModel;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace HeroesEvolve.Api
{
    // What each event carries. Read-only, and part of the contract described
    // on HeroesEvolveEvents: a property here is never renamed or removed, and
    // anything new arrives as a new property.

    /// <summary>A prisoner stripped of his kit.</summary>
    public sealed class HeroRobbedArgs
    {
        internal HeroRobbedArgs(Hero robber, PartyBase robberParty, Hero victim, string motive,
                                List<StolenPiece> pieces, int totalValue, Settlement nearestSettlement,
                                bool collectedOath, bool sworeOath)
        {
            Robber = robber;
            RobberParty = robberParty;
            RobberPartyName = robberParty != null && robberParty.Name != null ? robberParty.Name.ToString() : null;
            RobberClan = robber != null ? robber.Clan
                       : robberParty != null && robberParty.MobileParty != null ? robberParty.MobileParty.ActualClan
                       : null;
            Victim = victim;
            Motive = motive;
            Pieces = new ReadOnlyCollection<StolenPiece>(pieces ?? new List<StolenPiece>());
            TotalValue = totalValue;
            NearestSettlement = nearestSettlement;
            CollectedOath = collectedOath;
            SworeOath = sworeOath;
        }

        /// <summary>Who robbed him. Null for a band of bandits, which has no leader.</summary>
        public Hero Robber { get; private set; }

        /// <summary>The party the pieces went into: the robber's, or the band itself.</summary>
        public PartyBase RobberParty { get; private set; }

        /// <summary>That party's name, which is how a band is named ("Deserters").</summary>
        public string RobberPartyName { get; private set; }

        /// <summary>The robber's clan, or the band's own bandit clan. May be null.</summary>
        public Clan RobberClan { get; private set; }

        /// <summary>The prisoner who was robbed.</summary>
        public Hero Victim { get; private set; }

        /// <summary>
        /// Why: "character" (the robber's own doing, which costs him standing
        /// with the victim's house and makes the victim swear vengeance),
        /// "justice" (an honourable lord stripping a dishonourable prisoner;
        /// costs nothing), "vengeance" (his house was owed and collected), or
        /// "bandits". The player's own robberies are "character" or
        /// "vengeance".
        /// </summary>
        public string Motive { get; private set; }

        /// <summary>Every piece taken, in the order they came off him.</summary>
        public IList<StolenPiece> Pieces { get; private set; }

        /// <summary>The pieces' worth together, modifiers included.</summary>
        public int TotalValue { get; private set; }

        /// <summary>
        /// Where: the keep he was held in, the settlement the robber's party
        /// was in, or the nearest town, castle or village to it. May be null.
        /// </summary>
        public Settlement NearestSettlement { get; private set; }

        /// <summary>True when the robbery collected an oath (see OathCollected).</summary>
        public bool CollectedOath { get; private set; }

        /// <summary>True when the robbery made the victim swear one (see OathSworn).</summary>
        public bool SworeOath { get; private set; }
    }

    /// <summary>One piece taken in a robbery.</summary>
    public sealed class StolenPiece
    {
        internal StolenPiece(EquipmentElement element, EquipmentIndex slot, int value, Hero owner)
        {
            Element = element;
            Owner = owner;
            Item = element.Item;
            ItemId = element.Item != null ? element.Item.StringId : null;
            Slot = slot;
            Value = value;
        }

        /// <summary>The item.</summary>
        public ItemObject Item { get; private set; }

        /// <summary>Its id, for a listener that keeps ids rather than objects.</summary>
        public string ItemId { get; private set; }

        /// <summary>The slot of his battle equipment it came out of.</summary>
        public EquipmentIndex Slot { get; private set; }

        /// <summary>What it was worth, its modifier included.</summary>
        public int Value { get; private set; }

        // Kept for the mod itself, to know a piece again when the robber puts
        // it on (StolenPieceWorn): the exact element, modifier and all, and
        // whose it was.
        internal EquipmentElement Element { get; private set; }
        internal Hero Owner { get; private set; }
    }

    /// <summary>A robbed man swore vengeance on his robber.</summary>
    public sealed class OathSwornArgs
    {
        internal OathSwornArgs(Hero swearer, Hero against)
        {
            Swearer = swearer;
            Against = against;
        }

        /// <summary>The man who swore: the one who was robbed. His whole house is owed.</summary>
        public Hero Swearer { get; private set; }

        /// <summary>The man it was sworn against: the robber. His whole house owes.</summary>
        public Hero Against { get; private set; }
    }

    /// <summary>
    /// An oath collected. The debt is between houses, so the man who swore and
    /// the man who collected are often different people, and so are the
    /// robber and the man who paid for him.
    /// </summary>
    public sealed class OathCollectedArgs
    {
        internal OathCollectedArgs(Hero swearer, Hero against, Hero collector, Hero payer)
        {
            Swearer = swearer;
            Against = against;
            Collector = collector;
            Payer = payer;
        }

        /// <summary>Who swore the oath, back when he was robbed.</summary>
        public Hero Swearer { get; private set; }

        /// <summary>Who it was sworn against: the original robber.</summary>
        public Hero Against { get; private set; }

        /// <summary>Who collected it: the captor who stripped the payer.</summary>
        public Hero Collector { get; private set; }

        /// <summary>Who paid: the prisoner stripped to settle it.</summary>
        public Hero Payer { get; private set; }
    }

    /// <summary>A lord put on a piece he had just taken off a prisoner.</summary>
    public sealed class StolenPieceWornArgs
    {
        internal StolenPieceWornArgs(Hero wearer, ItemObject item, EquipmentIndex slot, Hero previousOwner)
        {
            Wearer = wearer;
            Item = item;
            Slot = slot;
            PreviousOwner = previousOwner;
        }

        /// <summary>Who put it on.</summary>
        public Hero Wearer { get; private set; }

        /// <summary>The piece.</summary>
        public ItemObject Item { get; private set; }

        /// <summary>The slot of his battle equipment it went into.</summary>
        public EquipmentIndex Slot { get; private set; }

        /// <summary>Whose it was until the robbery.</summary>
        public Hero PreviousOwner { get; private set; }
    }

    /// <summary>A piece of battle gear bought in a town.</summary>
    public sealed class GearBoughtArgs
    {
        internal GearBoughtArgs(Hero buyer, ItemObject item, EquipmentIndex slot, ItemObject replaced,
                                int price, Settlement town, int tier, int replacedTier)
        {
            Buyer = buyer;
            Item = item;
            Slot = slot;
            Replaced = replaced;
            Price = price;
            Town = town;
            Tier = tier;
            ReplacedTier = replacedTier;
        }

        /// <summary>Who bought it.</summary>
        public Hero Buyer { get; private set; }

        /// <summary>The piece bought, now worn.</summary>
        public ItemObject Item { get; private set; }

        /// <summary>The slot of his battle equipment it went into.</summary>
        public EquipmentIndex Slot { get; private set; }

        /// <summary>What it replaced, sold to the same town. Null when the slot was empty.</summary>
        public ItemObject Replaced { get; private set; }

        /// <summary>What he paid, his clan's share included.</summary>
        public int Price { get; private set; }

        /// <summary>Where.</summary>
        public Settlement Town { get; private set; }

        /// <summary>The piece's tier, 1 to 6: the game's own tier counted from one.</summary>
        public int Tier { get; private set; }

        /// <summary>The replaced piece's tier on the same scale, or 0 when there was none.</summary>
        public int ReplacedTier { get; private set; }
    }
}
