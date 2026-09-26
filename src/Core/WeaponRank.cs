using System.Collections.Generic;

namespace HeroesEvolve.Core
{
    /// <summary>
    /// The order in which a hero's weapons take their shares of his growth
    /// (SkillGrowth.TargetForRank): the weapon he has put the most focus into
    /// first, and slot order wherever focus does not decide.
    ///
    /// The rule used to be slot order alone, on the grounds that the game reads
    /// it: CharacterHelper.GetDefaultWeapon walks slots 0 to 4, and a kill in a
    /// simulated battle trains the skill of the first real weapon it finds.
    /// That holds for autoresolve and for nothing else. In a fought battle an
    /// agent draws whatever suits the moment, so the first slot says little
    /// about what a lord actually uses. And it was a lever nobody could see:
    /// moving a spear up a slot changed which weapon a companion grew into,
    /// with nothing on any screen to say so.
    ///
    /// Focus is the game's own statement of what a hero is for, and it already
    /// governs how fast he learns. Hero.AddSkillXp multiplies every grant by
    /// CalculateLearningRate, 1.25 x (1 + 0.4 x attribute + focus), and past
    /// the learning limit, 10 x (attribute - 1) + 30 x focus, that rate falls
    /// away to nothing: with an attribute of 5, a weapon without focus stops
    /// learning at about 60 whatever target it is set. Ranked by slot, the
    /// largest share could go to a weapon the game would not let him learn
    /// while the one he had invested in waited behind it. Ranked by focus, the
    /// target and the learning rate point at the same weapon.
    ///
    /// Who spends the focus is the only difference between heroes. The AI
    /// spends a lord's every day (DefaultCharacterDevelopmentModel.
    /// GetNextSkillToAddFocus: each point to the skill furthest past its
    /// learning limit), and the player's clan's only when he has asked it to
    /// (CampaignOptions.AutoAllocateClanMemberPerks). So for his brother or a
    /// companion, where he puts the points decides which weapon grows fastest.
    ///
    /// Ties keep slot order rather than growing alike. Focus stops at five, so
    /// a grown lord often holds the same in his two best weapons, and growing
    /// those alike means both at the full target: equal skills draw equal focus
    /// from the AI, the tie never breaks, and over a long campaign lords finish
    /// elite in two or three weapons where TaleWorlds wrote the second at 80% of
    /// the first. Splitting the difference instead keeps the total but puts his
    /// best weapon below its curve, and his gear ceiling with it. Kept in slot
    /// order, the shares pull the two apart, the AI invests in the one ahead,
    /// and focus decides from there. A hero with no focus in any weapon he
    /// carries -- one the game has not spent for yet -- ranks exactly as before.
    /// </summary>
    public static class WeaponRank
    {
        /// <summary>
        /// Positions into a slot-ordered list of a hero's weapon skills, most
        /// focus first. Equal focus keeps slot order.
        /// </summary>
        public static int[] ByFocus(IList<int> focusBySlot)
        {
            if (focusBySlot == null) return new int[0];

            int count = focusBySlot.Count;
            int[] order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;

            // Insertion sort: stable, which is the tie rule, and four weapons
            // never justify anything cleverer.
            for (int i = 1; i < count; i++)
            {
                int current = order[i];
                int focus = focusBySlot[current];
                int j = i - 1;
                while (j >= 0 && focusBySlot[order[j]] < focus)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = current;
            }
            return order;
        }

        /// <summary>
        /// What focus did to the order of the weapons of the heroes counted.
        /// The census reads it: every figure is against slot order, which is
        /// what growth used before.
        /// </summary>
        public sealed class Tally
        {
            /// <summary>Heroes carrying two or more weapon skills -- the only ones order can matter to.</summary>
            public int MultiWeapon;

            /// <summary>Of those, heroes whose primary is not the weapon in their first slot.</summary>
            public int PrimaryByFocus;

            /// <summary>Of those, heroes whose order differs from slot order anywhere, primary or below.</summary>
            public int ReorderedByFocus;

            /// <summary>Of those, heroes whose top two weapons hold equal focus, so the slot chose the primary.</summary>
            public int TiedToSlot;

            /// <summary>Of the tied, heroes with no focus in any weapon they carry.</summary>
            public int NoFocus;

            /// <summary>Counts one hero, his focus listed in the order he carries his weapon skills.</summary>
            public void Add(IList<int> focusBySlot)
            {
                if (focusBySlot == null || focusBySlot.Count < 2) return;

                int[] order = ByFocus(focusBySlot);
                MultiWeapon++;

                if (order[0] != 0) PrimaryByFocus++;

                for (int i = 0; i < order.Length; i++)
                {
                    if (order[i] != i) { ReorderedByFocus++; break; }
                }

                int top = focusBySlot[order[0]];
                if (focusBySlot[order[1]] == top)
                {
                    TiedToSlot++;
                    if (top <= 0) NoFocus++;
                }
            }

            public string Describe()
            {
                return "multiWeapon=" + MultiWeapon
                       + " primaryByFocus=" + PrimaryByFocus
                       + " reorderedByFocus=" + ReorderedByFocus
                       + " tiedToSlot=" + TiedToSlot
                       + " noFocus=" + NoFocus;
            }
        }
    }
}
