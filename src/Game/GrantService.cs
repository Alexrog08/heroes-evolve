using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Repairs the vanilla come-of-age bug by handing a hero a coherent loadout
    /// for free. This is fixing a defect, not economy: it must work for a lord
    /// with no gold who never visits a town.
    /// </summary>
    public static class GrantService
    {
        /// <summary>The one-handed sword vanilla's dummy fallback hands out.</summary>
        internal const string DummySwordId = "iron_spatha_sword_t2";

        /// <summary>
        /// True when the hero's battle equipment is empty, or is the vanilla
        /// dummy set: a lone spatha and nothing else in the weapon slots.
        /// </summary>
        public static bool NeedsGrant(Hero hero)
        {
            if (hero == null || hero.BattleEquipment == null) return false;

            int weapons = 0;
            bool onlyDummySword = true;

            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                ItemObject item = hero.BattleEquipment[SlotMapping.WeaponSlot(i)].Item;
                if (item == null) continue;
                weapons++;
                if (item.StringId != DummySwordId) onlyDummySword = false;
            }

            if (weapons == 0) return true;
            return weapons == 1 && onlyDummySword;
        }

        /// <summary>
        /// Resolves everything this hero would be given, without writing a
        /// single slot. Split out of Grant so the diagnostic dry-run runs the
        /// real decision code rather than a copy of it. Returns null only when
        /// the hero cannot be reasoned about at all.
        /// </summary>
        public static ResolvedGrant Resolve(Hero hero, float clanWeight, float skillWeight,
                                            int minimumTier, int dominanceMargin)
        {
            if (hero == null || hero.BattleEquipment == null) return null;

            SkillProfile skills = HeroAdapter.ReadSkills(hero);
            SlotSnapshot current = HeroAdapter.ReadEquipment(hero.BattleEquipment);

            int clanTier = hero.Clan != null ? hero.Clan.Tier : 0;
            int ceiling = TierCeiling.Compute(clanTier, skills.MaxCombatSkill, clanWeight, skillWeight, minimumTier);

            CultureObject culture = hero.Culture;
            if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;

            bool cultureMounted = HeroAdapter.CultureFieldsMountedElites(hero);
            MountedRangedAvailability availability = ItemCatalog.RangedAvailability(hero, culture, ceiling);

            // PlanTarget is called here as well as inside Plan (which calls it
            // again internally) purely to read WantsMount: Plan's own return
            // value is just the slot placements, and both the mount grant
            // below and the `mounted` flag need to know what the planner
            // assumed about this hero's mount state. PlanTarget is pure and
            // cheap (no game calls), so computing it twice costs nothing and
            // avoids changing Plan's public signature for every existing caller.
            LoadoutTarget target = LoadoutPlanner.PlanTarget(skills, current, availability, dominanceMargin, cultureMounted);
            List<PlannedSlot> plan = LoadoutPlanner.Plan(skills, current, availability, dominanceMargin, cultureMounted);

            // Must agree with the planner's own assumption (HasMount ||
            // WantsMount, see LoadoutPlanner.Plan): the plan already
            // restricted which ranged weapons are viable on the assumption
            // this hero ends up mounted, and GrantMount below is what makes
            // that assumption true. Using current.HasMount alone here silently
            // disagreed with the plan whenever WantsMount -- not an already-
            // owned horse -- was what put the hero on one: a Vlandian
            // crossbowman on foot, whose culture fields mounted elites, had
            // his crossbow planned around mounted-viability rules that were
            // never actually going to apply, because nothing ever gave him
            // the horse those rules assumed.
            bool mounted = current.HasMount || target.WantsMount;

            ResolvedGrant resolved = new ResolvedGrant();
            resolved.ClanTier = clanTier;
            resolved.MaxCombatSkill = skills.MaxCombatSkill;
            resolved.Ceiling = ceiling;
            resolved.CultureFieldsMountedElites = cultureMounted;
            resolved.WantsMount = target.WantsMount;
            resolved.Mounted = mounted;
            resolved.Availability = availability;
            resolved.Culture = culture;
            resolved.PlannedWeaponCount = plan.Count;

            foreach (PlannedSlot slot in plan)
            {
                EquipmentIndex index = SlotMapping.WeaponSlot(slot.SlotIndex);
                ResolvedSlot entry = new ResolvedSlot();
                entry.Label = "w" + slot.SlotIndex;
                entry.Want = slot.Category.ToString();
                entry.Slot = index;
                entry.Existing = NameOf(hero.BattleEquipment[index].Item);
                entry.Item = ItemCatalog.FindBest(slot.Category, culture, ceiling, skills, hero, mounted);
                resolved.Slots.Add(entry);
            }

            ResolveMount(hero, culture, ceiling, skills, target.WantsMount, resolved);
            ResolveArmor(hero, culture, ceiling, resolved);

            return resolved;
        }

        /// <summary>
        /// Writes a resolved plan into the hero's battle equipment and returns
        /// how many slots were actually filled. The only method in this class
        /// that mutates anything.
        /// </summary>
        public static int Apply(Hero hero, ResolvedGrant resolved)
        {
            if (hero == null || hero.BattleEquipment == null || resolved == null) return 0;

            int granted = 0;
            for (int i = 0; i < resolved.Slots.Count; i++)
            {
                ResolvedSlot entry = resolved.Slots[i];
                if (!entry.WouldWrite) continue;

                hero.BattleEquipment[entry.Slot] = new EquipmentElement(entry.Item, null, null, false);
                granted++;
            }
            return granted;
        }

        public static void Grant(Hero hero, float clanWeight, float skillWeight,
                                 int minimumTier, int dominanceMargin)
        {
            ResolvedGrant resolved = Resolve(hero, clanWeight, skillWeight, minimumTier, dominanceMargin);
            if (resolved == null) return;

            int granted = Apply(hero, resolved);

            ModLog.Info("GRANT hero=" + hero.Name + " tier=" + resolved.Ceiling
                        + " planned=" + resolved.PlannedWeaponCount + " granted=" + granted);
        }

        /// <summary>
        /// Resolves the mount the plan assumes, then a compatible harness.
        /// Only acts when the plan actually wants a mount and the hero does
        /// not already have one -- an equipped mount or harness is never
        /// replaced. The harness lookup only ever runs once a mount was
        /// found, since compatibility is judged against that specific mount's
        /// family (see ItemCatalog.FindBestHarness). Note the harness is
        /// resolved against the mount we are about to grant, not against the
        /// (still empty) Horse slot, so the pairing holds at apply time.
        /// </summary>
        private static void ResolveMount(Hero hero, CultureObject culture, int ceiling,
                                         SkillProfile skills, bool wantsMount, ResolvedGrant resolved)
        {
            ResolvedSlot horse = new ResolvedSlot();
            horse.Label = "Horse";
            horse.Want = "Mount";
            horse.Slot = EquipmentIndex.Horse;
            horse.Existing = NameOf(hero.BattleEquipment[EquipmentIndex.Horse].Item);

            if (!wantsMount)
            {
                horse.SkipReason = "plan wants no mount";
                resolved.Slots.Add(horse);
                return;
            }

            if (hero.BattleEquipment[EquipmentIndex.Horse].Item != null)
            {
                horse.SkipReason = "already mounted";
                resolved.Slots.Add(horse);
                return;
            }

            ItemObject mount = ItemCatalog.FindBestMount(culture, ceiling, skills);
            horse.Item = mount;
            resolved.Slots.Add(horse);

            if (mount == null) return;

            ResolvedSlot harness = new ResolvedSlot();
            harness.Label = "Harness";
            harness.Want = "Harness";
            harness.Slot = EquipmentIndex.HorseHarness;
            harness.Existing = NameOf(hero.BattleEquipment[EquipmentIndex.HorseHarness].Item);

            if (hero.BattleEquipment[EquipmentIndex.HorseHarness].Item != null)
            {
                harness.SkipReason = "already fitted";
            }
            else
            {
                harness.Item = ItemCatalog.FindBestHarness(mount, culture, ceiling);
            }

            resolved.Slots.Add(harness);
        }

        private static void ResolveArmor(Hero hero, CultureObject culture, int ceiling, ResolvedGrant resolved)
        {
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                ResolvedSlot entry = new ResolvedSlot();
                entry.Label = SlotMapping.NameOf(slot);
                entry.Want = ArmorTypeFor(slot).ToString();
                entry.Slot = slot;
                entry.Existing = NameOf(hero.BattleEquipment[slot].Item);

                if (hero.BattleEquipment[slot].Item != null)
                {
                    entry.SkipReason = "already worn";
                }
                else
                {
                    entry.Item = ItemCatalog.FindBestArmor(ArmorTypeFor(slot), culture, ceiling);
                }

                resolved.Slots.Add(entry);
            }
        }

        /// <summary>Null-safe item name for logging.</summary>
        private static string NameOf(ItemObject item)
        {
            if (item == null) return null;
            return item.StringId;
        }

        /// <summary>Maps an armour slot to the item type that fills it.</summary>
        private static ItemObject.ItemTypeEnum ArmorTypeFor(EquipmentIndex slot)
        {
            switch (slot)
            {
                case EquipmentIndex.Head: return ItemObject.ItemTypeEnum.HeadArmor;
                case EquipmentIndex.Body: return ItemObject.ItemTypeEnum.BodyArmor;
                case EquipmentIndex.Leg: return ItemObject.ItemTypeEnum.LegArmor;
                case EquipmentIndex.Gloves: return ItemObject.ItemTypeEnum.HandArmor;
                default: return ItemObject.ItemTypeEnum.Cape;
            }
        }
    }
}
