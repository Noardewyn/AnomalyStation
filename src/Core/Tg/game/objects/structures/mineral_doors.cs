// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/mineral_doors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole. The mineral doors built from sheets (iron, silver, gold, uranium, sandstone, plasma, diamond, wood,
// paper frames): a mob with a client and free hands, a cyborg next to it or a mech opens and shuts one with tg's sound, its opening
// and closing flicks and a second's swing, it blocks air and sight while shut (the plasma and diamond ones are see-through), the
// wrench secures it; dug apart with a pickaxe, welded apart, the wooden and paper ones pried apart and set alight by a hot item, the
// paper frame patched with paper. The var block is generated (D111).

//NOT using the existing /obj/machinery/door type, since that has some complications on its own, mainly based on its
//machineryness

// tg: code/game/objects/structures/mineral_doors.dm:4
public partial class obj_structure_mineral_door : obj_structure
{
    // tg: code/game/objects/structures/mineral_doors.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_item_stack initialized_mineral = Dm.@new(sheetType) as obj_item_stack; // Okay this kinda sucks.
        Waiting.NoSleep(set_custom_materials(initialized_mineral.mats_per_unit, sheetAmount));
        qdel(initialized_mineral);
        air_update_turf(TRUE, TRUE);
        return dot;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:45
    public override object Destroy(float? force = null)
    {
        if (!Dm.truthy(door_opened))
            air_update_turf(TRUE, FALSE);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/mineral_doors.dm:50
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        turf T = loc as turf;
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        if (!Dm.truthy(door_opened))
            move_update_air(T);
        return dot;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:56
    public override object Bumped(atom_movable bumped_atom = null)
    {
        base.Bumped(bumped_atom);
        if (!Dm.truthy(door_opened))
        {
            Waiting.Detach(TryToSwitchState(bumped_atom));
            return null;
        }
        return null;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:61
    public override async DmTask<object> attack_ai(mob user = null) //those aren't machinery, they're just big fucking slabs of a mineral
    {
        if (isAI(user)) //so the AI can't open it
            return null;
        else if (iscyborg(user)) //but cyborgs can
        {
            if (get_dist(user, this) <= 1) //not remotely though
                return await TryToSwitchState(user);
        }
        return null;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:68
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/mineral_doors.dm:71
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        return await TryToSwitchState(user);
    }

    // tg: code/game/objects/structures/mineral_doors.dm:77
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (mover is obj_effect_beam)
            return !Dm.truthy(opacity) ? TRUE : FALSE;
        return dot;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:82
    public virtual async DmTask<object> TryToSwitchState(atom user = null)
    {
        if (Dm.truthy(isSwitchingStates) || !Dm.truthy(anchored))
            return null;
        if (user is mob_living matters)
        {
            if (matters.client != null)
            {
                if (matters is mob_living_carbon carbon_user)
                {
                    if (carbon_user.handcuffed == null)
                        await SwitchState();
                }
                else
                    await SwitchState();
            }
        }
        else if (ismecha(user))
            await SwitchState();
        return null;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:97
    public virtual async DmTask SwitchState()
    {
        if (Dm.truthy(door_opened))
            await Close();
        else
            await Open();
    }

    // tg: code/game/objects/structures/mineral_doors.dm:103
    public virtual async DmTask Open()
    {
        isSwitchingStates = TRUE;
        playsound(this, openSound, 100, TRUE);
        await set_opacity(FALSE);
        flick(Dm.text("[]opening", Dm.initial(this, nameof(icon_state))), this);
        await sleep(1 * SECONDS);
        set_density(FALSE);
        door_opened = TRUE;
        layer = OPEN_DOOR_LAYER;
        air_update_turf(TRUE, FALSE);
        update_appearance();
        isSwitchingStates = FALSE;

        if (close_delay != -1)
            _addtimer(Dm.@new<datum_callback>(this, nameof(Close)), close_delay, file: "code/game/objects/structures/mineral_doors.dm", line: 117);
    }

    // tg: code/game/objects/structures/mineral_doors.dm:119
    public virtual async DmTask Close()
    {
        if (Dm.truthy(isSwitchingStates) || !Dm.truthy(door_opened))
            return;
        turf T = get_turf(this);
        foreach (mob_living L in Dm.each<mob_living>(T.contents))
            return;
        isSwitchingStates = TRUE;
        playsound(this, closeSound, 100, TRUE);
        flick(Dm.text("[]closing", Dm.initial(this, nameof(icon_state))), this);
        await sleep(1 * SECONDS);
        set_density(TRUE);
        await set_opacity(TRUE);
        door_opened = FALSE;
        layer = Dm.num(Dm.initial(this, nameof(layer)));
        air_update_turf(TRUE, TRUE);
        update_appearance();
        isSwitchingStates = FALSE;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:137
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", Dm.initial(this, nameof(icon_state)), Dm.truthy(door_opened) ? "open" : "");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/mineral_doors.dm:141
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(await pickaxe_door(user, tool)))
            return ITEM_INTERACT_SUCCESS;

        if (!Dm.truthy(user.combat_mode))
            return Dm.truthy(await attack_hand(user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;

        return NONE;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:150
    public override async DmTask<object> set_anchored(object anchorvalue = null) //called in default_unfasten_wrench() chain
    {
        object dot = await base.set_anchored(anchorvalue);
        await set_opacity(Dm.truthy(anchored) ? (Dm.truthy(door_opened) ? FALSE : TRUE) : FALSE);
        air_update_turf(TRUE, Dm.num(anchorvalue));
        return dot;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:155
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 4 * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }


    /////////////////////// TOOL OVERRIDES ///////////////////////


    // tg: code/game/objects/structures/mineral_doors.dm:164
    public virtual async DmTask<object> pickaxe_door(mob_living user = null, obj_item I = null) //override if the door isn't supposed to be a minable mineral.
    {
        if (user == null)
            return null;
        if (!Dm.eq(I.tool_behaviour, TOOL_MINING))
            return null;
        object dot = TRUE;
        to_chat(user, span_notice(I18n.T("You start digging []...", this)));
        if (Dm.truthy(await I.use_tool(this, user, 40, volume: 50)))
        {
            to_chat(user, span_notice(I18n.T("You finish digging.")));
            await deconstruct(TRUE);
        }
        return dot;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:175
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null) //override if the door is supposed to be flammable.
    {
        await base.welder_act(user, tool);
        object dot = TRUE;
        if (Dm.truthy(anchored))
        {
            to_chat(user, span_warning(I18n.T("[] is still firmly secured to the ground!", this)));
            return dot;
        }

        user.visible_message(span_notice(I18n.T("[] starts to weld apart []!", user, this)), span_notice(I18n.T("You start welding apart [].", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 60, 5, 50)))
        {
            to_chat(user, span_warning(I18n.T("You failed to weld apart []!", this)));
            return dot;
        }

        user.visible_message(span_notice(I18n.T("[] welded [] into pieces!", user, this)), span_notice(I18n.T("You welded apart []!", this)));
        await deconstruct(TRUE);
        return dot;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:190
    public virtual async DmTask<object> crowbar_door(mob_living user = null, obj_item I = null) //if the door is flammable, call this in crowbar_act() so we can still decon it
    {
        object dot = TRUE;
        if (Dm.truthy(anchored))
        {
            to_chat(user, span_warning(I18n.T("[] is still firmly secured to the ground!", this)));
            return dot;
        }

        user.visible_message(span_notice(I18n.T("[] starts to pry apart []!", user, this)), span_notice(I18n.T("You start prying apart [].", this)));
        if (!Dm.truthy(await I.use_tool(this, user, 60, volume: 50)))
        {
            to_chat(user, span_warning(I18n.T("You failed to pry apart []!", this)));
            return dot;
        }

        user.visible_message(span_notice(I18n.T("[] pried [] into pieces!", user, this)), span_notice(I18n.T("You pried apart []!", this)));
        await deconstruct(TRUE);
        return dot;
    }


    /////////////////////// END TOOL OVERRIDES ///////////////////////


    // tg: code/game/objects/structures/mineral_doors.dm:208
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        turf T = get_turf(this);
        if (Dm.truthy(disassembled))
            Dm.@new(sheetType, T, sheetAmount);
        else
            Dm.@new(sheetType, T, Math.Max(sheetAmount - 2, 1));
    }
}

// tg: code/game/objects/structures/mineral_doors.dm:246
public partial class obj_structure_mineral_door_transparent : obj_structure_mineral_door
{
    // tg: code/game/objects/structures/mineral_doors.dm:250
    public override async DmTask Close()
    {
        await base.Close();
        await set_opacity(FALSE);
    }
}

// tg: code/game/objects/structures/mineral_doors.dm:266
public partial class obj_structure_mineral_door_wood : obj_structure_mineral_door
{
    // tg: code/game/objects/structures/mineral_doors.dm:276
    public override async DmTask<object> pickaxe_door(mob_living user = null, obj_item I = null)
    {
        return null;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:279
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:282
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await crowbar_door(user, tool);
    }

    // tg: code/game/objects/structures/mineral_doors.dm:285
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
        {
            await fire_act(tool.get_temperature());
            return ITEM_INTERACT_SUCCESS;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }
}

// tg: code/game/objects/structures/mineral_doors.dm:292
public partial class obj_structure_mineral_door_paperframe : obj_structure_mineral_door
{
    // tg: code/game/objects/structures/mineral_doors.dm:302
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH_NEIGHBORS(this);
        return dot;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:307
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        if (Dm.num(atom_integrity) < max_integrity)
            Dm.append(dot, span_info(I18n.T("It looks a bit damaged, you may be able to fix it with some <b>paper</b>.")));
        return dot;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:312
    public override async DmTask<object> pickaxe_door(mob_living user = null, obj_item I = null)
    {
        return null;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:315
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }

    // tg: code/game/objects/structures/mineral_doors.dm:318
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await crowbar_door(user, tool);
    }

    // tg: code/game/objects/structures/mineral_doors.dm:321
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST) //BURN IT ALL DOWN JIM
        {
            await fire_act(tool.get_temperature());
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(user.combat_mode) && tool is obj_item_paper && (Dm.num(atom_integrity) < max_integrity))
        {
            user.visible_message(span_notice(I18n.T("[] starts to patch the holes in [].", user, this)), span_notice(I18n.T("You start patching some of the holes in []!", this)));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
                return ITEM_INTERACT_BLOCKING;
            atom_integrity = Math.Min(Dm.num(atom_integrity) + 4, max_integrity);
            qdel(tool);
            user.visible_message(span_notice(I18n.T("[] patches some of the holes in [].", user, this)), span_notice(I18n.T("You patch some of the holes in []!", this)));
            return ITEM_INTERACT_SUCCESS;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/objects/structures/mineral_doors.dm:337
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH_NEIGHBORS(this);
        return base.Destroy(force);
    }
}
