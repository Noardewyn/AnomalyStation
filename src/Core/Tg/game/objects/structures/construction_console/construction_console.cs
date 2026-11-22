// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/construction_console/construction_console.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Camera console used to control a base building drone
 *
 * Using this console will put the user in control of a [base building drone][/mob/eye/camera/remote/base_construction].
 * The drone will appear somewhere within the allowed_area var, or if no area is specified, at the location of the console.area
 * Upon interacting, the user will be granted a set of base building actions that will generally be carried out at the drone's location.
 * To create a new base builder system, this class should be the only thing that needs to be subtyped.
 *
 */
// tg: code/game/objects/structures/construction_console/construction_console.dm:11
public partial class obj_machinery_computer_camera_advanced_base_construction : obj_machinery_computer_camera_advanced
{
    // tg: code/game/objects/structures/construction_console/construction_console.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //Populate the actions list with the different action objects that will be granted to console users
        populate_actions_list();
        //Map spawned consoles will automatically restock their materials
        if (Dm.truthy(mapload))
            restock_materials();
        return dot;
    }

    /**
     * Fill the construction_actios list with actions
     *
     * Instantiate each action object that we'll be giving to users of
     * this console, and put it in the actions list
     */
    // tg: code/game/objects/structures/construction_console/construction_console.dm:44
    public virtual void populate_actions_list()
    {
    }

    /**
     * Reload materials used by the console
     *
     * Restocks any materials used by the base construction console.
     * This might mean refilling the internal RCD (should it be initialized), or
     * giving the structures list default values.
     */
    // tg: code/game/objects/structures/construction_console/construction_console.dm:54
    public virtual void restock_materials()
    {
    }

    ///Find a spawn location for the eyeobj. If no allowed_area is defined, spawn ontop of the console.
    // tg: code/game/objects/structures/construction_console/construction_console.dm:58
    public virtual async DmTask<object> find_spawn_spot()
    {
        if (allowed_area != null)
            return pick(get_area_turfs(allowed_area) as DmList);
        return get_turf(this);
    }

    // tg: code/game/objects/structures/construction_console/construction_console.dm:63
    public override async DmTask<object> CreateEye()
    {
        // the aux console's spot is the base controller: tg makes the drone inside it; here it stands on the controller's turf, where the eye is drawn from
        var spawn_spot = get_turf(await find_spawn_spot() as atom);
        if (spawn_spot == null)
            return FALSE;
        eyeobj = Dm.@new<mob_eye_camera_remote_base_construction>(spawn_spot, this);
        return TRUE;
    }

    // tg: code/game/objects/structures/construction_console/construction_console.dm:70
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        //If we have an internal RCD, we can refill it by slapping the console with some materials
        if (internal_rcd == null || (tool is not obj_item_rcd_ammo && tool is not obj_item_stack_sheet))
            return NONE;
        await internal_rcd.item_interaction(user, tool, modifiers);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/construction_console/construction_console.dm:77
    public override object Destroy(float? force = null)
    {
        qdel(internal_rcd);
        return base.Destroy(force);
    }

    ///Go through every action object in the construction_action list (which should be fully initialized by now) and grant it to the user.
    // tg: code/game/objects/structures/construction_console/construction_console.dm:82
    public override async DmTask GrantActions(mob_living user = null)
    {
        await base.GrantActions(user);
        //When the eye is in use, make it visible to players so they know when someone is building.
        eyeobj.SetInvisibility(INVISIBILITY_NONE, type);
    }

    // tg: code/game/objects/structures/construction_console/construction_console.dm:87
    public override async DmTask remove_eye_control(mob_living user = null)
    {
        await base.remove_eye_control(user);
        //Set back to default invisibility when not in use.
        eyeobj.RemoveInvisibility(type);
    }
}

/**
 * A mob used by [/obj/machinery/computer/camera_advanced/base_construction] for building in specific areas.
 *
 * Controlled by a user who is using a base construction console.
 * The user will be granted a set of building actions by the console, and the actions will be carried out by this mob.
 * The mob is constrained to a given area defined by the base construction console.
 *
 */
// tg: code/game/objects/structures/construction_console/construction_console.dm:100
public partial class mob_eye_camera_remote_base_construction : mob_eye_camera_remote
{
    // tg: code/game/objects/structures/construction_console/construction_console.dm:109
    [DmParams("mapload", "console_link")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        linked_console = Dm.arg<obj_machinery_computer_camera_advanced_base_construction>(__extra, 0);
        if (linked_console == null)
        {
            stack_trace("A base consturuction drone was created with no linked console", "code/game/objects/structures/construction_console/construction_console.dm", 112);
            return INITIALIZE_HINT_QDEL;
        }
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/structures/construction_console/construction_console.dm:116
    public override void setLoc(object destination = null, float? force_update = null)
    {
        area curr_area = get_area(destination as atom) as area;
        //Only move if we're in the allowed area. If no allowed area is defined, then we're free to move wherever.
        if (linked_console.allowed_area == null || istype(curr_area, linked_console.allowed_area))
            base.setLoc(destination, force_update);
    }

    // tg: code/game/objects/structures/construction_console/construction_console.dm:122
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        //This camera eye is visible, and as such needs to keep its dir updated
        dir = Dm.num(direction);
        return await base.relaymove(user, direction);
    }
}
