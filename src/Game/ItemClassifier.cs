using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>Maps game items onto the core's category vocabulary.</summary>
    public static class ItemClassifier
    {
        public static WeaponCategory Classify(ItemObject item)
        {
            if (item == null) return WeaponCategory.None;

            if (item.ItemType == ItemObject.ItemTypeEnum.Shield) return WeaponCategory.Shield;
            if (item.ItemType == ItemObject.ItemTypeEnum.Arrows) return WeaponCategory.Arrows;
            if (item.ItemType == ItemObject.ItemTypeEnum.Bolts) return WeaponCategory.Bolts;
            if (item.ItemType == ItemObject.ItemTypeEnum.Bow) return WeaponCategory.Bow;
            if (item.ItemType == ItemObject.ItemTypeEnum.Crossbow) return WeaponCategory.Crossbow;
            if (item.ItemType == ItemObject.ItemTypeEnum.Thrown) return WeaponCategory.Throwing;

            if (!item.HasWeaponComponent) return WeaponCategory.None;

            WeaponComponentData weapon = item.PrimaryWeapon;
            if (weapon == null) return WeaponCategory.Other;

            switch (weapon.WeaponClass)
            {
                case WeaponClass.OneHandedSword: return WeaponCategory.OneHandedSword;
                case WeaponClass.TwoHandedSword: return WeaponCategory.TwoHandedSword;
                case WeaponClass.OneHandedAxe: return WeaponCategory.OneHandedAxe;
                case WeaponClass.TwoHandedAxe: return WeaponCategory.TwoHandedAxe;
                case WeaponClass.Mace: return WeaponCategory.Mace;
                case WeaponClass.TwoHandedMace: return WeaponCategory.Mace;
                case WeaponClass.OneHandedPolearm: return WeaponCategory.Spear;
                case WeaponClass.TwoHandedPolearm: return WeaponCategory.Polearm;
                case WeaponClass.LowGripPolearm: return WeaponCategory.Polearm;
                case WeaponClass.Bow: return WeaponCategory.Bow;
                case WeaponClass.Crossbow: return WeaponCategory.Crossbow;
                case WeaponClass.Arrow: return WeaponCategory.Arrows;
                case WeaponClass.Bolt: return WeaponCategory.Bolts;
                case WeaponClass.Javelin: return WeaponCategory.Throwing;
                case WeaponClass.ThrowingAxe: return WeaponCategory.Throwing;
                case WeaponClass.ThrowingKnife: return WeaponCategory.Throwing;
                case WeaponClass.Stone: return WeaponCategory.Throwing;
                default: return WeaponCategory.Other;
            }
        }

        /// <summary>
        /// Whether this hero could actually use the weapon from horseback.
        /// Decided per item, never per class: light crossbows reload mounted and
        /// heavy ones do not, and a modded bow may carry the flag too.
        /// </summary>
        public static bool IsUsableMounted(ItemObject item, Hero hero)
        {
            if (item == null || hero == null) return false;
            if (!item.HasWeaponComponent) return true;

            WeaponComponentData weapon = item.PrimaryWeapon;
            if (weapon == null) return true;

            if ((weapon.WeaponFlags & WeaponFlags.CantReloadOnHorseback) == 0) return true;

            // Only the crossbow perk is documented to lift the restriction:
            // "You can reload any crossbow on horseback."
            if (Classify(item) == WeaponCategory.Crossbow)
            {
                return hero.GetPerkValue(DefaultPerks.Crossbow.MountedCrossbowman);
            }

            return false;
        }

        /// <summary>The skill that gates using this item at all, via its difficulty.</summary>
        public static bool MeetsDifficulty(ItemObject item, SkillProfile skills)
        {
            if (item == null) return false;
            if (item.Difficulty <= 0f) return true;

            WeaponCategory category = Classify(item);
            SkillKind skill = LoadoutPlanner.SkillForCategory(category);

            if (item.ItemType == ItemObject.ItemTypeEnum.Horse) skill = SkillKind.Riding;

            return skills.Get(skill) >= (int)item.Difficulty;
        }
    }
}
