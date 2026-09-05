using TaleWorlds.Core;

namespace HeroLoadoutFixer
{
    /// <summary>Translation between core slot indices and Bannerlord's EquipmentIndex.</summary>
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
