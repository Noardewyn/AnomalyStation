// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/theft_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate G3b): the traitor's nuke screwdriver, which the syndicate toolbox carries; N1a: the nuke core and its container. The supermatter sliver
// extraction tools come with the supermatter theft. The var blocks are generated (D111).

//snowflake screwdriver, works as a key to start nuke theft, traitor only
// tg: code/game/objects/items/tools/theft_tools.dm:86
public partial class obj_item_screwdriver_nuke : obj_item_screwdriver
{
    // tg: code/game/objects/items/tools/theft_tools.dm:101
    public override object get_belt_overlay()
    {
        return mutable_appearance(Dm.resource("icons/obj/clothing/belt_overlays.dmi"), "screwdriver_nuke");
    }
}

// STEALING THE NUKE (syndicate N1a, D316)

//the nuke core - objective item
// tg: code/game/objects/items/tools/theft_tools.dm:7
public partial class obj_item_nuke_core : obj_item
{
    // tg: code/game/objects/items/tools/theft_tools.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/items/tools/theft_tools.dm:22
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tools/theft_tools.dm:26
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_nuke_core_container container)
            return NONE;
        await container.load(this, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tools/theft_tools.dm:32
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (cooldown < world.time - 60)
        {
            cooldown = world.time;
            flick(pulseicon, this);
            radiation_pulse(get_turf(this) as atom, max_range: 2, threshold: RAD_EXTREME_INSULATION);
        }
        return null;
    }

    // tg: code/game/objects/items/tools/theft_tools.dm:38
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is rubbing [] against []self! It looks like [] trying to commit suicide!", user, this, user.p_them(), user.p_theyre())));
        return TOXLOSS;
    }
}

//nuke core box, for carrying the core
// tg: code/game/objects/items/tools/theft_tools.dm:43
public partial class obj_item_nuke_core_container : obj_item
{
    // tg: code/game/objects/items/tools/theft_tools.dm:53
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref core);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tools/theft_tools.dm:57
    public virtual async DmTask<object> load(obj_item_nuke_core ncore = null, mob user = null)
    {
        if (core != null || ncore == null)
            return FALSE;
        await ncore.forceMove(this);
        core = ncore;
        icon_state = "core_container_loaded";
        to_chat(user, span_warning(I18n.T("Container is sealing...")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(seal)), 5 * SECONDS, file: "code/game/objects/items/tools/theft_tools.dm", line: 64);
        return TRUE;
    }

    // tg: code/game/objects/items/tools/theft_tools.dm:67
    public virtual void seal()
    {
        if (core != null)
        {
            STOP_PROCESSING(SSobj, core);
            icon_state = "core_container_sealed";
            playsound(this, "sound/items/deconstruct.ogg", 60, TRUE);
            if (ismob(loc))
                to_chat(loc, span_warning(I18n.T("[] is sealed, []'s radiation is contained.", this, core)));
        }
    }

    // tg: code/game/objects/items/tools/theft_tools.dm:75
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_nuke_core)
            return NONE;

        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
        {
            to_chat(user, span_warning(I18n.T("The [] is stuck to your hand!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        await load(tool as obj_item_nuke_core, user);
        return ITEM_INTERACT_SUCCESS;
    }
}
