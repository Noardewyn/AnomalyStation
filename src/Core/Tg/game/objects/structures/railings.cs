// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/railings.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P4: /obj/structure/railing and its wooden fence are live. The init, the face blocking (CanPass, on_exit by dir and
// ON_BORDER_1) and the fence's layer are translated; M5 slice 7: the tools and atom_deconstruct; examine and CanAStarPass are
// generated live-partial stubs (docs/ai/specs/live-partial.txt's P4 section). The var blocks are generated.

// tg: code/game/objects/structures/railings.dm:1
public partial class obj_structure_railing : obj_structure
{
    // tg: code/game/objects/structures/railings.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(climbable))
            Waiting.NoSleep(AddElement(typeof(datum_element_climbable)));

        if (Dm.truthy(density) && Dm.truthy(Dm.bitand(flags_1, ON_BORDER_1))) // blocks normal movement from and to the direction it's facing.
            Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));

        Dm.Deferred("code/game/objects/structures/railings.dm:75", "Initialize: AddElement(/datum/element/contextual_screentip_tools, tool_behaviors): screentips (M10+)");

        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), ROTATION_NEEDS_ROOM)); // M5 slice 5: the element is translated
        return dot;
    }

    // tg: code/game/objects/structures/railings.dm:58  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_structure_railing__Initialize__loc_connections;

    // M5 slice 7: the railing's tools (the welder's repair, the wirecutters cut it apart, the wrench unfastens it) and its
    // deconstruction into rods.
    // tg: code/game/objects/structures/railings.dm:78
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.eq(anchored, TRUE))
            Dm.append(ref dot, span_notice(I18n.T("The railing is <b>bolted</b> to the floor.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The railing is <i>unbolted</i> from the floor and can be deconstructed with <b>wirecutters</b>.")));
        return dot;
    }

    // tg: code/game/objects/structures/railings.dm:85
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // tg: code/game/objects/structures/railings.dm:86
        add_fingerprint(user);
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/objects/structures/railings.dm:89
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        // tg: code/game/objects/structures/railings.dm:93
        add_fingerprint(user);
        if (Dm.eq(atom_integrity, max_integrity))
        {
            to_chat(user, span_warning(I18n.T("[] is already in good condition!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You begin repairing []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        atom_integrity = max_integrity;
        to_chat(user, span_notice(I18n.T("You repair [].", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/railings.dm:110
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item I = null)
    {
        if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
        {
            to_chat(user, span_warning(I18n.T("You try to cut apart the railing, but it's too hard!")));
            I.play_tool_sound(this, 100);
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_warning(I18n.T("You cut apart the railing.")));
        I.play_tool_sound(this, 100);
        await deconstruct();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/railings.dm:120
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        float rods_to_make = this is obj_structure_railing_corner ? 1 : 2;
        obj rod = Dm.@new(item_deconstruct, drop_location(), rods_to_make) as obj;
        // tg: code/game/objects/structures/railings.dm:123
        transfer_fingerprints_to(rod);
        _ = rod;
    }

    ///Implements behaviour that makes it possible to unanchor the railing.
    // tg: code/game/objects/structures/railings.dm:126
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item I = null)
    {
        object dot = await base.wrench_act(user, I);
        to_chat(user, span_notice(I18n.T("You begin to [] the floor...", Dm.truthy(anchored) ? "unfasten the railing from" : "fasten the railing to")));
        if (Dm.truthy(await I.use_tool(this, user, volume: 75, extra_checks: Dm.@new<datum_callback>(this, nameof(check_anchored), anchored))))
        {
            await set_anchored(Dm.truthy(anchored) ? FALSE : TRUE);
            to_chat(user, span_notice(I18n.T("You [] the floor.", Dm.truthy(anchored) ? "fasten the railing to" : "unfasten the railing from")));
        }
        _ = dot;
        return TRUE;
    }

    // tg: code/game/objects/structures/railings.dm:134
    public override async DmTask<object> CanPass(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanPass(mover, border_dir);
        if (Dm.truthy(Dm.bitand(border_dir, dir)))
            return (dot is var or__1 && Dm.truthy(or__1) ? or__1 : (mover.throwing is var or__2 && Dm.truthy(or__2) ? or__2 : Dm.bitand(mover.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)));
        return TRUE;
    }

    // tg: code/game/objects/structures/railings.dm:145
    [SignalHandler]
    public virtual object on_exit(datum source = null, atom_movable leaving = null, object direction = null)
    {
        if (leaving == this)
            return null; // Let's not block ourselves.

        if (!Dm.truthy(Dm.bitand(direction, dir)))
            return null;

        if (!Dm.truthy(density))
            return null;

        if (Dm.truthy(leaving.throwing))
            return null;

        if (Dm.truthy(Dm.bitand(leaving.movement_type, Dm.bitor(PHASING, MOVETYPES_NOT_TOUCHING_GROUND))))
            return null;

        if (Dm.ge(leaving.move_force, MOVE_FORCE_EXTREMELY_STRONG))
            return null;

        Waiting.NoSleep(leaving.Bump(this));
        return COMPONENT_ATOM_BLOCK_EXIT;
    }

    // tg: code/game/objects/structures/railings.dm:169
    public virtual object check_anchored(object checked_anchored = null)
    {
        if (Dm.eq(anchored, checked_anchored))
            return TRUE;
        return null;
    }
}

// tg: code/game/objects/structures/railings.dm:174
public partial class obj_structure_railing_wooden_fence : obj_structure_railing
{
    // tg: code/game/objects/structures/railings.dm:184
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ATOM_DIR_CHANGE, nameof(on_change_layer));
        adjust_dir_layer(dir);
        return dot;
    }

    // tg: code/game/objects/structures/railings.dm:189
    [SignalHandler]
    public virtual void on_change_layer(datum source = null, object old_dir = null, object new_dir = null)
    {
        adjust_dir_layer(new_dir);
    }

    // tg: code/game/objects/structures/railings.dm:193
    public virtual void adjust_dir_layer(object direction = null)
    {
        layer = (Dm.truthy(Dm.bitand(direction, NORTH)) ? MOB_LAYER : Dm.num(Dm.initial(this, (obj_structure_railing_wooden_fence p) => p.layer)));
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/railings.dm:58  var/static/list/loc_connections = list(COMSIG_ATOM_EXIT = PROC_REF(on_exit))
    internal DmList obj_structure_railing__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_EXIT, nameof(obj_structure_railing.on_exit)));
}

// Gaps package C1 (the AI's pathfinding and movement).
public partial class obj_structure_railing : obj_structure
{
    // tg: code/game/objects/structures/railings.dm:140
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        if (!Dm.truthy(Dm.bitand(to_dir, dir)))
            return TRUE;
        return base.CanAStarPass(to_dir, pass_info);
    }
}
