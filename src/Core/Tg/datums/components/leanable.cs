// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/leanable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps L1 (D249, D327): the whole file, leaning on walls: a carbon or cyborg drags itself onto a wall, window, airlock, false wall,
// computer, mime's wall or jukebox behind it and leans against it, offset towards it (the client draws the offset on the ground,
// so the mob stands against the wall) and passable until it moves, turns, is pulled, shoved or teleported (then it falls), or the
// thing it leans on moves or changes its density. The field of view the lean widens (TRAIT_EXPANDED_FOV, update_fov) is the 2D
// client's cone: the 3D view is the client's. The var block is generated (D111).

/// Things with this component can be leaned onto
// tg: code/datums/components/leanable.dm:2
public partial class datum_component_leanable : datum_component
{
    // tg: code/datums/components/leanable.dm:10
    [DmParams("leaner", "leaning_offset")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        var leaner = Dm.arg(__extra, 0) as mob_living;
        object leaning_offset = Dm.arg(__extra, 1) ?? 11f;
        object dot = await base.Initialize(Dm.forward(__extra, leaner, leaning_offset));
        this.leaning_offset = Dm.num(leaning_offset);
        mousedrop_receive(parent as atom, leaner, leaner);
        return dot;
    }

    // tg: code/datums/components/leanable.dm:15
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOUSEDROPPED_ONTO, nameof(mousedrop_receive));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(parent, COMSIG_ATOM_DENSITY_CHANGED, nameof(on_density_change));
        var leanable_atom = (atom)parent;
        is_currently_leanable = Dm.truthy(leanable_atom.density) ? TRUE : FALSE;
    }

    // tg: code/datums/components/leanable.dm:22, :30  (UnregisterFromParent is defined twice; the later one, :30, is the one that runs)
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_MOUSEDROPPED_ONTO, COMSIG_MOVABLE_MOVED));
    }

    // tg: code/datums/components/leanable.dm:33
    public override object Destroy(float? force = null)
    {
        stop_leaning_leaners();
        return base.Destroy(force);
    }

    // tg: code/datums/components/leanable.dm:37
    public virtual void stop_leaning_leaners(object fall = null)
    {
        foreach (mob_living leaner in Dm.each_cast<mob_living>(leaning_mobs.Copy()))
        {
            leaner.stop_leaning();
            if (Dm.truthy(fall))
            {
                to_chat(leaner, span_danger(I18n.T("You lose balance!")));
                Waiting.NoSleep(leaner.Paralyze(0.5f * SECONDS));
            }
        }
        leaning_mobs.Cut();
    }

    // tg: code/datums/components/leanable.dm:45
    [SignalHandler]
    public virtual void on_moved(datum source = null)
    {
        foreach (mob_living leaner in Dm.each_cast<mob_living>(leaning_mobs.Copy()))
            leaner.stop_leaning();
    }

    // tg: code/datums/components/leanable.dm:51
    [SignalHandler]
    public virtual object mousedrop_receive(atom source = null, atom_movable dropped = null, mob user = null, object @params = null)
    {
        if (dropped != user)
            return null;
        if (!iscarbon(dropped) && !iscyborg(dropped))
            return null;
        var leaner = (mob_living)dropped;
        if (Dm.truthy(INCAPACITATED_IGNORING(leaner, INCAPABLE_RESTRAINTS)) || IS_UNCONSCIOUS_OR_CRIT(leaner) || HAS_TRAIT(leaner, TRAIT_NO_TRANSFORM))
            return null;
        if (HAS_TRAIT_FROM(leaner, TRAIT_UNDENSE, LEANING_TRAIT))
            return null;
        turf checked_turf = get_step(leaner, REVERSE_DIR(leaner.dir));
        // DEVIATION(DV421): the view turns a player's mob (DV15), so a player faces the wall he points at: a player's drop on the thing
        // ahead of him turns him around first, his back to it, as tg's lean wants
        if (checked_turf != get_turf(source) && leaner.client != null && get_step(leaner, leaner.dir) == get_turf(source))
        {
            Waiting.NoSleep(leaner.setDir(REVERSE_DIR(leaner.dir)));
            checked_turf = get_step(leaner, REVERSE_DIR(leaner.dir));
        }
        if (checked_turf != get_turf(source))
            return null;
        if (!Dm.truthy(is_currently_leanable))
            return COMPONENT_CANCEL_MOUSEDROPPED_ONTO;
        leaner.start_leaning(source, leaning_offset);
        Dm.append(ref leaning_mobs, leaner);
        RegisterSignals(leaner, Dm.list(COMSIG_LIVING_STOPPED_LEANING, COMSIG_QDELETING), nameof(stopped_leaning));
        return COMPONENT_CANCEL_MOUSEDROPPED_ONTO;
    }

    // tg: code/datums/components/leanable.dm:71
    [SignalHandler]
    public virtual void stopped_leaning(datum source = null)
    {
        Dm.remove(ref leaning_mobs, source);
        UnregisterSignal(source, Dm.list(COMSIG_LIVING_STOPPED_LEANING, COMSIG_QDELETING));
    }

    // tg: code/datums/components/leanable.dm:143
    [SignalHandler]
    public virtual void on_density_change()
    {
        is_currently_leanable = Dm.truthy(is_currently_leanable) ? FALSE : TRUE;
        if (!Dm.truthy(is_currently_leanable))
        {
            stop_leaning_leaners(fall: TRUE);
            return;
        }
        stop_leaning_leaners();
    }
}

// tg: code/datums/components/leanable.dm:77-141 (interleaved types, rules 1.9)
public partial class mob_living
{
    /**
     * Makes the mob lean on an atom
     * Arguments
     *
     * * atom/lean_target - the target the mob is trying to lean on
     * * leaning_offset - pixel offset to apply on the mob when leaning
     */
    // tg: code/datums/components/leanable.dm:84
    public virtual void start_leaning(atom lean_target = null, float leaning_offset = 0)
    {
        float new_x = 0;
        float new_y = 0;
        float facing = Dm.num(dir);
        if (facing == SOUTH)
            new_y += leaning_offset;
        else if (facing == NORTH)
            new_y -= leaning_offset;
        else if (facing == WEST)
            new_x += leaning_offset;
        else if (facing == EAST)
            new_x -= leaning_offset;

        add_offsets(LEANING_TRAIT, x_add: new_x, y_add: new_y);
        add_traits(Dm.list(TRAIT_UNDENSE, TRAIT_EXPANDED_FOV), LEANING_TRAIT);
        visible_message(
            span_notice(I18n.T("[] leans against [].", this, lean_target)),
            span_notice(I18n.T("You lean against [].", lean_target)));
        RegisterSignals(this, Dm.list(
            COMSIG_MOB_CLIENT_PRE_MOVE,
            COMSIG_LIVING_DISARM_HIT,
            COMSIG_LIVING_GET_PULLED), nameof(stop_leaning));

        RegisterSignal(this, COMSIG_MOVABLE_POST_TELEPORT, nameof(teleported_away_while_leaning));
        RegisterSignal(this, COMSIG_ATOM_POST_DIR_CHANGE, nameof(lean_dir_changed));
        // update_fov(): the 2D field of view cone (living_fov.dm); the 3D view is the client's
    }

    /// You fall on your face if you get teleported while leaning
    // tg: code/datums/components/leanable.dm:114
    [SignalHandler]
    public virtual void teleported_away_while_leaning()
    {
        // Make sure we unregister signal handlers and reset animation
        stop_leaning();
        // -1000 aura
        visible_message(span_notice(I18n.T("[] falls flat on [] face from losing [] balance!", this, p_their(), p_their())), span_warning(I18n.T("You fall suddenly as the object you were leaning on vanishes from contact with you!")));
        Waiting.NoSleep(Knockdown(3 * SECONDS));
    }

    // tg: code/datums/components/leanable.dm:124
    [SignalHandler]
    public virtual void stop_leaning()
    {
        UnregisterSignal(this, Dm.list(
            COMSIG_MOB_CLIENT_PRE_MOVE,
            COMSIG_LIVING_DISARM_HIT,
            COMSIG_LIVING_GET_PULLED,
            COMSIG_ATOM_POST_DIR_CHANGE,
            COMSIG_MOVABLE_POST_TELEPORT));
        remove_offsets(LEANING_TRAIT);
        remove_traits(Dm.list(TRAIT_UNDENSE, TRAIT_EXPANDED_FOV), LEANING_TRAIT);
        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_STOPPED_LEANING]))
            this._SendSignal(COMSIG_LIVING_STOPPED_LEANING, Dm.list(this));
        // update_fov(): the 2D field of view cone (living_fov.dm); the 3D view is the client's
    }

    // tg: code/datums/components/leanable.dm:138
    [SignalHandler]
    public virtual void lean_dir_changed(atom source = null, object old_dir = null, object new_dir = null)
    {
        if (!Dm.eq(old_dir, new_dir))
            stop_leaning(); // INVOKE_ASYNC: stop_leaning never sleeps
    }
}

// The leanable things' drop handlers (gaps L1): each adds the component on the first drop, once, rather than at init (tg: there are
// tons of walls, windows, airlocks and computers); the computer's and the mime's wall's are in their files.

// tg: code/game/turfs/closed/walls.dm:73
public partial class turf_closed_wall : turf_closed
{
    // tg: code/game/turfs/closed/walls.dm:73
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        //Adds the component only once. We do it here & not in Initialize() because there are tons of walls & we don't want to add to their init times
        await LoadComponent(typeof(datum_component_leanable), dropped);
        return null;
    }
}

// tg: code/game/objects/structures/window.dm:82
public partial class obj_structure_window : obj_structure
{
    // tg: code/game/objects/structures/window.dm:82
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        object dot = await base.mouse_drop_receive(dropped, user, @params);
        if (Dm.truthy(Dm.bitand(flags_1, ON_BORDER_1)))
            return dot;

        //Adds the component only once. We do it here & not in Initialize() because there are tons of windows & we don't want to add to their init times
        await LoadComponent(typeof(datum_component_leanable), dropped);
        return dot;
    }
}

// tg: code/game/machinery/airlock_control.dm:10
public partial class obj_machinery_door_airlock : obj_machinery_door
{
    // tg: code/game/machinery/airlock_control.dm:10
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        object dot = await base.mouse_drop_receive(dropped, user, @params);
        // We add the component only once here & not in Initialize() because there are tons of airlocks & we don't want to add to their init times
        // This is on airlock rather than on door because windoors are door and leaning looks whack on windoors
        await LoadComponent(typeof(datum_component_leanable), dropped);
        return dot;
    }
}

// tg: code/game/objects/structures/false_walls.dm:158
public partial class obj_structure_falsewall : obj_structure
{
    // tg: code/game/objects/structures/false_walls.dm:158
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        object dot = await base.mouse_drop_receive(dropped, user, @params);
        await LoadComponent(typeof(datum_component_leanable), dropped);
        return dot;
    }
}
