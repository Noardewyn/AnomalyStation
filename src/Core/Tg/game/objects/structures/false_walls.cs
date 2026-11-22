// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/false_walls.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M5 slice 4: a false wall is a girder wall recipe, sheets on a displaced girder): /obj/structure/falsewall and
// /obj/structure/falsewall/reinforced whole, so that the var-only mineral false walls are live too. Generated live-partial
// stubs (docs/ai/specs/live-partial.txt, "M5 slice 4"): mouse_drop_receive (leaning, M9). Gaps ST5 (D333): get_save_vars, the uranium
// false wall's radiation (a pulse when touched or hit, passed along by the walls next to it, once in 1.5 seconds) and the material
// false wall (its sheets, its name from its materials, see-through while a material is translucent, the girder underlay). The var
// blocks are generated.

/*
 * False Walls
 */
// tg: code/game/objects/structures/false_walls.dm:4
public partial class obj_structure_falsewall : obj_structure
{
    // tg: code/game/objects/structures/false_walls.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // minerals are only applied to fake mineral walls
        // ...yes, real iron walls are not actually made of iron
        if (ispath(walltype, typeof(turf_closed_wall_mineral)))
        {
            obj_item_stack initialized_mineral = Dm.@new(mineral) as obj_item_stack; // Okay this kinda sucks.
            Waiting.NoSleep(set_custom_materials(initialized_mineral.mats_per_unit, mineral_amount));
            qdel(initialized_mineral);
        }

        air_update_turf(TRUE, TRUE);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/false_walls.dm:46
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(opening))
            return null;
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(density))
        {
            foreach (mob_living obstacle in Dm.each<mob_living>(get_turf(this))) //Stop people from using this as a shield
                return dot;
        }

        opening = TRUE;
        update_appearance();
        _addtimer(Dm.@new<datum_callback>(this, nameof(toggle_open)), 0.5f * SECONDS, file: "code/game/objects/structures/false_walls.dm", line: 59);
        return dot;
    }

    // tg: code/game/objects/structures/false_walls.dm:29  (gaps ST5)
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.remove(ref dot, nameof(icon));
        return dot;
    }

    // tg: code/game/objects/structures/false_walls.dm:61
    public virtual async DmTask toggle_open()
    {
        if (!QDELETED(this))
        {
            set_density(Dm.truthy(density) ? FALSE : TRUE);
            await set_opacity(density);
            opening = FALSE;
            update_appearance();
            air_update_turf(TRUE, Dm.truthy(density) ? FALSE : TRUE);
        }
    }

    //Calling icon_update will refresh the smoothwalls if it's closed, otherwise it will make sure the icon is correct if it's open
    // tg: code/game/objects/structures/false_walls.dm:69
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        object dot = await base.update_icon(updates, __extra);
        if (!Dm.truthy(density) || !Dm.truthy(Dm.bitand(updates, UPDATE_SMOOTHING)))
            return dot;

        if (Dm.truthy(opening))
            smoothing_flags = NONE;
        else
        {
            smoothing_flags = Dm.num(Dm.bitor(SMOOTH_BITMASK, SMOOTH_OBJ));
            QUEUE_SMOOTH(this);
        }
        return dot;
    }

    // tg: code/game/objects/structures/false_walls.dm:80
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(opening))
        {
            icon = Dm.initial(this, (atom p) => p.icon);
            icon_state = Dm.text("[]-[]", base_icon_state, Dm.truthy(density) ? "opening" : "closing");
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(density))
        {
            icon = fake_icon;
            icon_state = Dm.text("[]-[]", base_icon_state, smoothing_junction);
        }
        else
        {
            icon = Dm.initial(this, (atom p) => p.icon);
            icon_state = Dm.text("[]-open", base_icon_state);
        }
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/false_walls.dm:94
    public virtual async DmTask<object> ChangeToWall(float? delete = null)
    {
        delete ??= 1;
        turf T = get_turf(this);
        // TURF-IDENTITY: tg returns T, which names the turf here, the new wall
        object new_turf = await T.place_on_top(walltype);
        if (Dm.truthy(delete))
            qdel(this);
        return new_turf ?? T;
    }

    // tg: code/game/objects/structures/false_walls.dm:100
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!Dm.truthy(opening) || !Dm.truthy(tool.tool_behaviour))
            return await base.tool_act(user, tool, modifiers);
        to_chat(user, span_warning(I18n.T("You must wait until the door has stopped moving!")));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/structures/false_walls.dm:106
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(density))
        {
            to_chat(user, span_warning(I18n.T("You can't reach, close it first!")));
            return null;
        }
        turf loc_turf = get_turf(this);
        if (Dm.truthy(loc_turf.density))
        {
            to_chat(user, span_warning(I18n.T("[] is blocked!", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        if (!isfloorturf(loc_turf))
        {
            to_chat(user, span_warning(I18n.T("[] bolts must be tightened on the floor!", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        user.visible_message(span_notice(I18n.T("[] tightens some bolts on the wall.", user)), span_notice(I18n.T("You tighten the bolts on the wall.")));
        await ChangeToWall();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/false_walls.dm:122
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        await dismantle(user, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/false_walls.dm:128
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(opening))
        {
            to_chat(user, span_warning(I18n.T("You must wait until the door has stopped moving!")));
            return ITEM_INTERACT_BLOCKING; // honest to god no idea what the point of this blocker is, I'm just the messenger
        }
        return NONE;
    }

    // tg: code/game/objects/structures/false_walls.dm:134
    public virtual async DmTask dismantle(mob user = null, float? disassembled = null, obj_item tool = null)
    {
        disassembled ??= TRUE;
        user.visible_message(span_notice(I18n.T("[] dismantles the false wall.", user)), span_notice(I18n.T("You dismantle the false wall.")));
        if (Dm.truthy(tool))
            tool.play_tool_sound(this, 100);
        else
            playsound(this, "sound/items/tools/welder.ogg", 100, TRUE);
        await deconstruct(disassembled);
    }

    // tg: code/game/objects/structures/false_walls.dm:142
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

    // tg: code/game/objects/structures/false_walls.dm:149
    public override object get_dumping_location()
    {
        return null;
    }

    // tg: code/game/objects/structures/false_walls.dm:152
    public override object examine_descriptor(mob user = null)
    {
        return "wall";
    }

    //So you can't detect falsewalls by examine.
    // tg: code/game/objects/structures/false_walls.dm:155
    public override object examine_status(mob user = null)
    {
        return span_notice(I18n.T("The outer plating is <b>welded</b> firmly in place."));
    }
}

/*
 * False R-Walls
 */
// tg: code/game/objects/structures/false_walls.dm:165
public partial class obj_structure_falsewall_reinforced : obj_structure_falsewall
{
    // tg: code/game/objects/structures/false_walls.dm:176
    public override object examine_status(mob user = null)
    {
        return span_notice(I18n.T("The outer <b>grille</b> is fully intact."));
    }

    // tg: code/game/objects/structures/false_walls.dm:179
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        await dismantle(user, TRUE, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/false_walls.dm:183
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        return NONE;
    }
}

// tg: code/game/objects/structures/false_walls.dm:191  (gaps ST5)
public partial class obj_structure_falsewall_uranium : obj_structure_falsewall
{
    // tg: code/game/objects/structures/false_walls.dm:208
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ATOM_PROPAGATE_RAD_PULSE, nameof(radiate));
        return dot;
    }

    // tg: code/game/objects/structures/false_walls.dm:212
    public override async DmTask<object> attackby(obj_item W = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        radiate();
        return await base.attackby(W, user, modifiers, attack_modifiers);
    }

    // tg: code/game/objects/structures/false_walls.dm:216
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        radiate();
        return await base.attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/false_walls.dm:220
    [SignalHandler]
    public virtual void radiate()
    {
        if (Dm.truthy(active))
            return;
        if (Dm.num(world.time) <= last_event + 1.5f * SECONDS)
            return;
        active = TRUE;
        radiation_pulse(
            this,
            max_range: 3,
            threshold: RAD_LIGHT_INSULATION,
            chance: URANIUM_IRRADIATION_CHANCE,
            minimum_exposure_time: URANIUM_RADIATION_MINIMUM_EXPOSURE_TIME);
        propagate_radiation_pulse();
        last_event = Dm.num(world.time);
        active = FALSE;
    }
}

// tg: code/game/objects/structures/false_walls.dm:429  (gaps ST5)
public partial class obj_structure_falsewall_material : obj_structure_falsewall
{
    // tg: code/game/objects/structures/false_walls.dm:441
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (Dm.truthy(disassembled))
            Dm.@new(girder_type, loc);
        foreach (datum_material material_datum in Dm.each<datum_material>(custom_materials))
            Dm.@new(material_datum.sheet_type, loc, FLOOR(Dm.num(custom_materials[material_datum]) / SHEET_MATERIAL_AMOUNT, 1));
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/false_walls.dm:448
    public override async DmTask finalize_material_effects(DmList materials = null)
    {
        await base.finalize_material_effects(materials);
        desc = I18n.T("A huge chunk of [] used to separate rooms.", get_material_english_list(materials));
    }

    // tg: code/game/objects/structures/false_walls.dm:452
    public override async DmTask toggle_open()
    {
        if (!QDELETED(this))
        {
            set_density(Dm.truthy(density) ? FALSE : TRUE);
            float mat_opacity = TRUE;
            foreach (datum_material mat in Dm.each<datum_material>(custom_materials))
            {
                if (Dm.num(mat.alpha) < 255)
                {
                    mat_opacity = FALSE;
                    break;
                }
            }
            await set_opacity(Dm.num(Dm.truthy(density) && Dm.truthy(mat_opacity)));
            opening = FALSE;
            update_appearance();
            air_update_turf(TRUE, Dm.truthy(density) ? FALSE : TRUE);
        }
    }

    // tg: code/game/objects/structures/false_walls.dm:465
    public override async DmTask<object> ChangeToWall(float? delete = null)
    {
        delete ??= 1;
        turf current_turf = get_turf(this);
        // TURF-IDENTITY: tg's current_turf and new_wall name the same turf, the new wall
        turf_closed_wall_material new_wall = await current_turf.place_on_top(typeof(turf_closed_wall_material)) as turf_closed_wall_material;
        await new_wall.set_custom_materials(custom_materials);
        if (Dm.truthy(delete))
            qdel(this);
        return (object)new_wall ?? current_turf;
    }

    // tg: code/game/objects/structures/false_walls.dm:473
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        foreach (datum_material mat in Dm.each<datum_material>(custom_materials))
        {
            if (Dm.num(mat.alpha) < 255)
            {
                update_transparency_underlays();
                return dot;
            }
        }
        return dot;
    }

    // tg: code/game/objects/structures/false_walls.dm:480
    public virtual void update_transparency_underlays()
    {
        underlays.Cut();
        string girder_icon_state = "displaced";
        if (Dm.truthy(opening))
            girder_icon_state += Dm.text("_[]", Dm.truthy(density) ? "opening" : "closing");
        else if (!Dm.truthy(density))
            girder_icon_state += "_open";
        mutable_appearance girder_underlay = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/structures.dmi"), girder_icon_state, layer: LOW_OBJ_LAYER - 0.01f, appearance_flags: Dm.bitor(Dm.bitor(RESET_ALPHA, RESET_COLOR), KEEP_APART));
        Dm.append(underlays, girder_underlay);
    }
}
