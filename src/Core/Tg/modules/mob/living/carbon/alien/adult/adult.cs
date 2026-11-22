// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/adult/adult.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the adult alien base with its devouring. The var blocks are generated (D111).

public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:38  GLOBAL_LIST_INIT(strippable_alien_humanoid_items, create_strippable_list(list(...)))
    public DmList strippable_alien_humanoid_items;

    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:38  (GLOBAL_MANAGED(strippable_alien_humanoid_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstrippable_alien_humanoid_items()
    {
        strippable_alien_humanoid_items = (DmList)create_strippable_list(Dm.list(
            typeof(datum_strippable_item_hand_left),
            typeof(datum_strippable_item_hand_right),
            typeof(datum_strippable_item_mob_item_slot_handcuffs),
            typeof(datum_strippable_item_mob_item_slot_legcuffs)));
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/adult.dm:1
public partial class mob_living_carbon_alien_adult : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW, 0.5f, -11));
        Waiting.NoSleep(AddElement(typeof(datum_element_strippable), GLOB.strippable_alien_humanoid_items));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:50
    public override async DmTask<object> cuff_resist(obj_item cuffs = null, object breakouttime = null, float? cuff_break = null)
    {
        cuff_break ??= 0;
        playsound(this, Dm.resource("sound/mobs/non-humanoids/hiss/hiss5.ogg"), 40, TRUE, TRUE); //Alien roars when starting to break free
        await base.cuff_resist(cuffs, cuff_break: INSTANT_CUFFBREAK);
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:54
    public override async DmTask<object> resist_grab(object moving_resist = null)
    {
        if (Dm.truthy(pulledby.grab_state))
        {
            visible_message(
                span_danger(I18n.T("[] breaks free of []'s grip!", this, pulledby)),
                span_danger(I18n.T("You break free of []'s grip!", pulledby)));
        }
        await pulledby.stop_pulling();
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:63
    public override async DmTask alien_evolve(mob_living_carbon_alien new_xeno = null)
    {
        await drop_all_held_items();
        await base.alien_evolve(new_xeno);
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:79
    public override async DmTask<object> check_breath(datum_gas_mixture breath = null)
    {
        if (Dm.num(breath?.total_moles()) > 0 && !HAS_TRAIT(this, TRAIT_SNEAK))
            playsound(get_turf(this), SFX_LOW_HISS, 50, FALSE, -5);
        return await base.check_breath(breath);
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:84
    public override async DmTask<object> setGrabState(object newstate = null)
    {
        object dot = null;
        if (Dm.eq(newstate, grab_state))
            return dot;
        if (Dm.num(newstate) > GRAB_AGGRESSIVE)
            newstate = GRAB_AGGRESSIVE;
        dot = grab_state;
        grab_state = Dm.num(newstate);
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_SET_GRAB_STATE]))
            this._SendSignal(COMSIG_MOVABLE_SET_GRAB_STATE, Dm.list(this, grab_state));
        if (Dm.truthy(pulling?._listen_lookup?[COMSIG_MOVABLE_CHANGED_GRABBED_STATE]))
            pulling._SendSignal(COMSIG_MOVABLE_CHANGED_GRABBED_STATE, Dm.list(pulling, grab_state));
        update_incapacitated();
        switch (grab_state) // Current state.
        {
            case GRAB_PASSIVE:
                REMOVE_TRAIT(pulling, TRAIT_IMMOBILIZED, CHOKEHOLD_TRAIT);
                if (Dm.ge(dot, GRAB_NECK)) // Previous state was a a neck-grab or higher.
                    REMOVE_TRAIT(pulling, TRAIT_FLOORED, CHOKEHOLD_TRAIT);
                break;
            case GRAB_AGGRESSIVE:
                if (Dm.ge(dot, GRAB_NECK)) // Grab got downgraded.
                    REMOVE_TRAIT(pulling, TRAIT_FLOORED, CHOKEHOLD_TRAIT);
                else // Grab got upgraded from a passive one.
                    ADD_TRAIT(pulling, TRAIT_IMMOBILIZED, CHOKEHOLD_TRAIT);
                break;
            case GRAB_NECK or GRAB_KILL:
                if (Dm.le(dot, GRAB_AGGRESSIVE))
                    ADD_TRAIT(pulling, TRAIT_FLOORED, CHOKEHOLD_TRAIT);
                break;
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:108
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        atom dropping = dropped;
        if (Dm.truthy(await devour_lad(dropping as atom_movable)))
            return null;
        return await base.mouse_drop_receive(dropping, user, @params);
    }

    /// Returns FALSE if we're not allowed to eat it, true otherwise
    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:114
    public virtual object can_consume(atom_movable poor_soul = null)
    {
        if (!isliving(poor_soul) || pulling != poor_soul)
            return FALSE;
        if (Dm.truthy(incapacitated) || grab_state < GRAB_AGGRESSIVE)
            return FALSE;
        if (!Dm.eq(get_dir(this, poor_soul), dir)) // Gotta face em 4head
            return FALSE;
        return TRUE;
    }

    /// Attempts to devour the passed in thing in devour_time seconds
    /// The mob needs to be consumable, as decided by [/mob/living/carbon/alien/adult/proc/can_consume]
    /// Returns FALSE if the attempt never even started, TRUE otherwise
    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:126
    public virtual async DmTask<object> devour_lad(atom_movable candidate = null, float? devour_time = null)
    {
        devour_time ??= 13.5f * SECONDS;
        await setDir(get_dir(this, candidate));
        if (!Dm.truthy(can_consume(candidate)))
            return FALSE;
        mob_living lucky_winner = candidate as mob_living;

        lucky_winner.audible_message(span_danger(I18n.T("You hear a great snapping, like the disjointing of muscle and bone.")));
        lucky_winner.visible_message(span_danger(I18n.T("[] is attempting to devour []!", this, lucky_winner)),
                span_userdanger(I18n.T("[] is attempting to devour you!", this)));

        playsound(lucky_winner, Dm.resource("sound/mobs/non-humanoids/alien/alien_eat.ogg"), 100);
        if (!Dm.truthy(await do_after(this, devour_time, lucky_winner, extra_checks: Dm.@new<datum_callback>(this, nameof(can_consume), lucky_winner))))
            return TRUE;
        if (!Dm.truthy(can_consume(lucky_winner)))
            return TRUE;

        obj_item_organ_stomach_alien melting_pot = get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_alien;
        if (!(melting_pot is obj_item_organ_stomach_alien))
        {
            visible_message(span_clown(I18n.T("[] can't seem to consume []!", this, lucky_winner)),
                span_alien(I18n.T("You feel a pain in your... chest? You can't get [] down.", lucky_winner)));
            return TRUE;
        }

        lucky_winner.audible_message(span_danger(I18n.T("You hear a deep groan, and a harsh snap like a mantrap.")));
        lucky_winner.visible_message(span_danger(I18n.T("[] devours []!", this, lucky_winner)),
                span_userdanger(I18n.T("[] devours you!", this)));
        log_combat(this, lucky_winner, "devoured");
        await melting_pot.consume_thing(lucky_winner);
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:155
    public override object get_butt_sprite()
    {
        return Dm.icon(Dm.resource("icons/mob/butts.dmi"), BUTT_SPRITE_XENOMORPH);
    }
}

public static partial class GlobalProcs
{
    //For alien evolution/promotion/queen finder procs. Checks for an active alien of that type
    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:68
    public static object get_alien_type(object alien_path = null, mob ignored = null)
    {
        foreach (mob_living_carbon_alien alien in Dm.each<mob_living_carbon_alien>(GLOB.carbon_list))
        {
            if (alien == ignored)
                continue;
            if (!istype(alien, alien_path as Type))
                continue;
            if (!Dm.truthy(alien.key) || alien.stat == DEAD) //Only living aliens with a ckey are valid.
                continue;
            return alien;
        }
        return null;
    }
}

// Aliens can touch acid
// tg: code/modules/mob/living/carbon/alien/adult/adult.dm:159
public partial class mob_living_carbon_alien : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/alien/adult/adult.dm:159
    public override object can_touch_acid(atom acided_atom = null, object acid_power = null, object acid_volume = null)
    {
        return TRUE;
    }
}
