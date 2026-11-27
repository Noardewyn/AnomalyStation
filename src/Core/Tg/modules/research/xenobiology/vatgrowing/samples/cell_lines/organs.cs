// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/vatgrowing/samples/cell_lines/organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the organ cell lines are var-only but the base's colour mutation. The corrupt heart's cell line
// grows the heretics' corrupted heart, which waits for the heretic (D333). The var blocks are generated (D111).

/// Cell lines for organ cytology, letting you duplicate or grow new mutant strains!
// tg: code/modules/research/xenobiology/vatgrowing/samples/cell_lines/organs.dm:2
public partial class datum_micro_organism_cell_line_organs : datum_micro_organism_cell_line
{
    // tg: code/modules/research/xenobiology/vatgrowing/samples/cell_lines/organs.dm:7
    public override object mutate_color(atom beautiful_mutant = null)
    {
        object dot = base.mutate_color(beautiful_mutant);

        if (isorgan(beautiful_mutant))
        {
            obj_item_organ organ = (obj_item_organ)beautiful_mutant;
            // Rare affix organs get more health
            organ.maxHealth *= Dm.num(dot);
        }
        return dot;
    }
}
