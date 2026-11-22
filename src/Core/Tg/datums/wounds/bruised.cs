// Translated from tgstation (snapshot 1d99bd7f): code/datums/wounds/bruised.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 3: the wound types). The var blocks and the pregen data are generated (D111).

/*
 * Bruise wounds
 *
 * Very minor, flavor wounds, for the Coroner to see what may have led up to an individual's death.
 */
// tg: code/datums/wounds/bruised.dm:14
public partial class datum_wound_bruised : datum_wound
{
    // tg: code/datums/wounds/bruised.dm:33
    public override async DmTask handle_process(object seconds_per_tick = null)
    {
        if (victim == null || HAS_TRAIT(victim, TRAIT_STASIS) || IS_DEAD_OR_FAKING(victim))
            return;

        bruise_ticks--;
        if (bruise_ticks == 0)
            qdel(this);
    }

    // tg: code/datums/wounds/bruised.dm:41
    public override async DmTask<object> on_xadone(object power = null)
    {
        object dot = await base.on_xadone(power);
        qdel(this);
        return dot;
    }

    // tg: code/datums/wounds/bruised.dm:45
    public override async DmTask on_synthflesh(object reac_volume = null)
    {
        await base.on_synthflesh(reac_volume);
        qdel(this);
    }
}
