// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/assistant/gimmick_assistants.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R2 (D327): the file's procs, the loadouts of the Geared Assistants Pilot station trait (the outfits themselves are their
// generated var blocks, D111). Some of the costumes they hand out are not ported yet and are props (not-translated.md section 183).

// This file contains the louadouts for the assistant gimmicks station trait! When active, gives assistants random stupid gear

/// Parent type of gimmick loadouts for assistants for the functional assistant station traits
// tg: code/modules/jobs/job_types/assistant/gimmick_assistants.dm:4
public partial class datum_outfit_job_assistant_gimmick : datum_outfit_job_assistant
{
    // tg: code/modules/jobs/job_types/assistant/gimmick_assistants.dm:10
    public override void give_jumpsuit(mob_living_carbon_human target = null)
    {
        return; //dont do colorized and stuff, it messes with our uniforms
    }
}

// tg: code/modules/jobs/job_types/assistant/gimmick_assistants.dm:38
public partial class datum_outfit_job_assistant_gimmick_cyborg : datum_outfit_job_assistant_gimmick
{
    // tg: code/modules/jobs/job_types/assistant/gimmick_assistants.dm:50
    public override async DmTask post_equip(mob_living_carbon_human equipped = null, float? visuals_only = null)
    {
        await base.post_equip(equipped, visuals_only);
        var robotongue = Dm.@new<obj_item_organ_tongue_robot>();
        await robotongue.Insert(equipped, movement_flags: DELETE_IF_REPLACED);
    }
}

// tg: code/modules/jobs/job_types/assistant/gimmick_assistants.dm:97
public partial class datum_outfit_job_assistant_gimmick_mopper : datum_outfit_job_assistant_gimmick
{
    // tg: code/modules/jobs/job_types/assistant/gimmick_assistants.dm:106
    public override async DmTask post_equip(mob_living_carbon_human equipped = null, float? visuals_only = null)
    {
        await base.post_equip(equipped, visuals_only);

        foreach (turf turf in Dm.each<turf>(range(1, equipped)))
        {
            if (Dm.truthy(await turf.is_blocked_turf()))
                continue;
            var bucket = Dm.@new<obj_structure_mop_bucket>(turf);
            await equipped.start_pulling(bucket);
            break;
        }
    }
}
