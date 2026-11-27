// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/station_trait/veteran_advisor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the Veteran Security Advisor's job, which the Veteran Advisor station trait opens. The player who signs up in the
// lobby starts on a security officer's spot with the outfit's gear (the M1911 holster, the telescopic baton, the cane, the cigar, the
// mindshield) and the PTSD trauma. The paraplegic quirk tg gives him at spawn waits for the quirks (docs/ai/specs/not-translated.md),
// so he walks. The job's and the outfit's var blocks are generated.

// tg: code/modules/jobs/job_types/station_trait/veteran_advisor.dm:1
public partial class datum_job_veteran_advisor : datum_job
{
    // tg: code/modules/jobs/job_types/station_trait/veteran_advisor.dm:40
    public override object get_default_roundstart_spawn_point()
    {
        object dot = null;
        foreach (obj_effect_landmark_start spawn_point in Dm.each<obj_effect_landmark_start>(GLOB.start_landmarks_list))
        {
            if (!Dm.eq(spawn_point.name, "Security Officer"))
                continue;
            dot = spawn_point;
            if (Dm.truthy(spawn_point.used)) //so we can revert to spawning them on top of eachother if something goes wrong
                continue;
            spawn_point.used = TRUE;
            break;
        }
        if (dot == null) // Try to fall back to "our" landmark
            dot = base.get_default_roundstart_spawn_point();
        if (dot == null)
            log_mapping(Dm.text("Job [] ([]) couldn't find a round start spawn point.", title, type));
        return dot;
    }

    // tg: code/modules/jobs/job_types/station_trait/veteran_advisor.dm:54
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        await base.after_spawn(spawned, player_client);
        var veteran = spawned as mob_living_carbon;
        // spawned.add_quirk(/datum/quirk/paraplegic) //Even in 2300s veterans are getting it bad
        Dm.Deferred("code/modules/jobs/job_types/station_trait/veteran_advisor.dm:57", "after_spawn: the paraplegic quirk and its wheelchair wait for the quirks");
        if (veteran != null)
            await veteran.gain_trauma(typeof(datum_brain_trauma_special_ptsd)); //War, war never changes...
    }
}
