// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/outfits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file's one proc (gaps S4d, D327): the obsessed's bloodied outfit; the outfits themselves are generated (D111).

// tg: code/modules/mafia/outfits.dm:159
public partial class datum_outfit_mafia_obsessed : datum_outfit_mafia
{
    // tg: code/modules/mafia/outfits.dm:166
    public override async DmTask post_equip(mob_living_carbon_human H = null, float? visuals_only = null)
    {
        foreach (obj_item carried_item in Dm.each<obj_item>(H.get_equipped_items(INCLUDE_POCKETS | INCLUDE_ACCESSORIES)))
            await carried_item.add_mob_blood(H); //Oh yes, there will be blood...
        await H.regenerate_icons();
    }
}
