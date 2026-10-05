using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// Developer-console commands, group "hev". Requires cheat_mode = 1 in
    /// engine_config.txt for the console itself to open.
    ///
    /// These exist to make the come-of-age bug reachable on demand. Waiting for
    /// it to occur naturally means playing to midgame before finding out whether
    /// the repair works at all, and paying that cost again after every fix.
    /// </summary>
    public static class ConsoleCommands
    {
        [CommandLineFunctionality.CommandLineArgumentFunction("census", "hev")]
        public static string Census(List<string> args)
        {
            if (Campaign.Current == null) return "hev: no campaign is running.";

            Diagnostics.RunCensus(HeroLoadoutBehavior.ClanWeight, HeroLoadoutBehavior.SkillWeight,
                                  HeroLoadoutBehavior.MinimumTier, HeroLoadoutBehavior.DominanceMargin);
            return "hev: census written to hev.log.";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("dry_run", "hev")]
        public static string DryRun(List<string> args)
        {
            if (Campaign.Current == null) return "hev: no campaign is running.";

            Hero hero = FindHero(args);
            if (hero == null) return Usage("hev.dry_run", args);

            return Diagnostics.DryRun(hero, HeroLoadoutBehavior.ClanWeight, HeroLoadoutBehavior.SkillWeight,
                                      HeroLoadoutBehavior.MinimumTier, HeroLoadoutBehavior.DominanceMargin);
        }

        /// <summary>
        /// A named lord against the player, both ways: how likely he is to
        /// strip the player if he holds him, how much of that is his grudge,
        /// and what stripping him would cost. With no name, the census lines
        /// for every house. Reads only -- no standing moves.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("feud", "hev")]
        public static string Feud(List<string> args)
        {
            if (Campaign.Current == null) return "hev: no campaign is running.";

            Hero hero = FindHero(args);
            if (hero != null) return Diagnostics.FeudWith(hero);

            if (args != null && args.Count > 0) return Usage("hev.feud", args);

            List<string> lines = Diagnostics.Feuds();
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < lines.Count; i++) text.Append("FEUD ").Append(lines[i]).Append('\n');
            return text.ToString();
        }

        /// <summary>
        /// What the purchase engine would do for a hero in the town the player
        /// is standing in. Reads only -- nothing is bought and no gold moves.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("market", "hev")]
        public static string Market(List<string> args)
        {
            if (Campaign.Current == null) return "hev: no campaign is running.";

            Settlement settlement = Settlement.CurrentSettlement;
            if (settlement == null) return "hev: stand inside a town first.";
            if (!settlement.IsTown) return "hev: " + settlement.Name + " is not a town.";

            Hero hero = FindHero(args);
            if (hero == null) return Usage("hev.market", args);

            return Diagnostics.MarketDryRun(hero, settlement, HeroLoadoutBehavior.ClanWeight,
                                            HeroLoadoutBehavior.SkillWeight, HeroLoadoutBehavior.MinimumTier);
        }

        /// <summary>
        /// Reproduces the vanilla come-of-age failure on a named hero and then
        /// runs the real repair over it. This MODIFIES the hero: strips every
        /// weapon, armour and mount slot, leaves the dummy spatha vanilla would
        /// have left, and runs the same GrantService.Resolve/Apply pair the daily
        /// tick uses, logging the plan slot by slot on the way through.
        /// Meant for a throwaway save.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_repair", "hev")]
        public static string TestRepair(List<string> args)
        {
            if (Campaign.Current == null) return "hev: no campaign is running.";

            Hero hero = FindHero(args);
            if (hero == null) return Usage("hev.test_repair", args);
            if (hero.BattleEquipment == null) return "hev: " + hero.Name + " has no battle equipment.";

            // Strip() destroys equipment in place -- items are overwritten, not
            // returned to any inventory -- and GrantService.Grant has no
            // eligibility check of its own (that guard lives in the behaviour's
            // TryRepair). Without this, "hev.test_repair <your own name>" would
            // permanently delete the player's crafted and unique gear, and the
            // same for a companion equipped over a whole campaign. Restricting
            // the command to the population the mod actually acts on is also
            // the only thing that makes the test meaningful.
            //
            // The repair's own filter, not the general one. The two parted when
            // companions stopped getting the starting kit: asked with the
            // general filter, this would strip a companion and then watch the
            // real repair decline to dress him again.
            if (!HeroFilter.IsEligibleForRepair(hero))
            {
                return "hev: " + hero.Name + " is not a hero the repair touches ("
                       + HeroFilter.WhyNoRepair(hero) + "). Refusing to strip. "
                       + "Use hev.dry_run to inspect any hero without modifying it.";
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("hev: THIS MODIFIED " + hero.Name + ". Do not save over a campaign you care about.");

            ModLog.Info("TESTREPAIR hero=" + hero.Name + " before=" + Describe(hero));
            report.AppendLine("before: " + Describe(hero));

            Strip(hero);
            ModLog.Info("TESTREPAIR hero=" + hero.Name + " stripped=" + Describe(hero)
                        + " needsGrant=" + GrantService.NeedsGrant(hero));
            report.AppendLine("stripped: " + Describe(hero));
            report.AppendLine("needsGrant: " + GrantService.NeedsGrant(hero));

            // Resolve/log/Apply rather than Grant: the forced test exists to be
            // inspected, and a slot is only judgeable against the category the
            // planner asked for. Grant would write the same items but log only
            // the totals.
            ResolvedGrant resolved = GrantService.Resolve(hero, HeroLoadoutBehavior.ClanWeight,
                                                          HeroLoadoutBehavior.SkillWeight,
                                                          HeroLoadoutBehavior.MinimumTier,
                                                          HeroLoadoutBehavior.DominanceMargin);
            if (resolved == null) return "hev: could not resolve a loadout for " + hero.Name + ".";

            ModLog.Info("TESTREPAIR plan hero=" + hero.Name
                        + " current=" + resolved.CurrentWeapons
                        + " target=" + resolved.TargetWeapons
                        + " placed=" + resolved.PlacedWeapons);
            report.AppendLine("target: " + resolved.TargetWeapons);
            report.AppendLine("placed: " + resolved.PlacedWeapons);

            for (int i = 0; i < resolved.Slots.Count; i++)
            {
                ResolvedSlot slot = resolved.Slots[i];
                ModLog.Info("TESTREPAIR   " + slot.Label + " want=" + slot.Want + " -> "
                            + (slot.SkipReason != null ? "SKIP (" + slot.SkipReason + ")"
                               : slot.Item == null ? "NONE FOUND" : slot.Item.StringId));
            }

            int granted = GrantService.Apply(hero, resolved);
            ModLog.Info("GRANT hero=" + hero.Name + " tier=" + resolved.Ceiling
                        + " planned=" + resolved.PlannedWeaponCount + " granted=" + granted);

            ModLog.Info("TESTREPAIR hero=" + hero.Name + " after=" + Describe(hero));
            report.AppendLine("after: " + Describe(hero));
            report.AppendLine("(full detail in hev.log)");

            return report.ToString();
        }

        /// <summary>
        /// Empties every slot the mod fills, then puts back the lone spatha
        /// vanilla's dummy fallback leaves behind -- the exact state a hero is
        /// in when the come-of-age bug hits.
        /// </summary>
        /// <summary>
        /// Robs a hero through the real robbery, so the rags it leaves can be
        /// read rather than waited for.
        ///
        /// test_repair next door strips a hero by hand and calls the repair,
        /// which is the other path entirely: it answers "this man never had
        /// anything". This one goes through PlunderService.Take, which is where
        /// Rags hangs, so what comes back is what a captured lord would really
        /// be standing in.
        ///
        /// Needed because the thing worth checking is rare and slow in play. A
        /// robbery wants a capture, a captor whose character rolls for it, and
        /// a slot some culture sells nothing for at the rag tier. Waiting for
        /// that combination to turn up by itself is not a test.
        ///
        /// The spoils go nowhere anybody keeps: a throwaway roster, so running
        /// this does not quietly hand the player's party a lord's armour.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_robbery", "hev")]
        public static string TestRobbery(List<string> args)
        {
            if (Campaign.Current == null) return "hev: no campaign is running.";

            // No name: one lord of every culture, which is the test this was
            // written for. The case that decided the rag tiers is Nord, whose
            // boots exist at tier 1 and not at tier 2, and asking a player to
            // go and find a Nord lord by name to prove it is how a check stops
            // being run.
            if (args == null || args.Count == 0) return SweepCultures();

            Hero hero = FindHero(args);
            if (hero == null) return Usage("hev.test_robbery", args);
            if (hero.BattleEquipment == null) return "hev: " + hero.Name + " has no battle equipment.";

            if (!PlunderService.CanBeStripped(hero))
            {
                return "hev: " + hero.Name + " is not someone this mod will strip. "
                       + "Use hev.dry_run to inspect any hero without modifying it.";
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("hev: THIS MODIFIED " + hero.Name + ". Do not save over a campaign you care about.");

            int before = Rags.HandedOut;
            int emptyBefore = Rags.SlotsLeftEmpty;

            ModLog.Info("TESTROBBERY hero=" + hero.Name
                        + " culture=" + (hero.Culture != null ? hero.Culture.StringId : "none")
                        + " before=" + Describe(hero));
            report.AppendLine("before: " + Describe(hero));

            // Somewhere for the loot to go that nobody is carrying afterwards.
            MobileParty bin = hero.PartyBelongedTo != null ? hero.PartyBelongedTo : MobileParty.MainParty;
            if (bin == null) return "hev: no party to put the spoils in.";

            int value;
            int taken = PlunderService.Take(bin.Party, hero, out value);

            ModLog.Info("TESTROBBERY hero=" + hero.Name + " taken=" + taken + " value=" + value
                        + " after=" + Describe(hero)
                        + " ragsHandedOut=" + (Rags.HandedOut - before)
                        + " slotsLeftEmpty=" + (Rags.SlotsLeftEmpty - emptyBefore));

            // The notice, on demand. It is the one part of a robbery a player
            // actually reads, and the one part no unit test can prove: a text
            // variable whose name does not match renders as the literal
            // {CAPTOR}. Forced past the faction check, because the thing being
            // checked here is the wording, not who gets told.
            Hero witness = bin.LeaderHero != null && bin.LeaderHero != hero
                           ? bin.LeaderHero : Hero.MainHero;
            if (witness != null)
            {
                PlunderService.Announce(witness.MapFaction,
                                        PlunderService.NameOf(witness), hero, true);
            }
            report.AppendLine("notice sent to the log -- read it, it is the point.");

            report.AppendLine("taken: " + taken + " pieces worth " + value);
            report.AppendLine("rags handed back: " + (Rags.HandedOut - before));
            report.AppendLine("slots left empty: " + (Rags.SlotsLeftEmpty - emptyBefore)
                              + (Rags.SlotsLeftEmpty - emptyBefore > 0 ? "   <-- these stay bare for life" : ""));
            report.AppendLine("after: " + Describe(hero));
            report.AppendLine("(the spoils went into " + bin.Name + "; full detail in hev.log)");

            return report.ToString();
        }

        /// <summary>
        /// Shows the notice a freed man gets, without waiting to be captured.
        ///
        /// There is no cheat that takes the player prisoner, and losing a
        /// battle on purpose to read one line is not a test anybody runs. This
        /// walks the real path bar the event itself: it notes a robbery
        /// against the player's current captivity and then asks for the
        /// release notice, so the ledger key, the lookup and the wording are
        /// all exercised with the game's own figures.
        ///
        /// What it cannot reach is HeroPrisonerReleased firing, and that part
        /// needs no test: every one of the eight EndCaptivityAction routes
        /// passes showNotification as a hardcoded true, and
        /// TakePrisonerAction.ApplyInternal is the only writer of
        /// CaptivityStartTime, so the key cannot drift between the two ends.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_release", "hev")]
        public static string TestRelease(List<string> args)
        {
            if (Campaign.Current == null) return "hev: no campaign is running.";
            if (Hero.MainHero == null) return "hev: there is no player hero.";

            // Both wordings, because which one a player sees depends on how
            // he got out and neither is reachable on demand.
            CaptivityRobberies.Record(Hero.MainHero);
            bool escaped = PlunderService.AnnounceRelease(
                Hero.MainHero, EndCaptivityDetail.ReleasedAfterEscape);

            CaptivityRobberies.Record(Hero.MainHero);
            bool freed = PlunderService.AnnounceRelease(
                Hero.MainHero, EndCaptivityDetail.Ransom);

            // Consumed by the calls above, so a third ask is a silent one --
            // which is the guarantee that one release cannot speak twice.
            bool cleared = !CaptivityRobberies.Consume(Hero.MainHero);

            // And a dead man hears nothing, though his entry still clears.
            CaptivityRobberies.Record(Hero.MainHero);
            bool silent = !PlunderService.AnnounceRelease(
                Hero.MainHero, EndCaptivityDetail.Death);
            bool deadCleared = !CaptivityRobberies.Consume(Hero.MainHero);

            if (!escaped || !freed) return "hev: FAILED -- nothing was sent. The ledger key did not match.";
            if (!cleared) return "hev: FAILED -- a notice fired but the ledger did not clear.";
            if (!silent) return "hev: FAILED -- a dead man was told what he was wearing.";
            if (!deadCleared) return "hev: FAILED -- death left an entry on the books.";

            return "hev: two notices sent, escape first, then release. "
                   + "Ledger clear. Look at the message feed, bottom left.";
        }

        /// <summary>
        /// Robs one lord of each culture and reports what each was left in.
        ///
        /// The figure worth reading is the last column. A slot a robbery empties
        /// and this cannot refill stays bare for that hero's life, because a
        /// lord only ever buys a better version of what he already carries and
        /// can never fill an empty slot. Anything but nought there names a
        /// culture that sells nothing for that slot even at the bottom of the
        /// scale.
        /// </summary>
        private static string SweepCultures()
        {
            Dictionary<string, Hero> pick = new Dictionary<string, Hero>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (hero == null || hero.Culture == null) continue;
                    if (hero.IsPrisoner || hero == Hero.MainHero) continue;
                    if (!PlunderService.CanBeStripped(hero)) continue;
                    if (!PlunderService.HasAnythingToTake(hero)) continue;
                    if (hero.PartyBelongedTo == null) continue;

                    string culture = hero.Culture.StringId;
                    if (pick.ContainsKey(culture)) continue;

                    pick[culture] = hero;
                }
                catch
                {
                    // One unreadable hero must not cost the sweep.
                }
            }

            if (pick.Count == 0) return "hev: found nobody to rob.";

            StringBuilder report = new StringBuilder();
            report.AppendLine("hev: THIS MODIFIED " + pick.Count + " LORDS. Do not save over a campaign you care about.");
            report.AppendLine("culture        lord                 took  rags  bare");

            int bareTotal = 0;

            foreach (KeyValuePair<string, Hero> pair in pick)
            {
                Hero hero = pair.Value;
                int rags = Rags.HandedOut;
                int bare = Rags.SlotsLeftEmpty;

                ModLog.Info("TESTROBBERY hero=" + hero.Name + " culture=" + pair.Key
                            + " before=" + Describe(hero));

                int value;
                int taken = PlunderService.Take(hero.PartyBelongedTo.Party, hero, out value);

                rags = Rags.HandedOut - rags;
                bare = Rags.SlotsLeftEmpty - bare;
                bareTotal += bare;

                ModLog.Info("TESTROBBERY hero=" + hero.Name + " taken=" + taken
                            + " rags=" + rags + " bare=" + bare
                            + " after=" + Describe(hero));

                report.AppendLine(Pad(pair.Key, 14) + Pad(hero.Name.ToString(), 20)
                                  + Pad(taken.ToString(), 6) + Pad(rags.ToString(), 6)
                                  + bare + (bare > 0 ? "  <-- bare for life" : ""));
            }

            report.AppendLine(bareTotal == 0
                              ? "every slot refilled."
                              : bareTotal + " slots could not be refilled. See hev.log.");
            report.AppendLine("(weapon kinds should match before and after; full detail in hev.log)");

            return report.ToString();
        }

        private static string Pad(string text, int width)
        {
            if (text == null) text = "";
            if (text.Length >= width) return text.Substring(0, width - 1) + " ";
            return text.PadRight(width);
        }

        private static void Strip(Hero hero)
        {
            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                hero.BattleEquipment[SlotMapping.WeaponSlot(i)] = EquipmentElement.Invalid;
            }

            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                hero.BattleEquipment[slot] = EquipmentElement.Invalid;
            }

            hero.BattleEquipment[EquipmentIndex.Horse] = EquipmentElement.Invalid;
            hero.BattleEquipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;

            ItemObject spatha = MBObjectManager.Instance.GetObject<ItemObject>(HeroAdapter.DummySwordId);
            if (spatha != null)
            {
                hero.BattleEquipment[EquipmentIndex.Weapon0] = new EquipmentElement(spatha, null, null, false);
            }
        }

        private static string Describe(Hero hero)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                text.Append('w').Append(i).Append('=').Append(IdOf(hero, SlotMapping.WeaponSlot(i))).Append(' ');
            }
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                text.Append(SlotMapping.NameOf(slot)).Append('=').Append(IdOf(hero, slot)).Append(' ');
            }
            text.Append("Horse=").Append(IdOf(hero, EquipmentIndex.Horse));
            text.Append(" Harness=").Append(IdOf(hero, EquipmentIndex.HorseHarness));
            return text.ToString();
        }

        private static string IdOf(Hero hero, EquipmentIndex slot)
        {
            ItemObject item = hero.BattleEquipment[slot].Item;
            return item == null ? "-" : item.StringId;
        }

        /// <summary>
        /// Matches a hero by name, joining every argument so multi-word names
        /// work without quoting. An exact match always wins. A partial match is
        /// only accepted when it is unique: because test_repair destroys the
        /// target's equipment, silently resolving "Ari" to whichever of several
        /// heroes happens to come first in Hero.AllAliveHeroes would strip a
        /// lord the user never named. Ambiguity is reported instead, via
        /// <see cref="_lastAmbiguity"/>.
        /// </summary>
        private static Hero FindHero(List<string> args)
        {
            _lastAmbiguity = null;

            if (args == null || args.Count == 0) return null;

            string wanted = string.Join(" ", args.ToArray()).Trim();
            if (wanted.Length == 0) return null;

            List<Hero> partials = new List<Hero>();
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero.Name == null) continue;
                string name = hero.Name.ToString();

                if (string.Equals(name, wanted, System.StringComparison.OrdinalIgnoreCase)) return hero;
                if (name.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) >= 0) partials.Add(hero);
            }

            if (partials.Count == 1) return partials[0];
            if (partials.Count == 0) return null;

            StringBuilder names = new StringBuilder();
            for (int i = 0; i < partials.Count && i < 10; i++)
            {
                if (i > 0) names.Append(", ");
                names.Append(partials[i].Name);
            }
            if (partials.Count > 10) names.Append(", ... (").Append(partials.Count).Append(" total)");

            _lastAmbiguity = "hev: \"" + wanted + "\" matches several heroes: " + names
                             + ". Give the full name.";
            return null;
        }

        /// <summary>
        /// Set by FindHero when a partial name matched more than one hero, so
        /// Usage can say so instead of the misleading "no hero matches".
        /// </summary>
        private static string _lastAmbiguity;

        private static string Usage(string command, List<string> args)
        {
            if (_lastAmbiguity != null) return _lastAmbiguity;

            string wanted = args == null || args.Count == 0 ? "" : string.Join(" ", args.ToArray()).Trim();
            if (wanted.Length == 0) return "hev: usage: " + command + " <hero name>";
            return "hev: no living hero matches \"" + wanted + "\".";
        }
    }
}
