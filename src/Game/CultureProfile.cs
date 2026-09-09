using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// Whether a culture puts its lords on horses, read from the lords
    /// TaleWorlds authored for it.
    ///
    /// The game does not preserve this. Over sixty campaign years the lords it
    /// generates drift toward cavalry regardless of culture: Battania's authored
    /// lords are 2.5% mounted and its campaign-born ones 48%, and every one of
    /// the ten mounted Nord lords in the save was born in play, against
    /// twenty-one authored Nord lords of which not one rides. Nord fields no
    /// cavalry at all -- one of its 144 troops carries a mount, a caravan guard
    /// on a borrowed Khuzait horse -- so a mounted Nord lord leads an army with
    /// nothing to lead.
    ///
    /// The obvious source is wrong: the troop tree does not predict lord
    /// composition. Empire fields 28% mounted troops and mounts 97% of its
    /// lords; Battania fields 17% and mounts 2.5%. Nothing about how a culture
    /// arms its soldiers says how it mounts its nobles.
    ///
    /// (An earlier version of this comment claimed Nord's elite line is mounted
    /// because SandBoxCore's spcultures.xml points nord at sturgian_warrior_son.
    /// That is the base game's placeholder, overridden by NavalDLC at runtime:
    /// the live troop survey walks nord's elite line and finds nord_thegn
    /// through nord_huscarl, fifteen troops and every one on foot. The XML was
    /// read; the game was not.)
    ///
    /// What does hold is the authored lord roster itself, and it is close to
    /// binary: among the surviving authored lords of this save, Vlandia, Empire,
    /// Khuzait and Aserai mount 100%, Sturgia 95%, Battania and Nord 0%.
    ///
    /// Read from CharacterObject rather than from living heroes on purpose --
    /// the authored characters stay registered after their heroes die, so the
    /// profile is as stable in year 200 as in year 1.
    /// </summary>
    public static class CultureProfile
    {
        /// <summary>
        /// Below this share of mounted authored lords, a culture is treated as
        /// keeping its lords on foot. The observed values sit at 0-5% and
        /// 95-100%, so anything in the middle of that gap separates them; the
        /// margin either side is more than twenty points.
        /// </summary>
        private const float FootCultureCeiling = 0.25f;

        /// <summary>
        /// Cultures with fewer authored lords than this are not judged at all.
        /// A handful of characters cannot establish a profile, and the safe
        /// answer for an unknown culture is to change nothing.
        /// </summary>
        private const int MinimumSample = 4;

        private static Dictionary<string, float> _shares;
        private static Dictionary<string, int> _counts;

        /// <summary>Forces a recompute; call when a campaign is loaded.</summary>
        public static void Reset()
        {
            _shares = null;
            _counts = null;
        }

        /// <summary>
        /// False only when the culture's authored lords are demonstrably a foot
        /// roster. An unknown or thinly-sampled culture returns true, leaving
        /// the hero's own role label to decide.
        /// </summary>
        public static bool MountsItsLords(CultureObject culture)
        {
            if (culture == null) return true;

            Build();

            int count;
            if (!_counts.TryGetValue(culture.StringId, out count) || count < MinimumSample) return true;

            float share;
            if (!_shares.TryGetValue(culture.StringId, out share)) return true;

            return share >= FootCultureCeiling;
        }

        private static void Build()
        {
            if (_shares != null) return;

            _shares = new Dictionary<string, float>();
            _counts = new Dictionary<string, int>();

            Dictionary<string, int> mounted = new Dictionary<string, int>();

            foreach (CharacterObject character in CharacterObject.All)
            {
                try
                {
                    if (character == null || !character.IsHero) continue;
                    if (character.Occupation != Occupation.Lord) continue;

                    // Authored characters only. Heroes the campaign generates are
                    // registered as CharacterObject_NNNN and are exactly the
                    // population whose drift this profile exists to correct, so
                    // counting them would define the problem away.
                    if (character.StringId == null || !character.StringId.StartsWith("lord_")) continue;

                    CultureObject culture = character.Culture;
                    if (culture == null || culture.StringId == null) continue;

                    string key = culture.StringId;

                    int n;
                    _counts.TryGetValue(key, out n);
                    _counts[key] = n + 1;

                    if (!IsMountedFormation(character.DefaultFormationClass)) continue;

                    int m;
                    mounted.TryGetValue(key, out m);
                    mounted[key] = m + 1;
                }
                catch
                {
                    // One unreadable character must not cost the profile.
                }
            }

            foreach (KeyValuePair<string, int> pair in _counts)
            {
                int m;
                mounted.TryGetValue(pair.Key, out m);
                _shares[pair.Key] = pair.Value == 0 ? 1f : (float)m / pair.Value;
            }
        }

        private static bool IsMountedFormation(FormationClass formation)
        {
            return formation == FormationClass.Cavalry
                   || formation == FormationClass.LightCavalry
                   || formation == FormationClass.HeavyCavalry
                   || formation == FormationClass.HorseArcher;
        }

        /// <summary>Writes the computed profile to the log for inspection.</summary>
        public static void Report()
        {
            Build();

            foreach (KeyValuePair<string, int> pair in _counts)
            {
                float share;
                _shares.TryGetValue(pair.Key, out share);

                StringBuilder line = new StringBuilder("CULTURE ");
                line.Append(pair.Key)
                    .Append(" authoredLords=").Append(pair.Value)
                    .Append(" mountedShare=").Append((int)(share * 100)).Append('%')
                    .Append(" mountsItsLords=")
                    .Append(pair.Value < MinimumSample ? "unknown(thin sample)"
                            : (share >= FootCultureCeiling ? "yes" : "NO"));

                ModLog.Info(line.ToString());
            }

            ReportCorrections();
        }

        /// <summary>
        /// How many living lords the culture rule actually dismounts.
        ///
        /// Sampled dry-run lines cannot show this: the sampler takes two heroes
        /// per culture and may well pick ones the game already labelled on foot,
        /// which proves nothing. A count of heroes whose mounted label was
        /// overridden is the direct evidence.
        /// </summary>
        private static void ReportCorrections()
        {
            Dictionary<string, int> corrected = new Dictionary<string, int>();
            Dictionary<string, int> mountedLabels = new Dictionary<string, int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;

                    CultureObject culture = hero.Culture;
                    if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;
                    if (culture == null || culture.StringId == null) continue;

                    if (!Core.BattleRoleRules.IsMounted(HeroAdapter.ReadRole(hero))) continue;

                    int labelled;
                    mountedLabels.TryGetValue(culture.StringId, out labelled);
                    mountedLabels[culture.StringId] = labelled + 1;

                    if (MountsItsLords(culture)) continue;

                    int n;
                    corrected.TryGetValue(culture.StringId, out n);
                    corrected[culture.StringId] = n + 1;
                }
                catch
                {
                    // One unreadable hero must not cost the count.
                }
            }

            foreach (KeyValuePair<string, int> pair in mountedLabels)
            {
                int n;
                corrected.TryGetValue(pair.Key, out n);
                ModLog.Info("CULTURE correction " + pair.Key
                            + " mountedLabels=" + pair.Value
                            + " dismountedByRule=" + n);
            }
        }
    }
}
