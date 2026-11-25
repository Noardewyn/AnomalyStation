// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/species_parts/moth_bodyparts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2a (D349): the moth chest's butt sprite (the file's only proc); the var blocks are generated (D111).

// tg: code/modules/surgery/bodyparts/species_parts/moth_bodyparts.dm:13
public partial class obj_item_bodypart_chest_moth : obj_item_bodypart_chest
{
    // tg: code/modules/surgery/bodyparts/species_parts/moth_bodyparts.dm:22
    public override object get_butt_sprite()
    {
        return Dm.icon(Dm.resource("icons/mob/butts.dmi"), BUTT_SPRITE_FUZZY);
    }
}
