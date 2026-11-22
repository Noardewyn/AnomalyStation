// Translated from tgstation (snapshot 1d99bd7f): code/datums/cinematics/_cinematic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1a, D316: the nuke's cinematics). The screen object's vars and the var block are generated (D111).

public static partial class GlobalProcs
{
    internal const string CINEMATIC_SOURCE = "cinematic";

    /**
     * Plays a cinematic, duh. Can be to a select few people, or everyone.
     *
     * cinematic_type - datum typepath to what cinematic you wish to play.
     * watchers - a list of all mobs you are playing the cinematic to. If world, the cinematical will play globally to all players.
     * special_callback - optional callback to be invoked mid-cinematic.
     */
    // tg: code/datums/cinematics/_cinematic.dm:10
    public static async DmTask<datum_cinematic> play_cinematic(Type cinematic_type = null, object watchers = null, datum_callback special_callback = null)
    {
        if (cinematic_type == null || !typeof(datum_cinematic).IsAssignableFrom(cinematic_type))
        {
            stack_trace(Dm.text("play_cinematic called with a non-cinematic type. (Got: [])", cinematic_type), file: "code/datums/cinematics/_cinematic.dm", line: 12);
            return null;
        }
        var playing = await Dm.new_async(cinematic_type, watchers, special_callback) as datum_cinematic;

        if (watchers == world)
            watchers = GLOB.mob_list;

        await playing.start_cinematic(watchers as DmList);

        return playing;
    }
}

/// Cinematic datum. Used to show an animation to everyone.
// tg: code/datums/cinematics/_cinematic.dm:32
public partial class datum_cinematic : datum
{
    // tg: code/datums/cinematics/_cinematic.dm:48
    [DmParams("watcher", "special_callback")]
    public override DmTask New(params object[] __extra)
    {
        object watcher = Dm.arg(__extra, 0);
        screen = Dm.@new<atom_movable_screen_cinematic>(this);
        // DEVIATION(DV379): the cinematic stays on a client's screen when its mob logs out for another (a player the nuke gibs becomes a
        // ghost as the explosion starts, and the ghost's login clears the screen): the explosion and the summary play out to the end
        screen.clear_with_screen = FALSE;
        if (watcher == world)
            is_global = TRUE;

        special_callback = Dm.arg<datum_callback>(__extra, 1);
        return DmTask.CompletedTask;
    }

    // tg: code/datums/cinematics/_cinematic.dm:55
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref screen);
        special_callback = null;
        watching.Cut();
        locked.Cut();
        return base.Destroy(force);
    }

    /// Actually goes through the process of showing the cinematic to the list of watchers.
    // tg: code/datums/cinematics/_cinematic.dm:63
    public virtual async DmTask start_cinematic(DmList watchers = null)
    {
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_PLAY_CINEMATIC, src) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(Dm.bitand(SSdcs.SEND_SIGNAL(COMSIG_GLOB_PLAY_CINEMATIC, Dm.list(SSdcs, this)), COMPONENT_GLOB_BLOCK_CINEMATIC)))
            return;

        // Register a signal to handle what happens when a different cinematic tries to play over us.
        RegisterSignal(SSdcs, COMSIG_GLOB_PLAY_CINEMATIC, nameof(handle_replacement_cinematics));

        // Pause OOC
        bool ooc_toggled = false;
        if (Dm.truthy(is_global) && Dm.truthy(stop_ooc) && Dm.truthy(GLOB.ooc_allowed))
        {
            ooc_toggled = true;
            toggle_ooc(FALSE);
        }

        // Place the /atom/movable/screen/cinematic into everyone's screens, and prevent movement.
        foreach (mob watching_mob in Dm.each<mob>(watchers))
        {
            show_to(watching_mob, GET_CLIENT(watching_mob) as client);
            RegisterSignal(watching_mob, COMSIG_MOB_CLIENT_LOGIN, nameof(show_to));
            // Close watcher ui's, too, so they can watch it.
            SStgui.close_user_uis(watching_mob);
        }

        // Actually plays the animation. This will sleep, likely.
        await play_cinematic();

        // Cleans up after it's done playing.
        _addtimer(Dm.@new<datum_callback>(this, nameof(clean_up_cinematic), ooc_toggled ? TRUE : FALSE), cleanup_time,
            file: "code/datums/cinematics/_cinematic.dm", line: 89);
    }

    /// Cleans up the cinematic after a set timer of it sticking on the end screen.
    // tg: code/datums/cinematics/_cinematic.dm:90
    public virtual void clean_up_cinematic(object was_ooc_toggled = null)
    {
        if (Dm.truthy(was_ooc_toggled))
            toggle_ooc(TRUE);

        stop_cinematic();
    }

    /// Whenever another cinematic starts to play over us, we have the chacne to block it.
    // tg: code/datums/cinematics/_cinematic.dm:97
    [SignalHandler]
    public virtual object handle_replacement_cinematics(datum source = null, datum_cinematic other = null)
    {
        // Stop our's and allow others to play if we're local and it's global
        if (!Dm.truthy(is_global) && Dm.truthy(other.is_global))
        {
            stop_cinematic();
            return NONE;
        }

        return COMPONENT_GLOB_BLOCK_CINEMATIC;
    }

    /// Whenever a mob watching the cinematic logs in, show them the ongoing cinematic
    // tg: code/datums/cinematics/_cinematic.dm:108
    [SignalHandler]
    public virtual void show_to(mob watching_mob = null, client watching_client = null)
    {
        if (!HAS_TRAIT_FROM(watching_mob, TRAIT_NO_TRANSFORM, CINEMATIC_SOURCE))
            lock_mob(watching_mob);

        // Only show the actual cinematic to cliented mobs.
        if (watching_client == null || Dm.@in(watching_client, watching))
            return;

        watching.Add(watching_client);
        watching_mob.overlay_fullscreen("cinematic", typeof(atom_movable_screen_fullscreen_cinematic_backdrop));
        watching_client.screen.Add(screen);
        RegisterSignal(watching_client, COMSIG_QDELETING, nameof(remove_watcher));
    }

    /// Simple helper for playing sounds from the cinematic.
    // tg: code/datums/cinematics/_cinematic.dm:124
    public virtual void play_cinematic_sound(sound sound_to_play = null)
    {
        if (Dm.truthy(is_global))
            Dm.output_to(world, sound_to_play); // SEND_SOUND(world, sound_to_play) (DIRECT_OUTPUT, rules 12)
        else
        {
            foreach (client watching_client in Dm.each<client>(watching))
                Dm.output_to(watching_client, sound_to_play);
        }
    }

    /// Invoke any special callbacks for actual effects synchronized with animation.
    /// (Such as a real nuke explosion happening midway)
    // tg: code/datums/cinematics/_cinematic.dm:133
    public virtual async DmTask invoke_special_callback()
    {
        if (special_callback != null)
            await special_callback.Invoke();
    }

    /// The actual cinematic occurs here.
    // tg: code/datums/cinematics/_cinematic.dm:137
    public virtual DmTask play_cinematic()
    {
        return DmTask.CompletedTask;
    }

    /// Stops the cinematic and removes it from all the viewers.
    // tg: code/datums/cinematics/_cinematic.dm:141
    public virtual void stop_cinematic()
    {
        foreach (client viewing_client in Dm.each<client>(watching.Copy()))
            remove_watcher(viewing_client);

        foreach (datum_weakref locked_ref in Dm.each<datum_weakref>(locked.Copy()))
            unlock_mob(locked_ref);

        qdel(this);
    }

    /// Locks a mob, preventing them from moving, being hurt, or acting
    // tg: code/datums/cinematics/_cinematic.dm:151
    public virtual void lock_mob(mob to_lock = null)
    {
        locked.Add(WEAKREF(to_lock));
        ADD_TRAIT(to_lock, TRAIT_NO_TRANSFORM, CINEMATIC_SOURCE);
    }

    /// Unlocks a previously locked weakref
    // tg: code/datums/cinematics/_cinematic.dm:156
    public virtual void unlock_mob(datum_weakref mob_ref = null)
    {
        var locked_mob = mob_ref.resolve() as mob;
        if (locked_mob == null)
            return;
        REMOVE_TRAIT(locked_mob, TRAIT_NO_TRANSFORM, CINEMATIC_SOURCE);
        UnregisterSignal(locked_mob, COMSIG_MOB_CLIENT_LOGIN);
    }

    /// Removes the passed client from our watching list.
    // tg: code/datums/cinematics/_cinematic.dm:164
    [SignalHandler]
    public virtual void remove_watcher(client no_longer_watching = null)
    {
        if (!Dm.@in(no_longer_watching, watching))
        {
            stack_trace("cinematic remove_watcher was passed a client which wasn't watching.", file: "code/datums/cinematics/_cinematic.dm", line: 170);
            return;
        }

        UnregisterSignal(no_longer_watching, COMSIG_QDELETING);
        // We'll clear the cinematic if they have a mob which has one,
        // but we won't remove TRAIT_NO_TRANSFORM. Wait for the cinematic end to do that.
        no_longer_watching.mob?.clear_fullscreen("cinematic");
        no_longer_watching.screen.Remove(screen);

        watching.Remove(no_longer_watching);
    }
}
