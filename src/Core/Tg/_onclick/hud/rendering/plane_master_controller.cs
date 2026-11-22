// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/rendering/plane_master_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The plane masters a controller forwards to (/atom/movable/screen/plane_master, the 2D render planes of a hud's plane master
// groups) are not ported (rules 13: the 3D client draws no planes). The seam is get_true_plane, which finds no plane master, so
// every forwarding loop runs over nothing; the controller's own filters, which tg's `. = ..()` keeps on the controller, are what
// reach the client: protocol version 29 sends the game controller's filters to its hud's player, and the client draws them over
// its 3D view (the eye blur's gaussian blur, protocol.md section 38).

///Atom that manages and controls multiple planes. It's an atom so we can hook into add_filter etc. Multiple controllers can control one plane.
///Of note: plane master controllers are currently not very extensively used, because render plates fill a semi similar niche
///This could well change someday, and I'd like to keep this stuff around, so we use it for a few cases just out of convenience
// tg: code/_onclick/hud/rendering/plane_master_controller.dm:4
public partial class atom_movable_plane_master_controller : atom_movable
{
    // The New() that INITIALIZE_IMMEDIATE(/atom/movable/plane_master_controller) defines (__DEFINES/subsystems.dm:96; inline, rules 11.1 (c), (d))
    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:10
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            DmList args = Dm.args(Dm.forward(__extra, loc));
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }

    ///Ensures that all the planes are correctly in the controlled_planes list.
    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:13
    [DmParams("mapload", "hud")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_hud hud = Dm.arg<datum_hud>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (!istype(hud, typeof(datum_hud)))
            return dot;
        owner_hud = hud;
        return dot;
    }

    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:19
    public virtual object get_planes()
    {
        object returned_planes = Dm.list();
        foreach (object true_plane in Dm.each(controlled_planes))
            Dm.append(ref returned_planes, get_true_plane(true_plane));
        return returned_planes;
    }

    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:25
    public virtual object get_true_plane(object true_plane = null)
    {
        // var/list/returned_planes = owner_hud.get_true_plane_masters(true_plane) (:26) and its stack_trace for none: the seam
        // (rules 13, the header). The port has no plane masters, so a plane master controller controls none.
        return Dm.list();
    }

    ///Full override so we can just use filterrific
    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:34
    public override void add_filter(object name = null, object priority = null, DmList @params = null, float? update = null)
    {
        update ??= TRUE;
        base.add_filter(name, priority, @params, update);
        foreach (atom_movable_screen_plane_master pm_iterator in Dm.each_cast<atom_movable_screen_plane_master>(get_planes()))
            pm_iterator.add_filter(name, priority, @params, update);
    }

    ///Full override so we can just use filterrific
    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:40
    public override object remove_filter(object name_or_names = null, float? update = null)
    {
        update ??= TRUE;
        object dot = null;
        dot = base.remove_filter(name_or_names, update);
        foreach (atom_movable_screen_plane_master pm_iterator in Dm.each_cast<atom_movable_screen_plane_master>(get_planes()))
            pm_iterator.remove_filter(name_or_names, update);
        return dot;
    }

    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:45
    public override async DmTask update_filters(object start_index = null)
    {
        await base.update_filters(start_index);
        foreach (atom_movable_screen_plane_master pm_iterator in Dm.each_cast<atom_movable_screen_plane_master>(get_planes()))
            await pm_iterator.update_filters(start_index);
    }

    ///Gets all filters for this controllers plane masters
    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:51
    public virtual object get_filters(object name = null)
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (atom_movable_screen_plane_master pm_iterator in Dm.each_cast<atom_movable_screen_plane_master>(get_planes()))
            Dm.append(ref dot, pm_iterator.get_filter(name));
        // The seam (the header; gaps RG3): the controller's own filter stands for its plane masters' (the one the client draws), so
        // the drugs' animate() of the filters they get here reaches it.
        object own = get_filter(name);
        if (own != null)
            Dm.append(ref dot, own);
        return dot;
    }

    ///Transitions all filters owned by this plane master controller
    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:57
    public override void transition_filter(object name = null, DmList new_params = null, object time = null, object easing = null, object loop = null)
    {
        base.transition_filter(name, new_params, time, easing, loop);
        foreach (atom_movable_screen_plane_master pm_iterator in Dm.each_cast<atom_movable_screen_plane_master>(get_planes()))
            pm_iterator.transition_filter(name, new_params, time, easing, loop);
    }

    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:62
    public override void transition_filter_chain(object name = null, object num_loops = null, params object[] __extra)
    {
        foreach (atom_movable_screen_plane_master pm_iterator in Dm.each_cast<atom_movable_screen_plane_master>(get_filters(name)))
            Waiting.NoSleep(Dm.Call(pm_iterator, nameof(transition_filter_chain), Dm.arglist(Dm.args(name, num_loops, __extra))));
    }

    ///Full override so we can just use filterrific
    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:67
    public override async DmTask add_atom_colour(object coloration = null, object colour_priority = null)
    {
        await base.add_atom_colour(coloration, colour_priority);
        foreach (atom_movable_screen_plane_master pm_iterator in Dm.each_cast<atom_movable_screen_plane_master>(get_planes()))
            await pm_iterator.add_atom_colour(coloration, colour_priority);
    }

    ///Removes an instance of colour_type from the atom's atom_colours list
    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:74
    public override async DmTask remove_atom_colour(object colour_priority = null, object coloration = null)
    {
        await base.remove_atom_colour(colour_priority, coloration);
        foreach (atom_movable_screen_plane_master pm_iterator in Dm.each_cast<atom_movable_screen_plane_master>(get_planes()))
            await pm_iterator.remove_atom_colour(colour_priority, coloration);
    }

    ///Resets the atom's color to null, and then sets it to the highest priority colour available
    // tg: code/_onclick/hud/rendering/plane_master_controller.dm:81
    public override async DmTask update_atom_colour()
    {
        foreach (atom_movable_screen_plane_master pm_iterator in Dm.each_cast<atom_movable_screen_plane_master>(get_planes()))
            await pm_iterator.update_atom_colour();
    }
}
