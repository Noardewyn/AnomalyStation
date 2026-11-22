// Translated from tgstation (snapshot 1d99bd7f): code/game/sound/sound_keys/sound_keys_assoc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4b audio slice 1): its one proc; the var blocks of the weighted keys are generated (D111).

/// subtype that accepts weighted lists
// tg: code/game/sound/sound_keys/sound_keys_assoc.dm:2
public partial class datum_sound_effect_assoc : datum_sound_effect
{
    // tg: code/game/sound/sound_keys/sound_keys_assoc.dm:4
    public override object return_sfx()
    {
        return pick_weight(file_paths);
    }
}
