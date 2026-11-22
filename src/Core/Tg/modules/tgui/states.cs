// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148): the whole file; the cyborg's override (:87-92) came with the cyborgs (silicons SI2, D326), the AI's (:79-85)
// with the AI (silicons SI4). isAdminGhostAI (admin ghost AI interaction, M10) is a GAP read as FALSE, as in _machinery.cs.

public partial class datum
{
    /**
     * public
     *
     * Checks the UI state for a mob.
     *
     * required user mob The mob who opened/is using the UI.
     * required state datum/ui_state The state to check.
     *
     * return UI_state The state of the UI.
     */
    // tg: code/modules/tgui/states.dm:19
    public virtual async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        object src_object = ui_host(user);
        object dot = UI_CLOSE;
        if (!Dm.truthy(state))
            return dot;

        if (isobserver(user))
        {
            // If they turn on ghost AI control, admins can always interact.
            bool isAdminGhostAI__gap = false; // GAP(lead): isAdminGhostAI (admin ghost AI control, M10)
            if (isAdminGhostAI__gap)
                dot = Dm.max(dot, UI_INTERACTIVE);

            // Regular ghosts can always at least view if in range.
            if (Dm.truthy(user.client))
            {
                DmList clientviewlist = getviewsize(user.client.view);
                if (get_dist(src_object, user) < Dm.num(Dm.max(clientviewlist[1f], clientviewlist[2f])))
                    dot = Dm.max(dot, UI_UPDATE);
            }
        }

        // Check if the state allows interaction
        object result = await state.can_use_topic(src_object, user);
        dot = Dm.max(dot, result);
        return dot;
    }
}

// tg: code/modules/tgui/states.dm:51
[DmPath("/datum/ui_state")]
public partial class datum_ui_state : datum
{
    /**
     * private
     *
     * Checks if a user can use src_object's UI, and returns the state.
     * Can call a mob proc, which allows overrides for each mob.
     *
     * required src_object datum The object/datum which owns the UI.
     * required user mob The mob who opened/is using the UI.
     *
     * return UI_state The state of the UI.
     */
    // tg: code/modules/tgui/states.dm:51
    public virtual async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        // Don't allow interaction by default.
        return UI_CLOSE;
    }
}

public partial class mob
{
    /**
     * public
     *
     * Standard interaction/sanity checks. Different mob types may have overrides.
     *
     * return UI_state The state of the UI.
     */
    // tg: code/modules/tgui/states.dm:62
    public virtual object shared_ui_interaction(object src_object = null)
    {
        // Close UIs if mindless.
        if (!Dm.truthy(client) && !HAS_TRAIT(this, TRAIT_PRESERVE_UI_WITHOUT_CLIENT))
            return UI_CLOSE;
        // Disable UIs if unconscious.
        else if (IS_UNCONSCIOUS_OR_CRIT(this))
            return UI_DISABLED;
        // Update UIs if incapicitated but conscious.
        else if (Dm.truthy(incapacitated))
            return UI_UPDATE;
        return UI_INTERACTIVE;
    }
}

public partial class mob_living : mob
{
    // tg: code/modules/tgui/states.dm:74
    public override object shared_ui_interaction(object src_object = null)
    {
        object dot = base.shared_ui_interaction(src_object);
        if (!Dm.truthy(Dm.bitand(mobility_flags, MOBILITY_UI)) && !Dm.truthy(Dm.bitand(((atom)src_object).interaction_flags_atom, INTERACT_ATOM_IGNORE_MOBILITY)) && Dm.eq(dot, UI_INTERACTIVE))
            return UI_UPDATE;
        return dot;
    }


    /**
     * public
     *
     * Distance versus interaction check.
     *
     * required src_object atom/movable The object which owns the UI.
     *
     * return UI_state The state of the UI.
     */
    // tg: code/modules/tgui/states.dm:103
    public virtual object shared_living_ui_distance(atom_movable src_object = null, float? viewcheck = null, float? allow_tk = null)
    {
        viewcheck ??= TRUE;
        allow_tk ??= TRUE;
        obj_item item_in_hand = get_active_held_item() as obj_item;
        if (istype(item_in_hand, typeof(obj_item_machine_remote))) //snowflake, this lets you interact with all.
        {
            // var/obj/item/machine_remote/remote = item_in_hand; if(remote.controlling_machine_or_bot == src_object) return UI_INTERACTIVE
            // GAP(types): /obj/item/machine_remote is generated only (the machine remote, M10+); no remote controls anything.
            Dm.Deferred("code/modules/tgui/states.dm:107", "shared_living_ui_distance: the machine remote's controlled machine (M10+)");
        }

        float dist = get_dist(src_object, this);
        // If the object is obscured or too far away, close it.
        if (dist > 5 || (Dm.truthy(viewcheck) && !Dm.@in(src_object, view(5, this))))
            return UI_CLOSE;
        // Disable if further than 2 tiles
        if (dist > 2)
            return UI_DISABLED;
        // Update but block interactions if further than a tile away
        if (dist > 1)
            return UI_UPDATE;
        // Open and interact if 1-0 tiles away.
        return UI_INTERACTIVE;
    }
}

public partial class mob_living_carbon_human
{
    // tg: code/modules/tgui/states.dm:123
    public override object shared_living_ui_distance(atom_movable src_object = null, float? viewcheck = null, float? allow_tk = null)
    {
        viewcheck ??= TRUE;
        allow_tk ??= TRUE;
        // if(allow_tk && dna.check_mutation(/datum/mutation/telekinesis) && tkMaxRangeCheck(src, src_object)) return UI_INTERACTIVE
        // GAP(lead): check_mutation is not translated (genetics, M8; dna.cs) and no mob has a mutation, so the telekinesis
        // branch is never taken; it is not called here because its NotTranslated stub would report on every window update.
        bool telekinesis__gap = false;
        if (Dm.truthy(allow_tk) && telekinesis__gap && tkMaxRangeCheck__local(this, src_object))
            return UI_INTERACTIVE;
        return base.shared_living_ui_distance(src_object, viewcheck, allow_tk);
    }

    // tg: code/_onclick/telekinesis.dm:214 /proc/tkMaxRangeCheck (TK_MAXRANGE, telekinesis.dm:7, is 15). GAP(lead): not
    // declared (telekinesis is M8): a local copy, as mob.cs's.
    private static bool tkMaxRangeCheck__local(mob user, atom target)
    {
        float d = get_dist(user, target);
        if (d > 15)
        {
            user.balloon_alert(user, I18n.T("can't TK, too far!"));
            return false;
        }
        return true;
    }
}

// tg: code/modules/tgui/states.dm:87
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/tgui/states.dm:87
    public override object shared_ui_interaction(object src_object = null)
    {
        // Disable UIs if the object isn't installed in the borg AND the borg is either locked, has a dead cell, or no cell.
        atom device = src_object as atom;
        if ((device != null && device.loc != this) && (cell == null || Dm.num(cell__obj.charge) <= 0 || Dm.truthy(lockcharge)))
            return UI_DISABLED;
        return base.shared_ui_interaction(src_object);
    }
}

// tg: code/modules/tgui/states.dm:79  (silicons SI4)
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/tgui/states.dm:79
    public override object shared_ui_interaction(object src_object = null)
    {
        // Disable UIs if the AI is unpowered.
        if (apc_override == src_object) //allows AI to (eventually) use the interface for their own APC even when out of power
            return UI_INTERACTIVE;
        if (lacks_power())
            return UI_DISABLED;
        return base.shared_ui_interaction(src_object);
    }
}
