// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/wizard/grand_ritual/finales/clown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps MS1): the clown world mood event's add_effects; the clown finale of the grand ritual comes with the wizard. The var
// blocks are generated (D111).

/**
 * Clown enjoyers who are effected by this become ecstatic, they have achieved their life's dream.
 * This moodlet is equivalent to the one for simply being a traitor.
 */
// tg: code/modules/antagonists/wizard/grand_ritual/finales/clown.dm:46
public partial class datum_mood_event_clown_world : datum_mood_event
{
    // tg: code/modules/antagonists/wizard/grand_ritual/finales/clown.dm:49
    [DmParams("param")]
    public override async DmTask add_effects(params object[] __extra)
    {
        description = I18n.T("I LOVE working at Clown Research Station []!!", station_name());
    }
}
