using System;

namespace HeroesEvolve.Core
{
    /// <summary>
    /// What a hero currently has equipped, expressed only in core types.
    /// The four weapon slots mirror Bannerlord's Weapon0..Weapon3.
    /// </summary>
    public sealed class SlotSnapshot
    {
        public const int WeaponSlotCount = 4;
        private readonly WeaponCategory[] _weapons;

        public SlotSnapshot(WeaponCategory[] weapons,
                            bool hasMount, bool hasHarness,
                            bool hasHelmet, bool hasBody, bool hasLegs,
                            bool hasGloves, bool hasCape)
        {
            if (weapons == null || weapons.Length != WeaponSlotCount)
                throw new ArgumentException("weapons must have exactly four entries");

            _weapons = new WeaponCategory[WeaponSlotCount];
            Array.Copy(weapons, _weapons, WeaponSlotCount);

            HasMount = hasMount;
            HasHarness = hasHarness;
            HasHelmet = hasHelmet;
            HasBody = hasBody;
            HasLegs = hasLegs;
            HasGloves = hasGloves;
            HasCape = hasCape;
        }

        public bool HasMount { get; private set; }
        public bool HasHarness { get; private set; }
        public bool HasHelmet { get; private set; }
        public bool HasBody { get; private set; }
        public bool HasLegs { get; private set; }
        public bool HasGloves { get; private set; }
        public bool HasCape { get; private set; }

        public WeaponCategory WeaponAt(int index)
        {
            return _weapons[index];
        }

        public int EmptyWeaponSlots
        {
            get
            {
                int count = 0;
                for (int i = 0; i < WeaponSlotCount; i++)
                {
                    if (_weapons[i] == WeaponCategory.None) count++;
                }
                return count;
            }
        }

        public bool Contains(WeaponCategory category)
        {
            for (int i = 0; i < WeaponSlotCount; i++)
            {
                if (_weapons[i] == category) return true;
            }
            return false;
        }

        /// <summary>True when any equipped weapon rules out using a shield.</summary>
        public bool HasTwoHandedEquipped
        {
            get
            {
                for (int i = 0; i < WeaponSlotCount; i++)
                {
                    if (CategoryRules.IsTwoHanded(_weapons[i])) return true;
                }
                return false;
            }
        }

        /// <summary>Indices of the empty weapon slots, lowest first.</summary>
        public int[] EmptySlotIndices()
        {
            int[] buffer = new int[WeaponSlotCount];
            int n = 0;
            for (int i = 0; i < WeaponSlotCount; i++)
            {
                if (_weapons[i] == WeaponCategory.None) buffer[n++] = i;
            }
            int[] result = new int[n];
            Array.Copy(buffer, result, n);
            return result;
        }
    }
}
