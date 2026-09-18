namespace HeroesEvolve.Core
{
    /// <summary>
    /// The talent the sheet TaleWorlds wrote for a character insists on.
    ///
    /// A sheet is not a curve. TaleWorlds wrote a lord's skills once and in the
    /// base game they never move, so a figure there is not where he stands at
    /// his age: it is what the man was written to be worth. This mod takes his
    /// years back off that sheet so he grows into his strength instead of
    /// opening the campaign with it -- and that is only fair if the sheet is a
    /// promise rather than a loan. So it is one. Whatever the dice dealt him,
    /// his sheet is a floor under his talent: he climbs back onto it in his
    /// fifties and stands Surplus above it once he is grown. A lord written
    /// with 250 in a lance is never left permanently lesser than that because
    /// the hash disliked his id.
    ///
    /// Measured against the norm of its own field (SkillGrowth.PeakNormFor),
    /// which is what talent multiplies, so a talent of best/norm peaks exactly
    /// on the sheet's best skill there. The sheet's own figure is the whole
    /// calibration.
    ///
    /// The first version read each field against the typical grown lord
    /// TaleWorlds wrote -- combat 180, civil 220 -- and put that in place of the
    /// dice. It placed the ten rulers on TaleWorlds' nobility scale and said
    /// nothing about anybody else: 428 lords share 74 sheets, mostly archetypes,
    /// and read as intent an archetype deals every lord on it the same gift, so
    /// all 74 were set aside. A floor needs no such care, because it only speaks
    /// when it stands above what the dice dealt. A rookie sheet asks for
    /// nothing; a knight's asks for what a knight is worth; and the dice still
    /// separate the twenty-two knights who share it. Over the roster TaleWorlds
    /// wrote, 284 of 484 lords stand on their sheet in some field and 200 are
    /// left entirely to the dice -- and where it speaks it lifts the unlucky
    /// tail and nothing else: the median lord still peaks at 143% of his written
    /// best in combat, while the tenth percentile goes from 82% to 105%.
    ///
    /// Only Ceiling limits it, and only where a sheet asks for a figure the
    /// game could never show.
    /// </summary>
    public static class AuthoredTalent
    {
        /// <summary>
        /// How far past his sheet a lord stands once he is grown, at
        /// SkillGrowth.MatureAge.
        ///
        /// Five percent. Forty years of war have to be worth something, or a
        /// lord's whole career leaves him exactly where the generator would have
        /// handed him over on the first day. Small on purpose: the sheet is the
        /// statement and this is only the interest on it. With the curve's own
        /// maturity the sheet itself is back at about fifty-five, and his dice
        /// may take him far past either figure.
        /// </summary>
        public const float Surplus = 0.05f;

        /// <summary>
        /// Which talent a skill on a sheet speaks for: Talent.Combat for the six
        /// weapons, Talent.Naval for the War Sails skills, and Talent.Civil for
        /// everything else, including skills other mods add, which growth already
        /// treats as civil. Riding and Athletics return null: they follow the
        /// weapon a hero carries and say nothing on their own.
        /// </summary>
        public static string DomainOf(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;

            switch (skillId)
            {
                case "OneHanded":
                case "TwoHanded":
                case "Polearm":
                case "Bow":
                case "Crossbow":
                case "Throwing":
                    return Talent.Combat;

                case "Riding":
                case "Athletics":
                    return null;

                case "Mariner":
                case "Boatswain":
                case "Shipmaster":
                    return Talent.Naval;

                default:
                    return Talent.Civil;
            }
        }

        /// <summary>
        /// The most talent a sheet can insist on in a field: what the game
        /// itself can show, GameSkillMaximum over that field's norm. A sheet
        /// written past that asks for a number no hero could display and the
        /// target would clamp to 330 regardless, so it is held here.
        ///
        /// Nothing TaleWorlds wrote comes near it, because every norm is his own
        /// ceiling in that field over Talent.Maximum: the strongest sheet in each
        /// field asks for a little over the most talent there is, and nothing
        /// else asks for as much.
        ///
        /// It was 1.95 until the sheets became a floor, deliberately below
        /// Talent.Maximum so that no written lord could stand beyond the reach of
        /// the dice: seven lords were dealt more than Caladog, and he finished
        /// near 292, three percent short of his own sheet. That was the single
        /// place where the sheet was not a promise, and the choice was made the
        /// other way. A sheet is honoured in full, Caladog's included, so he
        /// matures past his written 300 and is the one man in the world the dice
        /// cannot match. Every other written lord is still passed by the luckiest
        /// heroes, because no other sheet was written at the top of its field.
        /// </summary>
        public static float CeilingFor(int peakNorm)
        {
            if (peakNorm <= 0) return 0f;

            return SkillGrowth.GameSkillMaximum / (float)peakNorm;
        }

        /// <summary>
        /// The talent a sheet's best skill in one field insists on: enough for a
        /// grown lord to peak Surplus above it, against that field's norm. Zero
        /// where the sheet writes nothing there, so the dice decide that field
        /// alone, and never more than the ceiling.
        /// </summary>
        public static float Floor(int bestAuthoredSkill, int peakNorm)
        {
            if (bestAuthoredSkill <= 0 || peakNorm <= 0) return 0f;

            float talent = bestAuthoredSkill * (1f + Surplus) / peakNorm;
            float ceiling = CeilingFor(peakNorm);
            return talent < ceiling ? talent : ceiling;
        }

        /// <summary>
        /// What one written skill is worth: itself, at any age.
        ///
        /// The talent floor promises a man the best skill of each field, and
        /// leaves the other lines of his sheet to the mod's own rules, which cut
        /// them: a lord TaleWorlds wrote with 90 in trade and no focus there had
        /// it shaved to the fifty a single point of focus earns, and a census
        /// found the whole civil middle ten to thirty-five percent under what he
        /// wrote. This is the rest of the promise, one line at a time.
        ///
        /// No age term, which is the correction the second census forced. The
        /// first version carried each line along the maturity curve like
        /// everything else, and that ages the same man twice: TaleWorlds already
        /// wrote his sheet for his years. Measured over his roster, the median
        /// best weapon runs 110 at eighteen to twenty-four against 200 past
        /// fifty-five -- 55% -- so multiplying a 27-year-old's written 90 by the
        /// 0.65 his age earns left the floor at 61 and the curve cut him to it.
        /// The census showed it plainly: trade still at 65 against his 90,
        /// medicine 73 against 90.
        ///
        /// What a lord was written with is what he has, whatever his age. What
        /// he grows beyond it is decided by his talent, his weapons and his
        /// focus -- and his best skill in each field still ends Surplus above
        /// the sheet at MatureAge, because the talent floor puts it there.
        ///
        /// A floor and nothing else: it never lifts a line above the sheet.
        /// </summary>
        public static int WrittenFloor(int writtenSkill)
        {
            return writtenSkill > 0 ? writtenSkill : 0;
        }

        /// <summary>
        /// A hero's talent in one field: what the dice dealt him, never below
        /// what his sheet insists on. Nothing here ever lowers a hero, which is
        /// what makes a sheet safe to read for every lord TaleWorlds wrote
        /// rather than for ten of them.
        /// </summary>
        public static float AtLeastHisSheet(float dealt, int bestAuthoredSkill, int peakNorm)
        {
            float floor = Floor(bestAuthoredSkill, peakNorm);
            return dealt > floor ? dealt : floor;
        }
    }
}
