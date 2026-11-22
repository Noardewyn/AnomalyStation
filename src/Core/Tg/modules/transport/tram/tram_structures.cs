// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/tram/tram_structures.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file but add_context (the screentips stay deferred, live-partial.txt).

// tg: code/modules/transport/tram/tram_structures.dm:12
public partial class obj_structure_grille_tram : obj_structure_grille
{
    // tg: code/modules/transport/tram/tram_structures.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RemoveElement(typeof(datum_element_atmos_sensitive), mapload);
        //atmos_sensitive applies connect_loc which 1. reacts to movement in order to 2. unregister and register signals to
        //the old and new locs. we dont want that, pretend these grilles and windows are plastic or something idk
        return dot;
    }
}

// tg: code/modules/transport/tram/tram_structures.dm:23
public partial class obj_structure_tram : obj_structure
{
    // tg: code/modules/transport/tram/tram_structures.dm:18 (the earlier of the type's two Initialize definitions, rules 2.5)
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RemoveElement(typeof(datum_element_atmos_sensitive), mapload);
        return dot;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:72
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_blocks_explosives)));
        object dot = Initialize__d0(mapload, __extra);
        obj_item_stack initialized_mineral = Dm.@new(mineral) as obj_item_stack;
        Waiting.NoSleep(set_custom_materials(initialized_mineral.mats_per_unit, mineral_amount));
        qdel(initialized_mineral);
        air_update_turf(TRUE, TRUE);
        register_context();
        return dot;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:81
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.eq(state, TRAM_SCREWED_TO_FRAME))
            Dm.append(ref dot, span_notice(I18n.T("The panel is [] to the frame. To dismantle use a []", EXAMINE_HINT("screwed"), EXAMINE_HINT("screwdriver."))));
        else if (Dm.eq(state, TRAM_IN_FRAME))
            Dm.append(ref dot, span_notice(I18n.T("The panel is [] but [] into the frame. To dismantle use a []", EXAMINE_HINT("unscrewed,"), EXAMINE_HINT("pried"), EXAMINE_HINT("crowbar."))));
        else if (Dm.eq(state, TRAM_OUT_OF_FRAME))
            Dm.append(ref dot, span_notice(I18n.T("The panel is [] out of the frame, but still[] To dismantle use []", EXAMINE_HINT("pried"), EXAMINE_HINT("wired."), EXAMINE_HINT("wirecutters."))));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:103
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        float ratio = Dm.num(atom_integrity) / Dm.num(max_integrity);
        ratio = ceil(ratio * 4) * 25;
        cut_overlay(damage_overlay);
        if (ratio > 75)
            return dot;

        damage_overlay = mutable_appearance(Dm.resource("icons/obj/structures.dmi"), Dm.text("damage[]", ratio), -(layer + 0.1f)) as mutable_appearance;
        Dm.append(ref dot, damage_overlay);
        return dot;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:114
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);

        if (!Dm.truthy((user as mob_living)?.combat_mode))
        {
            user.visible_message(span_notice(I18n.T("[] knocks on [].", user, this)), span_notice(I18n.T("You knock on [].", this)));
            playsound(this, knock_sound, 50, TRUE);
        }
        else
        {
            user.visible_message(span_warning(I18n.T("[] bashes []!", user, this)), span_warning(I18n.T("You bash []!", this)));
            playsound(this, bash_sound, 100, TRUE);
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:125
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("mode", RCD_DECONSTRUCT), Dm.kv("delay", 3 * SECONDS), Dm.kv("cost", 10));
        return FALSE;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:131
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_DECONSTRUCT))
        {
            qdel(this);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:138
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.truthy(dot)) //received damage
            update_appearance();
        return dot;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:143
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        await add_atom_colour(NARSIE_WINDOW_COLOUR, FIXED_COLOUR_PRIORITY);
        return null;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:146
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);

        if (Dm.num(current_size) >= STAGE_FIVE)
            await deconstruct(disassembled: FALSE);
        return null;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:152
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.num(atom_integrity) >= Dm.num(max_integrity))
        {
            to_chat(user, span_warning(I18n.T("[] is already in good condition!", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 0, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return FALSE;
        to_chat(user, span_notice(I18n.T("You begin repairing []...", this)));
        if (Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
        {
            atom_integrity = max_integrity;
            to_chat(user, span_notice(I18n.T("You repair [].", this)));
            update_appearance();
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:165
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.truthy(tool.tool_behaviour))
        {
            if (Dm.eq(state, TRAM_SCREWED_TO_FRAME))
            {
                to_chat(user, span_warning(I18n.T("The security screws need to be removed first!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.eq(state, TRAM_OUT_OF_FRAME))
            {
                to_chat(user, span_warning(I18n.T("The cabling need to be cut first!")));
                return ITEM_INTERACT_BLOCKING;
            }
        }
        return NONE;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:177
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.eq(state, TRAM_IN_FRAME))
        {
            user.visible_message(span_notice(I18n.T("[] wedges [] into the tram panel's gap in the frame and starts prying...", user, tool)), span_notice(I18n.T("You wedge [] into the tram panel's gap in the frame and start prying...", tool)));
            if (!Dm.truthy(await tool.use_tool(this, user, 1 * SECONDS, volume: 50)))
                return ITEM_INTERACT_BLOCKING;
            state = TRAM_OUT_OF_FRAME;
            to_chat(user, span_notice(I18n.T("The panel pops out of the frame, exposing some cabling that look like they can be cut.")));
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.eq(state, TRAM_OUT_OF_FRAME))
        {
            user.visible_message(span_notice(I18n.T("[] snaps the tram panel into place.", user)), span_notice(I18n.T("You snap the tram panel into place.")));
            state = TRAM_IN_FRAME;
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE; //Head back to item_interaction_secondary() to get the same message as other unsuitable tools
    }

    // tg: code/modules/transport/tram/tram_structures.dm:196
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.eq(state, TRAM_SCREWED_TO_FRAME))
        {
            user.visible_message(span_notice(I18n.T("[] begins to unscrew the tram panel from the frame...", user)), span_notice(I18n.T("You begin to unscrew the tram panel from the frame...")));
            if (!Dm.truthy(await tool.use_tool(this, user, 1 * SECONDS, volume: 50)))
                return ITEM_INTERACT_BLOCKING;
            state = TRAM_IN_FRAME;
            to_chat(user, span_notice(I18n.T("The screws come out, and a gap forms around the edge of the pane.")));
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.eq(state, TRAM_IN_FRAME))
        {
            user.visible_message(span_notice(I18n.T("[] resecures the tram panel to the frame...", user)), span_notice(I18n.T("You resecure the tram panel to the frame.")));
            state = TRAM_SCREWED_TO_FRAME;
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:215
    public override async DmTask<object> wirecutter_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(state, TRAM_OUT_OF_FRAME))
            return NONE;
        user.visible_message(span_notice(I18n.T("[] starts cutting the connective cabling on []...", user, this)), span_notice(I18n.T("You start cutting the connective cabling on []", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 1 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("The panels falls out of the way exposing the frame backing.")));
        await deconstruct(disassembled: TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:226
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (Dm.truthy(disassembled))
            Dm.@new(girder_type, loc);
        if (Dm.truthy(mineral_amount))
        {
            for (float i = 1; i <= mineral_amount; i++)
                Dm.@new(mineral, loc);
        }
    }

    // tg: code/modules/transport/tram/tram_structures.dm:455
    public override object get_dumping_location()
    {
        return null;
    }
}

// tg: code/modules/transport/tram/tram_structures.dm:332
public partial class obj_structure_tram_alt_uranium : obj_structure_tram_alt
{
    // tg: code/modules/transport/tram/tram_structures.dm:348
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        radiate();
        return await base.attackby(attacking_item, user, modifiers, attack_modifiers);
    }

    // tg: code/modules/transport/tram/tram_structures.dm:352
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        radiate();
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/transport/tram/tram_structures.dm:356
    public virtual void radiate()
    {
        if (Dm.truthy(active))
            return;
        if (world.time <= last_event + 1.5f * SECONDS)
            return;
        active = TRUE;
        radiation_pulse(
            this,
            max_range: 3,
            threshold: RAD_LIGHT_INSULATION,
            chance: URANIUM_IRRADIATION_CHANCE,
            minimum_exposure_time: URANIUM_RADIATION_MINIMUM_EXPOSURE_TIME);
        propagate_radiation_pulse();
        last_event = world.time;
        active = FALSE;
    }
}

// tg: code/modules/transport/tram/tram_structures.dm:391
public partial class obj_structure_tram_alt_wood : obj_structure_tram_alt
{
    // tg: code/modules/transport/tram/tram_structures.dm:404
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.get_sharpness()) || !Dm.truthy(tool.force))
            return NONE;
        float duration = ((4.8f * SECONDS) / Dm.num(tool.force)) * 2; //In seconds, for now.
        if (tool is obj_item_hatchet || tool is obj_item_fireaxe)
            duration /= 4; //Much better with hatchets and axes.
        to_chat(user, span_notice(I18n.T("You begin breaking down [].", this)));
        if (!Dm.truthy(await do_after(user, duration * (1 * SECONDS), target: this))) //Into deciseconds.
            return ITEM_INTERACT_BLOCKING;
        await deconstruct(disassembled: FALSE);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/transport/tram/tram_structures.dm:460
public partial class obj_structure_tram_spoiler : obj_structure_tram
{
    // tg: code/modules/transport/tram/tram_structures.dm:485
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:489
    public override void LateInitialize(params object[] __extra)
    {
        RegisterSignal(SStransport, COMSIG_TRANSPORT_UPDATED, nameof(set_spoiler));
    }

    // tg: code/modules/transport/tram/tram_structures.dm:503
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            Dm.append(ref dot, span_warning(I18n.T("The electronics panel is sparking occasionally. It can be reset with a []", EXAMINE_HINT("multitool."))));

        if (Dm.truthy(locked))
            Dm.append(ref dot, span_warning(I18n.T("The spoiler is [] in place!", EXAMINE_HINT("welded"))));
        else
            Dm.append(ref dot, span_notice(I18n.T("The spoiler can be locked in place with a []", EXAMINE_HINT("welder."))));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:513
    public virtual void set_spoiler(object source = null, object controller = null, object controller_active = null, object controller_status = null, object travel_direction = null)
    {
        float spoiler_direction = Dm.num(travel_direction);
        if (Dm.truthy(locked) || Dm.truthy(Dm.bitand(controller_status, COMM_ERROR)) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            if (!Dm.truthy(deployed))
            {
                // Bring out the blades
                if (Dm.truthy(locked))
                    visible_message(span_danger(I18n.T("[] locks up due to its servo overheating!", this)));
                do_sparks(3, cardinal_only: FALSE, source: this);
                deploy_spoiler();
            }
            return;
        }

        if (!Dm.truthy(controller_active))
            return;

        float facing = Dm.num(dir);
        if (spoiler_direction == SOUTH || spoiler_direction == EAST)
        {
            if (facing == NORTH || facing == EAST)
                retract_spoiler();
            else if (facing == SOUTH || facing == WEST)
                deploy_spoiler();
        }
        else if (spoiler_direction == NORTH || spoiler_direction == WEST)
        {
            if (facing == NORTH || facing == EAST)
                deploy_spoiler();
            else if (facing == SOUTH || facing == WEST)
                retract_spoiler();
        }
    }

    // tg: code/modules/transport/tram/tram_structures.dm:544
    public virtual void deploy_spoiler()
    {
        if (Dm.truthy(deployed))
            return;
        flick("tram-spoiler-deploying", this);
        icon_state = "tram-spoiler-deployed";
        deployed = TRUE;
        update_appearance();
    }

    // tg: code/modules/transport/tram/tram_structures.dm:552
    public virtual void retract_spoiler()
    {
        if (!Dm.truthy(deployed))
            return;
        flick("tram-spoiler-retracting", this);
        icon_state = "tram-spoiler-retracted";
        deployed = FALSE;
        update_appearance();
    }

    // tg: code/modules/transport/tram/tram_structures.dm:560
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return null;
        to_chat(user, span_warning(I18n.T("You short-circuit the []'s servo to overheat!", this)), type: MESSAGE_TYPE_INFO);
        playsound(this, SFX_SPARKS, 100, vary: TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        do_sparks(5, cardinal_only: FALSE, source: this);
        obj_flags = Dm.num(Dm.bitor(obj_flags, EMAGGED));
        return null;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:568
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return FALSE;

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, I18n.T("electronics reset!"));
            obj_flags = Dm.num(Dm.bitand(obj_flags, ~EMAGGED));
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:579
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return FALSE;

        if (Dm.num(atom_integrity) >= Dm.num(max_integrity))
        {
            to_chat(user, span_warning(I18n.T("You begin to weld [], [].", this, Dm.truthy(locked) ? I18n.T("repairing damage") : I18n.T("preventing retraction"))));
            if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
                return null;
            locked = Dm.num(!Dm.truthy(locked));
            user.visible_message(span_warning(Dm.truthy(locked) ? I18n.T("[] welds [] in place with [].", user, this, tool) : I18n.T("[] repairs [] with [].", user, this, tool)),
                span_warning(Dm.truthy(locked) ? I18n.T("You finish welding [], locking it in place.", this) : I18n.T("You finish welding [], it can move freely again!", this)), null, COMBAT_MESSAGE_RANGE);

            if (Dm.truthy(locked))
                deploy_spoiler();

            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        to_chat(user, span_notice(I18n.T("You begin repairing []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return null;
        atom_integrity = max_integrity;
        to_chat(user, span_notice(I18n.T("You repair [].", this)));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/transport/tram/tram_structures.dm:603
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(deployed) && Dm.truthy(locked))
            Dm.append(ref dot, mutable_appearance(icon, "tram-spoiler-welded"));
        return dot;
    }
}
