// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/create_mob.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    /**
     * Fully randomizes everything about a human, including DNA and name.
     */
    // tg: code/modules/admin/create_mob.dm:4
    public static async DmTask randomize_human(mob_living_carbon_human human = null, float? randomize_mutations = null)
    {
        randomize_mutations ??= FALSE;
        human.gender = (Dm.truthy(((datum_species)human.dna.species).sexes) ? (string)pick(MALE, FEMALE, PLURAL, NEUTER) : PLURAL);
        human.physique = human.gender;
        // as in randomize_human_normie: a test harness world without tg's strings files has empty name lists; its human keeps
        // its name
        if (Dm.length(GLOB.last_names) > 0)
        {
            human.real_name = human.generate_random_mob_name();
            human.name = Dm.str(human.get_visible_name());
        }
        human.set_hairstyle(random_hairstyle(human.gender), update: FALSE);
        human.set_facial_hairstyle(random_facial_hairstyle(human.gender), update: FALSE);
        human.set_haircolor("#" + Dm.str(random_color()), update: FALSE);
        human.set_facial_haircolor(human.hair_color, update: FALSE);
        await human.set_eye_color(random_eye_color());
        human.skin_tone = (string)pick(GLOB.skin_tones);
        ((datum_species)human.dna.species).randomize_active_underwear_only(human);
        // Needs to be called towards the end to update all the UIs just set above
        human.dna.initialize_dna(newblood_type: random_human_blood_type(), create_mutation_blocks: randomize_mutations, randomize_features: TRUE);
        // Snowflake for Ethereals
        await human.updatehealth();
        await human.updateappearance(mutcolor_update: TRUE);
    }

    /**
     * Randomizes a human, but produces someone who looks exceedingly average (by most standards).
     *
     * (IE, no wacky hair styles / colors)
     */
    // tg: code/modules/admin/create_mob.dm:28
    public static async DmTask randomize_human_normie(mob_living_carbon_human human = null, float? randomize_mutations = null, float? update_body = null)
    {
        randomize_mutations ??= FALSE;
        update_body ??= TRUE;
        // Sorry enbys but statistically you are not average enough
        human.gender = (Dm.truthy(((datum_species)human.dna.species).sexes) ? (string)pick(MALE, FEMALE) : PLURAL);
        human.physique = human.gender;
        // the syndicate campaign G1c (D316, the mulligan serum): the names are translated now. GLOB.last_names?: a test harness world
        // without tg's strings files (TgStrings not mounted) has empty name lists (rules 10.6, as SSid_access?); its human keeps its name
        if (Dm.length(GLOB.last_names) > 0)
        {
            human.real_name = human.generate_random_mob_name();
            human.name = Dm.str(human.get_visible_name());
        }
        human.voice = SStts?.random_tts_voice(human.gender); // a toy room has no SStts
        await human.set_eye_color(random_eye_color());
        human.skin_tone = (string)pick(GLOB.skin_tones);
        // No underwear generation handled here
        object picked_color = random_hair_color();
        human.set_haircolor(picked_color, update: FALSE);
        human.set_facial_haircolor(picked_color, update: FALSE);
        var hairstyle = SSaccessories.hairstyles_list[random_hairstyle(human.gender)] as datum_sprite_accessory;
        if (hairstyle != null && Dm.truthy(hairstyle.natural_spawn) && !Dm.truthy(hairstyle.locked))
            human.set_hairstyle(hairstyle.name, update: FALSE);
        var facial_hair = SSaccessories.facial_hairstyles_list[random_facial_hairstyle(human.gender)] as datum_sprite_accessory;
        if (facial_hair != null && Dm.truthy(facial_hair.natural_spawn) && !Dm.truthy(facial_hair.locked))
            human.set_facial_hairstyle(facial_hair.name, update: FALSE);
        // Normal DNA init stuff, these can generally be wacky but we care less, they're aliens after all
        human.dna.initialize_dna(newblood_type: random_human_blood_type(), create_mutation_blocks: randomize_mutations, randomize_features: TRUE);
        await human.updatehealth();
        if (Dm.truthy(update_body))
            await human.updateappearance(mutcolor_update: TRUE);
    }
}
