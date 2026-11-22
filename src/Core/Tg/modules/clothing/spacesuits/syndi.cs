// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/spacesuits/syndi.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the suits and helmets have no procs (their var blocks are generated, D111); the suit-to-helmet list the syndicate
// kits pick from (syndicate G3).

public partial class datum_controller_global_vars
{
    // Don't blame me, blame whoever added this many variations
    // tg: code/modules/clothing/spacesuits/syndi.dm:20  GLOBAL_LIST_INIT(syndicate_space_suits_to_helmets, list(...))
    public DmList syndicate_space_suits_to_helmets;

    // tg: code/modules/clothing/spacesuits/syndi.dm:20  (GLOBAL_MANAGED(syndicate_space_suits_to_helmets, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsyndicate_space_suits_to_helmets()
    {
        syndicate_space_suits_to_helmets = Dm.list(
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate), typeof(obj_item_clothing_head_helmet_space_syndicate)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_green), typeof(obj_item_clothing_head_helmet_space_syndicate_green)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_green_dark), typeof(obj_item_clothing_head_helmet_space_syndicate_green_dark)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_orange), typeof(obj_item_clothing_head_helmet_space_syndicate_orange)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_blue), typeof(obj_item_clothing_head_helmet_space_syndicate_blue)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_black), typeof(obj_item_clothing_head_helmet_space_syndicate_black)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_black_green), typeof(obj_item_clothing_head_helmet_space_syndicate_black_green)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_black_blue), typeof(obj_item_clothing_head_helmet_space_syndicate_black_blue)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_black_orange), typeof(obj_item_clothing_head_helmet_space_syndicate_black_orange)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_black_red), typeof(obj_item_clothing_head_helmet_space_syndicate_black_red)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_black_white), typeof(obj_item_clothing_head_helmet_space_syndicate_black)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_black_med), typeof(obj_item_clothing_head_helmet_space_syndicate_black_med)),
            Dm.kv(typeof(obj_item_clothing_suit_space_syndicate_black_engie), typeof(obj_item_clothing_head_helmet_space_syndicate_black_engie)));
        Dm.append(ref gvars_datum_init_order, "syndicate_space_suits_to_helmets");
    }
}
