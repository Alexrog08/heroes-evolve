using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace HeroesEvolve
{
    /// <summary>
    /// The console lines that stage the three robbery tests, written out with
    /// this campaign's own names. Reads only.
    ///
    /// The tests want a lord of a house at war with the player, a second lord
    /// of the same house who is leading a party, somebody of the player's own
    /// who can be taken, and a lord of some other house. Finding those by
    /// hand means the encyclopedia and a guess at who has a party this week,
    /// and the game's capture cheat then refuses anybody who is not free, not
    /// at war or in a battle, and stops on a name that sits inside another
    /// man's. This asks the questions the cheat will ask, so the lines it
    /// prints are lines the cheat accepts.
    ///
    /// It reads the oaths too, so that it can be asked again halfway. Once a
    /// house has sworn against the player's, that is the house whose lord
    /// the second test names, and a house the player's is already owed by is
    /// the one the third test collects from.
    ///
    /// The staging itself is left to the game. campaign.add_prisoner_to_party
    /// goes through TakePrisonerAction, which is the capture this mod listens
    /// for, so what follows is the real robbery and not a rehearsal of it.
    /// </summary>
    public static class RobberyTestPlan
    {
        public static string Describe()
        {
            Hero me = Hero.MainHero;
            Clan house = Clan.PlayerClan;
            if (me == null || house == null) return "hev: no player.";

            // The cheat wants a capturer who is free and has a party.
            if (!me.IsActive || me.PartyBelongedTo == null)
            {
                return "hev: be free and with your party before staging these.";
            }

            IFaction mine = me.MapFaction;
            List<string> names = EveryName();

            int sworn, leftAlone;
            Dictionary<string, int> oaths = VengeanceOaths.Outstanding(out sworn, out leftAlone);

            // Free lords at war with the player whom this mod would strip.
            // The ones a name is enough to find, while there are any: the
            // mod's own commands take a name and nothing else.
            List<Hero> foes = new List<Hero>();
            List<Hero> namesakes = new List<Hero>();
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (hero == null || hero.Clan == null || hero.Clan == house) continue;
                    if (!hero.IsLord || !AtWar(hero, mine)) continue;
                    if (!CanBeTaken(hero) || !PlunderService.CanBeStripped(hero)) continue;

                    if (Ref(hero, names) == hero.StringId) namesakes.Add(hero);
                    else foes.Add(hero);
                }
                catch
                {
                    // One unreadable hero must not cost the plan.
                }
            }

            if (foes.Count == 0) foes = namesakes;
            if (foes.Count == 0) return NoWar(mine);

            // The man to rob, and a kinsman of his at the head of a party to
            // collect for it. Not of a house the player is owed by, or the
            // demand would be vengeance. A man with honour first, so that it
            // is the plain one, and one who leads nothing, because the cheat
            // disbands the party of a leader it takes.
            Hero victim = null, collector = null;
            int best = -1;

            for (int i = 0; i < foes.Count; i++)
            {
                Hero candidate = foes[i];
                if (oaths.ContainsKey(VengeanceOaths.Key(house, candidate.Clan))) continue;

                Hero kinsman = CollectorFor(candidate.Clan, candidate, mine);

                int score = (kinsman != null ? 4 : 0)
                            + (candidate.GetTraitLevel(DefaultTraits.Honor) >= 0 ? 2 : 0)
                            + (candidate.PartyBelongedTo == null ? 1 : 0);
                if (score <= best) continue;

                best = score;
                victim = candidate;
                collector = kinsman;
            }

            // A house that has already sworn against the player's is the one
            // to collect, whoever the first test would name now.
            bool alreadySworn = false;
            foreach (Clan clan in Clan.All)
            {
                if (clan == null || clan == house) continue;
                if (!oaths.ContainsKey(VengeanceOaths.Key(clan, house))) continue;

                Hero holder = CollectorFor(clan, null, mine);
                if (holder == null) continue;

                collector = holder;
                alreadySworn = true;
                break;
            }

            // A house to be owed by: one the player's is owed by already, or
            // failing that any but the first man's, so that the third test
            // does not answer the first.
            Hero debtor = null;
            bool alreadyOwed = false;
            for (int i = 0; i < foes.Count; i++)
            {
                if (!oaths.ContainsKey(VengeanceOaths.Key(house, foes[i].Clan))) continue;
                debtor = foes[i];
                alreadyOwed = true;
                break;
            }

            for (int i = 0; debtor == null && i < foes.Count; i++)
            {
                if (victim != null && foes[i].Clan == victim.Clan) continue;
                if (foes[i] == victim) continue;
                debtor = foes[i];
            }

            Hero mineToLose = OneOfMine(me);

            string player = Ref(me, names);
            List<string> lines = new List<string>();

            lines.Add("staged robbery tests for this campaign. Reads only: nothing has moved yet.");

            if (victim != null)
            {
                lines.Add("1) You rob " + victim.Name + " (" + victim.Clan.Name + ", Honor "
                          + victim.GetTraitLevel(DefaultTraits.Honor) + "):");
                lines.Add("   campaign.add_prisoner_to_party " + Ref(victim, names) + " | " + player);
                lines.Add("   then talk to your prisoner and demand the arms.");
            }
            else
            {
                lines.Add("1) Not possible now: your house is owed by every lord you could take.");
            }

            if (collector != null && mineToLose != null)
            {
                lines.Add("2) House " + collector.Clan.Name + " collects from yours"
                          + (alreadySworn ? " (its oath is sworn)" : " (once test 1 is done)")
                          + ":");
                lines.Add("   campaign.add_prisoner_to_party " + Ref(mineToLose, names)
                          + " | " + Ref(collector, names));
            }
            else if (collector == null)
            {
                lines.Add("2) Not possible now: no house at war with you has both a lord to rob"
                          + " and another leading a party.");
            }
            else
            {
                lines.Add("2) Not possible now: nobody of your clan can be taken"
                          + " (a free companion or kinsman wearing more than rags).");
            }

            if (debtor != null)
            {
                lines.Add("3) You collect a debt (" + debtor.Clan.Name + "):");
                if (alreadyOwed) lines.Add("   (your house already holds an oath against them)");
                else lines.Add("   hev.test_oath " + debtor.Name);
                lines.Add("   campaign.add_prisoner_to_party " + Ref(debtor, names) + " | " + player);
                lines.Add("   then talk to your prisoner: the demand should be the vengeance one.");
            }
            else
            {
                lines.Add("3) Not possible now: no lord of a second house can be taken.");
            }

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                ModLog.Info("TESTPLAN " + lines[i]);
                text.Append(i == 0 ? "hev: " : "").Append(lines[i]).Append('\n');
            }

            return text.ToString();
        }

        /// <summary>
        /// What the capture cheat asks of a prisoner -- free, a lord or a
        /// wanderer, not in a battle -- and what this mod asks before it
        /// bothers: something on him worth taking.
        /// </summary>
        private static bool CanBeTaken(Hero hero)
        {
            if (hero == null || hero == Hero.MainHero || !hero.IsActive) return false;
            if (!hero.IsLord && !hero.IsWanderer) return false;
            if (hero.IsChild || hero.BattleEquipment == null) return false;

            MobileParty party = hero.PartyBelongedTo;
            if (party != null && party.MapEvent != null) return false;

            return PlunderService.HasAnythingToTake(hero);
        }

        /// <summary>
        /// A lord of this house who is free, at war with the player and at the
        /// head of a party of his own: the capturer the cheat accepts, and the
        /// man whose oath the robbery will be judged on.
        /// </summary>
        private static Hero CollectorFor(Clan clan, Hero except, IFaction mine)
        {
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero == except || hero.Clan != clan) continue;
                if (!hero.IsActive || !AtWar(hero, mine)) continue;

                MobileParty party = hero.PartyBelongedTo;
                if (party == null || party.LeaderHero != hero || party.MapEvent != null) continue;

                return hero;
            }

            return null;
        }

        /// <summary>
        /// Somebody of the player's clan the cheat will take and the mod will
        /// strip. One riding with the player first, since the cheat disbands
        /// the party of a leader it takes.
        ///
        /// Not PlunderService.CanBeStripped: that refuses anybody inside the
        /// player's own party, which is where this man is now and where he no
        /// longer is by the time the capture is announced.
        /// </summary>
        private static Hero OneOfMine(Hero me)
        {
            Hero fallback = null;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (hero == null || hero == me || hero.Clan != Clan.PlayerClan) continue;
                    if (!hero.IsLord && hero.CompanionOf == null) continue;
                    if (!CanBeTaken(hero)) continue;

                    if (hero.PartyBelongedTo == me.PartyBelongedTo) return hero;
                    if (fallback == null) fallback = hero;
                }
                catch
                {
                    // One unreadable hero must not cost the plan.
                }
            }

            return fallback;
        }

        private static bool AtWar(Hero hero, IFaction mine)
        {
            IFaction theirs = hero.MapFaction;
            return mine != null && theirs != null && theirs != mine
                   && FactionManager.IsAtWarAgainstFaction(theirs, mine);
        }

        /// <summary>
        /// How to name a hero to the game's cheats: by his name when no other
        /// hero's name contains it, by his id when one does. The cheats match
        /// loosely and stop at the first ambiguity, and an id always matches
        /// exactly one.
        /// </summary>
        private static string Ref(Hero hero, List<string> names)
        {
            string name = hero.Name != null ? hero.Name.ToString() : "";
            if (name.Length == 0 || name.IndexOf('|') >= 0) return hero.StringId;

            int holders = 0;
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0) holders++;
            }

            // Once is himself.
            return holders == 1 ? name : hero.StringId;
        }

        /// <summary>Every hero's name, the dead included: the cheats look among them too.</summary>
        private static List<string> EveryName()
        {
            List<string> names = new List<string>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (hero != null && hero.Name != null) names.Add(hero.Name.ToString());
            }

            foreach (Hero hero in Hero.DeadOrDisabledHeroes)
            {
                if (hero != null && hero.Name != null) names.Add(hero.Name.ToString());
            }

            return names;
        }

        /// <summary>Nobody to take: says so, and how to start a war with the game's own cheat.</summary>
        private static string NoWar(IFaction mine)
        {
            Kingdom target = null;
            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated || kingdom == mine) continue;
                target = kingdom;
                break;
            }

            string text = "hev: no free lord at war with you can be stripped right now.";
            if (mine != null && target != null)
            {
                text += "\nStart a war with the game's own cheat and ask again:"
                        + "\n   campaign.declare_war " + mine.Name + " | " + target.Name;
            }

            return text;
        }
    }
}
