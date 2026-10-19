using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace HeroesEvolve.Api
{
    /// <summary>
    /// A listener that subscribes to the public events the way an outside mod
    /// does, and writes everything it is handed to hev.log.
    ///
    /// The way an outside mod does, because that is the part worth testing.
    /// The bridge that asked for these events references nothing of this
    /// mod: it finds the class by its name, finds each event on it, and binds
    /// a method that takes object. A unit test shows the runtime allows that
    /// binding; this shows it in the game, against the real events, with the
    /// real arguments -- which is the only place the arguments can be read.
    /// Switched on and off from the console (hev.api_probe) on a test save.
    /// </summary>
    internal static class ApiProbe
    {
        private static readonly List<KeyValuePair<EventInfo, Delegate>> _bound =
            new List<KeyValuePair<EventInfo, Delegate>>();

        /// <summary>Subscribes to every event, or unsubscribes if it already has.</summary>
        public static string Toggle()
        {
            if (_bound.Count > 0)
            {
                for (int i = 0; i < _bound.Count; i++) _bound[i].Key.RemoveEventHandler(null, _bound[i].Value);
                _bound.Clear();
                ModLog.Info("APIPROBE off");
                return "off";
            }

            // By name, as from another assembly. A typo here would be the same
            // typo a bridge could make, so it is worth failing on.
            Type events = Type.GetType("HeroesEvolve.Api.HeroesEvolveEvents, HeroesEvolve");
            if (events == null) return "failed: HeroesEvolve.Api.HeroesEvolveEvents not found by name";

            MethodInfo heard = typeof(ApiProbe).GetMethod("Heard", BindingFlags.NonPublic | BindingFlags.Static);

            foreach (EventInfo evt in events.GetEvents(BindingFlags.Public | BindingFlags.Static))
            {
                Delegate handler = Delegate.CreateDelegate(evt.EventHandlerType, heard);
                evt.AddEventHandler(null, handler);
                _bound.Add(new KeyValuePair<EventInfo, Delegate>(evt, handler));
            }

            FieldInfo version = events.GetField("ApiVersion", BindingFlags.Public | BindingFlags.Static);
            ModLog.Info("APIPROBE on: " + _bound.Count + " events, ApiVersion="
                        + (version != null ? version.GetValue(null) : "?"));
            return "on (" + _bound.Count + " events)";
        }

        private static void Heard(object args)
        {
            ModLog.Info("APIPROBE " + (args != null ? args.GetType().Name : "null") + " " + Describe(args));
        }

        /// <summary>Every public property by name, read the way a bridge reads them.</summary>
        private static string Describe(object args)
        {
            if (args == null) return "null";

            StringBuilder text = new StringBuilder();
            foreach (PropertyInfo property in args.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (text.Length > 0) text.Append(' ');
                text.Append(property.Name).Append('=').Append(Show(property.GetValue(args, null)));
            }

            return text.ToString();
        }

        private static string Show(object value)
        {
            if (value == null) return "null";

            Hero hero = value as Hero;
            if (hero != null) return hero.Name != null ? hero.Name.ToString() : hero.StringId;

            Settlement settlement = value as Settlement;
            if (settlement != null) return settlement.Name != null ? settlement.Name.ToString() : settlement.StringId;

            Clan clan = value as Clan;
            if (clan != null) return clan.Name != null ? clan.Name.ToString() : clan.StringId;

            PartyBase party = value as PartyBase;
            if (party != null) return party.Name != null ? party.Name.ToString() : "party";

            ItemObject item = value as ItemObject;
            if (item != null) return item.StringId;

            if (value is string) return (string)value;

            IEnumerable list = value as IEnumerable;
            if (list != null)
            {
                StringBuilder text = new StringBuilder("[");
                foreach (object entry in list)
                {
                    if (text.Length > 1) text.Append(", ");
                    text.Append('{').Append(Describe(entry)).Append('}');
                }
                return text.Append(']').ToString();
            }

            return value.ToString();
        }
    }
}
