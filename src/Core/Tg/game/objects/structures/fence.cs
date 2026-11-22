// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/fence.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The chain link fence (Lavaland's and the gateway's): wirecutters cut a hole in ten seconds, a
// medium hole climbable, a large one walked through (the sprite shows the cut); its door opens and closes with a click. The var
// blocks are generated (D111).

//Chain link fences
//Sprites ported from /VG/

// tg: code/game/objects/structures/fence.dm:13
public partial class obj_structure_fence : obj_structure
{
    // tg: code/game/objects/structures/fence.dm:5
    internal const float CUT_TIME = 100;
    // tg: code/game/objects/structures/fence.dm:6
    internal const float CLIMB_TIME = 150;

    // tg: code/game/objects/structures/fence.dm:8
    internal const float NO_HOLE = 0; //section is intact
    // tg: code/game/objects/structures/fence.dm:9
    internal const float MEDIUM_HOLE = 1; //medium hole in the section - can climb through
    // tg: code/game/objects/structures/fence.dm:10
    internal const float LARGE_HOLE = 2; //large hole in the section - can walk through
    // tg: code/game/objects/structures/fence.dm:11
    internal const float MAX_HOLE_SIZE = LARGE_HOLE;

    // tg: code/game/objects/structures/fence.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_cut_status();
        return dot;
    }

    // tg: code/game/objects/structures/fence.dm:32
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        switch (hole_size)
        {
            case MEDIUM_HOLE:
                Dm.append(ref dot, I18n.T("There is a large hole in \\the [].", this));
                break;
            case LARGE_HOLE:
                Dm.append(ref dot, I18n.T("\\The [] has been completely cut through.", this));
                break;
        }
        return dot;
    }

    // tg: code/game/objects/structures/fence.dm:61
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(cuttable))
        {
            to_chat(user, span_warning(I18n.T("This section of the fence can't be cut!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(invulnerable))
        {
            to_chat(user, span_warning(I18n.T("This fence is too strong to cut through!")));
            return ITEM_INTERACT_BLOCKING;
        }

        float current_stage = hole_size;
        if (current_stage >= MAX_HOLE_SIZE)
        {
            to_chat(user, span_warning(I18n.T("This fence has too much cut out of it already!")));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_danger(I18n.T("\\The [] starts cutting through \\the [] with \\the [].", user, this, tool)),
            span_danger(I18n.T("You start cutting through \\the [] with \\the [].", this, tool)));

        if (!Dm.truthy(await tool.use_tool(this, user, CUT_TIME)))
            return ITEM_INTERACT_BLOCKING;
        if (current_stage != hole_size)
            return ITEM_INTERACT_BLOCKING;
        switch (++hole_size)
        {
            case MEDIUM_HOLE:
                visible_message(span_notice(I18n.T("\\The [] cuts into \\the [] some more.", user, this)));
                to_chat(user, span_info(I18n.T("You could probably fit yourself through that hole now. Although climbing through would be much faster if you made it even bigger.")));
                Waiting.NoSleep(AddElement(typeof(datum_element_climbable)));
                break;
            case LARGE_HOLE:
                visible_message(span_notice(I18n.T("\\The [] completely cuts through \\the [].", user, this)));
                to_chat(user, span_info(I18n.T("The hole in \\the [] is now big enough to walk through.", this)));
                RemoveElement(typeof(datum_element_climbable));
                break;
        }
        update_cut_status();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/fence.dm:94
    public virtual void update_cut_status()
    {
        if (!Dm.truthy(cuttable))
            return;
        float new_density = TRUE;
        switch (hole_size)
        {
            case NO_HOLE:
                icon_state = Dm.initial(this, (atom p) => p.icon_state);
                break;
            case MEDIUM_HOLE:
                icon_state = "straight_cut2";
                break;
            case LARGE_HOLE:
                icon_state = "straight_cut3";
                new_density = FALSE;
                break;
        }
        set_density(new_density);
    }
}

//FENCE DOORS

// tg: code/game/objects/structures/fence.dm:110
public partial class obj_structure_fence_door : obj_structure_fence
{
    // tg: code/game/objects/structures/fence.dm:116
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(update_icon_state());
        return dot;
    }

    // tg: code/game/objects/structures/fence.dm:125
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(can_open(user)))
            toggle(user);

        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/game/objects/structures/fence.dm:131
    public virtual void toggle(mob user = null)
    {
        visible_message(span_notice(I18n.T("\\The [] [] \\the [].", user, Dm.truthy(density) ? I18n.T("opens") : I18n.T("closes"), this)));
        set_density(Dm.truthy(density) ? FALSE : TRUE);
        Waiting.NoSleep(update_icon_state());
        playsound(this, Dm.resource("sound/machines/click.ogg"), 100, TRUE);
    }

    // tg: code/game/objects/structures/fence.dm:137
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(density) ? "door_closed" : "door_opened";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/fence.dm:141
    public virtual object can_open(mob user = null)
    {
        return TRUE;
    }
}
