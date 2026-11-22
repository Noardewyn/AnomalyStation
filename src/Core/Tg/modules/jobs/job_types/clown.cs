// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/clown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the job and the outfit. The clown's April fools blood type waits for the holidays (not-translated.md 106).

// tg: code/modules/jobs/job_types/clown.dm:1
public partial class datum_job_clown : datum_job
{
    // tg: code/modules/jobs/job_types/clown.dm:41
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        if (ishuman(spawned))
        {
            await spawned.apply_pref_name(typeof(datum_preference_name_clown), player_client);
            if (Dm.truthy(check_holidays(APRIL_FOOLS))) // Clown blood is real
            {
                mob_living_carbon_human human_clown = spawned as mob_living_carbon_human;
                human_clown.set_blood_type(typeof(datum_blood_type_clown));
            }
        }

        await base.after_spawn(spawned, player_client);
    }
}

// tg: code/modules/jobs/job_types/clown.dm:51
public partial class datum_outfit_job_clown : datum_outfit_job
{
    // tg: code/modules/jobs/job_types/clown.dm:87
    public override async DmTask pre_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        await base.pre_equip(user, visuals_only);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_BANANIUM_SHIPMENTS))
            backpack_contents[typeof(obj_item_stack_sheet_mineral_bananium_five)] = 1;
    }

    // tg: code/modules/jobs/job_types/clown.dm:92
    public override object get_types_to_preload()
    {
        DmList dot = base.get_types_to_preload() as DmList;
        if (HAS_TRAIT(SSstation, STATION_TRAIT_BANANIUM_SHIPMENTS))
            Dm.append(ref dot, typeof(obj_item_stack_sheet_mineral_bananium_five));
        return dot;
    }

    // tg: code/modules/jobs/job_types/clown.dm:97
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        await base.post_equip(user, visuals_only);
        if (Dm.truthy(visuals_only))
            return;

        await user.fully_replace_character_name(user.real_name, pick(GLOB.clown_names)); //rename the mob AFTER they're equipped so their ID gets updated properly.
        await user.dna.add_mutation(typeof(datum_mutation_clumsy), MUTATION_SOURCE_CLOWN_CLUMSINESS);
        ADD_TRAIT(user, TRAIT_CLOWN_ENJOYER, INNATE_TRAIT);
        user.add_faction(FACTION_CLOWN);
    }
}
