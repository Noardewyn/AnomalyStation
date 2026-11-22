// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/showcase.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the mappers' fake objects of the display rooms, each drawn with tg's sprite of the thing it
// shows: the fake ID and security consoles with their screens, the old pods, the cyborg statue, the mech exhibits, the microwaves,
// the cloning pod, the TV, the wizard and the katana; unscrewed, unwrenched and crowbarred apart). The var blocks are generated (D111).

/*Completely generic structures for use by mappers to create fake objects, i.e. display rooms*/
// tg: code/game/objects/structures/showcase.dm:5
public partial class obj_structure_showcase : obj_structure
{
    // tg: code/game/objects/structures/showcase.dm:1-2 (the file's #defines, #undef'd at its end)
    internal const float SHOWCASE_CONSTRUCTED = 1;
    internal const float SHOWCASE_SCREWDRIVERED = 2;

    //Deconstructing
    //Showcases can be any sprite, so it makes sense that they can't be constructed.
    //However if a player wants to move an existing showcase or remove one, this is for that.

    // tg: code/game/objects/structures/showcase.dm:139
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored))
            return FALSE;
        if (deconstruction_state == SHOWCASE_SCREWDRIVERED)
        {
            to_chat(user, span_notice(I18n.T("You screw the screws back into the showcase.")));
            tool.play_tool_sound(this, 100);
            deconstruction_state = SHOWCASE_CONSTRUCTED;
        }
        else if (deconstruction_state == SHOWCASE_CONSTRUCTED)
        {
            to_chat(user, span_notice(I18n.T("You unscrew the screws.")));
            tool.play_tool_sound(this, 100);
            deconstruction_state = SHOWCASE_SCREWDRIVERED;
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/showcase.dm:152
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 100)))
            return null;
        to_chat(user, span_notice(I18n.T("You start to crowbar the showcase apart...")));
        Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 4);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/showcase.dm:160
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (deconstruction_state != SHOWCASE_CONSTRUCTED)
            return FALSE;
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    //Feedback is given in examine because showcases can basically have any sprite assigned to them

    // tg: code/game/objects/structures/showcase.dm:168
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (deconstruction_state == SHOWCASE_CONSTRUCTED)
            Dm.append(ref dot, I18n.T("It's fully constructed."));
        else if (deconstruction_state == SHOWCASE_SCREWDRIVERED)
            Dm.append(ref dot, I18n.T("It has its screws loosened."));
        else
            Dm.append(ref dot, I18n.T("If you see this, something is wrong."));
        return dot;
    }
}

// tg: code/game/objects/structures/showcase.dm:14
public partial class obj_structure_showcase_fakeid : obj_structure_showcase
{
    // tg: code/game/objects/structures/showcase.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_overlay("id");
        add_overlay("id_key");
        return dot;
    }
}

// tg: code/game/objects/structures/showcase.dm:25
public partial class obj_structure_showcase_fakesec : obj_structure_showcase
{
    // tg: code/game/objects/structures/showcase.dm:31
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, "security");
        Dm.append(ref dot, "security_key");
        return dot;
    }
}
