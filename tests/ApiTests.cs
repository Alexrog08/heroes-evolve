using System;
using System.Collections.Generic;
using System.Reflection;
using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// The public events other mods subscribe to: that one subscriber cannot
    /// take another down, or this mod with it, and that a subscriber which
    /// never references this mod's assembly can still hear them.
    /// </summary>
    public static class ApiTests
    {
        public static void RunAll()
        {
            NobodyListening();
            EverySubscriberHears();
            OneFailureIsContained();
            AFailingReporterIsContained();
            ReflectionSubscriber();
            StaticReflectionSubscriber();
            MotiveNames();
        }

        private static void NobodyListening()
        {
            Check.Equal(0, EventFan.Raise<string>(null, "x", null), "no subscribers is no failures");
        }

        private static void EverySubscriberHears()
        {
            List<string> heard = new List<string>();
            Action<string> handlers = null;
            handlers += s => heard.Add("a:" + s);
            handlers += s => heard.Add("b:" + s);

            Check.Equal(0, EventFan.Raise(handlers, "robbed", null), "two that work fail nothing");
            Check.Equal(2, heard.Count, "both heard it");
            Check.Equal("a:robbed", heard[0], "in the order they subscribed");
            Check.Equal("b:robbed", heard[1], "and the second after the first");
        }

        private static void OneFailureIsContained()
        {
            // The guarantee a subscriber was promised: if its code throws, it
            // costs that subscriber its own notice and nothing else -- not the
            // next subscriber's, and not the robbery that raised it.
            List<string> heard = new List<string>();
            List<string> reported = new List<string>();

            Action<string> handlers = null;
            handlers += s => { throw new InvalidOperationException("bridge bug"); };
            handlers += s => heard.Add(s);

            int failures = EventFan.Raise(handlers, "robbed",
                                          (d, ex) => reported.Add(ex.Message));

            Check.Equal(1, failures, "the one that threw is counted");
            Check.Equal(1, heard.Count, "the one after it still heard");
            Check.Equal(1, reported.Count, "and the failure was reported once");
            Check.Equal("bridge bug", reported[0], "with its own exception");
        }

        private static void AFailingReporterIsContained()
        {
            Action<string> handlers = s => { throw new InvalidOperationException("bridge bug"); };

            bool threw = false;
            try
            {
                EventFan.Raise(handlers, "robbed", (d, ex) => { throw new Exception("log full"); });
            }
            catch
            {
                threw = true;
            }

            Check.False(threw, "a reporter that throws does not escape either");
        }

        private static void ReflectionSubscriber()
        {
            // The way the bridge subscribes: it never references this mod, so
            // it finds the event by name and hands it a method taking object.
            // The runtime allows that binding because the delegate's argument
            // type is more specific than the method's -- which is what lets the
            // payload be a class with properties rather than a long list of
            // parameters, and grow without breaking anybody.
            ReflectionListener listener = new ReflectionListener();

            EventInfo evt = typeof(ProbeEvents).GetEvent("Robbed");
            MethodInfo method = typeof(ReflectionListener).GetMethod("OnAny");
            Delegate bound = Delegate.CreateDelegate(evt.EventHandlerType, listener, method);
            evt.AddEventHandler(null, bound);

            ProbeEvents.Raise(new ProbeArgs("vengeance"));

            Check.True(listener.Heard != null, "a method taking object heard a typed event");
            PropertyInfo motive = listener.Heard != null ? listener.Heard.GetType().GetProperty("Motive") : null;
            Check.True(motive != null, "and found the property by name");
            Check.Equal("vengeance", motive != null ? (string)motive.GetValue(listener.Heard, null) : null,
                        "and read it");

            evt.RemoveEventHandler(null, bound);
        }

        private static void StaticReflectionSubscriber()
        {
            // The README's own example binds a static method; the same rule
            // must hold for it, or the first bridge to copy it fails.
            StaticListener.Heard = null;

            EventInfo evt = typeof(ProbeEvents).GetEvent("Robbed");
            MethodInfo method = typeof(StaticListener).GetMethod("OnAny");
            Delegate bound = Delegate.CreateDelegate(evt.EventHandlerType, method);
            evt.AddEventHandler(null, bound);

            ProbeEvents.Raise(new ProbeArgs("justice"));

            Check.True(StaticListener.Heard is ProbeArgs, "a static method taking object heard it too");
            evt.RemoveEventHandler(null, bound);
        }

        private static void MotiveNames()
        {
            // Text, not an enum: a subscriber reading by reflection would need
            // this mod's enum type to read an enum, and a name does not move if
            // the enum is ever reordered.
            Check.Equal("character", PlunderRules.MotiveName(PlunderRules.Motive.Character, false), "character");
            Check.Equal("justice", PlunderRules.MotiveName(PlunderRules.Motive.Justice, false), "justice");
            Check.Equal("vengeance", PlunderRules.MotiveName(PlunderRules.Motive.Vengeance, false), "vengeance");
            Check.Equal("bandits", PlunderRules.MotiveName(PlunderRules.Motive.Character, true),
                        "a band is a band whatever the draw said");
            Check.Equal("none", PlunderRules.MotiveName(PlunderRules.Motive.None, false), "no robbery is none");
        }

        public sealed class ProbeArgs
        {
            public ProbeArgs(string motive) { Motive = motive; }
            public string Motive { get; private set; }
        }

        public static class ProbeEvents
        {
            public static event Action<ProbeArgs> Robbed;

            public static void Raise(ProbeArgs args)
            {
                EventFan.Raise(Robbed, args, null);
            }
        }

        public static class StaticListener
        {
            public static object Heard;

            public static void OnAny(object args)
            {
                Heard = args;
            }
        }

        public sealed class ReflectionListener
        {
            public object Heard;

            public void OnAny(object args)
            {
                Heard = args;
            }
        }
    }
}
