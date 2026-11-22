// Translated from tgstation (snapshot 1d99bd7f): code/datums/cinematics/malf_doomsday.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI8, D326: the malfunctioning AI's doomsday). The var block is generated (D111).

/// A malfunctioning AI has activated the doomsday device and wiped the station!
// tg: code/datums/cinematics/malf_doomsday.dm:2
public partial class datum_cinematic_malf : datum_cinematic
{
    // tg: code/datums/cinematics/malf_doomsday.dm:4
    public override async DmTask play_cinematic()
    {
        flick("intro_malf", screen);
        await stoplag(7.6f * SECONDS);
        flick("station_explode_fade_red", screen);
        play_cinematic_sound(Dm.sound("sound/effects/explosion/explosion_distant.ogg"));
        if (special_callback != null)
            await special_callback.Invoke();
        screen.icon_state = "summary_malf";
    }
}
