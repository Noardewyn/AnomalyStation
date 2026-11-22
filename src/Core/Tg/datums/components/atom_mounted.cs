// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/atom_mounted.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P3 (a new mirror: the wall mount of the light fixtures, which /obj/machinery/post_machine_initialize reaches; T1 ports
// tg's wallmount test). The var block is generated (D111). Dropping a mounted object when its support goes is
// construction and destruction (/obj/proc/deconstruct, M5).

// This element should be applied to wall-mounted machines/structures, so that if the support structure it's "hanging" from is broken or deconstructed, the wall-hung structure will deconstruct.
// tg: code/datums/components/atom_mounted.dm:2
public partial class datum_component_atom_mounted : datum_component
{
    // tg: code/datums/components/atom_mounted.dm:6
    [DmParams("target_structure")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object target_structure = Dm.arg(__extra, 0);
        object dot = null;
        if (!isobj(parent) || !isatom(target_structure))
            return COMPONENT_INCOMPATIBLE;
        dot = await base.Initialize(Dm.forward(__extra, target_structure));

        hanging_support_atom = target_structure as atom;
        RegisterSignal(hanging_support_atom, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        if (isturf(hanging_support_atom))
            RegisterSignal(hanging_support_atom, COMSIG_TURF_CHANGE, nameof(on_turf_changing));
        else
            RegisterSignal(hanging_support_atom, COMSIG_QDELETING, nameof(on_structure_delete));
        return dot;
    }

    // tg: code/datums/components/atom_mounted.dm:18
    public override async DmTask RegisterWithParent()
    {
        ADD_TRAIT(parent, TRAIT_WALLMOUNTED, INNATE_TRAIT);
        if (is_area_shuttle(get_area(parent)))
            RegisterSignal(parent, COMSIG_ATOM_BEFORE_SHUTTLE_MOVE, nameof(detach));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_move));
    }

    // tg: code/datums/components/atom_mounted.dm:24
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        REMOVE_TRAIT(parent, TRAIT_WALLMOUNTED, INNATE_TRAIT);
        DmList signals = Dm.list(COMSIG_MOVABLE_MOVED);
        if (is_area_shuttle(get_area(parent)))
            Dm.append(ref signals, COMSIG_ATOM_BEFORE_SHUTTLE_MOVE);
        UnregisterSignal(parent, signals);
    }

    // tg: code/datums/components/atom_mounted.dm:31
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(hanging_support_atom, COMSIG_ATOM_EXAMINE);
        hanging_support_atom = null;
        return base.Destroy(force);
    }

    /**
     * When the wall is examined, explains that it's supporting the linked object.
     */
    // tg: code/datums/components/atom_mounted.dm:39
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.truthy(Dm.@in(parent, view(Dm.truthy(user.client?.view) ? user.client.view : world.view, user))))
            Dm.append(ref examine_list, span_notice(I18n.T("\\The [] is currently supporting []. Deconstruction or excessive damage would cause it to [].", hanging_support_atom, span_bold(I18n.T("\\the []", parent)), span_bold(I18n.T("fall to the ground")))));
    }

    /// When the type of turf changes, if it is changing into a floor we should drop our contents
    // tg: code/datums/components/atom_mounted.dm:46
    [SignalHandler]
    public virtual void on_turf_changing(turf source = null, object path = null, object new_baseturfs = null, object flags = null, DmList post_change_callbacks = null)
    {
        float reload = FALSE;

        //For special effects where we explicitly want to preserve objects after change
        if (Dm.truthy(Dm.bitand(flags, CHANGETURF_INHERIT_MOUNTS)) || Dm.truthy(Dm.locate<obj_structure_wall_support>(source)))
            reload = TRUE;
        //if we transforming from open to open turf we can skip deconstruction under some conditions
        else if (isopenturf(source) && ispath(path, typeof(turf_open)))
        {
            //we are transforming from plating into anything that isn't a groundless turf
            if (isplatingturf(source) && !isgroundlessturf(path))
                reload = TRUE;
            //we are transforming into plating turf
            else if (ispath(LAZYACCESS(source.baseturfs, length(source.baseturfs)), typeof(turf_open_floor_plating)))
                reload = TRUE;
        }

        if (Dm.truthy(reload))
        {
            obj target = parent as obj;
            qdel(this);
            Dm.append(ref post_change_callbacks, Dm.@new<datum_callback>(target, nameof(obj.remount)));
            return;
        }

        // DEVIATION(DV178): dropped after the change (tg drops it before): a frame left by the deconstruction (a light's
        // light_construct) would otherwise mount on the wall that is going and hang in the air of the 3D view.
        Dm.append(ref post_change_callbacks, Dm.@new<datum_callback>(this, nameof(drop_wallmount)));
    }

    ///When the atom the object is mounted on is destroyed deconstruct
    // tg: code/datums/components/atom_mounted.dm:72
    [SignalHandler, DmPrivate]
    public virtual void on_structure_delete(datum source = null, object force = null)
    {
        if (source is obj_structure_wall_support)
        {
            obj target = parent as obj;
            if (!Dm.truthy(Waiting.NoSleep(target.find_and_mount_on_atom())))
                Waiting.NoSleep(drop_wallmount());
        }
        else
            Waiting.NoSleep(drop_wallmount());
    }

    /// If we get dragged from our wall (by a singulo for instance) we should deconstruct
    // tg: code/datums/components/atom_mounted.dm:85
    [SignalHandler, DmPrivate]
    public virtual void on_move(datum source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null)
    {
        // If we're having our lighting messed with we're likely to get dragged about
        // That shouldn't lead to a decon
        if (HAS_TRAIT(parent, TRAIT_LIGHTING_DEBUGGED))
            return;
        Waiting.NoSleep(drop_wallmount());
    }

    ///Called when the object is about to be shuttle rotated so we have to delete ourself and mount again later
    // tg: code/datums/components/atom_mounted.dm:96
    [SignalHandler, DmPrivate]
    public virtual void detach(datum source = null, object newT = null, object rotation = null, object move_mode = null, object moving_dock = null)
    {
        qdel(this);
    }

    /**
     * Handles the dropping of the linked object. This is done via deconstruction, as that should be the most sane way to handle it for most objects.
     * Except for intercoms, which are handled by creating a new wallframe intercom, as they're apparently items.
    */
    // tg: code/datums/components/atom_mounted.dm:104
    [DmPrivate]
    public virtual async DmTask drop_wallmount()
    {
        obj hanging_parent = parent as obj;
        // DEVIATION(DV504): in a map editor's world what hangs on a wall stays when the wall's tile is edited (another wall in its place, a thing put on its
        // tile): the map still has the lamp, and the view would show it fallen apart
        if (MapEditSeam.On)
            return;
        hanging_parent.visible_message(message: span_warning(I18n.T("\\The [] falls apart!", hanging_parent)), vision_distance: 5);
        if (InertSeam.IsInert(hanging_parent) || InertSeam.HasUndeclaredOverride(hanging_parent, "atom_deconstruct")
            || InertSeam.HasUndeclaredOverride(hanging_parent, "on_deconstruction"))
        {
            // Its type-specific deconstruction is not ported. Remove the unsupported visual through qdel.
            qdel(hanging_parent);
            return;
        }
        await hanging_parent.deconstruct(FALSE);
    }
}

// tg: code/datums/components/atom_mounted.dm:113
public partial class obj : atom_movable
{
    /// Returns a list of potential turfs to mount on. This should not check if those turfs are valid but only locate them
    // tg: code/datums/components/atom_mounted.dm:113
    [DmProtected]
    public virtual object get_turfs_to_mount_on()
    {
        DmList dot = null;
        //Infer using icon offsets. Can support diagonal mounting
        float pixel_direction = NONE;
        if (pixel_x > (ICON_SIZE_X / 2))
            Dm.combine(ref pixel_direction, EAST);
        else if (pixel_x < -(ICON_SIZE_X / 2))
            Dm.combine(ref pixel_direction, WEST);
        if (pixel_y > (ICON_SIZE_Y / 2))
            Dm.combine(ref pixel_direction, NORTH);
        else if (pixel_y < -(ICON_SIZE_Y / 2))
            Dm.combine(ref pixel_direction, SOUTH);

        dot = Dm.list();
        if (pixel_direction != NONE)
            Dm.append(ref dot, get_step(this, pixel_direction));
        Dm.append(ref dot, get_turf(this));
        return dot;
    }

    /**
     * Checks if our object can mount on this turf
     *
     * Arguments
     * * turf/target - the turf we are trying to mount on
    */
    // tg: code/datums/components/atom_mounted.dm:139
    [DmProtected, ShouldBePure]
    public virtual object is_mountable_turf(turf target = null)
    {
        return Dm.num(isclosedturf(target));
    }

    /// Returns an list of object types we can mount on if the turf is unmountable
    // tg: code/datums/components/atom_mounted.dm:146
    [DmProtected, ShouldBePure]
    public virtual object get_mountable_objects()
    {
        return get_mountable_objects__attachables;
    }

    // tg: code/datums/components/atom_mounted.dm:151  var/static/list/obj/attachables (a TgState field, rules 10.6)
    private static ref DmList get_mountable_objects__attachables => ref TgState.Current.obj__get_mountable_objects__attachables;

    /**
     * Finds an support atom to hang this object on. If you need to mount the object on Late Initialize
     * then pass TRUE inside Initialize() but not in LateInitialize().
     * The flag is only applied if no support atom could be found during Initialize() as a last resort
     *
     * Arguments
     * * mark_for_late_init - if TRUE will apply the MOUNT_ON_LATE_INITIALIZE which gets cleared on every call
     * * late_init - should only be passed as TRUE from inside LateInitialize()
    */
    // tg: code/datums/components/atom_mounted.dm:170
    public virtual async DmTask<object> find_and_mount_on_atom(float? mark_for_late_init = null, float? late_init = null)
    {
        mark_for_late_init ??= FALSE;
        late_init ??= FALSE;
        if (Dm.truthy(Dm.bitand(obj_flags, MOUNT_ON_LATE_INITIALIZE)))
            Dm.mask(ref obj_flags, Dm.bitnot(MOUNT_ON_LATE_INITIALIZE));
        else if (Dm.truthy(late_init))
            return TRUE;

        area location = get_area(this) as area;
        if (!isarea(location))
            return FALSE;

        string msg = null;
        if (Dm.truthy(FALSE) && !Dm.truthy(mark_for_late_init)) // PERFORM_ALL_TESTS(maptest_log_mapping) is FALSE in production (rules 1.7)
            msg = Dm.text("[] Could not find attachable object at [] ", type, location.type);

        DmList attachable_turfs = get_turfs_to_mount_on() as DmList;
        foreach (turf target in Dm.each_cast<turf>(attachable_turfs))
        {
            atom attachable_atom = null;
            if (Dm.truthy(is_mountable_turf(target)))
                attachable_atom = target; //your usual wallmount
            else
            {
                DmList attachables = get_mountable_objects() as DmList;
                foreach (obj attachable in Dm.each<obj>(target))
                {
                    if (Dm.truthy(is_type_in_list(attachable, attachables)) && !QDELING(attachable))
                    {
                        attachable_atom = attachable;
                        break;
                    }
                }
            }
            if (Dm.truthy(attachable_atom))
            {
                await AddComponent(typeof(datum_component_atom_mounted), attachable_atom);
                if (is_area_shuttle(location))
                    RegisterSignal(this, COMSIG_ATOM_AFTER_SHUTTLE_MOVE, nameof(remount), @override: TRUE);
                return TRUE;
            }
            if (Dm.truthy(msg))
                msg += Dm.text("([],[],[]) ", target.x, target.y, target.z);
        }
        if (Dm.truthy(msg))
            log_mapping(msg);

        if (Dm.truthy(mark_for_late_init))
            Dm.combine(ref obj_flags, MOUNT_ON_LATE_INITIALIZE);
        return FALSE;
    }

    ///Used to remount an object in special cases
    // tg: code/datums/components/atom_mounted.dm:210
    [SignalHandler, DmPrivate]
    public virtual void remount()
    {
        Waiting.NoSleep(find_and_mount_on_atom());
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/atom_mounted.dm:151  /obj/proc/get_mountable_objects: var/static/list/obj/attachables
    internal DmList obj__get_mountable_objects__attachables = Dm.list(typeof(obj_structure_table), typeof(obj_structure_window), typeof(obj_structure_fence), typeof(obj_structure_falsewall), typeof(obj_structure_wall_support));
}
