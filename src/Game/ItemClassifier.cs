using System.Collections.Generic;
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

            return FromWeaponClass(weapon.WeaponClass);
        }

        /// <summary>
        /// One WeaponClass -> one category. Shared by Classify (which reads the
        /// item's primary usage) and by Supports (which walks every usage), so
        /// the two can never disagree about what a class means.
        /// </summary>
        public static WeaponCategory FromWeaponClass(WeaponClass weaponClass)
        {
            switch (weaponClass)
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
                case WeaponClass.SmallShield: return WeaponCategory.Shield;
                case WeaponClass.LargeShield: return WeaponCategory.Shield;
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

            // A long bow cannot be drawn from horseback at all. The game says so
            // through the item's usage set (ItemUsageSetFlags.RequiresNoMount),
            // not through WeaponFlags -- noble_long_bow carries only
            // NotUsableWithOneHand and TwoHandIdleOnMount, so the flag check
            // below waves it straight through. Observed live: a mounted Vlandian
            // king was handed noble_long_bow, a weapon he cannot use on the
            // horse he was granted in the same pass.
            //
            // The usage string is read directly because the string -> flags
            // lookup lives behind a native delegate. Mods follow the same
            // naming, so this holds for them too; a mod inventing its own
            // dismounted-only usage name would slip through, which is a smaller
            // failure than the one being fixed.
            if (RequiresNoMount(item)) return false;

            if ((weapon.WeaponFlags & WeaponFlags.CantReloadOnHorseback) == 0) return true;

            // Only the crossbow perk is documented to lift the restriction:
            // "You can reload any crossbow on horseback."
            if (Classify(item) == WeaponCategory.Crossbow)
            {
                return hero.GetPerkValue(DefaultPerks.Crossbow.MountedCrossbowman);
            }

            return false;
        }

        /// <summary>
        /// True when any of the item's usages is one the game forbids on a
        /// mount. Only long bows use this today.
        /// </summary>
        private static bool RequiresNoMount(ItemObject item)
        {
            foreach (WeaponComponentData usage in AllUsages(item))
            {
                if (usage.ItemUsage == "long_bow") return true;
            }
            return false;
        }

        /// <summary>
        /// Every way the item can be wielded, not just the first.
        ///
        /// This matters far more than it looks. Crafted weapons carry several
        /// usages and PrimaryWeapon returns only usage zero, which for the
        /// TwoHandedSword crafting template is OneHandedBastardSword -- so
        /// every crafted bastard sword in the game files itself as one-handed.
        /// The live catalogue census showed the damage: 8 items classified
        /// TwoHandedSword against 153 OneHandedSword, and 23 Polearm against
        /// 107 Spear, because the TwoHandedPolearm template's first usage is
        /// OneHandedPolearm. A hero who should carry a two-hander was choosing
        /// from eight items.
        /// </summary>
        public static IEnumerable<WeaponComponentData> AllUsages(ItemObject item)
        {
            if (item == null || !item.HasWeaponComponent) yield break;

            IEnumerable<WeaponComponentData> usages = item.Weapons;
            if (usages == null)
            {
                WeaponComponentData primary = item.PrimaryWeapon;
                if (primary != null) yield return primary;
                yield break;
            }

            foreach (WeaponComponentData usage in usages)
            {
                if (usage != null) yield return usage;
            }
        }

        /// <summary>
        /// Whether the item can serve the requested category through ANY of its
        /// usages. A bastard sword answers both a one-handed and a two-handed
        /// request -- that is what makes it a bastard sword, and the reason the
        /// planner is allowed to treat it as a wildcard.
        /// </summary>
        public static bool Supports(ItemObject item, WeaponCategory wanted)
        {
            if (item == null || wanted == WeaponCategory.None) return false;

            // The item's primary identity is checked first and on its own:
            // Classify resolves shields, ammunition, bows, crossbows and thrown
            // weapons from ItemType before ever reaching a WeaponClass, and
            // those answers must not be weakened by the usage walk below. The
            // walk only ever widens the match.
            if (CategoryRules.SameFamily(wanted, Classify(item))) return true;
            if (!item.HasWeaponComponent) return false;

            foreach (WeaponComponentData usage in AllUsages(item))
            {
                if (CategoryRules.SameFamily(wanted, FromWeaponClass(usage.WeaponClass))) return true;
            }
            return false;
        }

        /// <summary>
        /// True when the item can also be wielded two-handed in the given
        /// family -- the test for the redundancy rule: a hero who already
        /// carries a two-handed sword gains nothing from a one-handed slot
        /// filled by a weapon that is itself a two-handed sword.
        /// </summary>
        public static bool AlsoServesTwoHanded(ItemObject item, WeaponCategory twoHandedCategory)
        {
            if (item == null) return false;

            foreach (WeaponComponentData usage in AllUsages(item))
            {
                if (FromWeaponClass(usage.WeaponClass) == twoHandedCategory) return true;
            }
            return false;
        }

        /// <summary>
        /// The skill that gates using this item at all, via its difficulty,
        /// where the caller has no particular category in mind (armour, mounts).
        /// </summary>
        public static bool MeetsDifficulty(ItemObject item, SkillProfile skills)
        {
            return MeetsDifficulty(item, skills, WeaponCategory.None);
        }

        /// <summary>
        /// As above, but gated on the skill for the category actually being
        /// requested.
        ///
        /// Deriving the skill from Classify(item) reads the item's PRIMARY
        /// usage, and that is wrong for anything wieldable more than one way: a
        /// bastard sword classifies as OneHandedSword, so a hero asking for a
        /// two-hander had the item gated on his One Handed skill instead of his
        /// Two Handed one. A hero strong in one and weak in the other was
        /// denied, or allowed, on the wrong number. This is the same mistake
        /// already fixed in Supports, which survived next door.
        ///
        /// Passing None keeps the old behaviour, which is what armour and mounts
        /// want -- neither belongs to a weapon category, and Horse routes to
        /// Riding explicitly below.
        /// </summary>
        public static bool MeetsDifficulty(ItemObject item, SkillProfile skills, WeaponCategory requested)
        {
            if (item == null) return false;
            if (item.Difficulty <= 0f) return true;

            if (item.ItemType == ItemObject.ItemTypeEnum.Horse)
            {
                return skills.Get(SkillKind.Riding) >= (int)item.Difficulty;
            }

            WeaponCategory category = requested == WeaponCategory.None ? Classify(item) : requested;
            SkillKind skill = LoadoutPlanner.SkillForCategory(category);

            return skills.Get(skill) >= (int)item.Difficulty;
        }
    }
}
