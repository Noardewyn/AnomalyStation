// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: a player's own organs sell for ten times more. The var blocks are generated.

// tg: code/modules/cargo/exports/organs.dm:4
public partial class datum_export_organ : datum_export
{
    // tg: code/modules/cargo/exports/organs.dm:2  #define CLIENT_ORGAN_MULT 10 (file-local)
    private const int CLIENT_ORGAN_MULT = 10;

    // tg: code/modules/cargo/exports/organs.dm:8
    public override object get_base_cost(obj exported_item = null)
    {
        // Multiply value for organs that started in a player
        // Unaffected by price elasticity as there's a limited amount of these in play
        float multiplier = 1;
        if (HAS_TRAIT(exported_item, TRAIT_CLIENT_STARTING_ORGAN))
            multiplier = CLIENT_ORGAN_MULT;
        return round(Dm.num(base.get_base_cost(exported_item)) * multiplier);
    }
}
