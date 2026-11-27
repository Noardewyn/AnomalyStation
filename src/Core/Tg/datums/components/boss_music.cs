// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/boss_music.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * Attaches to a mob and plays that music while they have a target.
 */
// tg: code/datums/components/boss_music.dm:4
public partial class datum_component_boss_music : datum_component
{
    // tg: code/datums/components/boss_music.dm:19
    [DmParams("boss_track", "signal")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object boss_track = Dm.arg(__extra, 0);
        object signal = Dm.arg(__extra, 1);
        object dot = await base.Initialize(__extra);
        if (!ismob(parent) || signal == null)
            return COMPONENT_INCOMPATIBLE;
        this.boss_track = boss_track;
        this.signal = signal;
        track_duration = SSsounds?.get_sound_length(boss_track); // a harness world without the Master has no SSsounds
        return dot;
    }

    // tg: code/datums/components/boss_music.dm:27
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        foreach (object callback in Dm.each(music_callbacks))
            deltimer(callback);
        music_callbacks = null;

        foreach (object player_refs in Dm.each(players_listening_refs))
            clear_target(player_refs as datum_weakref);
        players_listening_refs = null;
        return dot;
    }

    // tg: code/datums/components/boss_music.dm:37
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        if (isbasicmob(parent))
            RegisterSignal(parent, signal, nameof(basic_target_found));
        else
            RegisterSignal(parent, signal, nameof(on_target_found));
    }

    // tg: code/datums/components/boss_music.dm:44
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, signal);
        await base.UnregisterFromParent(__extra);
    }

    /// Handler wrapper for basic mobs getting a target. The signal we pass to basic mobs passes along the blackboard key which we can access off parent.
    // tg: code/datums/components/boss_music.dm:49
    [SignalHandler]
    public virtual void basic_target_found(mob source = null, object key = null)
    {
        mob new_target = ((datum_ai_controller)source.ai_controller).blackboard[key] as mob;
        on_target_found(source, new_target);
    }

    ///Handles giving the boss music to a new target the fauna has received.
    ///Keeps track of them to not repeatedly overwrite its own track.
    // tg: code/datums/components/boss_music.dm:56
    [SignalHandler]
    public virtual void on_target_found(mob source = null, mob new_target = null)
    {
        if (QDELETED(source) || !(new_target is mob))
            return;

        datum_weakref new_ref = WEAKREF(new_target) as datum_weakref;
        if (Dm.@in(new_ref, players_listening_refs))
            return;

        Dm.append(ref players_listening_refs, new_ref);
        RegisterSignal(new_target, COMSIG_LIVING_DEATH, nameof(on_mob_death));
        Dm.append(ref music_callbacks, _addtimer(Dm.@new<datum_callback>(this, nameof(clear_target), new_ref), Dm.num(track_duration), TIMER_STOPPABLE, file: "code/datums/components/boss_music.dm", line: 68));
        new_target.playsound_local(get_turf(new_target), boss_track, 200, FALSE, channel: CHANNEL_BOSS_MUSIC, pressure_affected: FALSE, use_reverb: FALSE);
    }

    ///Called when a mob listening to boss music dies- ends their music early.
    // tg: code/datums/components/boss_music.dm:72
    [SignalHandler]
    public virtual void on_mob_death(mob_living source = null)
    {
        datum_weakref player_ref = WEAKREF(source) as datum_weakref;
        clear_target(player_ref);
    }

    ///Removes old_target from the list of players listening, and stops their music if it is still playing.
    ///This allows them to have music played again if they re-enter combat with this fauna.
    // tg: code/datums/components/boss_music.dm:79
    public virtual void clear_target(datum_weakref old_ref = null)
    {
        players_listening_refs?.Remove(old_ref);

        mob old_target = old_ref?.resolve() as mob;
        if (old_target != null)
        {
            UnregisterSignal(old_target, COMSIG_LIVING_DEATH);
            old_target.stop_sound_channel(CHANNEL_BOSS_MUSIC);
        }
    }
}
