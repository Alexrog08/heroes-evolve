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
    }
}
