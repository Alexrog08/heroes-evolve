using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// A captor trying on what he has just taken.
    ///
    /// The same rules as buying, with the money removed: the same category he
    /// already carries, half a tier better, inside his ceiling, of a culture he
    /// can wear, and only into a slot he already fills. Nothing here re-decides
    /// what kind of fighter he is, exactly as nothing in the market does.
    ///
    /// Most of the time he can use none of it, and that is right. A Vlandian who
    /// strips a Khuzait is holding Khuzait gear, which the culture rule keeps off
    /// him; he will sell it, and it will turn up on a shelf somewhere. But when
    /// the loot does fit -- a neighbour of the same culture, a piece with no
    /// culture at all -- he wears it, which is what anyone would do and what
    /// makes taking it worth the standing it cost him.
    ///
    /// Several pieces, not one. The trip through a town is capped at a single
    /// purchase to pace the spending; there is no spending here, and a man who
    /// has just emptied a prisoner would not stop after one buckle.
    /// </summary>
    public static class LootFitting
    {
        /// <summary>
        /// One pass per slot at most. The loop re-reads the pile after each
        /// piece because taking one out of it changes what is left, and the
        /// bound is what stops a bad interaction turning into a spin.
        /// </summary>
        private const int MaxPieces = 11;

        /// <summary>
        /// Puts on whatever of this pile suits him. Returns how many pieces he
        /// kept.
        /// </summary>
        public static int Equip(Hero hero, ItemRoster loot, float clanWeight, float skillWeight, int minimumTier)
        {
            if (hero == null || hero.BattleEquipment == null || loot == null) return 0;
            if (!HeroFilter.IsEligibleToShop(hero)) return 0;

            int ceiling = HeroAdapter.ReadCeiling(hero, HeroAdapter.ReadSkills(hero),
                                                  clanWeight, skillWeight, minimumTier);
            int worn = 0;

            for (int pass = 0; pass < MaxPieces; pass++)
            {
                List<StockEntry> pile = MarketScanner.Stock(loot);
                if (pile.Count == 0) break;

                // No settlement: nothing is for sale. No limit: nothing is
                // charged. Everything else is the market's own reasoning.
                ShoppingTrip.Candidate fit = ShoppingTrip.Best(hero, null, pile, ceiling, int.MaxValue);
                if (fit == null) break;

                if (!Wear(hero, loot, fit)) break;
                worn++;
            }

            if (worn > 0)
            {
                ModLog.Info("LOOTFIT hero=" + hero.Name + " wore=" + worn);
            }

            return worn;
        }

        /// <summary>
        /// Moves one piece out of the pile and onto the hero, and what it
        /// displaces back into the pile.
        ///
        /// A swap, not a gift: he ends up with the same number of things he
        /// started with, and the piece he took off is still there to be sold or
        /// lost later. Refuses if the pile no longer holds what was chosen,
        /// which cannot happen today but is cheaper to check than to debug.
        /// </summary>
        private static bool Wear(Hero hero, ItemRoster loot, ShoppingTrip.Candidate fit)
        {
            int index = loot.FindIndexOfElement(fit.Offer.Element);
            if (index < 0 || loot.GetElementNumber(index) <= 0) return false;

            EquipmentElement displaced = hero.BattleEquipment[fit.Slot];

            loot.AddToCounts(fit.Offer.Element, -1);
            hero.BattleEquipment[fit.Slot] = fit.Offer.Element;
            if (displaced.Item != null) loot.AddToCounts(displaced, 1);

            return true;
        }
    }
}
