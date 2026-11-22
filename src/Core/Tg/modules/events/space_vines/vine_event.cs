// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/space_vines/vine_event.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4b (D327): the space vines event (kudzu takes root on a free hallway floor with one random mutation, a potency of 50 to 100
// and a production of 1 to 4) but its admin options (/datum/event_admin_setup/*: the spot, the starting mutations, the potency and
// the production; the admins' Trigger Event panel, D275). The var blocks are generated (D111).

// tg: code/modules/events/space_vines/vine_event.dm:18
public partial class datum_round_event_spacevine : datum_round_event
{
    // tg: code/modules/events/space_vines/vine_event.dm:31
    public override async DmTask<object> start()
    {
        DmList final_turf_candidates = Dm.list(); // final list of eligible empty floor turfs in the hallway areas that can be chosen

        if (override_turf != null)
            Dm.append(ref final_turf_candidates, override_turf);
        else
        {
            obj_structure_spacevine vine = Dm.@new<obj_structure_spacevine>();
            DmList floor_candidates = Dm.list();
            foreach (area_station_hallway area in Dm.each<area_station_hallway>(shuffle(GLOB.areas)))
            {
                foreach (turf_open floor__1 in Dm.each<turf_open>(area.get_turfs_from_all_zlevels()))
                {
                    if (isopenspaceturf(floor__1))
                        continue;
                    Dm.append(ref floor_candidates, floor__1);
                }
            }

            // Enter() is expensive to call on potentially hundreds to thousands of turfs at once and can even lead to server crashes.
            // We can pick() a subset instead and get close enough results at a fraction of the cost.
            float turfs_to_test = 100;
            DmList sampled_floor_candidates = pick_n(floor_candidates, min(turfs_to_test, length(floor_candidates))) as DmList; // results in at most 100 calls of Enter(), a reasonable amount while still feeling random.

            foreach (turf_open floor__2 in Dm.each_cast<turf_open>(sampled_floor_candidates))
            {
                if (Dm.truthy(await floor__2.Enter(vine)))
                    Dm.append(ref final_turf_candidates, floor__2);
            }
            qdel(vine);
        }

        if (!Dm.truthy(length(final_turf_candidates)))
            return null;

        // Pick a turf to spawn at if we can
        turf floor = pick(final_turf_candidates) as turf;
        DmList selected_mutations = Dm.list();

        if (Dm.truthy(mutations_overridden))
            selected_mutations = override_mutations;
        else
            selected_mutations = Dm.list(pick(valid_subtypesof(typeof(datum_spacevine_mutation))));

        if (potency == null)
            potency = rand(50, 100);

        if (production == null)
            production = rand(1, 4);

        await Dm.new_async<datum_spacevine_controller>(floor, selected_mutations, potency, production, this); //spawn a controller at turf with randomized stats and a single random mutation
        return null;
    }
}

// tg: code/modules/events/space_vines/vine_event.dm:75-126  the admin options /datum/event_admin_setup/set_location/spacevine,
// multiple_choice/spacevine, input_number/spacevine_potency and input_number/spacevine_production (prompt_admins, get_options,
// apply_to_event): the admins' Trigger Event panel (D275), not translated (their var blocks are generated).
