// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/external/wings/wings.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b: the moth wings the DNA infuser grows from a mothroach): the wing organ with its flight (the toggle flight
// action, the jetpack drift, the open and closed sprites, the slip in the air) and the wing overlays. can_soften_fall came first
// (D325: the fall damage of multi-z) and make_flap_sound with open_wings and close_wings for the flap emote (M9 slice B). Holds the
// [DmPath] of a type that an M2 slot signature or var block names, in the earliest .dme file that declares it (rules 2.3).

// tg: code/modules/surgery/organs/external/wings/wings.dm:1  #define DEFAULT_WING_FORCE 2.25 NEWTONS (the var default, emitted)

///Wing base type. can do things :>
// tg: code/modules/surgery/organs/external/wings/wings.dm:4
[DmPath("/obj/item/organ/wings")]
public partial class obj_item_organ_wings : obj_item_organ
{
    ///Checks if the wings can soften short falls
    // tg: code/modules/surgery/organs/external/wings/wings.dm:35
    public virtual object can_soften_fall()
    {
        return TRUE;
    }

    ///Set flap_sound to play the sound when emoting *flap
    // tg: code/modules/surgery/organs/external/wings/wings.dm:39
    public virtual void make_flap_sound(mob_living_carbon wing_owner = null)
    {
        if (flap_sound == null)
            return;
        playsound(wing_owner, flap_sound, 50, TRUE);
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        setup_jetpack();
        if (flight_level > WINGS_FLIGHTLESS)
            // only flightful wings get orange juice
            food_reagents = Dm.list(Dm.kv(typeof(datum_reagent_flightpotion), 5));
        return base.Initialize(mapload, __extra);
    }

    /**
     * update proc to kick our jetpack component whenever circumstances change
     *
     * Flightless wings never generate lift of their own, drifting in 0G and hook
     * the organ being implanted/removed. Anything airworthy flies under its own power, so we
     * hook the wings being opened/closed instead
     */
    // tg: code/modules/surgery/organs/external/wings/wings.dm:58
    public virtual void setup_jetpack()
    {
        if (flight_level <= WINGS_FLIGHTLESS)
        {
            Waiting.NoSleep(AddComponent(
                typeof(datum_component_jetpack),
                TRUE,
                drift_force,
                COMSIG_ORGAN_IMPLANTED,
                COMSIG_ORGAN_REMOVED,
                null,
                Dm.@new<datum_callback>(this, nameof(allow_flight)),
                null));
            return;
        }

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_jetpack),
            TRUE,
            drift_force,
            COMSIG_WINGS_OPENED,
            COMSIG_WINGS_CLOSED,
            null,
            Dm.@new<datum_callback>(this, nameof(can_fly)),
            Dm.@new<datum_callback>(this, nameof(can_fly))));
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:83
    public override object Destroy(float? force = null)
    {
        qdel(fly);
        fly = null;
        return base.Destroy(force);
    }

    ///You only get the juice if your wings are worth a damn
    // tg: code/modules/surgery/organs/external/wings/wings.dm:88
    public override async DmTask<object> grind_results()
    {
        if (flight_level <= WINGS_FLIGHTLESS)
            return null;
        return Dm.list(Dm.kv(typeof(datum_reagent_flightpotion), 5));
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:93
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        await update_flight(organ_owner);
        return dot;
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:97
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        if (fly != null)
            await fly.Remove(organ_owner as mob);
        if (Dm.truthy(wings_open))
            await toggle_flight(organ_owner as mob_living_carbon_human);
    }

    //If some goober tries to varedit the flight_level instead of running the set_flight() proc, we take care of that
    // tg: code/modules/surgery/organs/external/wings/wings.dm:104
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        if (Dm.eq(var_name, nameof(flight_level)))
        {
            await set_flight(owner, var_value);
            return TRUE;
        }
        return await base.vv_edit_var(var_name, var_value);
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:110
    public virtual async DmTask set_flight(mob_living_carbon organ_owner = null, object state = null)
    {
        organ_owner ??= owner;
        if (state != null && !Dm.eq(state, flight_level))
        {
            //Force landing while old binds are still live
            if (Dm.truthy(wings_open) && organ_owner != null)
                await toggle_flight(organ_owner as mob_living_carbon_human);
            flight_level = Dm.num(state);
            setup_jetpack();
        }
        await update_flight(organ_owner);
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:120
    public virtual async DmTask update_flight(mob_living_carbon organ_owner = null)
    {
        organ_owner ??= owner;
        if (organ_owner == null)
            return;

        if (fly != null)
            await fly.Remove(organ_owner);
        if (Dm.truthy(wings_open))
            await toggle_flight(organ_owner as mob_living_carbon_human);

        if (flight_level <= WINGS_FLIGHTLESS)
            return;
        if (QDELETED(fly))
            fly = Dm.@new<datum_action_innate_flight>();
        await fly.Grant(organ_owner);
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:135
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        await handle_flight(owner as mob_living_carbon_human);
    }

    ///Called on_life(). Handle flight code and check if we're still flying
    // tg: code/modules/surgery/organs/external/wings/wings.dm:140
    public virtual async DmTask<object> handle_flight(mob_living_carbon_human human = null)
    {
        if (!HAS_TRAIT_FROM(human, TRAIT_MOVE_FLOATING, SPECIES_FLIGHT_TRAIT))
            return FALSE;
        if (!Dm.truthy(can_fly()))
        {
            await toggle_flight(human);
            return FALSE;
        }
        return TRUE;
    }

    ///Check if still eligible for active flight (wings covered, atmosphere too thin, etc)
    // tg: code/modules/surgery/organs/external/wings/wings.dm:150
    public virtual object can_fly()
    {
        mob_living_carbon human = owner;
        if (IS_UNCONSCIOUS_OR_CRIT(human) || Dm.eq(human.body_position, LYING_DOWN) || human.client == null)
            return FALSE;
        if (flight_level < WINGS_AIRWORTHY) //Them wings are useless!
            return FALSE;
        var wing_overlay = bodypart_overlay as datum_bodypart_overlay_mutant_wings;
        if (!Dm.truthy(cant_hide) && wing_overlay != null && Dm.truthy(Dm.bitand(human.obscured_slots, wing_overlay.slot_blocker)))
        {
            to_chat(human, span_warning(I18n.T("Your clothing blocks your wings from extending!")));
            return FALSE;
        }
        turf location = get_turf(human);
        if (location == null)
            return FALSE;
        if (flight_level < WINGS_MAGIC)
        {
            datum_gas_mixture environment = Waiting.NoSleep(location.return_air()) as datum_gas_mixture;
            if (environment != null && Dm.num(environment.return_pressure()) < HAZARD_LOW_PRESSURE + 10)
            {
                to_chat(human, span_warning(I18n.T("The atmosphere is too thin for you to fly!")));
                return FALSE;
            }
        }
        return TRUE;
    }

    ///Check if we can passively drift in low-gravity. Used by WINGS_FLIGHTLESS jetpack component
    // tg: code/modules/surgery/organs/external/wings/wings.dm:172
    public virtual object allow_flight()
    {
        if (owner == null || owner.client == null)
            return FALSE;
        if (Dm.truthy(owner.has_gravity()))
            return FALSE;
        var wing_overlay = bodypart_overlay as datum_bodypart_overlay_mutant_wings;
        if (!Dm.truthy(cant_hide) && wing_overlay != null && Dm.truthy(Dm.bitand(owner.obscured_slots, wing_overlay.slot_blocker)))
            return FALSE;
        datum_gas_mixture current = owner.loc is atom owner_loc ? Waiting.NoSleep(owner_loc.return_air()) as datum_gas_mixture : null;
        if (current != null && Dm.num(current.return_pressure()) >= ONE_ATMOSPHERE * 0.85f)
            return TRUE;
        return FALSE;
    }

    ///Slipping but in the air?
    // tg: code/modules/surgery/organs/external/wings/wings.dm:186
    public virtual async DmTask<object> fly_slip(mob_living_carbon_human human = null)
    {
        obj buckled_obj = null;
        if (human.buckled != null)
            buckled_obj = human.buckled as obj;

        to_chat(human, span_notice(I18n.T("Your wings spazz out and launch you!")));

        playsound(human.loc as atom, "sound/misc/slip.ogg", 50, TRUE, -3);

        foreach (obj_item choking_hazard in Dm.each_cast<obj_item>(human.held_items.Copy()))
            await human.accident(choking_hazard);

        float olddir = human.dir;

        await human.stop_pulling();
        if (buckled_obj != null)
        {
            await buckled_obj.unbuckle_mob(human);
            await step(buckled_obj, olddir);
        }
        else
            await human.AddComponent(typeof(datum_component_force_move), get_ranged_target_turf(human, olddir, 4), TRUE);
        return TRUE;
    }

    ///UNSAFE PROC, should only be called through the Activate or other sources that check for CanFly
    // tg: code/modules/surgery/organs/external/wings/wings.dm:209
    public virtual async DmTask toggle_flight(mob_living_carbon_human human = null)
    {
        if (!HAS_TRAIT_FROM(human, TRAIT_MOVE_FLOATING, SPECIES_FLIGHT_TRAIT))
        {
            MODIFY_PHYSIOLOGY(human, PHYS_COEFF_STUN, 2);
            human.add_traits(Dm.list(TRAIT_MOVE_FLOATING, TRAIT_IGNORING_GRAVITY, TRAIT_NOGRAV_ALWAYS_DRIFT), SPECIES_FLIGHT_TRAIT);
            await human.add_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_wings));
            await human.AddElement(typeof(datum_element_forced_gravity), 0);
            ADD_TRAIT(human, TRAIT_PASSTABLE, SPECIES_FLIGHT_TRAIT);
            await open_wings();
            to_chat(human, span_notice(I18n.T("You beat your wings and begin to hover gently above the ground...")));
            await human.set_resting(FALSE, TRUE);
            await human.refresh_gravity();
            RegisterSignal(human, COMSIG_HUMAN_SPEC_STUN, nameof(on_spec_stun));
            return;
        }

        MODIFY_PHYSIOLOGY(human, PHYS_COEFF_STUN, 0.5f);
        human.remove_traits(Dm.list(TRAIT_MOVE_FLOATING, TRAIT_IGNORING_GRAVITY, TRAIT_NOGRAV_ALWAYS_DRIFT), SPECIES_FLIGHT_TRAIT);
        human.remove_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_wings));
        human.RemoveElement(typeof(datum_element_forced_gravity), 0);
        REMOVE_TRAIT(human, TRAIT_PASSTABLE, SPECIES_FLIGHT_TRAIT);
        to_chat(human, span_notice(I18n.T("You settle gently back onto the ground...")));
        await close_wings();
        await human.refresh_gravity();
        UnregisterSignal(human, COMSIG_HUMAN_SPEC_STUN);
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:232
    [SignalHandler]
    public virtual void on_spec_stun(datum source = null, DmList amount = null)
    {
        if (owner.buckled != null)
            return;
        Waiting.NoSleep(toggle_flight(owner as mob_living_carbon_human));
        Waiting.Detach(fly_slip(owner as mob_living_carbon_human));
    }

    ///Spread wings. Activate Jetpack component
    // tg: code/modules/surgery/organs/external/wings/wings.dm:239
    public virtual async DmTask open_wings()
    {
        wings_open = TRUE;
        if (Dm.truthy(has_open_sprite))
        {
            var overlay = bodypart_overlay as datum_bodypart_overlay_mutant_wings;
            overlay?.open_wings();
            await owner.update_body_parts();
        }
        SEND_SIGNAL(COMSIG_WINGS_OPENED, Dm.list(this, owner));
    }

    ///Close wings. Deactivate jetpack component
    // tg: code/modules/surgery/organs/external/wings/wings.dm:248
    public virtual async DmTask close_wings()
    {
        wings_open = FALSE;
        if (Dm.truthy(has_open_sprite))
        {
            var overlay = bodypart_overlay as datum_bodypart_overlay_mutant_wings;
            overlay?.close_wings();
            await owner.update_body_parts();
        }
        if (isturf(owner?.loc))
        {
            // tg types the organ's own loc (its limb) as the turf; Entered tells it the organ came in
            atom location = loc as atom;
            await location.Entered(this, null); // tg passes NONE (0) as the old loc
        }
        SEND_SIGNAL(COMSIG_WINGS_CLOSED, Dm.list(this, owner));
    }
}

///hud action for starting and stopping flight
// tg: code/modules/surgery/organs/external/wings/wings.dm:261
public partial class datum_action_innate_flight : datum_action_innate
{
    // tg: code/modules/surgery/organs/external/wings/wings.dm:268
    public override async DmTask<object> Activate()
    {
        var human = owner as mob_living_carbon_human;
        var wings = human.get_organ_slot(ORGAN_SLOT_EXTERNAL_WINGS) as obj_item_organ_wings;
        if (wings != null && Dm.truthy(wings.can_fly()))
            await wings.toggle_flight(human);
        return null;
    }
}

///Bodypart overlay of default wings. Does not have any wing functionality
// tg: code/modules/surgery/organs/external/wings/wings.dm:274
public partial class datum_bodypart_overlay_mutant_wings : datum_bodypart_overlay_mutant
{
    // tg: code/modules/surgery/organs/external/wings/wings.dm:286
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, slot_blocker)) ? TRUE : FALSE;
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:289
    public override object override_color(obj_item_bodypart bodypart_owner = null)
    {
        if (bodypart_owner.owner?.get_organ_by_type(typeof(obj_item_organ_tail_fish_cerulean)) != null)
            return bodypart_owner.owner.dna.features[FEATURE_TAIL_FISH_COLOR];
        return bodypart_owner.draw_color;
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:294
    public virtual void open_wings()
    {
        return;
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:297
    public virtual void close_wings()
    {
        return;
    }
}

///Bodypart overlay of functional wings, including open and close sprite functionality!
// tg: code/modules/surgery/organs/external/wings/wings.dm:301
public partial class datum_bodypart_overlay_mutant_wings_functional : datum_bodypart_overlay_mutant_wings
{
    // tg: code/modules/surgery/organs/external/wings/wings.dm:306
    public override object get_global_feature_list()
    {
        if (Dm.truthy(wings_open))
            return SSaccessories.feature_list[FEATURE_WINGS_OPEN];
        return base.get_global_feature_list();
    }

    ///Update our wingsprite to the open wings variant
    // tg: code/modules/surgery/organs/external/wings/wings.dm:312
    public override void open_wings()
    {
        wings_open = TRUE;
        feature_key = FEATURE_WINGS_OPEN;
        set_appearance_from_name(sprite_datum.name); //It'll look for the same name again, but this time from the open wings list
    }

    ///Update our wingsprite to the closed wings variant
    // tg: code/modules/surgery/organs/external/wings/wings.dm:318
    public override void close_wings()
    {
        wings_open = FALSE;
        feature_key = Dm.initial(this, (datum_bodypart_overlay_mutant p) => p.feature_key);
        set_appearance_from_name(sprite_datum.name);
    }

    // tg: code/modules/surgery/organs/external/wings/wings.dm:323
    public override object icon_render_key(obj_item_bodypart limb = null)
    {
        object dot = base.icon_render_key(limb);
        dot = Dm.add(dot, Dm.truthy(wings_open) ? "open" : "closed");
        return dot;
    }
}

///angel wings, which relate to humans. comes with holiness.
// tg: code/modules/surgery/organs/external/wings/wings.dm:327
[DmPath("/obj/item/organ/wings/angel")]
public partial class obj_item_organ_wings_angel : obj_item_organ_wings
{
}
