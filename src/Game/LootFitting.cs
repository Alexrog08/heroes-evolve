using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using HeroesEvolve.Api;

namespace HeroesEvolve
{
    /// <summary>
    /// A captor trying on what he has just taken.
    ///
    /// The same rules as buying, with the money removed: the same category he
    /// already carries, enough better to be worth the swap, inside his ceiling,
    /// and only into a slot he already fills. Nothing here re-decides what kind
    /// of fighter he is, exactly as nothing in the market does.
    ///
    /// Most of the time he keeps almost none of it, and that is right -- but the
    /// reason changed when culture stopped being a wall. A Vlandian who strips a
    /// Khuzait is holding Khuzait gear, and MarketRules.CultureShare makes
    /// him want a full tier and a half of it before he will take off his own
    /// people's harness for it. So he wears the odd piece that is genuinely
    /// better and sells the rest, which is what anyone would do and what makes
    /// taking it worth the standing it cost him.
    ///
    /// Several pieces, not one: a man who has just emptied a prisoner does not
    /// stop after one buckle, exactly as a trip through a town buys every gap
    /// the shelves can fill.
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
        ///
        /// justTaken is what the robbery that called this has just put in the
        /// pile, and whose each piece was. A piece he puts on from that list
        /// is told to the mods listening (StolenPieceWorn) with its former
        /// owner; anything else in his baggage is his own, and is not news.
        /// </summary>
        public static int Equip(Hero hero, ItemRoster loot, float clanWeight, float skillWeight, int minimumTier,
                                IList<StolenPiece> justTaken = null)
        {
            if (hero == null || hero.BattleEquipment == null || loot == null) return 0;
            if (!HeroFilter.IsEligibleToShop(hero)) return 0;

            int ceiling = HeroAdapter.ReadCeiling(hero, HeroAdapter.ReadSkills(hero),
                                                  clanWeight, skillWeight, minimumTier);
            int worn = 0;
            HashSet<int> told = justTaken != null && justTaken.Count > 0 ? new HashSet<int>() : null;

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

                TellIfStolen(hero, fit, justTaken, told);
            }

            if (worn > 0)
            {
                ModLog.Info("LOOTFIT hero=" + hero.Name + " wore=" + worn);
            }

            return worn;
        }

        /// <summary>
        /// Whether the piece he just put on is one the robbery took, and if so
        /// whose. Matched on the item and its modifier, so a fine helm is not
        /// mistaken for a plain one of the same make, and each stolen piece is
        /// told at most once: two identical swords taken off two prisoners are
        /// two pieces with two owners.
        /// </summary>
        private static void TellIfStolen(Hero hero, ShoppingTrip.Candidate fit, IList<StolenPiece> justTaken,
                                         HashSet<int> told)
        {
            if (justTaken == null || told == null) return;

            EquipmentElement put = fit.Offer.Element;
            for (int i = 0; i < justTaken.Count; i++)
            {
                if (told.Contains(i)) continue;

                EquipmentElement stolen = justTaken[i].Element;
                if (stolen.Item != put.Item || stolen.ItemModifier != put.ItemModifier) continue;

                told.Add(i);

                HeroesEvolveEvents.RaiseStolenPieceWorn(hero, put.Item, fit.Slot, justTaken[i].Owner);
                return;
            }
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

            PurchaseWatch.RecordLootSwap(hero, fit.Slot, displaced, fit.Offer.Element,
                                         PurchaseWatch.ForQuality(hero, displaced, fit.Offer));

            return true;
        }
    }
}
