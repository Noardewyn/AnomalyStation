// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/reflection.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H1 (the mirrors): the whole file. The reflection holder and its reflections are tg's vis_contents of the mirror; the 3D
// client draws the mirror's glass as a real mirror instead of the flipped copies (DEVIATION DV470), so they only carry tg's state
// (which movables the mirror reflects, the omen's vanished reflection). The var blocks are generated (D111).

/**
 * A simple-ish component that reflects the icons of movables on the parent like a mirror.
 * Sadly, there's no easy way to make the SOUTH dir reflection flip the visual so that you can see
 * the back NORTH dir of a target while it's facing SOUTH beside adding the VIS_INHERIT_DIR flag
 * to the target movable, which I'm not doing to spare eventual issues with other vis overlays in the future.
 */
// tg: code/datums/components/reflection.dm:7
public partial class datum_component_reflection : datum_component
{
    private static ref DmList Initialize__connections => ref TgState.Current.datum_component_reflection__Initialize__connections;

    /**
     * Init Args
     * * set_reflected_dir: Optional: What dir to reflect. If not provided, uses (and updates to) the parent's dir.
     * * reflection_filter: Optional: A list of filters to apply to the reflection.
     * * reflection_matrix: Optional: A matrix to apply as the transform of the reflection.
     * * can_reflect: Optional: A callback to check if a movable should be reflected.
     * * alpha: The transparency of the reflection holder.
     * * update_signals: Optional: Additional signals to provide to update_signals (to check for when to recalculate all reflections).
     * * check_reflect_signals: Optional: Additional signals to provide to check_reflect_signals (to check for when to update a single reflection).
     */
    // tg: code/datums/components/reflection.dm:44
    [DmParams("set_reflected_dir", "reflection_filter", "reflection_matrix", "can_reflect", "alpha", "update_signals", "check_reflect_signals")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object set_reflected_dir = Dm.arg(__extra, 0);
        var reflection_filter = Dm.arg(__extra, 1) as DmList;
        var reflection_matrix = Dm.arg(__extra, 2) as matrix;
        var can_reflect = Dm.arg(__extra, 3) as datum_callback;
        object alpha = Dm.arg(__extra, 4) ?? 150;
        var update_signals = Dm.arg(__extra, 5) as DmList;
        var check_reflect_signals = Dm.arg(__extra, 6) as DmList;
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        await AddComponent(typeof(datum_component_connect_range), parent, Initialize__connections, 1, Dm.named("works_in_containers", FALSE));
        // Always supplied to check_reflect_signals
        DmList default_can_reflect_signals = Dm.list(
            COMSIG_ATOM_POST_DIR_CHANGE,
            COMSIG_ATOM_UPDATED_ICON,
            COMSIG_CARBON_APPLY_OVERLAY,
            COMSIG_CARBON_REMOVE_OVERLAY,
            COMSIG_LIVING_POST_UPDATE_TRANSFORM);
        // Always supplied to update_signals
        DmList default_update_signals = Dm.list(
            COMSIG_MOVABLE_MOVED);

        this.reflection_matrix = reflection_matrix;
        this.reflection_filter = reflection_filter;
        this.can_reflect = can_reflect;
        this.check_reflect_signals = Dm.add(check_reflect_signals ?? Dm.list(), default_can_reflect_signals) as DmList;
        reflection_holder = Dm.@new<obj_effect_abstract>(parent);
        reflection_holder.alpha = Dm.num(alpha);
        reflection_holder.appearance_flags = KEEP_TOGETHER;
        reflection_holder.vis_flags = VIS_INHERIT_ID;
        reflection_holder.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        if (reflection_filter != null)
            reflection_holder.add_filter("reflection", 1, reflection_filter);

        var mov_parent = (atom_movable)parent;
        mov_parent.vis_contents.Add(reflection_holder);
        set_reflection(Dm.truthy(set_reflected_dir) ? set_reflected_dir : REVERSE_DIR(mov_parent.dir));

        if (!Dm.truthy(set_reflected_dir))
            RegisterSignal(parent, COMSIG_ATOM_DIR_CHANGE, nameof(on_dir_change));
        RegisterSignals(parent, Dm.add(update_signals ?? Dm.list(), default_update_signals) as DmList, nameof(get_reflection_targets));
        return null;
    }

    // tg: code/datums/components/reflection.dm:87
    public override object Destroy(float? force = null)
    {
        foreach (atom_movable tracked in Dm.each<atom_movable>(reflected_movables?.Copy()))
            nuke_reflection(tracked);
        QDEL_NULL(ref reflection_holder);
        can_reflect = null;
        return base.Destroy(force);
    }

    ///Called when the parent changes its direction.
    // tg: code/datums/components/reflection.dm:95
    [SignalHandler]
    public virtual void on_dir_change(atom_movable source = null, object old_dir = null, object new_dir = null)
    {
        set_reflection(REVERSE_DIR(Dm.num(new_dir)));
    }

    ///Turns the allowed reflected direction alongside the parent's dir. then calls get_reflection_targets.
    // tg: code/datums/components/reflection.dm:100
    public virtual void set_reflection(object new_dir = null)
    {
        new_dir ??= SOUTH;
        if (Dm.eq(reflected_dir, new_dir))
            return;

        reflected_dir = new_dir;
        get_reflection_targets(parent as atom_movable);
    }

    ///Unsets the old reflected movables and sets it with new ones.
    // tg: code/datums/components/reflection.dm:108
    [SignalHandler]
    public virtual void get_reflection_targets(atom_movable source = null)
    {
        // clean slate
        foreach (atom_movable tracked in Dm.each<atom_movable>(reflected_movables?.Copy()))
            nuke_reflection(tracked);
        // find anything adjacent, if we can't see it that's fine (we check view later)
        foreach (atom_movable target in Dm.each<atom_movable>(range(1, source)))
            track_reflection(target);
    }

    ///Checks if the target movable can be reflected or not.
    // tg: code/datums/components/reflection.dm:118
    public virtual bool check_can_reflect(atom_movable target = null)
    {
        if (target == parent || !Dm.@in(target, view(1, parent)))
            return false;
        if (target.invisibility >= INVISIBILITY_MAXIMUM)
            return false;
        var mov_parent = (atom_movable)parent;
        if (target.loc != mov_parent.loc && !Dm.eq(get_dir(mov_parent, target), reflected_dir))
            return false;
        if (can_reflect != null && !Dm.truthy(Waiting.NoSleep(can_reflect.Invoke(target))))
            return false;
        return true;
    }

    ///Called when a movable enters a turf within the connected range
    // tg: code/datums/components/reflection.dm:131
    [SignalHandler]
    public virtual void on_movable_entered_or_initialized(atom_movable source = null, atom_movable arrived = null)
    {
        track_reflection(arrived);
    }

    // tg: code/datums/components/reflection.dm:135
    public virtual void track_reflection(atom_movable target = null, float? check_view = null)
    {
        // this stuff really shouldn't be tracked
        if (QDELETED(target) || target == parent || target.loc == parent)
            return;
        // i don't really want to do this but there's a bunch of abstract effects we should ignore...
        // we can revisit this later when we actually have an object that wants to reflect this stuff
        if (iseffect(target))
            return;
        if (!Dm.truthy(LAZYFIND(reflected_movables, target))) // not lazyaccess - value may be null
        {
            LAZYSET(ref reflected_movables, target, null);
            RegisterSignals(target, check_reflect_signals, nameof(update_reflection));
            RegisterSignals(target, Dm.list(COMSIG_QDELETING), nameof(nuke_reflection));
        }
        update_reflection(target);
    }

    // tg: code/datums/components/reflection.dm:149
    [SignalHandler]
    public virtual void nuke_reflection(atom_movable target = null)
    {
        var reflection = LAZYACCESS(reflected_movables, target) as atom_movable;
        LAZYREMOVE(ref reflected_movables, target);
        if (!QDELETED(reflection))
            qdel(reflection);
        UnregisterSignal(target, check_reflect_signals);
        UnregisterSignal(target, COMSIG_QDELETING);
    }

    ///Called when a movable exits a turf within the connected range
    // tg: code/datums/components/reflection.dm:160
    [SignalHandler]
    public virtual void on_movable_exited(atom_movable source = null, atom_movable gone = null)
    {
        if (!Dm.truthy(LAZYFIND(reflected_movables, gone))) // not lazyaccess - value may be null
            return;
        if (check_can_reflect(gone))
            return;
        nuke_reflection(gone);
    }

    /// Handles updating the appearance of the reflection to match the target movable.
    // tg: code/datums/components/reflection.dm:170
    public virtual void copy_appearance_to_reflection(obj_effect_abstract reflection = null, atom_movable target = null)
    {
        reflection.appearance = atom.copy_appearance_filter_overlays(target.appearance);
        reflection.vis_flags = VIS_INHERIT_ID;
        reflection.transform = reflection_matrix ?? matrix();
        // updating the dir so facing towards / away from it correctly faces the reflection away / towards it,
        // while facing left / right will correctly face the reflection left / right
        // there's probably a more intelligent way to tackle this but this is more readable, i guess
        float reflected = Dm.num(reflected_dir);
        if (Dm.truthy(Dm.bitand(reflected, EAST)))
        {
            if (Dm.truthy(Dm.bitand(target.dir, NORTH)))
                reflection.dir = WEST;
            else if (Dm.truthy(Dm.bitand(target.dir, SOUTH)))
                reflection.dir = EAST;
            else
                reflection.dir = REVERSE_DIR(target.dir);
        }
        else if (Dm.truthy(Dm.bitand(reflected, WEST)))
        {
            if (Dm.truthy(Dm.bitand(target.dir, NORTH)))
                reflection.dir = EAST;
            else if (Dm.truthy(Dm.bitand(target.dir, SOUTH)))
                reflection.dir = WEST;
            else
                reflection.dir = REVERSE_DIR(target.dir);
        }
        else if (Dm.truthy(Dm.bitand(reflected, SOUTH)))
        {
            // east/west is the same, makes it easy on us
            reflection.dir = Dm.truthy(Dm.bitand(target.dir, Dm.bitor(EAST, WEST))) ? target.dir : REVERSE_DIR(target.dir);
        }
        else if (Dm.truthy(Dm.bitand(reflected, NORTH)))
        {
            reflection.dir = Dm.truthy(Dm.bitand(target.dir, Dm.bitor(NORTH, SOUTH))) ? target.dir : REVERSE_DIR(target.dir);
            // north needs snowflake handling to make a more... "understandable" reflection
            reflection.transform = (reflection.transform as matrix).Turn(180);
            reflection.pixel_y += 5;
        }

        // purely for vv
        reflection.name = Dm.text("[]'s reflection", target.name);

        target.SEND_SIGNAL(COMSIG_REFLECTION_UPDATED, Dm.list(target, parent, reflection));
        parent.SEND_SIGNAL(COMSIG_REFLECTED_IMAGE_UPDATED, Dm.list(parent, reflection));
    }

    ///Called when the target movable changes its appearance or dir.
    // tg: code/datums/components/reflection.dm:210
    [SignalHandler]
    public virtual void update_reflection(atom_movable source = null)
    {
        var reflection = LAZYACCESS(reflected_movables, source) as obj_effect_abstract;
        if (!check_can_reflect(source))
        {
            // temporarily hide any reflection
            if (reflection != null)
                reflection.vis_flags = Dm.bitor(reflection.vis_flags, VIS_HIDE);
            return;
        }
        // Lazy init the reflection
        if (reflection == null)
        {
            // If the loc is null, only a black (or grey depending on alpha) silhouette of the target will be rendered
            // Just putting this information here in case you want something like that in the future.
            reflection = Dm.@new<obj_effect_abstract>(parent);
            reflection_holder.vis_contents.Add(reflection);
            LAZYSET(ref reflected_movables, source, reflection);
        }

        // technically redundant (...because copying appearance copies vis flags), but good to be explicit
        reflection.vis_flags = Dm.bitand(reflection.vis_flags, Dm.bitnot(VIS_HIDE));
        copy_appearance_to_reflection(reflection, source);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/reflection.dm:48  var/static/list/connections
    internal DmList datum_component_reflection__Initialize__connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_reflection.on_movable_entered_or_initialized)),
        Dm.kv(COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(datum_component_reflection.on_movable_entered_or_initialized)),
        Dm.kv(COMSIG_ATOM_EXITED, nameof(datum_component_reflection.on_movable_exited)));
}
