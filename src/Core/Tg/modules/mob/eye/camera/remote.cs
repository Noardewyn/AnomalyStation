// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/eye/camera/remote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the camera eye of the infiltrator's navigation computer). The 3D client looks from the eye
// as from a ghost's orbit (the user's client.eye, the private block's eye, DV382). The var block is generated (D111).

/**
 * A camera controlled by a machine-operating user, like advanced cameras.
 * Handles assigning/unassigning it's users, as well as applying sight effects.
 */
// tg: code/modules/mob/eye/camera/remote.dm:5
public partial class mob_eye_camera_remote : mob_eye_camera
{
    // tg: code/modules/mob/eye/camera/remote.dm:33
    [DmParams("mapload", "creator")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        var creator = Dm.arg<obj_machinery>(__extra, 0);
        if (creator == null)
            return INITIALIZE_HINT_QDEL;

        object dot = base.Initialize(mapload, __extra);

        origin_ref = WEAKREF(creator) as datum_weakref;
        if (Dm.truthy(visible_to_user))
            set_user_icon(icon, icon_state);
        return dot;
    }

    // tg: code/modules/mob/eye/camera/remote.dm:43
    public override object Destroy(float? force = null)
    {
        var user = user_ref?.resolve() as mob_living;
        var origin = origin_ref?.resolve() as obj_machinery;
        if (origin != null && user != null)
            Waiting.Detach(origin.remove_eye_control(user));

        Waiting.Detach(assign_user(null));
        origin_ref = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/eye/camera/remote.dm:53
    public virtual async DmTask assign_user(mob_living new_user = null)
    {
        var old_user = user_ref?.resolve() as mob_living;
        if (Dm.truthy(_listen_lookup?[COMSIG_REMOTE_CAMERA_ASSIGN_USER]))
            _SendSignal(COMSIG_REMOTE_CAMERA_ASSIGN_USER, Dm.list(this, new_user, old_user));
        if (old_user != null)
        {
            old_user.remote_control = null;
            await old_user.reset_perspective(null);
            name = Dm.initial(this, (mob_eye_camera_remote e) => e.name);

            client old_user_client = GetViewerClient();
            if (user_image != null && old_user_client != null)
                Dm.remove(old_user_client.images, user_image);
            clear_camera_chunks();
        }

        user_ref = WEAKREF(new_user) as datum_weakref; //The user_ref can still be null!

        if (new_user != null)
        {
            new_user.remote_control = this;
            await new_user.reset_perspective(this);
            name = Dm.text("Camera Eye ([])", new_user.name);

            client new_user_client = GetViewerClient();
            if (user_image != null && new_user_client != null)
                Dm.append(new_user_client.images, user_image);
            if (Dm.truthy(use_visibility))
                update_visibility();
        }
    }

    /**
     * Sets the camera's user image to this icon and state.
     * If chosen_icon is null, the user image will be removed.
     */
    // tg: code/modules/mob/eye/camera/remote.dm:83
    public virtual void set_user_icon(object chosen_icon = null, object icon_state = null)
    {
        client user_client = GetViewerClient();

        if (chosen_icon != null)
        {
            set_user_icon(null); //remove whatever the last icon was
            if (!isicon(chosen_icon) || !(icon_state != null && Dm.istext(icon_state)))
                throw Dm.CRASH(Dm.text("Tried to set []'s user_image with bad parameters", this), "code/modules/mob/eye/camera/remote.dm", 91);

            user_image = image(chosen_icon, this, icon_state, FLY_LAYER);
            if (user_client != null)
                Dm.append(user_client.images, user_image);
        }
        else
        {
            if (user_client != null)
                Dm.remove(user_client.images, user_image);
            user_image = null; // QDEL_NULL(user_image): an /image is a plain value object (no qdel)
        }
    }

    // tg: code/modules/mob/eye/camera/remote.dm:101
    public override object update_remote_sight(mob_living user = null, DmList new_sight = null)
    {
        user.set_invis_see(SEE_INVISIBLE_LIVING); //can't see ghosts through cameras
        user.lighting_cutoff = LIGHTING_CUTOFF_VISIBLE;
        user.lighting_color_cutoffs = Dm.list(lighting_cutoff_red, lighting_cutoff_green, lighting_cutoff_blue);
        user.set_sight(SEE_TURFS);
        return TRUE;
    }

    // tg: code/modules/mob/eye/camera/remote.dm:108
    public override client GetViewerClient()
    {
        var user = user_ref?.resolve() as mob_living;

        if (user != null)
            return user.client;
        return null;
    }

    // tg: code/modules/mob/eye/camera/remote.dm:115
    public override void setLoc(object destination = null, float? force_update = null)
    {
        base.setLoc(destination, force_update);

        client user_client = GetViewerClient();
        if (user_image != null && user_client != null)
            SET_PLANE(user_image, ABOVE_GAME_PLANE, destination); //incase we move a z-level
    }

    // tg: code/modules/mob/eye/camera/remote.dm:122
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        float initial = Dm.initial(this, (mob_eye_camera_remote e) => e.sprint);

        if (last_moved < world.timeofday) // It's been too long since we last moved, reset sprint
            sprint = initial;

        for (float i = 0; i < Dm.max(sprint, initial); i += 20)
        {
            turf step = get_turf(get_step_multiz(this, direction) as atom);
            if (step != null)
            {
                if (!(ISINRANGE_EX(step.x, TRANSITIONEDGE, world.maxx - TRANSITIONEDGE) && ISINRANGE_EX(step.y, TRANSITIONEDGE, world.maxy - TRANSITIONEDGE)))
                    transition_step(step, direction);
                else
                    setLoc(step);
            }
        }

        last_moved = world.timeofday + wait_time;
        if (Dm.truthy(acceleration))
            sprint = Dm.min(sprint + momentum, max_sprint);
        else
            sprint = initial;
        return null;
    }

    // tg: code/modules/mob/eye/camera/remote.dm:142
    public virtual void transition_step(turf destination = null, object direction = null)
    {
        var from = SSmapping.get_level(destination.z) as datum_space_level;
        var into = from.neigbours[Dm.text("[]", direction)] as datum_space_level;
        if (into != null && Dm.truthy(allow_z_transition(from, into)))
        {
            float dest_x = destination.x;
            float dest_y = destination.y;
            switch (Dm.num(direction))
            {
                case NORTH:
                    dest_y = 1 + TRANSITIONEDGE;
                    break;
                case SOUTH:
                    dest_y = world.maxy - TRANSITIONEDGE;
                    break;
                case EAST:
                    dest_x = 1 + TRANSITIONEDGE;
                    break;
                case WEST:
                    dest_x = world.maxx - TRANSITIONEDGE;
                    break;
            }
            turf new_destination = locate(dest_x, dest_y, into.z_value);
            setLoc(new_destination ?? destination);
        }
        else
            setLoc(destination);
    }

    // tg: code/modules/mob/eye/camera/remote.dm:162
    public virtual object allow_z_transition(datum_space_level from = null, datum_space_level into = null)
    {
        return from == into ? TRUE : FALSE;
    }

    // tg: code/modules/mob/eye/camera/remote.dm:165
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        (user_ref?.resolve() as mob_living)?.on_looking_z_level_change(old_turf, new_turf);
    }
}
