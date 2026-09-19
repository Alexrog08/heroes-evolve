using System;

namespace HeroesEvolve.Core
{
    /// <summary>
    /// Whether a lord may buy or be granted gear a particular module shipped.
    ///
    /// The problem this answers came from a player running Ben's Ultimate
    /// Armory: "just a few hours after installing this mod, I see all the lords
    /// and ladies run around using armor from that mod". Nothing is broken in
    /// that report. An armour pack adds hundreds of pieces at the top of the
    /// tier scale, a lord buys the best piece he can afford, and the market
    /// rules do exactly what they are supposed to. The culture preference does
    /// not resist it either: MarketRules pays a quarter of a piece for being a
    /// lord's own people's work, and a piece belonging to no culture is not
    /// foreign to anybody, so a neutral piece two tiers better wins in every
    /// culture at once. A census of a vanilla campaign already shows the size
    /// of that channel before any mod widens it -- 91 of 332 purchases were
    /// pieces of no culture, and about half of what a town has on its shelves
    /// belongs to no culture at all.
    ///
    /// The exclusion list that existed before this is per item id, which the
    /// same player rightly called impractical: "it's really time consuming to
    /// list all the items one by one". A pack has hundreds.
    ///
    /// So the unit here is the module. One name keeps a whole pack out, and the
    /// player chooses which -- a mod they installed for the look of it stays,
    /// and one that eats the balance goes. What was asked for instead was that
    /// the mod recognise "BUA or other famous armor and weapon mods" by name,
    /// and that is the one shape this must not take: a list of names in the
    /// source breaks the day an author renames a mod, never covers the next one
    /// released, and makes this mod the judge of which mods are famous.
    ///
    /// Empty by default. Nothing is refused unless the player names it.
    /// </summary>
    public static class ModuleRules
    {
        /// <summary>
        /// Whether anything is excluded at all. The hot path asks this first
        /// because the answer is almost always no, and the scan that maps items
        /// to modules is only worth paying for when it is yes.
        /// </summary>
        public static bool Any(string[] excludedModules)
        {
            return excludedModules != null && excludedModules.Length > 0;
        }

        /// <summary>
        /// Whether this module's gear is refused.
        ///
        /// An item whose module is unknown is allowed, which is a decision and
        /// not an oversight. Counted over a stock installation, 2,321 item ids
        /// are declared in the modules' own XML while the catalogue holds 3,500:
        /// the other third is made at runtime -- every smithed blade among them
        /// -- and belongs to no file this can read. Refusing what cannot be
        /// placed would quietly delete a third of the world's gear the moment a
        /// player excluded anything at all, and he would have no way to tell
        /// why. Refusing only what can be placed costs the opposite: a mod that
        /// adds its items from code rather than from XML cannot be excluded this
        /// way. That is the cheaper mistake, and it is visible -- the census
        /// prints what each module declares, so a module that declares nothing
        /// says so plainly.
        ///
        /// Matched case-insensitively against the module id, which is the
        /// folder name under Modules and what the census prints.
        /// </summary>
        public static bool Refuses(string moduleId, string[] excludedModules)
        {
            if (string.IsNullOrEmpty(moduleId)) return false;
            if (!Any(excludedModules)) return false;

            for (int i = 0; i < excludedModules.Length; i++)
            {
                if (string.Equals(excludedModules[i], moduleId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
