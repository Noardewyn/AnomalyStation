// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/lavaboat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the oar and the boats' recipes are var-only). The var blocks are generated.

// Boat
// tg: code/modules/vehicles/lavaboat.dm:2
public partial class obj_vehicle_ridden_lavaboat : obj_vehicle_ridden
{
    // tg: code/modules/vehicles/lavaboat.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_vehicle_lavaboat)));
        return dot;
    }

    // tg: code/modules/vehicles/lavaboat.dm:17
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (key_type == null || is_key(inserted_key) || !is_key(tool))
            return NONE;
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            to_chat(user, span_warning(I18n.T("[] seems to be stuck to your hand!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You attach \\the [] to \\the [].", tool, this)));
        if (inserted_key != null) //just in case there's an invalid key
            await inserted_key.forceMove(drop_location() as atom);
        inserted_key = tool;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/vehicles/lavaboat.dm:29
    public override object examine_key_message()
    {
        if (key_type == null)
            return null;
        if (inserted_key == null)
            return span_notice(I18n.T("You can attach an oar to it by clicking \\the [] with one.", this));
        else
            return span_notice(I18n.T("Alt-click [] to detach \\the [].", this, inserted_key));
    }
}

// Dragon Boat
// tg: code/modules/vehicles/lavaboat.dm:79
public partial class obj_item_ship_in_a_bottle : obj_item
{
    // tg: code/modules/vehicles/lavaboat.dm:86
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        to_chat(user, span_notice(I18n.T("You're not sure how they get the ships in these things, but you're pretty sure you know how to get it out.")));
        create_boat(get_turf(this));
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/vehicles/lavaboat.dm:90
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (!Dm.truthy(dot)) // Shatter when not caught
            create_boat(get_turf(this));
        return dot;
    }

    // tg: code/modules/vehicles/lavaboat.dm:95
    public virtual void create_boat(object drop_loc = null)
    {
        playsound(drop_loc as atom, Dm.resource("sound/effects/glass/glassbr1.ogg"), 100, TRUE);
        Dm.@new<obj_vehicle_ridden_lavaboat_dragon>(drop_loc);
        qdel(this);
    }
}

// tg: code/modules/vehicles/lavaboat.dm:100
public partial class obj_vehicle_ridden_lavaboat_dragon : obj_vehicle_ridden_lavaboat
{
    // tg: code/modules/vehicles/lavaboat.dm:107
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_vehicle_lavaboat_dragonboat)));
        return dot;
    }

    // tg: code/modules/vehicles/lavaboat.dm:111
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("You can reform [] into its bottled shape by rubbing the dragon's nose with [].", this, EXAMINE_HINT("Alt-Click"))));
        return dot;
    }

    // tg: code/modules/vehicles/lavaboat.dm:115
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        balloon_alert(user, "bottling the boat...");
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
            return CLICK_ACTION_BLOCKING;
        balloon_alert(user, "bottled the boat!");
        obj_item_ship_in_a_bottle bottled_ship = Dm.@new<obj_item_ship_in_a_bottle>(user.drop_location());
        await user.put_in_hands(bottled_ship);
        qdel(this);
        return CLICK_ACTION_SUCCESS;
    }
}
