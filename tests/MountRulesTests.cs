using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class MountRulesTests
    {
        public static void RunAll()
        {
            // The five the game's own item_usage_sets.xml flags requires_no_mount,
            // counting long_bow's inheritance from bow.
            Check.True(MountRules.IsDismountedOnly("long_bow"), "a long bow needs the ground");
            Check.True(MountRules.IsDismountedOnly("polearm_pike"), "a pike needs the ground");
            Check.True(MountRules.IsDismountedOnly("polearm_bracing"), "a braced spear needs the ground");
            Check.True(MountRules.IsDismountedOnly("polearm_thrown"), "a thrown polearm needs the ground");
            Check.True(MountRules.IsDismountedOnly("onehanded_shield_dagger"), "shield and dagger needs the ground");

            // The near misses. Plain bow does NOT carry the flag -- only long_bow
            // does, through base_set -- and the couched lance is cavalry's own
            // weapon. Banning either would be worse than the bug being fixed.
            Check.False(MountRules.IsDismountedOnly("bow"), "an ordinary bow is fine mounted");
            Check.False(MountRules.IsDismountedOnly("polearm_couch"), "a couched lance is FOR horseback");
            Check.False(MountRules.IsDismountedOnly("throwing_javelin"), "javelins are fine mounted");

            // crossbow_light has an is_mounted=False usage, which looks damning
            // and is not: that is the reload animation, inherited from crossbow.
            // The set carries no requires_no_mount flag. Reading is_mounted
            // instead of the flag would have banned every light crossbow.
            Check.False(MountRules.IsDismountedOnly("crossbow_light"), "a light crossbow is not barred");
            Check.False(MountRules.IsDismountedOnly("crossbow_fast"), "a fast crossbow is not barred");

            Check.False(MountRules.IsDismountedOnly(null), "a null usage bars nothing");
            Check.False(MountRules.IsDismountedOnly(""), "an empty usage bars nothing");

            // One mode is enough. This is the case that makes the rule ANY-usable
            // rather than NONE-forbidden: the game's Javelin template produces an
            // item that is a thrown weapon AND a short spear.
            Check.True(MountRules.AllowsMounted(new string[] { "throwing_javelin", "polearm_thrown" }),
                       "a javelin he can throw mounted is his to carry");
            Check.True(MountRules.AllowsMounted(new string[] { "polearm_couch", "polearm_bracing" }),
                       "a lance that can also be braced is still a lance");

            // And a weapon with no mounted mode at all is refused.
            Check.False(MountRules.AllowsMounted(new string[] { "long_bow" }),
                        "a long bow alone is refused a rider");
            Check.False(MountRules.AllowsMounted(new string[] { "polearm_pike", "polearm_bracing" }),
                        "a pike that can only be braced is refused a rider");

            // Nothing known must never mean nothing allowed.
            Check.True(MountRules.AllowsMounted(null), "an unreadable weapon is not refused");
            Check.True(MountRules.AllowsMounted(new string[0]), "a weapon with no usages is not refused");
            Check.True(MountRules.AllowsMounted(new string[] { "" }), "an unnamed usage is not refused");
            Check.True(MountRules.AllowsMounted(new string[] { null }), "a null usage is not refused");

            // The mirror. polearm_couch is the one set the file flags
            // requires_mount, and a lance that also thrusts stays usable on foot.
            Check.True(MountRules.IsMountedOnly("polearm_couch"), "a couch needs a horse under it");
            Check.False(MountRules.IsMountedOnly("polearm_pike"), "a pike needs no horse");
            Check.False(MountRules.IsMountedOnly(null), "a null usage requires nothing");

            Check.False(MountRules.AllowsOnFoot(new string[] { "polearm_couch" }),
                        "a weapon that can only be couched is no use on foot");
            Check.True(MountRules.AllowsOnFoot(new string[] { "polearm_couch", "polearm_block_thrust" }),
                       "a lance that also thrusts is fine on foot");
            Check.True(MountRules.AllowsOnFoot(null), "an unreadable weapon is not refused on foot");
            Check.True(MountRules.AllowsOnFoot(new string[0]), "no usages is not refused on foot");

            // The two rules must not contradict each other on ordinary gear.
            string[] plain = new string[] { "onehanded_block_shield_swing_thrust" };
            Check.True(MountRules.AllowsMounted(plain) && MountRules.AllowsOnFoot(plain),
                       "an ordinary sword serves a rider and a footman alike");
        }
    }
}
