using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>Reads game state into the core's pure types.</summary>
    public static class HeroAdapter
    {
        public static SkillProfile ReadSkills(Hero hero)
        {
            if (hero == null) return new SkillProfile(0, 0, 0, 0, 0, 0, 0);

            return new SkillProfile(
                hero.GetSkillValue(DefaultSkills.OneHanded),
                hero.GetSkillValue(DefaultSkills.TwoHanded),
                hero.GetSkillValue(DefaultSkills.Polearm),
                hero.GetSkillValue(DefaultSkills.Bow),
                hero.GetSkillValue(DefaultSkills.Crossbow),
                hero.GetSkillValue(DefaultSkills.Throwing),
                hero.GetSkillValue(DefaultSkills.Riding));
        }

        public static SlotSnapshot ReadEquipment(Equipment equipment)
        {
            WeaponCategory[] weapons = new WeaponCategory[SlotSnapshot.WeaponSlotCount];

            if (equipment == null)
            {
                for (int i = 0; i < weapons.Length; i++) weapons[i] = WeaponCategory.None;
                return new SlotSnapshot(weapons, false, false, false, false, false, false, false);
            }

            for (int i = 0; i < weapons.Length; i++)
            {
                EquipmentElement element = equipment[SlotMapping.WeaponSlot(i)];
                weapons[i] = ItemClassifier.Classify(element.Item);
            }

            return new SlotSnapshot(
                weapons,
                equipment[EquipmentIndex.Horse].Item != null,
                equipment[EquipmentIndex.HorseHarness].Item != null,
                equipment[EquipmentIndex.Head].Item != null,
                equipment[EquipmentIndex.Body].Item != null,
                equipment[EquipmentIndex.Leg].Item != null,
                equipment[EquipmentIndex.Gloves].Item != null,
                equipment[EquipmentIndex.Cape].Item != null);
        }

        /// <summary>
        /// True when the hero's culture fields mounted elite troops, walking the
        /// elite line's upgrade targets. Works with modded cultures because it
        /// reads the troop tree rather than hardcoding faction names.
        /// </summary>
        public static bool CultureFieldsMountedElites(Hero hero)
        {
            if (hero == null) return false;

            CultureObject culture = hero.Culture;
            if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;
            if (culture == null) return false;

            CharacterObject troop = culture.EliteBasicTroop;
            int guard = 0;

            while (troop != null && guard < 8)
            {
                guard++;

                // BattleEquipments is IEnumerable<Equipment> in v1.4.8, not
                // MBReadOnlyList<Equipment> -- confirmed by reflecting the
                // installed TaleWorlds.Core.dll rather than assuming.
                IEnumerable<Equipment> sets = troop.BattleEquipments;
                if (sets != null)
                {
                    foreach (Equipment set in sets)
                    {
                        if (set != null && set[EquipmentIndex.Horse].Item != null) return true;
                    }
                }

                CharacterObject[] upgrades = troop.UpgradeTargets;
                if (upgrades == null || upgrades.Length == 0) break;
                troop = upgrades[upgrades.Length - 1];
            }

            return false;
        }
    }
}
