// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/lavalandruin_code/syndicate_base.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the syndicate lava base's tongue crate and its supermatter shard. The modular map root's var block is generated.

// tg: code/modules/mapfluff/ruins/lavalandruin_code/syndicate_base.dm:6
public partial class obj_structure_closet_crate_secure_freezer_commsagent : obj_structure_closet_crate_secure_freezer
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/syndicate_base.dm:10
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); //Contains a variety of less exotic tongues (And tongue accessories) for the comms agent to mess with.
        Dm.@new<obj_item_organ_tongue>(this);
        Dm.@new<obj_item_organ_tongue_lizard>(this);
        Dm.@new<obj_item_organ_tongue_fly>(this);
        Dm.@new<obj_item_organ_tongue_zombie>(this);
        Dm.@new<obj_item_organ_tongue_bone>(this);
        Dm.@new<obj_item_organ_tongue_robot>(this); //DANGER! CRYSTAL HYPERSTRUCTURE-
        Dm.@new<obj_item_organ_tongue_ethereal>(this);
        Dm.@new<obj_item_autosurgeon_syndicate_commsagent>(this);
        Dm.@new<obj_item_book_granter_sign_language>(this);
        Dm.@new<obj_item_clothing_gloves_radio>(this);
    }
}

// tg: code/modules/mapfluff/ruins/lavalandruin_code/syndicate_base.dm:23
public partial class obj_machinery_power_supermatter_crystal_shard_syndicate : obj_machinery_power_supermatter_crystal_shard
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/syndicate_base.dm:32
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_scalpel_supermatter) //You can already yoink the docs as a free objective win, another would be just gross
        {
            to_chat(user, span_danger(I18n.T("This shard's already in Syndicate custody, taking it again could cause more harm than good.")));
            return ITEM_INTERACT_BLOCKING;
        }

        await DmTask.CompletedTask;
        return NONE;
    }
}
