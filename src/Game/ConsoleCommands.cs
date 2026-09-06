using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Developer-console commands, group "hlf". Requires cheat_mode = 1 in
    /// engine_config.txt for the console itself to open.
    ///
    /// These exist to make the come-of-age bug reachable on demand. Waiting for
    /// it to occur naturally means playing to midgame before finding out whether
    /// the repair works at all, and paying that cost again after every fix.
    /// </summary>
    public static class ConsoleCommands
    {
        [CommandLineFunctionality.CommandLineArgumentFunction("census", "hlf")]
        public static string Census(List<string> args)
        {
            if (Campaign.Current == null) return "hlf: no campaign is running.";

            Diagnostics.RunCensus(HeroLoadoutBehavior.ClanWeight, HeroLoadoutBehavior.SkillWeight,
                                  HeroLoadoutBehavior.MinimumTier, HeroLoadoutBehavior.DominanceMargin);
            return "hlf: census written to hlf.log.";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("dry_run", "hlf")]
        public static string DryRun(List<string> args)
        {
            if (Campaign.Current == null) return "hlf: no campaign is running.";

            Hero hero = FindHero(args);
            if (hero == null) return Usage("hlf.dry_run", args);

            return Diagnostics.DryRun(hero, HeroLoadoutBehavior.ClanWeight, HeroLoadoutBehavior.SkillWeight,
                                      HeroLoadoutBehavior.MinimumTier, HeroLoadoutBehavior.DominanceMargin);
        }

        /// <summary>
        /// Reproduces the vanilla come-of-age failure on a named hero and then
        /// runs the real repair over it. This MODIFIES the hero: strips every
        /// weapon, armour and mount slot, leaves the dummy spatha vanilla would
        /// have left, and runs the same GrantService.Resolve/Apply pair the daily
        /// tick uses, logging the plan slot by slot on the way through.
        /// Meant for a throwaway save.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_repair", "hlf")]
        public static string TestRepair(List<string> args)
        {
            if (Campaign.Current == null) return "hlf: no campaign is running.";

            Hero hero = FindHero(args);
            if (hero == null) return Usage("hlf.test_repair", args);
            if (hero.BattleEquipment == null) return "hlf: " + hero.Name + " has no battle equipment.";

            // Strip() destroys equipment in place -- items are overwritten, not
            // returned to any inventory -- and GrantService.Grant has no
            // eligibility check of its own (that guard lives in the behaviour's
            // TryRepair). Without this, "hlf.test_repair <your own name>" would
            // permanently delete the player's crafted and unique gear, and the
            // same for a companion equipped over a whole campaign. Restricting
            // the command to the population the mod actually acts on is also
            // the only thing that makes the test meaningful.
            if (!HeroFilter.IsEligible(hero))
            {
                return "hlf: " + hero.Name + " is not a hero this mod touches "
                       + "(player character, companion, child, or template). Refusing to strip. "
                       + "Use hlf.dry_run to inspect any hero without modifying it.";
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("hlf: THIS MODIFIED " + hero.Name + ". Do not save over a campaign you care about.");

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
            if (resolved == null) return "hlf: could not resolve a loadout for " + hero.Name + ".";

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
            report.AppendLine("(full detail in hlf.log)");

            return report.ToString();
        }

        /// <summary>
        /// Empties every slot the mod fills, then puts back the lone spatha
        /// vanilla's dummy fallback leaves behind -- the exact state a hero is
        /// in when the come-of-age bug hits.
        /// </summary>
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

            ItemObject spatha = MBObjectManager.Instance.GetObject<ItemObject>(GrantService.DummySwordId);
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

            _lastAmbiguity = "hlf: \"" + wanted + "\" matches several heroes: " + names
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
            if (wanted.Length == 0) return "hlf: usage: " + command + " <hero name>";
            return "hlf: no living hero matches \"" + wanted + "\".";
        }
    }
}
