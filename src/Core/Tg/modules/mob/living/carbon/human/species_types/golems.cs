// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/golems.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3a (D349): the whole file: the golem, a being of stone whose nutrition is its health. Its organs (the silicate stomach, the rock
// brain, the resonator) and its stone limbs are separate types; the minerals it eats are golem_food (components/food/golem_food.cs).

/// Animated beings of stone. They have increased defenses, and do not need to breathe. They must eat minerals to live, which give additional buffs.
// tg: code/modules/mob/living/carbon/human/species_types/golems.dm:3
public partial class datum_species_golem : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:62
    public override async DmTask on_species_gain(mob_living_carbon_human human_who_gained_species = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(human_who_gained_species, old_species, pref_load, regenerate_icons, replace_missing);
        RegisterSignal(human_who_gained_species, COMSIG_MOB_AFTER_APPLY_DAMAGE, nameof(drain_nutrition));
        RegisterSignal(human_who_gained_species, COMSIG_LIVING_UPDATE_NUTRITION, nameof(check_nutrition));
        RegisterSignal(human_who_gained_species, COMSIG_CARBON_DEFIB_HEART_CHECK, nameof(defib_check));
        RegisterSignal(human_who_gained_species, COMSIG_ATOM_ITEM_INTERACTION, nameof(rebuild_check));
        RegisterSignal(human_who_gained_species, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(human_who_gained_species, COMSIG_LIVING_LIFE, nameof(on_life));
        // nutrition = health, so give people a head start
        await human_who_gained_species.set_nutrition(NUTRITION_LEVEL_WELL_FED);

        MODIFY_PHYSIOLOGY(human_who_gained_species, STAMINA, 0.6f);
        MODIFY_PHYSIOLOGY(human_who_gained_species, PHYS_COEFF_STUN, 0.6f);
        MODIFY_PHYSIOLOGY(human_who_gained_species, PHYS_COEFF_KNOCKDOWN, 1.2f); //this offsets the positive effects of the lowered stun coeff on knockdowns specifically.
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:77
    public override async DmTask on_species_loss(mob_living_carbon_human human = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        await base.on_species_loss(human, new_species, pref_load, __extra);
        UnregisterSignal(human, Dm.list(
            COMSIG_MOB_AFTER_APPLY_DAMAGE,
            COMSIG_LIVING_UPDATE_NUTRITION,
            COMSIG_CARBON_DEFIB_HEART_CHECK,
            COMSIG_ATOM_ITEM_INTERACTION,
            COMSIG_ATOM_EXAMINE,
            COMSIG_LIVING_LIFE));

        MODIFY_PHYSIOLOGY(human, STAMINA, 1 / 0.6f);
        MODIFY_PHYSIOLOGY(human, PHYS_COEFF_STUN, 1 / 0.6f);
        MODIFY_PHYSIOLOGY(human, PHYS_COEFF_KNOCKDOWN, 1 / 1.2f);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:92
    [SignalHandler]
    public virtual void on_life(mob_living_carbon_human source = null, object seconds_per_tick = null)
    {
        float spt = Dm.num(seconds_per_tick);
        if (Dm.num(source.nutrition) <= 20)
        {
            // this is "hard crit" for golems
            Waiting.NoSleep(source.Unconscious(1.5f * SECONDS * spt));
        }
        if (Dm.num(source.nutrition) <= 100 || Dm.num(source.health) < Dm.num(source.crit_threshold))
        {
            // and this is "crit damage" for golems
            float drain = 1;
            if (Dm.num(source.nutrition) <= 50)
                drain *= 2;
            if (Dm.num(source.health) < Dm.num(source.crit_threshold))
                drain *= 2;
            Waiting.NoSleep(source.adjust_nutrition(-1 * drain * spt, forced: TRUE));
        }
        if (Dm.num(source.nutrition) > NUTRITION_LEVEL_FAT)
        {
            // nutrition is health so let's keep this sane
            Waiting.NoSleep(source.set_nutrition(NUTRITION_LEVEL_FAT));
        }
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:110
    [SignalHandler]
    public virtual void on_examine(mob_living_carbon_human source = null, mob_living examiner = null, DmList examine_text = null)
    {
        if (!IS_DEAD_OR_FAKING(source))
            return;

        Dm.append(ref examine_text, span_warning(I18n.T("This golem appears to be in a state of disrepair. It may be possible to rebuild it by adding minerals into its form.")));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:119
    [SignalHandler]
    public virtual object rebuild_check(mob_living_carbon_human source = null, mob_living user = null, obj_item tool = null, params object[] __extra)
    {
        if (!IS_DEAD_OR_FAKING(source))
            return NONE;

        if (!isstack(tool) || !Dm.truthy(is_type_in_list(tool, GLOB.golem_stack_food_directory)))
            return NONE;

        Waiting.Detach(rebuild(source, user, tool as obj_item_stack)); // INVOKE_ASYNC
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:131
    public virtual async DmTask rebuild(mob_living_carbon_human source = null, mob_living user = null, obj_item_stack mats = null)
    {
        source.notify_revival(I18n.T("You are being rebuilt by []!", user.real_name));
        bool brute_ready = Dm.num(source.get_brute_loss()) < 50;
        bool burn_ready = Dm.num(source.get_fire_loss()) < 50;
        bool nutrition_ready = Dm.num(source.nutrition) > NUTRITION_LEVEL_VERY_HUNGRY;

        while (Dm.truthy(check_rebuild(source, user, mats)))
        {
            user.visible_message(
                span_notice(I18n.T("[] uses some of [] to rebuild []'s form.", user, mats, source)),
                span_notice(I18n.T("You use some of [] to rebuild []'s form.", mats, source)));

            float do_after_time = 2 * SECONDS;
            if (brute_ready && burn_ready && nutrition_ready && Dm.truthy(source.can_be_revived()))
            {
                do_after_time *= 4;
                user.show_message(span_notice(I18n.T("[] looks almost fully rebuilt, this will take a bit longer...", source)), MSG_VISUAL);
            }
            if (HAS_TRAIT(user, TRAIT_QUICK_BUILD))
                do_after_time *= 0.75f;

            if (!Dm.truthy(await do_after(user, do_after_time, source, extra_checks: Dm.@new<datum_callback>(this, nameof(check_rebuild), source, user, mats))))
                return;

            // calculated "effective sheet power" based on mats
            // ex. iron sheets will have a power of 1, reinforced glass a power of 1.5
            float mat_power = 0;
            foreach (object mat in Dm.each(mats.custom_materials))
                mat_power += Dm.num(mats.custom_materials[mat]);
            mat_power /= Dm.num(mats.amount); // mats per sheet
            mat_power /= SHEET_MATERIAL_AMOUNT; // normalize

            await mats.use(1);
            await source.heal_ordered_damage(mat_power * 5, Dm.list(BRUTE, BURN));
            await source.adjust_nutrition(mat_power * 20);
            if (!brute_ready)
            {
                if (Dm.num(source.get_brute_loss()) < 50)
                {
                    user.show_message(span_notice(I18n.T("[] looks sturdier than ever! It's not long now...", source)), MSG_VISUAL);
                    brute_ready = true;
                }
                continue;
            }
            if (!burn_ready)
            {
                if (Dm.num(source.get_fire_loss()) < 50)
                {
                    user.show_message(span_notice(I18n.T("[] seems to be regaining its integrity! Just a bit more...", source)), MSG_VISUAL);
                    burn_ready = true;
                }
                continue;
            }
            if (!nutrition_ready)
            {
                if (Dm.num(source.nutrition) > NUTRITION_LEVEL_HUNGRY)
                {
                    user.show_message(span_notice(I18n.T("[] seems to be stabilizing its form! Almost there...", source)), MSG_VISUAL);
                    nutrition_ready = true;
                }
                continue;
            }

            if (Dm.truthy(await source.revive(excess_healing: 10))) // give a bit of organ/tox/oxy healing for free
            {
                source.visible_message(
                    span_notice(I18n.T("[] stabilizes and reforms into a functional state!", source)),
                    span_boldnotice(I18n.T("You stabilize and reform into a functional state!")),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
                await source.set_resting(FALSE, silent: TRUE, instant: TRUE);
            }
            return;
        }
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:189
    public virtual object check_rebuild(mob_living_carbon_human source = null, mob_living user = null, obj_item_stack mats = null)
    {
        if (source.stat != DEAD)
            return FALSE;
        if (QDELETED(user))
            return FALSE;
        if (QDELETED(mats) || Dm.num(mats.amount) < 1)
            return FALSE;
        if (!Dm.truthy(user.is_holding(mats)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:200
    [SignalHandler]
    public virtual object defib_check(mob_living_carbon_human source = null, mob_living_carbon_human defib_user = null)
    {
        // golems can't be defibrillated, they have no heart and aren't even alive in the traditional sense
        // you gotta rebuild them with materials if they fall apart
        return DEFIB_FAIL_GOLEM;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:207
    [SignalHandler]
    public virtual void drain_nutrition(mob_living_carbon_human source = null, object damage_amt = null, object damage_type = null, params object[] __extra)
    {
        // our brute and burn damage is more than halved by our limbs
        // the other "half" of the damage is converted into nutrition loss (representing loss of rock material)
        if (!Dm.eq(damage_type, BURN) && !Dm.eq(damage_type, BRUTE))
            return;

        Waiting.NoSleep(source.adjust_nutrition(round(-3 * Dm.num(damage_amt), 0.01f), forced: TRUE));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:217
    [SignalHandler]
    public virtual void check_nutrition(mob_living_carbon_human source = null)
    {
        if (Dm.num(source.nutrition) < NUTRITION_LEVEL_STARVING)
        {
            if (!Dm.truthy(early_warning) && warning_cd <= world.time && !IS_UNCONSCIOUS(source)) // COOLDOWN_FINISHED, inline
            {
                source.visible_message(
                    span_warning(I18n.T("[] shudders weakly as their form begins to destabilize!", source)),
                    span_bolddanger(I18n.T("You feel your form destabilizing as you run low on material to sustain yourself! Find some minerals to eat soon, or you may crumble!")),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
                early_warning = TRUE;
                warning_cd = world.time + 1 * MINUTES; // COOLDOWN_START, inline
            }
        }
        else
            early_warning = FALSE;

        if (Dm.num(source.nutrition) < 50)
        {
            if (!Dm.truthy(final_warning) && warning_cd <= world.time && !IS_UNCONSCIOUS(source))
            {
                source.visible_message(
                    span_warning(I18n.T("[] looks like they're on the verge of falling apart!", source)),
                    span_userdanger(I18n.T("Your form shudders violently as you near complete destabilization! Eat some minerals quickly, or you may crumble!")),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
                final_warning = TRUE;
                warning_cd = world.time + 1 * MINUTES;
            }
        }
        else
            final_warning = FALSE;

        if (Dm.num(source.nutrition) < 2 && source.stat != DEAD)
        {
            source.visible_message(
                span_warning(I18n.T("[] shudders and crumbles into a pile of inert rocks!", source)),
                span_userdanger(I18n.T("You run our of material to sustain your animated form, and crumble into a pile of inert rocks!")),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            source.investigate_log("starved to death as a golem", INVESTIGATE_DEATHS);
            Waiting.NoSleep(source.death());
        }
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:257
    public override object get_physical_attributes()
    {
        return I18n.T("Golems are hardy creatures made out of stone, which are thus naturally resistant to many dangers, including asphyxiation, fire, radiation, electricity, and viruses.They gain special abilities depending on the type of material consumed, but they need to consume material to keep their body animated.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:261
    public override object get_species_description()
    {
        return I18n.T("Golems are lithoid creatures who eat rocks and minerals to survive and adapt.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:264
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("While Golems have long been commonly found on frontier worlds, peacefully mining and otherwise living in harmony with the environment, it is believed they were originally constructed in Nanotrasen laboratories as a form of cheap labor. Whatever happened up to this point is unknown, but they have since gained freedom and are now a rare sight in the galaxy."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/golems.dm:271
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "user-shield"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Lithoid")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Lithoids are creatures made out of minerals instead of blood and flesh. They are strong and immune to many environmental and personal dangers such as fire, radiation, lack of air, lava, viruses, and dismemberment.")))));

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "gem"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Metamorphic Rock")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Consuming minerals can grant Lithoids temporary benefits based on the type consumed.")))));

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "tools"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Natural Miners")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Golems can see dimly in the dark, sense minerals, break boulders, and mine stone with their bare hands. They can even smelt ores in an internal furnace, if their surrounding environment is hot enough.")))));

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "bolt"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Anima")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Maintaining the force animating stone is taxing. Lithoids must eat frequently in order to avoid returning to inanimate statues, and only derive nutrition from eating minerals.")))));

        return to_add;
    }
}
