// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/dog/dog_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The controllers' blackboards are generated; the corgi's access is the only proc (gaps package C2).

/**
 * Same thing but with make tiny corgis and use access cards.
 */
// tg: code/datums/ai/dog/dog_controller.dm:14
public partial class datum_ai_controller_basic_controller_dog_corgi : datum_ai_controller_basic_controller_dog
{
    // tg: code/datums/ai/dog/dog_controller.dm:28
    public override object get_access()
    {
        if (pawn is not mob_living_basic_pet_dog_corgi corgi_pawn)
            return null;

        return corgi_pawn.access_card?.GetAccess(); // a corgi without a card has no access: tg's own null read runs here
    }
}
