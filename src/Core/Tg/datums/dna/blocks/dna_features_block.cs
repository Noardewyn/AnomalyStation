// Translated from tgstation (snapshot 1d99bd7f): code/datums/dna/blocks/dna_features_block.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the feature blocks: the mutant and fish tail colours and the sprite accessories of the other species' parts. The var blocks are generated (D111).

// tg: code/datums/dna/blocks/dna_features_block.dm:1
public partial class datum_dna_block_feature_mutant_color : datum_dna_block_feature
{
    // tg: code/datums/dna/blocks/dna_features_block.dm:5
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        return sanitize_hexcolor(target.dna.features[feature_key], include_crunch: FALSE);
    }

    // tg: code/datums/dna/blocks/dna_features_block.dm:8
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        target.dna.features[feature_key] = sanitize_hexcolor(get_block(dna_hash));
    }
}

// tg: code/datums/dna/blocks/dna_features_block.dm:11
public partial class datum_dna_block_feature_fish_tail_color : datum_dna_block_feature
{
    // tg: code/datums/dna/blocks/dna_features_block.dm:15
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        return sanitize_hexcolor(target.dna.features[FEATURE_TAIL_FISH_COLOR], include_crunch: FALSE);
    }

    // tg: code/datums/dna/blocks/dna_features_block.dm:18
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        target.dna.features[feature_key] = sanitize_hexcolor(get_block(dna_hash));
    }
}

/// Features tied to a sprite accessory
// tg: code/datums/dna/blocks/dna_features_block.dm:22
public partial class datum_dna_block_feature_accessory : datum_dna_block_feature
{
    // tg: code/datums/dna/blocks/dna_features_block.dm:25
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        DmList accessories = SSaccessories.feature_list[feature_key] as DmList;
        float block_value = accessories.Find(target.dna.features[feature_key]);
        float max_value = length(accessories);
        return construct_block(block_value, max_value);
    }

    // tg: code/datums/dna/blocks/dna_features_block.dm:30
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        object block_value = get_block(dna_hash);
        float max_value = length(SSaccessories.feature_list[feature_key]);
        object deconstructed = deconstruct_block(block_value, max_value);
        target.dna.features[feature_key] = (SSaccessories.feature_list[feature_key] as DmList)[deconstructed];
    }
}

// tg: code/datums/dna/blocks/dna_features_block.dm:65
public partial class datum_dna_block_feature_accessory_moth_wing : datum_dna_block_feature_accessory
{
    // tg: code/datums/dna/blocks/dna_features_block.dm:68
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        if (Dm.eq(target.dna.features[feature_key], "Burnt Off")) // Why is this snowflake check a thing. Please find a way to fix this later
            return random_string(block_length, GLOB.hex_characters);
        return base.create_unique_block(target);
    }
}

// tg: code/datums/dna/blocks/dna_features_block.dm:73
public partial class datum_dna_block_feature_accessory_moth_antenna : datum_dna_block_feature_accessory
{
    // tg: code/datums/dna/blocks/dna_features_block.dm:76
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        if (Dm.eq(target.dna.features[feature_key], "Burnt Off"))
            return random_string(block_length, GLOB.hex_characters);
        return base.create_unique_block(target);
    }
}
