// Translated from tgstation (snapshot 1d99bd7f): code/game/sound/sound_keys/sound_keys.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4b audio slice 1): its one proc; the var blocks of /datum/sound_effect and its 134 subtypes (the keys and
// their files) are generated (D111).

/**
# sound_effect datum
* use for when you need multiple sound files to play at random in a playsound
* see var documentation below
* initialized and added to sfx_datum_by_key in /datum/controller/subsystem/sounds/init_sound_keys()
*/
// tg: code/game/sound/sound_keys/sound_keys.dm:7
public partial class datum_sound_effect : datum
{
    // tg: code/game/sound/sound_keys/sound_keys.dm:13
    public virtual object return_sfx()
    {
        return pick(file_paths);
    }
}
