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

            List<PlannedSlot> plan = LoadoutPlanner.Plan(skills, current, availability, dominanceMargin, cultureMounted);
            bool mounted = current.HasMount;

            int granted = 0;
            foreach (PlannedSlot slot in plan)
            {
                ItemObject item = ItemCatalog.FindBest(slot.Category, culture, ceiling, skills, hero, mounted);
                if (item == null) continue;

                hero.BattleEquipment[SlotMapping.WeaponSlot(slot.SlotIndex)] =
                    new EquipmentElement(item, null, null, false);
                granted++;
            }

            granted += GrantArmor(hero, culture, ceiling);

            ModLog.Info("GRANT hero=" + hero.Name + " tier=" + ceiling
                        + " planned=" + plan.Count + " granted=" + granted);
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
