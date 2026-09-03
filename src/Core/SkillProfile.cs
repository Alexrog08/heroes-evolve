using System;

namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// Immutable snapshot of a hero's combat-relevant skills.
    /// Pure: never references game types.
    /// </summary>
    public sealed class SkillProfile
    {
        private const int WeaponSkillCount = 6;
        private readonly int[] _values;

        public SkillProfile(int oneHanded, int twoHanded, int polearm,
                            int bow, int crossbow, int throwing, int riding)
        {
            _values = new int[7];
            _values[(int)SkillKind.OneHanded] = oneHanded;
            _values[(int)SkillKind.TwoHanded] = twoHanded;
            _values[(int)SkillKind.Polearm] = polearm;
            _values[(int)SkillKind.Bow] = bow;
            _values[(int)SkillKind.Crossbow] = crossbow;
            _values[(int)SkillKind.Throwing] = throwing;
            _values[(int)SkillKind.Riding] = riding;
        }

        public int Get(SkillKind kind)
        {
            return _values[(int)kind];
        }

        /// <summary>Highest of the six weapon skills. Riding is excluded, matching DynamicLordGear.</summary>
        public int MaxCombatSkill
        {
            get
            {
                int best = 0;
                for (int i = 0; i < WeaponSkillCount; i++)
                {
                    if (_values[i] > best) best = _values[i];
                }
                return best;
            }
        }

        /// <summary>
        /// The six weapon skills, highest first. Equal values keep enum order,
        /// so the result is deterministic across runs.
        /// </summary>
        public SkillKind[] CombatSkillsDescending()
        {
            SkillKind[] order = new SkillKind[WeaponSkillCount];
            for (int i = 0; i < WeaponSkillCount; i++) order[i] = (SkillKind)i;

            // Insertion sort: stable, and six elements never justify anything cleverer.
            for (int i = 1; i < WeaponSkillCount; i++)
            {
                SkillKind current = order[i];
                int currentValue = _values[(int)current];
                int j = i - 1;
                while (j >= 0 && _values[(int)order[j]] < currentValue)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = current;
            }
            return order;
        }

        /// <summary>True when Riding ranks first or second among all seven skills.</summary>
        public bool RidingInTopTwo()
        {
            int riding = _values[(int)SkillKind.Riding];
            int strictlyBetter = 0;
            for (int i = 0; i < WeaponSkillCount; i++)
            {
                if (_values[i] > riding) strictlyBetter++;
            }
            return strictlyBetter <= 1;
        }
    }
}
