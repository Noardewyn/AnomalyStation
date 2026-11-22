// Translated from tgstation (snapshot 1d99bd7f): code/datums/looping_sounds/weather.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the snowstorm's loop of varying lengths (each sound plays for its own length); the other storm loops are var blocks only
// (generated, D111).

// tg: code/datums/looping_sounds/weather.dm:59
public partial class datum_looping_sound_snowstorm : datum_looping_sound
{
    // hijacking sound loop code to run loops of varying length
    // doing this because set_mid_length can't run DURING a soundloop, which means we can't variably adjust the length of the sound
    // tg: code/datums/looping_sounds/weather.dm:89
    public override void start_sound_loop()
    {
        loop_started = TRUE;
        sound_loop();
    }

    // tg: code/datums/looping_sounds/weather.dm:93
    public override void sound_loop(object start_time = null)
    {
        if (!Dm.truthy(loop_started))
            return;
        object picked_sound = get_sound();
        play(picked_sound);
        if (Dm.truthy(sound_to_length?[picked_sound]))
            timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(sound_loop)), Dm.num(sound_to_length[picked_sound]),
                Dm.bitor(Dm.bitor(TIMER_CLIENT_TIME, TIMER_STOPPABLE), TIMER_DELETE_ME), SSsound_loops,
                file: "code/datums/looping_sounds/weather.dm", line: 99);
    }
}
