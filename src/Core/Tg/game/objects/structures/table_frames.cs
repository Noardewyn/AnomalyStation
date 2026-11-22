// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/table_frames.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 7 (furniture): the whole file. A table frame is wrenched apart into its rods, or takes one sheet of a stack that
// makes a table (the stack's table_type) and becomes that table. Nar'Sie's touch (a wooden frame) since gaps ST5.
// The var blocks are generated.

/*
 * Normal Frames
 */
// tg: code/game/objects/structures/table_frames.dm:13
public partial class obj_structure_table_frame : obj_structure
{
    // tg: code/game/objects/structures/table_frames.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/table_frames.dm:30
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            context[SCREENTIP_CONTEXT_RMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (isstack(held_item) && Dm.truthy(get_table_type(held_item as obj_item_stack)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Construct table";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return null;
    }

    // tg: code/game/objects/structures/table_frames.dm:42
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        balloon_alert(user, I18n.T("deconstructing..."));
        tool.play_tool_sound(this);
        if (!Dm.truthy(await tool.use_tool(this, user, 3 * SECONDS)))
            return ITEM_INTERACT_BLOCKING;
        playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/objects/structures/table_frames.dm:47
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/table_frames.dm:51
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await wrench_act(user, tool);
    }

    // tg: code/game/objects/structures/table_frames.dm:54
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!isstack(tool))
            return NONE;
        obj_item_stack our_stack = (obj_item_stack)tool;
        object table_type = get_table_type(our_stack);
        if (table_type == null)
            return NONE;

        if (Dm.num(our_stack.get_amount()) < 1)
        {
            balloon_alert(user, I18n.T("need more material!"));
            return ITEM_INTERACT_BLOCKING;
        }

        // Check if the turf is blocked by dense objects or objects that block construction
        foreach (obj @object in Dm.each<obj>(loc))
        {
            if (Dm.truthy(Dm.bitand(@object.pass_flags, PASSTABLE)))
                continue;
            if ((Dm.truthy(@object.density) && !Dm.truthy(Dm.bitand(@object.obj_flags, IGNORE_DENSITY))) || Dm.truthy(Dm.bitand(@object.obj_flags, BLOCKS_CONSTRUCTION)))
            {
                balloon_alert(user, I18n.T("[] is in the way!", @object.name));
                return ITEM_INTERACT_BLOCKING;
            }
        }

        balloon_alert(user, I18n.T("constructing table..."));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;

        // Check again after the delay in case something was placed during construction
        foreach (obj @object in Dm.each<obj>(loc))
        {
            if (Dm.truthy(Dm.bitand(@object.pass_flags, PASSTABLE)))
                continue;
            if ((Dm.truthy(@object.density) && !Dm.truthy(Dm.bitand(@object.obj_flags, IGNORE_DENSITY))) || Dm.truthy(Dm.bitand(@object.obj_flags, BLOCKS_CONSTRUCTION)))
            {
                balloon_alert(user, I18n.T("[] is in the way!", @object.name));
                return ITEM_INTERACT_BLOCKING;
            }
        }

        if (!Dm.truthy(await our_stack.use(1)))
            return ITEM_INTERACT_BLOCKING;

        Dm.@new((Type)table_type, loc, this, our_stack);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Gets the table type we make with our given stack.
    // tg: code/game/objects/structures/table_frames.dm:94
    public virtual object get_table_type(obj_item_stack our_stack = null)
    {
        return our_stack.get_table_type();
    }

    // tg: code/game/objects/structures/table_frames.dm:97
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        Dm.@new(framestack, get_turf(this), framestackamount);
    }
}

/*
 * Wooden Frames
 */
// tg: code/game/objects/structures/table_frames.dm:109
public partial class obj_structure_table_frame_wood : obj_structure_table_frame
{
    // tg: code/game/objects/structures/table_frames.dm:117
    public override object get_table_type(obj_item_stack our_stack = null)
    {
        if (istype(our_stack, typeof(obj_item_stack_sheet_mineral_wood)))
            return typeof(obj_structure_table_wood);
        if (istype(our_stack, typeof(obj_item_stack_tile_carpet)))
            return typeof(obj_structure_table_wood_poker);
        return null;
    }
}

// Gaps ST5 (D333): Nar'Sie's touch.
public partial class obj_structure_table_frame : obj_structure
{
    // tg: code/game/objects/structures/table_frames.dm:100
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        Dm.@new<obj_structure_table_frame_wood>(loc);
        qdel(this);
        await DmTask.CompletedTask;
        return null;
    }
}
