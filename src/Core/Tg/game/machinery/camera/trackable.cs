// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/camera/trackable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the camera tracking tool (the AI's, the SecurEye programs'), whole. The var block is generated (D111).

// tg: code/game/machinery/camera/trackable.dm:4
public partial class datum_trackable : datum
{
    ///How many ticks to try to find a target before giving up.
    // tg: code/game/machinery/camera/trackable.dm:2
    private const float CAMERA_TICK_LIMIT = 10;

    // tg: code/game/machinery/camera/trackable.dm:24
    [DmParams("source")]
    public override async DmTask New(params object[] __extra)
    {
        atom source = Dm.arg<atom>(__extra, 0);
        await base.New(__extra);
        tracking_holder = source;
        RegisterSignal(tracking_holder, COMSIG_MOB_RESET_PERSPECTIVE, nameof(perspective_reset));
    }

    // tg: code/game/machinery/camera/trackable.dm:29
    public override object Destroy(float? force = null)
    {
        tracking_holder = null;
        tracked_mob = null;
        STOP_PROCESSING(SSprocessing, this);
        return base.Destroy(force);
    }

    ///Generates a list of trackable people by name, returning a list of Humans + Non-Humans that can be tracked.
    // tg: code/game/machinery/camera/trackable.dm:36
    public virtual DmList find_trackable_mobs()
    {
        names.Cut();
        namecounts.Cut();

        DmList humans = this.humans;
        DmList others = this.others;
        humans.Cut();
        others.Cut();

        foreach (mob_living living_mob in Dm.each_cast<mob_living>(GLOB.mob_living_list))
        {
            if (!Dm.truthy(living_mob.can_track(usr as mob_living)))
                continue;

            string name = Dm.str(living_mob.name);
            while (Dm.@in(name, names))
            {
                namecounts[name] = Dm.num(namecounts[name]) + 1;
                name = Dm.text("[] ([])", name, namecounts[name]);
            }
            names.Add(name);
            namecounts[name] = 1;

            if (ishuman(living_mob))
                humans[name] = WEAKREF(living_mob);
            else
                others[name] = WEAKREF(living_mob);
        }

        DmList targets = Dm.add(sort_list(humans), sort_list(others)) as DmList;
        return targets;
    }

    /// Takes a mob to track, resets our state and begins trying to follow it
    /// Best we can at least
    // tg: code/game/machinery/camera/trackable.dm:66
    public virtual void set_tracked_mob(mob_living track = null)
    {
        set_rechecking(FALSE);
        if (tracked_mob != null)
            UnregisterSignal(tracked_mob, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE));
        if (track != null && !isliving(track))
        {
            tracked_mob = null;
            return;
        }
        tracked_mob = track;
        if (tracked_mob != null)
        {
            if (Dm.truthy(_listen_lookup?[COMSIG_TRACKABLE_TRACKING_STARTED]))
                _SendSignal(COMSIG_TRACKABLE_TRACKING_STARTED, Dm.list(this, tracked_mob));
            RegisterSignal(tracked_mob, COMSIG_QDELETING, nameof(target_deleted));
            RegisterSignal(tracked_mob, COMSIG_MOVABLE_MOVED, nameof(target_moved));
            RegisterSignal(tracked_mob, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE, nameof(glide_size_changed));
            attempt_track();
        }
    }

    // tg: code/game/machinery/camera/trackable.dm:81
    public virtual void target_deleted(datum source = null)
    {
        reset_tracking();
    }

    // tg: code/game/machinery/camera/trackable.dm:85
    public virtual void perspective_reset(atom source = null)
    {
        reset_tracking();
    }

    // tg: code/game/machinery/camera/trackable.dm:89
    public virtual void target_moved(datum source = null, params object[] __extra)
    {
        if (attempt_track())
            return;
        set_rechecking(TRUE);
    }

    /// Controls if we're processing to recheck the conditions that prevent tracking or not
    // tg: code/game/machinery/camera/trackable.dm:96
    public virtual void set_rechecking(float should_check = 0)
    {
        if (Dm.truthy(should_check))
        {
            START_PROCESSING(SSprocessing, this);
            cameraticks = Dm.initial(GetType(), (datum_trackable t) => t.cameraticks);
            rechecking = TRUE;
        }
        else
        {
            STOP_PROCESSING(SSprocessing, this);
            rechecking = FALSE;
        }
    }

    // tg: code/game/machinery/camera/trackable.dm:105
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(rechecking))
            return PROCESS_KILL;

        if (attempt_track())
        {
            set_rechecking(FALSE);
            return null;
        }

        if (cameraticks < CAMERA_TICK_LIMIT)
        {
            if (!Dm.truthy(cameraticks))
                to_chat(tracking_holder, span_warning(I18n.T("Target is not near any active cameras. Attempting to reacquire...")));
            cameraticks++;
            return null;
        }

        to_chat(tracking_holder, span_warning(I18n.T("Unable to reacquire, cancelling track...")));
        reset_tracking();
        return null;
    }

    /// Tries to track onto our target mob. Returns true if it succeeds, false otherwise
    // tg: code/game/machinery/camera/trackable.dm:123
    public virtual bool attempt_track()
    {
        if (tracked_mob == null)
        {
            reset_tracking();
            return false;
        }

        if (!Dm.truthy(tracked_mob.can_track(tracking_holder as mob_living)))
            return false;
        // In case we've been checking
        set_rechecking(FALSE);
        if (Dm.truthy(_listen_lookup?[COMSIG_TRACKABLE_TRACKING_TARGET]))
            _SendSignal(COMSIG_TRACKABLE_TRACKING_TARGET, Dm.list(this, tracked_mob));
        return true;
    }

    // tg: code/game/machinery/camera/trackable.dm:135
    public virtual void glide_size_changed(datum source = null, object new_glide_size = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_TRACKABLE_GLIDE_CHANGED]))
            _SendSignal(COMSIG_TRACKABLE_GLIDE_CHANGED, Dm.list(this, tracked_mob, new_glide_size));
    }

    /**
     * reset_tracking
     *
     * Resets our tracking
     */
    // tg: code/game/machinery/camera/trackable.dm:144
    public virtual void reset_tracking()
    {
        set_tracked_mob(null);
    }

    /**
     * track_input
     *
     * Sets a mob as being tracked, will give a tgui input list to find targets to track.
     * Args:
     *  tracker - The person trying to track, used for feedback messages. This is not the same as tracking_holder
     */
    // tg: code/game/machinery/camera/trackable.dm:154
    public virtual async DmTask track_input(mob_living tracker = null)
    {
        if (tracker == null || Dm.eq(tracker.stat, DEAD))
            return;

        object target_name = await tgui_input_list(tracker, "Select a target", "Tracking", find_trackable_mobs());
        if (!Dm.truthy(target_name) || target_name == null)
            return;
        datum_weakref mob_ref = (humans[target_name] == null ? others[target_name] : humans[target_name]) as datum_weakref;
        if (mob_ref == null)
        {
            to_chat(tracker, span_notice(I18n.T("Target is not on or near any active cameras. Tracking failed.")));
            return;
        }
        set_tracked_mob(mob_ref.resolve() as mob_living);
    }

    /**
     * track_name
     *
     * Sets a mob as being tracked, will track the passed in target name's target
     * Args:
     *  tracker - The person trying to track, used for feedback messages. This is not the same as tracking_holder
     *  tracked_mob_name - The person being tracked.
     */
    // tg: code/game/machinery/camera/trackable.dm:175
    public virtual void track_name(mob_living tracker = null, object tracked_mob_name = null)
    {
        if (tracker == null || Dm.eq(tracker.stat, DEAD))
            return;

        find_trackable_mobs(); //this is in case the tracked mob is newly/no-longer in camera field of view.
        datum_weakref mob_ref = (humans[tracked_mob_name] == null ? others[tracked_mob_name] : humans[tracked_mob_name]) as datum_weakref;
        if (mob_ref == null)
        {
            to_chat(tracker, span_notice(I18n.T("Target is not on or near any active cameras. Tracking failed.")));
            return;
        }
        to_chat(tracker, span_notice(I18n.T("Now tracking [] on camera.", tracked_mob_name)));
        set_tracked_mob(mob_ref.resolve() as mob_living);
    }

    /**
     * track_mob
     *
     * Sets a mob as being tracked, will track the passed in target
     * Args:
     *  tracker - The person trying to track, used for feedback messages. This is not the same as tracking_holder
     *  tracked - The person being tracked.
     */
    // tg: code/game/machinery/camera/trackable.dm:195
    public virtual void track_mob(mob_living tracker = null, mob_living tracked = null)
    {
        if (tracker == null || Dm.eq(tracker.stat, DEAD))
            return;
        // Need to make sure the tracked mob is in our list
        track_name(tracker, tracked.name);
    }

    ///List of all humans trackable by cameras (VAR_PRIVATE/static: one list for every tracking tool).
    private DmList humans => TgState.Current.datum_trackable__humans ??= Dm.list();
    ///List of all non-humans trackable by cameras, split so humans take priority (VAR_PRIVATE/static).
    private DmList others => TgState.Current.datum_trackable__others ??= Dm.list();
}

public sealed partial class TgState
{
    // tg: code/game/machinery/camera/trackable.dm:20  VAR_PRIVATE/static/list/humans
    internal DmList datum_trackable__humans;
    // tg: code/game/machinery/camera/trackable.dm:22  VAR_PRIVATE/static/list/others
    internal DmList datum_trackable__others;
}
