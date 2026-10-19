using System;
using System.Collections.Generic;
using System.Text;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using HeroesEvolve.Core;

namespace HeroesEvolve.Api
{
    /// <summary>
    /// What Heroes Evolve does that the game does not report, told to any mod
    /// that wants to hear it.
    ///
    /// Written for a bridge to Calradia Remembers, whose author asked for it:
    /// a lord who was stripped should remember who stripped him, and nothing
    /// in the game records it -- this mod saves nothing, and a robbery happens
    /// in a moment and is gone. The game's own events cover what the game
    /// does; these cover the five things only this mod does.
    ///
    /// The contract, which is the reason this class exists at all:
    ///
    /// - The names here and on the argument classes do not change. A listener
    ///   finds them by reflection, without referencing this assembly, so it
    ///   can run with or without the mod installed; a rename would break it
    ///   silently. New data is added as new properties, never by changing an
    ///   event's signature, and ApiVersion goes up when anything is added.
    /// - Each event carries one argument object. A listener may subscribe a
    ///   method that takes object and read the properties by name: the
    ///   runtime binds it, because the event's argument type is more specific
    ///   than the method's.
    /// - Every event is raised after the thing it reports is complete: the
    ///   pieces are in the robber's baggage, the oath is in the log, the
    ///   purchase is paid for. Synchronously, so CampaignTime.Now is when.
    /// - Each subscriber is called on its own. One that throws costs only its
    ///   own notice; the others still hear, and the robbery stands. The
    ///   failure goes to hev.log.
    /// - The player is not special. A robbery by the player in conversation,
    ///   and one done to the player, are raised like any other.
    /// </summary>
    public static class HeroesEvolveEvents
    {
        /// <summary>
        /// Raised by one each time anything is added. A listener may refuse to
        /// run against a version it was not written for, or adapt to it.
        /// </summary>
        public const int ApiVersion = 1;

        private const string Robbed = "HeroRobbed";
        private const string Sworn = "OathSworn";
        private const string Collected = "OathCollected";
        private const string Worn = "StolenPieceWorn";
        private const string Bought = "GearBought";

        /// <summary>
        /// The most a single event's subscriber failures are written out in
        /// one session. A bridge that throws on every purchase would otherwise
        /// fill the log; past this they are only counted.
        /// </summary>
        private const int FailuresLogged = 5;

        private static Action<HeroRobbedArgs> _heroRobbed;
        private static Action<OathSwornArgs> _oathSworn;
        private static Action<OathCollectedArgs> _oathCollected;
        private static Action<StolenPieceWornArgs> _stolenPieceWorn;
        private static Action<GearBoughtArgs> _gearBought;

        private static readonly Dictionary<string, int> _fired = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> _failed = new Dictionary<string, int>();

        /// <summary>A prisoner was stripped of his kit, by a lord, a band or the player.</summary>
        public static event Action<HeroRobbedArgs> HeroRobbed
        {
            add { _heroRobbed += value; Subscribed(Robbed, value); }
            remove { _heroRobbed -= value; }
        }

        /// <summary>
        /// A robbed man swore vengeance on his robber. Raised during the
        /// robbery that caused it, just before its HeroRobbed.
        /// </summary>
        public static event Action<OathSwornArgs> OathSworn
        {
            add { _oathSworn += value; Subscribed(Sworn, value); }
            remove { _oathSworn -= value; }
        }

        /// <summary>
        /// A house collected an oath by stripping somebody of the house it was
        /// sworn against. Raised during that robbery, just before its HeroRobbed.
        /// </summary>
        public static event Action<OathCollectedArgs> OathCollected
        {
            add { _oathCollected += value; Subscribed(Collected, value); }
            remove { _oathCollected -= value; }
        }

        /// <summary>
        /// A lord put on a piece he had just taken off a prisoner. After that
        /// robbery's HeroRobbed. The player never does: what he takes goes to
        /// his inventory.
        /// </summary>
        public static event Action<StolenPieceWornArgs> StolenPieceWorn
        {
            add { _stolenPieceWorn += value; Subscribed(Worn, value); }
            remove { _stolenPieceWorn -= value; }
        }

        /// <summary>
        /// A hero bought a piece of battle gear in a town: a lord, a companion
        /// or a caravan master. Common -- many a day across the map.
        /// </summary>
        public static event Action<GearBoughtArgs> GearBought
        {
            add { _gearBought += value; Subscribed(Bought, value); }
            remove { _gearBought -= value; }
        }

        // ------------------------------------------------------------------
        // Raised from inside the mod. Each builds its argument only when
        // somebody is listening: a purchase happens hundreds of times a day.

        internal static void RaiseHeroRobbed(Hero robber, PartyBase robberParty, Hero victim,
                                             PlunderRules.Motive motive, bool bandit,
                                             List<StolenPiece> pieces, int totalValue,
                                             bool collectedOath, bool sworeOath)
        {
            Count(_fired, Robbed);
            Action<HeroRobbedArgs> handlers = _heroRobbed;
            if (handlers == null) return;

            HeroRobbedArgs args;
            try
            {
                args = new HeroRobbedArgs(robber, robberParty, victim,
                                          PlunderRules.MotiveName(motive, bandit),
                                          pieces, totalValue, NearestSettlement(robberParty),
                                          collectedOath, sworeOath);
            }
            catch (Exception ex)
            {
                NotBuilt(Robbed, ex);
                return;
            }

            Deliver(Robbed, handlers, args);
        }

        internal static void RaiseOathSworn(Hero swearer, Hero against)
        {
            Count(_fired, Sworn);
            Action<OathSwornArgs> handlers = _oathSworn;
            if (handlers == null) return;

            Deliver(Sworn, handlers, new OathSwornArgs(swearer, against));
        }

        internal static void RaiseOathCollected(Hero swearer, Hero against, Hero collector, Hero payer)
        {
            Count(_fired, Collected);
            Action<OathCollectedArgs> handlers = _oathCollected;
            if (handlers == null) return;

            Deliver(Collected, handlers, new OathCollectedArgs(swearer, against, collector, payer));
        }

        internal static void RaiseStolenPieceWorn(Hero wearer, ItemObject item, EquipmentIndex slot, Hero previousOwner)
        {
            Count(_fired, Worn);
            Action<StolenPieceWornArgs> handlers = _stolenPieceWorn;
            if (handlers == null) return;

            Deliver(Worn, handlers, new StolenPieceWornArgs(wearer, item, slot, previousOwner));
        }

        internal static void RaiseGearBought(Hero buyer, ItemObject item, EquipmentIndex slot,
                                             ItemObject replaced, int price, Settlement town)
        {
            Count(_fired, Bought);
            Action<GearBoughtArgs> handlers = _gearBought;
            if (handlers == null) return;

            GearBoughtArgs args;
            try
            {
                args = new GearBoughtArgs(buyer, item, slot, replaced, price, town,
                                          item != null ? MarketScanner.TierOf(item) : 0,
                                          replaced != null ? MarketScanner.TierOf(replaced) : 0);
            }
            catch (Exception ex)
            {
                NotBuilt(Bought, ex);
                return;
            }

            Deliver(Bought, handlers, args);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// The counts start again with each campaign load, like the rest of
        /// the census. The subscribers do not: a bridge subscribes once, when
        /// the game starts, and stays for every campaign of the sitting.
        /// </summary>
        internal static void ResetSession()
        {
            _fired.Clear();
            _failed.Clear();
        }

        /// <summary>
        /// One line for the census: who is listening, how often each event
        /// fired this session (whether or not anybody heard it), and how many
        /// times a subscriber threw. Fired counts with nobody listening are
        /// still worth reading -- they show the hooks working before any
        /// bridge is installed.
        /// </summary>
        internal static string Describe()
        {
            StringBuilder text = new StringBuilder("API version=" + ApiVersion + " subscribers");
            text.Append(' ').Append(Robbed).Append('=').Append(CountOf(_heroRobbed));
            text.Append(' ').Append(Sworn).Append('=').Append(CountOf(_oathSworn));
            text.Append(' ').Append(Collected).Append('=').Append(CountOf(_oathCollected));
            text.Append(' ').Append(Worn).Append('=').Append(CountOf(_stolenPieceWorn));
            text.Append(' ').Append(Bought).Append('=').Append(CountOf(_gearBought));

            text.Append(" | fired");
            foreach (string name in new[] { Robbed, Sworn, Collected, Worn, Bought })
            {
                int n;
                _fired.TryGetValue(name, out n);
                text.Append(' ').Append(name).Append('=').Append(n);
            }

            int failures = 0;
            foreach (KeyValuePair<string, int> pair in _failed) failures += pair.Value;
            text.Append(" | subscriberFailures=").Append(failures);

            return text.ToString();
        }

        /// <summary>
        /// Where a robbery happened, as a listener would name it: the keep he
        /// was robbed in, the town or village the robber's party is in, or the
        /// nearest town, castle or village to it. Hideouts are left out --
        /// "near the Sea Raiders' hideout" says nothing to a lord. Null when
        /// there is nowhere to name; a listener's convenience is never worth
        /// a robbery, so a failure here answers null too.
        /// </summary>
        internal static Settlement NearestSettlement(PartyBase party)
        {
            try
            {
                if (party == null) return null;
                if (party.IsSettlement) return party.Settlement;

                MobileParty mobile = party.MobileParty;
                if (mobile == null) return null;
                if (mobile.CurrentSettlement != null) return mobile.CurrentSettlement;

                CampaignVec2 at = mobile.Position;
                return SettlementHelper.FindNearestSettlementToPoint(at, s => s.IsTown || s.IsCastle || s.IsVillage);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// An argument that could not be put together. The thing it reports
        /// has already happened and stands; only the notice is lost, and the
        /// log says so. The two that read the game to build theirs are
        /// guarded with this; the rest only copy what they are handed.
        /// </summary>
        private static void NotBuilt(string name, Exception ex)
        {
            try
            {
                ModLog.Error("API " + name + " not raised: " + ex.GetType().Name + " " + ex.Message);
            }
            catch
            {
            }
        }

        private static void Deliver<T>(string name, Action<T> handlers, T args)
        {
            int failures = EventFan.Raise(handlers, args, (subscriber, ex) => Report(name, subscriber, ex));
            if (failures > 0) Count(_failed, name, failures);
        }

        private static void Report(string name, Delegate subscriber, Exception ex)
        {
            int before;
            _failed.TryGetValue(name, out before);
            if (before >= FailuresLogged) return;

            ModLog.Error("API " + name + " subscriber " + Who(subscriber) + " threw "
                         + ex.GetType().Name + ": " + ex.Message
                         + (before + 1 == FailuresLogged ? " (further failures are only counted)" : ""));
        }

        /// <summary>
        /// Written down when a mod subscribes, so a log shows who is listening
        /// before anything happens. Guarded: a subscription can arrive while
        /// the game is still starting, before anything else here is ready.
        /// </summary>
        private static void Subscribed(string name, Delegate subscriber)
        {
            try
            {
                if (subscriber == null) return;
                ModLog.Info("API " + name + " subscribed by " + Who(subscriber));
            }
            catch
            {
            }
        }

        private static string Who(Delegate subscriber)
        {
            if (subscriber == null || subscriber.Method == null) return "?";

            Type owner = subscriber.Method.DeclaringType;
            return (owner != null ? owner.FullName + "." : "") + subscriber.Method.Name
                   + (owner != null ? " (" + owner.Assembly.GetName().Name + ")" : "");
        }

        private static int CountOf(Delegate handlers)
        {
            return handlers != null ? handlers.GetInvocationList().Length : 0;
        }

        private static void Count(Dictionary<string, int> counts, string name, int by = 1)
        {
            int n;
            counts.TryGetValue(name, out n);
            counts[name] = n + by;
        }
    }
}
