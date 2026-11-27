// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/necropolis_chests.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the megafauna's chests (the dragon, bubblegum and colossus chests with their crusher kinds); the tendril's and
// the puzzle chests and the skeleton key come with the tendrils. The var blocks are generated.

//Megafauna chests

// tg: code/modules/mining/lavaland/necropolis_chests.dm:48
public partial class obj_structure_closet_crate_necropolis_dragon : obj_structure_closet_crate_necropolis
{
    // tg: code/modules/mining/lavaland/necropolis_chests.dm:51
    public override async DmTask PopulateContents(params object[] __extra)
    {
        float loot = rand(1, 4);
        if (loot == 1)
            Dm.@new<obj_item_melee_ghost_sword>(this);
        else if (loot == 2)
            Dm.@new<obj_item_lava_staff>(this);
        else if (loot == 3)
            Dm.@new<obj_item_book_granter_action_spell_sacredflame>(this);
        else if (loot == 4)
            Dm.@new<obj_item_dragons_blood>(this);
    }
}

// tg: code/modules/mining/lavaland/necropolis_chests.dm:63
public partial class obj_structure_closet_crate_necropolis_dragon_crusher : obj_structure_closet_crate_necropolis_dragon
{
    // tg: code/modules/mining/lavaland/necropolis_chests.dm:66
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_crusher_trophy_tail_spike>(this);
    }
}

// tg: code/modules/mining/lavaland/necropolis_chests.dm:70
public partial class obj_structure_closet_crate_necropolis_bubblegum : obj_structure_closet_crate_necropolis
{
    // tg: code/modules/mining/lavaland/necropolis_chests.dm:79
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_clothing_suit_hooded_hostile_environment>(this);
        float loot = rand(1, 2);
        if (loot == 1)
            Dm.@new<obj_item_mayhem>(this);
        else if (loot == 2)
            Dm.@new<obj_item_soulscythe>(this);
    }
}

// tg: code/modules/mining/lavaland/necropolis_chests.dm:88
public partial class obj_structure_closet_crate_necropolis_bubblegum_crusher : obj_structure_closet_crate_necropolis_bubblegum
{
    // tg: code/modules/mining/lavaland/necropolis_chests.dm:91
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_crusher_trophy_demon_claws>(this);
    }
}

// tg: code/modules/mining/lavaland/necropolis_chests.dm:95
public partial class obj_structure_closet_crate_necropolis_colossus : obj_structure_closet_crate_necropolis
{
    // tg: code/modules/mining/lavaland/necropolis_chests.dm:98
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        if (hitting_projectile is obj_projectile_colossus)
            return BULLET_ACT_FORCE_PIERCE;
        return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
    }

    // tg: code/modules/mining/lavaland/necropolis_chests.dm:103
    public override async DmTask PopulateContents(params object[] __extra)
    {
        DmList choices = subtypesof(typeof(obj_machinery_anomalous_crystal));
        object random_crystal = pick(choices);
        Dm.@new((Type)random_crystal, this);
        Dm.@new<obj_item_organ_vocal_cords_colossus>(this);
        Dm.@new<obj_item_cain_and_abel>(this);
    }
}

// tg: code/modules/mining/lavaland/necropolis_chests.dm:110
public partial class obj_structure_closet_crate_necropolis_colossus_crusher : obj_structure_closet_crate_necropolis_colossus
{
    // tg: code/modules/mining/lavaland/necropolis_chests.dm:113
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_crusher_trophy_blaster_tubes>(this);
    }
}
