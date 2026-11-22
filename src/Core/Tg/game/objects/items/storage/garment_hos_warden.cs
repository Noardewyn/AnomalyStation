// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/garment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The head of security's and the warden's garment bags (gaps J4, D298); the other heads' bags are their packages'.

// tg: code/game/objects/items/storage/garment.dm:15
public partial class obj_item_storage_bag_garment_hos : obj_item_storage_bag_garment
{
    // tg: code/game/objects/items/storage/garment.dm:78
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_rank_security_head_of_security>(this);
        Dm.@new<obj_item_clothing_under_rank_security_head_of_security_skirt>(this);
        Dm.@new<obj_item_clothing_under_rank_security_head_of_security_alt>(this);
        Dm.@new<obj_item_clothing_under_rank_security_head_of_security_alt_skirt>(this);
        Dm.@new<obj_item_clothing_under_rank_security_head_of_security_grey>(this);
        Dm.@new<obj_item_clothing_under_rank_security_head_of_security_parade>(this);
        Dm.@new<obj_item_clothing_under_rank_security_head_of_security_parade_female>(this);
        Dm.@new<obj_item_clothing_gloves_tackler_combat>(this);
        Dm.@new<obj_item_clothing_suit_armor_hos>(this);
        Dm.@new<obj_item_clothing_suit_armor_hos_hos_formal>(this);
        Dm.@new<obj_item_clothing_suit_armor_hos_trenchcoat_winter>(this);
        Dm.@new<obj_item_clothing_suit_armor_vest_leather>(this);
        Dm.@new<obj_item_clothing_glasses_hud_security_sunglasses_eyepatch>(this);
        Dm.@new<obj_item_clothing_glasses_hud_security_sunglasses_gars_giga>(this);
        Dm.@new<obj_item_clothing_head_hats_hos_beret>(this);
        Dm.@new<obj_item_clothing_head_hats_hos_cap>(this);
        Dm.@new<obj_item_clothing_mask_gas_sechailer_swat>(this);
        Dm.@new<obj_item_clothing_neck_cloak_hos>(this);
    }
}

// tg: code/game/objects/items/storage/garment.dm:19
public partial class obj_item_storage_bag_garment_warden : obj_item_storage_bag_garment
{
    // tg: code/game/objects/items/storage/garment.dm:98
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_armor_vest_warden>(this);
        Dm.@new<obj_item_clothing_head_hats_warden>(this);
        Dm.@new<obj_item_clothing_head_hats_warden_drill>(this);
        Dm.@new<obj_item_clothing_head_hats_warden_red>(this);
        Dm.@new<obj_item_clothing_head_beret_sec_navywarden>(this);
        Dm.@new<obj_item_clothing_suit_armor_vest_warden_alt>(this);
        Dm.@new<obj_item_clothing_under_rank_security_warden_formal>(this);
    }
}
