// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/area_sound_manager.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file: a mob's looping sound picked by the area it stands in from a playlist (the storms' winds, rain and snow,
// whose playlists the weathers fill per impacted area), changed when it moves to an area of another loop or the playlist changes, removed
// off its levels. The var block is generated (D111).

///Allows you to set a theme for a set of areas without tying them to looping sounds explicitly
// tg: code/datums/components/area_sound_manager.dm:2
public partial class datum_component_area_sound_manager : datum_component
{
    // tg: code/datums/components/area_sound_manager.dm:13
    [DmParams("area_loop_pairs", "change_on", "remove_on", "acceptable_zs")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        var area_loop_pairs = Dm.arg(__extra, 0) as DmList;
        object change_on = Dm.arg(__extra, 1);
        object remove_on = Dm.arg(__extra, 2);
        var acceptable_zs = Dm.arg(__extra, 3) as DmList;
        if (!ismovable(parent))
            return null;
        area_to_looping_type = area_loop_pairs;
        accepted_zs = acceptable_zs;
        change_the_track();

        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(react_to_move));
        RegisterSignal(parent, COMSIG_MOVABLE_Z_CHANGED, nameof(react_to_z_move));
        // change on can be a list of signals
        if (change_on is DmList change_signals)
            RegisterSignals(parent, change_signals, nameof(handle_change));
        else if (change_on != null)
            RegisterSignal(parent, change_on, nameof(handle_change));
        // remove on can be a list of signals
        if (remove_on is DmList remove_signals)
            RegisterSignals(parent, remove_signals, nameof(handle_removal));
        else if (remove_on != null)
            RegisterSignal(parent, remove_on, nameof(handle_removal));
        return null;
    }

    // tg: code/datums/components/area_sound_manager.dm:33
    public override object Destroy(float? force = null)
    {
        qdel(our_loop); // QDEL_NULL
        our_loop = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/area_sound_manager.dm:37
    [SignalHandler]
    public virtual void react_to_move(datum source = null, atom oldloc = null, object dir = null, object forced = null)
    {
        DmList loop_lookup = area_to_looping_type;
        if (Dm.eq(loop_lookup?[get_area(oldloc)], loop_lookup?[get_area(parent)]))
            return;
        change_the_track(TRUE);
    }

    // tg: code/datums/components/area_sound_manager.dm:44
    [SignalHandler]
    public virtual void react_to_z_move(datum source = null, turf old_turf = null, turf new_turf = null)
    {
        if (!Dm.truthy(Dm.length(accepted_zs)) || (new_turf != null && Dm.@in(new_turf.z, accepted_zs)))
            return;
        qdel(this);
    }

    // tg: code/datums/components/area_sound_manager.dm:50
    [SignalHandler]
    public virtual void handle_removal(datum source = null)
    {
        qdel(this);
    }

    // tg: code/datums/components/area_sound_manager.dm:54
    [SignalHandler]
    public virtual void handle_change(datum source = null)
    {
        change_the_track();
    }

    // tg: code/datums/components/area_sound_manager.dm:58
    public virtual void change_the_track(object skip_start = null)
    {
        object existing_loop_id = our_loop?.timer_id;
        if (Dm.truthy(existing_loop_id))
            // Time left will sometimes return negative values, just ignore them and start a new sound loop now
            next_loop_time = world.time + max(Dm.num(timeleft(existing_loop_id, SSsound_loops) ?? 0), 0);
        qdel(our_loop); // QDEL_NULL
        our_loop = null;

        area our_area = get_area(parent) as area;
        var new_loop_type = area_to_looping_type?[our_area] as Type;
        if (new_loop_type == null)
            return;

        our_loop = Waiting.NoSleep(Dm.new_async(new_loop_type, parent, FALSE, TRUE, skip_start ?? FALSE)) as datum_looping_sound;
        // We're not ready to start another loop, wait before changing the sound so we don't double up
        if (Dm.num(next_loop_time) > world.time)
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(start_looping_sound)), Dm.num(next_loop_time) - world.time,
                Dm.bitor(Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_CLIENT_TIME), TIMER_NO_HASH_WAIT), TIMER_DELETE_ME), SSsound_loops,
                file: "code/datums/components/area_sound_manager.dm", line: 75);
            return;
        }
        start_looping_sound();
    }

    // tg: code/datums/components/area_sound_manager.dm:80
    public virtual void start_looping_sound()
    {
        our_loop?.start();
    }
}
