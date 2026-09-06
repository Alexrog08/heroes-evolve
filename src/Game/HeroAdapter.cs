using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>Reads game state into the core's pure types.</summary>
    public static class HeroAdapter
    {
        public static SkillProfile ReadSkills(Hero hero)
        {
            if (hero == null) return new SkillProfile(0, 0, 0, 0, 0, 0, 0);

            return new SkillProfile(
                hero.GetSkillValue(DefaultSkills.OneHanded),
                hero.GetSkillValue(DefaultSkills.TwoHanded),
                hero.GetSkillValue(DefaultSkills.Polearm),
                hero.GetSkillValue(DefaultSkills.Bow),
                hero.GetSkillValue(DefaultSkills.Crossbow),
                hero.GetSkillValue(DefaultSkills.Throwing),
                hero.GetSkillValue(DefaultSkills.Riding));
        }

        public static SlotSnapshot ReadEquipment(Equipment equipment)
        {
            WeaponCategory[] weapons = new WeaponCategory[SlotSnapshot.WeaponSlotCount];

            if (equipment == null)
            {
                for (int i = 0; i < weapons.Length; i++) weapons[i] = WeaponCategory.None;
                return new SlotSnapshot(weapons, false, false, false, false, false, false, false);
            }

            for (int i = 0; i < weapons.Length; i++)
            {
                EquipmentElement element = equipment[SlotMapping.WeaponSlot(i)];

                // The lone spatha vanilla's dummy fallback hands out is the
                // marker of the come-of-age failure, never kit the hero chose.
                // Reported as a OneHandedSword it satisfies the planner's
                // sidearm slot, so Reconcile plans around it and leaves it in
                // place: observed in a real campaign, a king with 258 weapon
                // skill and a tier-6 ceiling kept a tier-2 starter sword, and
                // because he then had four weapons NeedsGrant returned false
                // and he was never looked at again. Read it as an empty slot
                // so the repair replaces it like the junk it is.
                if (IsVanillaDummySword(element.Item))
                {
                    weapons[i] = WeaponCategory.None;
                    continue;
                }

                weapons[i] = ItemClassifier.Classify(element.Item);
            }

            return new SlotSnapshot(
                weapons,
                equipment[EquipmentIndex.Horse].Item != null,
                equipment[EquipmentIndex.HorseHarness].Item != null,
                equipment[EquipmentIndex.Head].Item != null,
                equipment[EquipmentIndex.Body].Item != null,
                equipment[EquipmentIndex.Leg].Item != null,
                equipment[EquipmentIndex.Gloves].Item != null,
                equipment[EquipmentIndex.Cape].Item != null);
        }

        /// <summary>
        /// The hero's battlefield role, as the game itself labels it.
        ///
        /// Mapped rather than passed through because the core must stay free of
        /// TaleWorlds types, and because FormationClass carries entries that say
        /// nothing about equipment (General, Bodyguard) or that are weight
        /// variants of a role we treat identically (HeavyInfantry, LightCavalry,
        /// HeavyCavalry). Anything unrecognised becomes Unset, which puts the
        /// hero back on the skill-only path rather than guessing.
        /// </summary>
        public static BattleRole ReadRole(Hero hero)
        {
            if (hero == null) return BattleRole.Unset;

            CharacterObject character = hero.CharacterObject;
            if (character == null) return BattleRole.Unset;

            switch (character.DefaultFormationClass)
            {
                case FormationClass.Infantry:
                case FormationClass.HeavyInfantry:
                    return BattleRole.Infantry;

                case FormationClass.Ranged:
                case FormationClass.Skirmisher:
                    return BattleRole.Ranged;

                case FormationClass.Cavalry:
                case FormationClass.LightCavalry:
                case FormationClass.HeavyCavalry:
                    return BattleRole.Cavalry;

                case FormationClass.HorseArcher:
                    return BattleRole.HorseArcher;

                default:
                    return BattleRole.Unset;
            }
        }

        /// <summary>
        /// The one-handed sword vanilla's dummy fallback hands out when a hero
        /// comes of age with no usable equipment template. Kept in step with
        /// GrantService.DummySwordId, which is what detects the same state.
        /// </summary>
        internal static bool IsVanillaDummySword(ItemObject item)
        {
            return item != null && item.StringId == GrantService.DummySwordId;
        }

    }
}
