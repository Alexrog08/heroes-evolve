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

        /// <summary>A lord fielding fewer than this many weapons cannot fight.</summary>
        private const int MinimumWeapons = 2;

        /// <summary>
        /// Armour at or below this tier is civilian clothing, not kit. The tier
        /// census named what lives down there: aserai_civil_d, cloth_tunic,
        /// vlandian_woman_dress, nord_casual_tunic, layered_robe.
        /// </summary>
        private const int CivilianArmorTier = 1;

        /// <summary>
        /// True when the hero's battle equipment shows the come-of-age failure.
        ///
        /// This used to test for empty weapon slots or the exact dummy spatha,
        /// and a 600-lord campaign proved that far too narrow: it caught none of
        /// the fifteen genuinely broken heroes in it. The failure does not leave
        /// a hero naked. It leaves them partly equipped, in one of two shapes:
        ///
        ///   Fourteen lords -- every one of them Cavalry, every one generated
        ///   during the campaign -- wore tier-6 armour, a helmet and a horse,
        ///   and carried a single weapon: one javelin, one lance, one sword. No
        ///   shield, no sidearm. Ages 18 to 58, so they had been like that for
        ///   decades.
        ///
        ///   One 21-year-old carried a full and coherent set of four weapons
        ///   while wearing layered_robe and a civilian cape.
        ///
        /// Neither shape has an empty weapon slot or a dummy spatha. So the test
        /// is now for the symptoms: too few weapons to fight with, or armour
        /// that is still civilian clothing.
        /// </summary>
        public static bool NeedsGrant(Hero hero)
        {
            if (hero == null || hero.BattleEquipment == null) return false;

            int weapons = 0;
            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                ItemObject item = hero.BattleEquipment[SlotMapping.WeaponSlot(i)].Item;
                if (item == null) continue;
                if (HeroAdapter.IsVanillaDummySword(item)) continue;
                weapons++;
            }

            if (weapons < MinimumWeapons) return true;

            return IsCivilian(hero.BattleEquipment[EquipmentIndex.Body].Item)
                   || IsCivilian(hero.BattleEquipment[EquipmentIndex.Head].Item);
        }

        /// <summary>
        /// True for an armour slot holding civilian clothing. An empty slot is
        /// not civilian -- it is a gap, and gaps are filled by the armour pass
        /// without needing to trigger a repair on their own. Only Body and Head
        /// are judged: an empty cape or glove slot is ordinary on a lord and
        /// treating it as a defect would put most of the map through a repair.
        /// </summary>
        private static bool IsCivilian(ItemObject item)
        {
            return item != null && (int)item.Tier + 1 <= CivilianArmorTier;
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
            int ceiling = HeroAdapter.ReadCeiling(hero, skills, clanWeight, skillWeight, minimumTier);

            CultureObject culture = hero.Culture;
            if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;

            // One question, one answer: does this culture put its lords on
            // horses. It decides both the dismount below and the fallback for a
            // hero carrying no usable role label.
            //
            // This used to be HeroAdapter.CultureMountsLords, which
            // walked a single upgrade branch of the elite line and so returned
            // whatever that one path happened to hold -- it reported False for
            // nord even though nord's elite line, borrowed wholesale from
            // Sturgia, is mounted. It also cost a troop-tree walk on every
            // resolve to feed a value only an Unset role ever reads.
            bool cultureMounted = CultureProfile.MountsItsLords(culture);

            BattleRole role = HeroAdapter.ReadRole(hero);

            // A culture that does not put its lords on horses does not get one
            // here either, whatever this particular hero's label says. The label
            // drifts over campaign generations -- every mounted Nord lord in the
            // save was born in play, against twenty-one authored ones on foot --
            // and Nord fields no cavalry for such a lord to lead. Only the mount
            // is dropped; the weapon archetype is untouched, since Cavalry and
            // Infantry build the same melee target.
            // What the failed generation left him outranks what his culture
            // expects. A broken lord keeps exactly one skill and one matching
            // weapon -- Nus only Throwing, Zandina only Polearm -- and that
            // number is the only thing the game ever recorded about who he was.
            // It is only honoured where his people actually field that shape, so
            // a Vlandian who shoots becomes a crossbowman and never an archer.
            role = CultureArchetypes.RoleFor(culture, skills, role);

            if (!cultureMounted) role = BattleRoleRules.Dismounted(role);
            MountedRangedAvailability availability = ItemCatalog.RangedAvailability(hero, culture, ceiling);

            // PlanTarget is called here as well as inside Plan (which calls it
            // again internally) purely to read WantsMount: Plan's own return
            // value is just the slot placements, and both the mount grant
            // below and the `mounted` flag need to know what the planner
            // assumed about this hero's mount state. PlanTarget is pure and
            // cheap (no game calls), so computing it twice costs nothing and
            // avoids changing Plan's public signature for every existing caller.
            LoadoutTarget target = LoadoutPlanner.PlanTarget(skills, current, availability, dominanceMargin, cultureMounted, role);
            List<PlannedSlot> plan = LoadoutPlanner.Plan(skills, current, availability, dominanceMargin, cultureMounted, role);

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
            resolved.CultureMountsLords = cultureMounted;
            resolved.Role = role.ToString();
            resolved.WantsMount = target.WantsMount;
            resolved.Mounted = mounted;
            resolved.Availability = availability;
            resolved.PlannedWeaponCount = plan.Count;
            resolved.TargetWeapons = Join(target.Weapons);
            resolved.CurrentWeapons = Describe(current);
            resolved.PlacedWeapons = JoinPlan(plan);

            foreach (PlannedSlot slot in plan)
            {
                EquipmentIndex index = SlotMapping.WeaponSlot(slot.SlotIndex);
                ResolvedSlot entry = new ResolvedSlot();
                entry.Label = "w" + slot.SlotIndex;
                entry.Want = slot.Category.ToString();
                entry.Slot = index;
                entry.Existing = NameOf(hero.BattleEquipment[index].Item);
                entry.Item = ItemCatalog.FindForGrant(slot.Category, culture, ceiling, skills, hero, mounted,
                                                      RedundantTwoHander(slot.Category, current, plan),
                                                      entry.Label);
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

        /// <summary>
        /// Repairs the hero and returns how many slots were filled. Zero means
        /// the catalogue had nothing this hero could be given -- see
        /// HeroLoadoutBehavior.TryRepair, which uses that to stop retrying.
        /// </summary>
        public static int Grant(Hero hero, float clanWeight, float skillWeight,
                                int minimumTier, int dominanceMargin)
        {
            ResolvedGrant resolved = Resolve(hero, clanWeight, skillWeight, minimumTier, dominanceMargin);
            if (resolved == null) return 0;

            int granted = Apply(hero, resolved);

            ModLog.Info("GRANT hero=" + hero.Name
                        + " id=" + (hero.CharacterObject != null ? hero.CharacterObject.StringId : "?")
                        + " tier=" + resolved.Ceiling
                        + " planned=" + resolved.PlannedWeaponCount + " granted=" + granted);

            return granted;
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

            ItemObject mount = ItemCatalog.FindMountForGrant(culture, ceiling, skills, hero, horse.Label);
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
                harness.Item = ItemCatalog.FindHarnessForGrant(mount, culture, ceiling, hero, harness.Label);
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

                ItemObject worn = hero.BattleEquipment[slot].Item;

                // Civilian clothing is the defect, not a choice the hero made,
                // so it is the one case where equipped gear is replaced. Only
                // when the ceiling can actually do better -- swapping one tier-1
                // robe for another would be churn, and a hero capped at tier 1
                // has nothing better available to him anyway.
                bool civilian = worn != null && (int)worn.Tier + 1 <= CivilianArmorTier
                                && ceiling > CivilianArmorTier
                                && !ItemCatalog.IsIrreplaceable(worn);

                if (worn != null && !civilian)
                {
                    entry.SkipReason = "already worn";
                }
                else
                {
                    entry.Item = ItemCatalog.FindArmorForGrant(ArmorTypeFor(slot), culture, ceiling,
                                                                hero, entry.Label);
                }

                resolved.Slots.Add(entry);
            }
        }

        /// <summary>
        /// The two-handed category a one-handed slot must not duplicate, or None.
        ///
        /// A bastard sword is a legitimate answer to a one-handed request, but
        /// not when the hero already carries a two-handed sword: the one-handed
        /// slot exists so the shield hand is free, and a second two-hander adds
        /// nothing. The same reasoning applies to axes, which have their own
        /// bastard variants. The mapping itself lives in
        /// CategoryRules.TwoHandedPartner, which the market shares; what is
        /// local here is looking in the plan as well as in what is worn, since
        /// the partner may be something this same repair is about to hand over.
        /// </summary>
        private static WeaponCategory RedundantTwoHander(WeaponCategory wanted, SlotSnapshot current,
                                                         List<PlannedSlot> plan)
        {
            WeaponCategory partner = CategoryRules.TwoHandedPartner(wanted);
            if (partner == WeaponCategory.None) return WeaponCategory.None;

            if (current.Contains(partner)) return partner;

            for (int i = 0; i < plan.Count; i++)
            {
                if (plan[i].Category == partner) return partner;
            }

            return WeaponCategory.None;
        }

        private static string Join(List<WeaponCategory> categories)
        {
            if (categories == null || categories.Count == 0) return "<none>";
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < categories.Count; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(categories[i]);
            }
            return text.ToString();
        }

        private static string JoinPlan(List<PlannedSlot> plan)
        {
            if (plan == null || plan.Count == 0) return "<none>";
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < plan.Count; i++)
            {
                if (i > 0) text.Append(',');
                text.Append('w').Append(plan[i].SlotIndex).Append(':').Append(plan[i].Category);
            }
            return text.ToString();
        }

        private static string Describe(SlotSnapshot current)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(current.WeaponAt(i));
            }
            return text.ToString();
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
