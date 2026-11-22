// Translated from tgstation (snapshot 1d99bd7f): code/datums/dna/blocks/dna_identity_block.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the identity blocks: gender, skin tone, eye colours, hair and facial hair styles, colours and gradients, height. The var blocks are generated (D111).

// tg: code/datums/dna/blocks/dna_identity_block.dm:1
public partial class datum_dna_block_identity_gender : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:3
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        //ignores TRAIT_AGENDER so that a "real" gender can be stored in the DNA if later use is needed
        switch (Dm.str(target.gender))
        {
            case MALE:
                return construct_block(G_MALE, GENDERS);
            case FEMALE:
                return construct_block(G_FEMALE, GENDERS);
            case NEUTER:
                return construct_block(G_NEUTER, GENDERS);
            default:
                return construct_block(G_PLURAL, GENDERS);
        }
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:16
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        //Always plural gender if agender
        if (HAS_TRAIT(target, TRAIT_AGENDER))
        {
            target.gender = PLURAL;
            return;
        }
        switch (Dm.num(deconstruct_block(get_block(dna_hash), GENDERS)))
        {
            case G_MALE:
                target.gender = MALE;
                break;
            case G_FEMALE:
                target.gender = FEMALE;
                break;
            case G_NEUTER:
                target.gender = NEUTER;
                break;
            default:
                target.gender = PLURAL;
                break;
        }
    }
}

// tg: code/datums/dna/blocks/dna_identity_block.dm:31
public partial class datum_dna_block_identity_skin_tone : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:33
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        return construct_block(GLOB.skin_tones.Find(target.skin_tone), length(GLOB.skin_tones));
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:36
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        target.skin_tone = Dm.str(GLOB.skin_tones[deconstruct_block(get_block(dna_hash), length(GLOB.skin_tones))]);
    }
}

/// Holds both the left and right eye color at once
// tg: code/datums/dna/blocks/dna_identity_block.dm:40
public partial class datum_dna_block_identity_eye_colors : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:43
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        object left = sanitize_hexcolor(target.eye_color_left, include_crunch: FALSE);
        object right = sanitize_hexcolor(target.eye_color_right, include_crunch: FALSE);
        return Dm.str(left) + Dm.str(right);
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:48
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        object colors = get_block(dna_hash);
        float right_color_begin = DNA_BLOCK_SIZE_COLOR + 1;
        await target.set_eye_color(sanitize_hexcolor(copytext(colors, 1, right_color_begin)), sanitize_hexcolor(copytext(colors, right_color_begin, length(colors) + 1)));
    }
}

// tg: code/datums/dna/blocks/dna_identity_block.dm:53
public partial class datum_dna_block_identity_hair_style : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:55
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        return construct_block(SSaccessories.hairstyles_list.Find(target.hairstyle), length(SSaccessories.hairstyles_list));
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:58
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        if (HAS_TRAIT(target, TRAIT_BALD))
        {
            target.set_hairstyle("Bald", update: FALSE);
            return;
        }
        object style = SSaccessories.hairstyles_list[deconstruct_block(get_block(dna_hash), length(SSaccessories.hairstyles_list))];
        target.set_hairstyle(style, update: FALSE);
    }
}

// tg: code/datums/dna/blocks/dna_identity_block.dm:65
public partial class datum_dna_block_identity_hair_color : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:68
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        return sanitize_hexcolor(target.hair_color, include_crunch: FALSE);
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:71
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        target.set_haircolor(sanitize_hexcolor(get_block(dna_hash)), update: FALSE);
    }
}

// tg: code/datums/dna/blocks/dna_identity_block.dm:74
public partial class datum_dna_block_identity_facial_style : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:76
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        return construct_block(SSaccessories.facial_hairstyles_list.Find(target.facial_hairstyle), length(SSaccessories.facial_hairstyles_list));
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:79
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        if (HAS_TRAIT(target, TRAIT_SHAVED))
        {
            target.set_facial_hairstyle("Shaved", update: FALSE);
            return;
        }
        object style = SSaccessories.facial_hairstyles_list[deconstruct_block(get_block(dna_hash), length(SSaccessories.facial_hairstyles_list))];
        target.set_facial_hairstyle(style, update: FALSE);
    }
}

// tg: code/datums/dna/blocks/dna_identity_block.dm:86
public partial class datum_dna_block_identity_facial_color : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:89
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        return sanitize_hexcolor(target.facial_hair_color, include_crunch: FALSE);
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:92
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        target.set_facial_haircolor(sanitize_hexcolor(get_block(dna_hash)), update: FALSE);
    }
}

// tg: code/datums/dna/blocks/dna_identity_block.dm:95
public partial class datum_dna_block_identity_hair_gradient : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:97
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        return construct_block(SSaccessories.hair_gradients_list.Find(target.get_hair_gradient_style(GRADIENT_HAIR_KEY)), length(SSaccessories.hair_gradients_list));
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:100
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        object gradient_style = SSaccessories.hair_gradients_list[deconstruct_block(get_block(dna_hash), length(SSaccessories.hair_gradients_list))];
        target.set_hair_gradient_style(gradient_style, update: FALSE);
    }
}

// tg: code/datums/dna/blocks/dna_identity_block.dm:104
public partial class datum_dna_block_identity_hair_gradient_color : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:107
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        return sanitize_hexcolor(target.get_hair_gradient_color(GRADIENT_HAIR_KEY), include_crunch: FALSE);
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:110
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        target.set_hair_gradient_color(sanitize_hexcolor(get_block(dna_hash)), update: FALSE);
    }
}

// tg: code/datums/dna/blocks/dna_identity_block.dm:113
public partial class datum_dna_block_identity_facial_gradient : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:115
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        return construct_block(SSaccessories.facial_hair_gradients_list.Find(target.get_hair_gradient_style(GRADIENT_FACIAL_HAIR_KEY)), length(SSaccessories.facial_hair_gradients_list));
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:118
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        object gradient_style = SSaccessories.facial_hair_gradients_list[deconstruct_block(get_block(dna_hash), length(SSaccessories.facial_hair_gradients_list))];
        target.set_facial_hair_gradient_style(gradient_style, update: FALSE);
    }
}

// tg: code/datums/dna/blocks/dna_identity_block.dm:122
public partial class datum_dna_block_identity_facial_gradient_color : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:125
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        return sanitize_hexcolor(target.get_hair_gradient_color(GRADIENT_FACIAL_HAIR_KEY), include_crunch: FALSE);
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:128
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
        target.set_facial_hair_gradient_color(sanitize_hexcolor(get_block(dna_hash)), update: FALSE);
    }
}

// tg: code/datums/dna/blocks/dna_identity_block.dm:131
public partial class datum_dna_block_identity_height : datum_dna_block_identity
{
    // tg: code/datums/dna/blocks/dna_identity_block.dm:143
    public override object create_unique_block(mob_living_carbon_human target = null)
    {
        float max_height_index = length(dna_heights);
        float found = dna_heights.Find(target.get_base_mob_height());
        float mob_height_index = found != 0 ? found : dna_heights.Find(HUMAN_HEIGHT_MEDIUM);
        return construct_block(mob_height_index, max_height_index, block_length);
    }

    // tg: code/datums/dna/blocks/dna_identity_block.dm:148
    public override async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        float max_height_index = length(dna_heights);
        object mob_height_index = deconstruct_block(get_block(dna_hash), max_height_index, block_length);
        object height = dna_heights[mob_height_index];
        await target.set_mob_height(Dm.truthy(height) ? Dm.num(height) : HUMAN_HEIGHT_MEDIUM, update_dna: FALSE);
    }
}
