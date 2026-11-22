// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/station_trait/bridge_assistant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R2 (D327): the whole file: the Bridge Assistant's job, which the Bridge Assistant station trait opens: the player who signs up
// in the lobby starts on the bridge, a chair first, and gains the axe adoration trauma (live since the brain traumas, gaps B1). The
// job's and the outfit's var blocks are generated (D111).

// tg: code/modules/jobs/job_types/station_trait/bridge_assistant.dm:1
public partial class datum_job_bridge_assistant : datum_job
{
    // tg: code/modules/jobs/job_types/station_trait/bridge_assistant.dm:38
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        await base.after_spawn(spawned, player_client);
        var bridgie = spawned as mob_living_carbon;
        if (istype(bridgie, typeof(mob_living_carbon)))
            await bridgie.gain_trauma(typeof(datum_brain_trauma_special_axedoration));
    }

    // tg: code/modules/jobs/job_types/station_trait/bridge_assistant.dm:44
    public override async DmTask<object> get_roundstart_spawn_point()
    {
        DmList chair_turfs = Dm.list();
        DmList possible_turfs = Dm.list();
        var bridge = GLOB.areas_by_type[typeof(area_station_command_bridge)] as area;
        if (bridge == null)
            return await base.get_roundstart_spawn_point(); //if no bridge, spawn on the arrivals shuttle (but also what the fuck)
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(bridge.get_zlevel_turf_lists()))
        {
            foreach (turf possible_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                if (Dm.truthy(await possible_turf.is_blocked_turf()))
                    continue;
                if (Dm.locate<obj_structure_chair>(possible_turf) != null)
                {
                    Dm.append(ref chair_turfs, possible_turf);
                    continue;
                }
                Dm.append(ref possible_turfs, possible_turf);
            }
        }
        if (Dm.truthy(length(chair_turfs)))
            return pick(chair_turfs); //prioritize turfs with a chair
        if (Dm.truthy(length(possible_turfs)))
            return pick(possible_turfs); //if none, just pick a random turf in the bridge
        return await base.get_roundstart_spawn_point(); //if the bridge has no turfs, spawn on the arrivals shuttle
    }
}
