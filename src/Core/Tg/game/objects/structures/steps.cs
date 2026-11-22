// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/steps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole. The small steps: walked onto a table or a platform next to them (climb_walkable), rotated, wrenched
// and taken apart. Their trip (on_enter: a runner from the side they face is knocked down for a second) is translated, but as in tg it
// never runs: climb_walkable's connect_loc, added after the steps' own, registers on_climb_enter for the same turf signal with
// override = TRUE (datums/elements/connect_loc.dm:32), which replaces it. The var block is generated (D111).

/// Short stairs you can use to climb tables quickly
// tg: code/game/objects/structures/steps.dm:2
public partial class obj_structure_steps : obj_structure
{
    // tg: code/game/objects/structures/steps.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        DmList loc_connections = TgState.Current.obj_structure_steps__Initialize__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_enter)));

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_climb_walkable)));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/steps.dm:23
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);
        if (held_item == null)
            return NONE;

        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(anchored) ? "Unsecure" : "Secure";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return null;
    }

    // tg: code/game/objects/structures/steps.dm:36
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/steps.dm:40
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(I18n.T("You start disassembling []...", this)));
        if (Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
            await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/steps.dm:46
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 2);
    }

    /// Watch your ankles
    // tg: code/game/objects/structures/steps.dm:50
    [SignalHandler]
    public virtual void on_enter(turf our_turf = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (!(arrived is mob_living arrived_mob) || !(old_loc is turf) || !Dm.truthy(Waiting.NoSleep(our_turf.Adjacent(old_loc))) || !Dm.truthy(has_gravity()) || HAS_TRAIT(arrived_mob, TRAIT_MOB_ELEVATED) || Dm.truthy(Dm.bitand(arrived_mob.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)) || !Dm.eq(arrived_mob.move_intent, MOVE_INTENT_RUN))
            return;
        object entered_dir = get_dir(our_turf, old_loc);
        if (Dm.eq(entered_dir, dir))
        {
            Waiting.NoSleep(arrived_mob.Knockdown(1 * SECONDS));
            to_chat(arrived_mob, span_warning(I18n.T("You tripped over \\the []!", this)));
        }
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/steps.dm:14  /obj/structure/steps/Initialize's var/static/list/loc_connections
    internal DmList obj_structure_steps__Initialize__loc_connections;
}
