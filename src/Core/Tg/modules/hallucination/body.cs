// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/body.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The var blocks are generated (D111).

/// Makes a random body appear and disappear quickly in view of the hallucinator.
// tg: code/modules/hallucination/body.dm:2
public partial class datum_hallucination_body : datum_hallucination
{
    // tg: code/modules/hallucination/body.dm:21
    public override async DmTask<object> start()
    {
        // This hallucination is purely visual, so we don't need to bother for clientless mobs
        if (hallucinator.client == null || IS_UNCONSCIOUS(hallucinator))
            return FALSE;

        DmList possible_points = Dm.list();
        if (Dm.truthy(spawn_under_hallucinator))
            Dm.append(possible_points, get_turf(hallucinator));
        else
        {
            foreach (turf_open open_turf in Dm.each<turf_open>(view(hallucinator)))
            {
                if (Dm.truthy(await open_turf.is_blocked_turf()))
                    continue;
                Dm.append(possible_points, open_turf);
            }
        }

        if (length(possible_points) == 0)
            return FALSE;

        turf picked = pick(possible_points) as turf;
        if (isspaceturf(picked) || !Dm.truthy(picked.has_gravity()))
            body_floats = TRUE;

        shown_body = make_body_image(picked) as image;

        if (hallucinator.client != null)
            Dm.combine(hallucinator.client.images, shown_body);
        return await queue_cleanup();
    }

    // tg: code/modules/hallucination/body.dm:44
    public virtual async DmTask<object> queue_cleanup()
    {
        QDEL_IN_HALLUCINATION(this, rand(3 * SECONDS, 5 * SECONDS), "code/modules/hallucination/body.dm", 45); //Only seen for a brief moment.
        return TRUE;
    }

    // tg: code/modules/hallucination/body.dm:48
    public override object Destroy(float? force = null)
    {
        if (hallucinator.client != null)
            Dm.remove(hallucinator.client.images, shown_body);
        shown_body = null;
        return base.Destroy(force);
    }

    /// Makes the image of the body to show at the location passed.
    // tg: code/modules/hallucination/body.dm:54
    public virtual object make_body_image(turf location = null)
    {
        image created_image = image(body_image_file, location, body_image_state, body_layer);
        SET_PLANE_EXPLICIT(created_image, GAME_PLANE, location);
        if (Dm.truthy(body_floats))
            DO_FLOATING_ANIM(created_image);
        return created_image;
    }
}

// tg: code/modules/hallucination/body.dm:64
public partial class datum_hallucination_body_husk_sideways : datum_hallucination_body_husk
{
    // tg: code/modules/hallucination/body.dm:71
    public override object make_body_image(turf location = null)
    {
        image body = base.make_body_image(location) as image;
        matrix turn_matrix = matrix();
        turn_matrix.Turn(90);
        body.transform = turn_matrix;
        return body;
    }
}

// tg: code/modules/hallucination/body.dm:93
public partial class datum_hallucination_body_staticguy : datum_hallucination_body
{
    // tg: code/modules/hallucination/body.dm:98
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(hallucinator))
            UnregisterSignal(hallucinator, COMSIG_MOVABLE_MOVED);
        if (Dm.truthy(del_timerid))
        {
            deltimer(del_timerid);
            del_timerid = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/body.dm:106
    public override async DmTask<object> queue_cleanup()
    {
        RegisterSignal(hallucinator, COMSIG_MOVABLE_MOVED, nameof(on_move));
        del_timerid = QDEL_IN_HALLUCINATION(this, rand(2 * MINUTES, 3 * MINUTES), "code/modules/hallucination/body.dm", 108, stoppable: true);
        return TRUE;
    }

    /// Signal proc for [COMSIG_MOVABLE_MOVED] - if we move out of view of the hallucination, it disappears, how spooky
    // tg: code/modules/hallucination/body.dm:111
    [SignalHandler]
    public virtual void on_move(datum source = null)
    {
        // Entering its turf will cause it to fade out then delete
        if (shown_body.loc == hallucinator.loc)
        {
            animate(shown_body, Dm.named("alpha", 0), Dm.named("time", 0.5f * SECONDS));
            deltimer(del_timerid);
            del_timerid = QDEL_IN_HALLUCINATION(this, 0.6f * SECONDS, "code/modules/hallucination/body.dm", 117, stoppable: true);
            return;
        }

        // Staying in view will do nothing
        if (Dm.@in(shown_body.loc, view(hallucinator)))
            return;

        // Leaving view will delete it immediately
        deltimer(del_timerid);
        del_timerid = null;
        qdel(this);
    }
}

// tg: code/modules/hallucination/body.dm:170
public partial class datum_hallucination_body_weird_freezer : datum_hallucination_body_weird
{
    // tg: code/modules/hallucination/body.dm:178
    public override object make_body_image(turf location = null)
    {
        image body = base.make_body_image(location) as image;
        body.pixel_w = Dm.num(pick(rand(-208, -48), rand(48, 208)));
        body.pixel_z = Dm.num(pick(rand(-208, -48), rand(48, 208)));
        body.alpha = 245;
        SET_PLANE_EXPLICIT(body, ABOVE_HUD_PLANE, location);
        return body;
    }

    // tg: code/modules/hallucination/body.dm:185
    public override async DmTask<object> queue_cleanup()
    {
        QDEL_IN_HALLUCINATION(this, 12 * SECONDS, "code/modules/hallucination/body.dm", 186); //The freezer stays on screen while you're frozen
        _addtimer(Dm.@new<datum_callback>(this, nameof(freeze_player)), 1 * SECONDS, file: "code/modules/hallucination/body.dm", line: 187); // You barely have a moment to react before you're frozen
        _addtimer(Dm.@new<datum_callback>(this, nameof(freeze_intimidate)), 11.8f * SECONDS, file: "code/modules/hallucination/body.dm", line: 188);
        await hallucinator.cause_hallucination(typeof(datum_hallucination_fake_sound_weird_radio_static), "freezer hallucination");
        return TRUE;
    }

    // tg: code/modules/hallucination/body.dm:191
    public virtual async DmTask freeze_player()
    {
        if (QDELETED(this))
            return;
        await hallucinator.cause_hallucination(typeof(datum_hallucination_ice), "freezer hallucination", Dm.named("duration", 11 * SECONDS), Dm.named("play_freeze_sound", FALSE));
    }

    // tg: code/modules/hallucination/body.dm:196
    public virtual void freeze_intimidate()
    {
        if (QDELETED(this))
            return;
        // Spook 'em before we delete
        shown_body.pixel_w = (shown_body.pixel_w / 2);
        shown_body.pixel_z = (shown_body.pixel_z / 2);
    }
}
