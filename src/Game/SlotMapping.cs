using TaleWorlds.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// Translation between core slot indices and Bannerlord's EquipmentIndex.
    ///
    /// Four weapon slots, and deliberately not five. EquipmentIndex numbers them
    /// Weapon0..Weapon3 as 0..3 and then puts ExtraWeaponSlot at 4 -- the banner.
    /// A banner is not gear a lord upgrades: it is the clan's, it carries a
    /// formation bonus rather than a statline, and no merchant sells one. Both
    /// the repair and the market stop at index 3 for that reason, so neither
    /// ever reads or writes a hero's banner.
    ///
    /// Written down because it is currently true by arithmetic rather than by
    /// intent, and a reader counting slots could raise the bound to five and
    /// start trading standards for javelins.
    /// </summary>
    public static class SlotMapping
    {
        public static EquipmentIndex WeaponSlot(int index)
        {
            switch (index)
            {
                case 0: return EquipmentIndex.Weapon0;
                case 1: return EquipmentIndex.Weapon1;
                case 2: return EquipmentIndex.Weapon2;
                default: return EquipmentIndex.Weapon3;
            }
        }

        public static readonly EquipmentIndex[] ArmorSlots =
        {
            EquipmentIndex.Head,
            EquipmentIndex.Body,
            EquipmentIndex.Leg,
            EquipmentIndex.Gloves,
            EquipmentIndex.Cape
        };

        /// <summary>Whether a slot is one of the four weapon slots -- never the banner.</summary>
        public static bool IsWeapon(EquipmentIndex slot)
        {
            return slot == EquipmentIndex.Weapon0 || slot == EquipmentIndex.Weapon1
                || slot == EquipmentIndex.Weapon2 || slot == EquipmentIndex.Weapon3;
        }

        /// <summary>Whether a slot is one of the five armour slots.</summary>
        public static bool IsArmor(EquipmentIndex slot)
        {
            for (int i = 0; i < ArmorSlots.Length; i++)
            {
                if (ArmorSlots[i] == slot) return true;
            }
            return false;
        }

        /// <summary>
        /// A readable slot name for logging. EquipmentIndex.ToString() cannot be
        /// used: Head shares its numeric value with NumAllWeaponSlots, and the
        /// runtime resolves the alias to whichever name it finds first -- real
        /// log output read "NumAllWeaponSlots=western_crowned_plated_helmet".
        /// </summary>
        public static string NameOf(EquipmentIndex slot)
        {
            switch (slot)
            {
                case EquipmentIndex.Weapon0: return "w0";
                case EquipmentIndex.Weapon1: return "w1";
                case EquipmentIndex.Weapon2: return "w2";
                case EquipmentIndex.Weapon3: return "w3";
                case EquipmentIndex.Head: return "Head";
                case EquipmentIndex.Body: return "Body";
                case EquipmentIndex.Leg: return "Leg";
                case EquipmentIndex.Gloves: return "Gloves";
                case EquipmentIndex.Cape: return "Cape";
                case EquipmentIndex.Horse: return "Horse";
                case EquipmentIndex.HorseHarness: return "Harness";
                default: return slot.ToString();
            }
        }
    }
}
