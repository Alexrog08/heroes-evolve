using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Repairs the vanilla come-of-age bug by handing a hero a coherent loadout
    /// for free. This is fixing a defect, not economy: it must work for a lord
    /// with no gold who never visits a town.
    /// </summary>
    public static class GrantService
    {
        /// <summary>The one-handed sword vanilla's dummy fallback hands out.</summary>
        private const string DummySwordId = "iron_spatha_sword_t2";

        /// <summary>
        /// True when the hero's battle equipment is empty, or is the vanilla
        /// dummy set: a lone spatha and nothing else in the weapon slots.
        /// </summary>
        public static bool NeedsGrant(Hero hero)
        {
            if (hero == null || hero.BattleEquipment == null) return false;

            int weapons = 0;
            bool onlyDummySword = true;

            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                ItemObject item = hero.BattleEquipment[SlotMapping.WeaponSlot(i)].Item;
                if (item == null) continue;
                weapons++;
                if (item.StringId != DummySwordId) onlyDummySword = false;
            }

            if (weapons == 0) return true;
            return weapons == 1 && onlyDummySword;
        }

        public static void Grant(Hero hero, float clanWeight, float skillWeight,
                                 int minimumTier, int dominanceMargin)
        {
            if (hero == null || hero.BattleEquipment == null) return;

            SkillProfile skills = HeroAdapter.ReadSkills(hero);
            SlotSnapshot current = HeroAdapter.ReadEquipment(hero.BattleEquipment);

            int clanTier = hero.Clan != null ? hero.Clan.Tier : 0;
            int ceiling = TierCeiling.Compute(clanTier, skills.MaxCombatSkill, clanWeight, skillWeight, minimumTier);

            CultureObject culture = hero.Culture;
            if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;

            bool cultureMounted = HeroAdapter.CultureFieldsMountedElites(hero);
            MountedRangedAvailability availability = ItemCatalog.RangedAvailability(hero, culture, ceiling);

            // PlanTarget is called here as well as inside Plan (which calls it
            // again internally) purely to read WantsMount: Plan's own return
            // value is just the slot placements, and both the mount grant
            // below and the `mounted` flag need to know what the planner
            // assumed about this hero's mount state. PlanTarget is pure and
            // cheap (no game calls), so computing it twice costs nothing and
            // avoids changing Plan's public signature for every existing caller.
            LoadoutTarget target = LoadoutPlanner.PlanTarget(skills, current, availability, dominanceMargin, cultureMounted);
            List<PlannedSlot> plan = LoadoutPlanner.Plan(skills, current, availability, dominanceMargin, cultureMounted);

            // Must agree with the planner's own assumption (HasMount ||
            // WantsMount, see LoadoutPlanner.Plan): the plan already
            // restricted which ranged weapons are viable on the assumption
            // this hero ends up mounted, and GrantMount below is what makes
            // that assumption true. Using current.HasMount alone here silently
            // disagreed with the plan whenever WantsMount -- not an already-
            // owned horse -- was what put the hero on one: a Vlandian
            // crossbowman on foot, whose culture fields mounted elites, had
            // his crossbow planned around mounted-viability rules that were
            // never actually going to apply, because nothing ever gave him
            // the horse those rules assumed.
            bool mounted = current.HasMount || target.WantsMount;

            int granted = 0;
            foreach (PlannedSlot slot in plan)
            {
                ItemObject item = ItemCatalog.FindBest(slot.Category, culture, ceiling, skills, hero, mounted);
                if (item == null) continue;

                hero.BattleEquipment[SlotMapping.WeaponSlot(slot.SlotIndex)] =
                    new EquipmentElement(item, null, null, false);
                granted++;
            }

            granted += GrantMount(hero, culture, ceiling, skills, target.WantsMount);
            granted += GrantArmor(hero, culture, ceiling);

            ModLog.Info("GRANT hero=" + hero.Name + " tier=" + ceiling
                        + " planned=" + plan.Count + " granted=" + granted);
        }

        /// <summary>
        /// Grants the mount the plan assumes, then a compatible harness.
        /// Only acts when the plan actually wants a mount and the hero does
        /// not already have one -- an equipped mount or harness is never
        /// replaced. The harness lookup only ever runs once a mount was
        /// just found, since compatibility is judged against that specific
        /// mount's family (see ItemCatalog.FindBestHarness).
        /// </summary>
        private static int GrantMount(Hero hero, CultureObject culture, int ceiling, SkillProfile skills, bool wantsMount)
        {
            if (!wantsMount) return 0;
            if (hero.BattleEquipment[EquipmentIndex.Horse].Item != null) return 0;

            ItemObject mount = ItemCatalog.FindBestMount(culture, ceiling, skills);
            if (mount == null) return 0;

            hero.BattleEquipment[EquipmentIndex.Horse] = new EquipmentElement(mount, null, null, false);
            int granted = 1;

            if (hero.BattleEquipment[EquipmentIndex.HorseHarness].Item == null)
            {
                ItemObject harness = ItemCatalog.FindBestHarness(mount, culture, ceiling);
                if (harness != null)
                {
                    hero.BattleEquipment[EquipmentIndex.HorseHarness] = new EquipmentElement(harness, null, null, false);
                    granted++;
                }
            }

            return granted;
        }

        private static int GrantArmor(Hero hero, CultureObject culture, int ceiling)
        {
            int granted = 0;

            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                if (hero.BattleEquipment[slot].Item != null) continue;

                ItemObject item = FindArmorFor(slot, culture, ceiling);
                if (item == null) continue;

                hero.BattleEquipment[slot] = new EquipmentElement(item, null, null, false);
                granted++;
            }

            return granted;
        }

        /// <summary>Maps an armour slot to its item type and defers to the catalogue.</summary>
        private static ItemObject FindArmorFor(EquipmentIndex slot, CultureObject culture, int ceiling)
        {
            ItemObject.ItemTypeEnum wanted;
            switch (slot)
            {
                case EquipmentIndex.Head: wanted = ItemObject.ItemTypeEnum.HeadArmor; break;
                case EquipmentIndex.Body: wanted = ItemObject.ItemTypeEnum.BodyArmor; break;
                case EquipmentIndex.Leg: wanted = ItemObject.ItemTypeEnum.LegArmor; break;
                case EquipmentIndex.Gloves: wanted = ItemObject.ItemTypeEnum.HandArmor; break;
                default: wanted = ItemObject.ItemTypeEnum.Cape; break;
            }

            return ItemCatalog.FindBestArmor(wanted, culture, ceiling);
        }
    }
}
