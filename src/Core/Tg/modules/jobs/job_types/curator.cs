// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/curator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps TL1: the outfit's procs (the job and the outfit's vars are generated): the round's first curator packs the broadcast
// camera, and every curator knows every language.

// tg: code/modules/jobs/job_types/curator.dm:39
public partial class datum_outfit_job_curator : datum_outfit_job
{
    // tg: code/modules/jobs/job_types/curator.dm:58
    public override async DmTask pre_equip(mob_living_carbon_human H = null, float? visuals_only = null)
    {
        if (Dm.truthy(visuals_only))
        {
            await base.pre_equip(H, visuals_only);
            return;
        }

        /// There can be only one cameraman on this station, and no, not that kind
        if (!TgState.Current.datum_outfit_job_curator__cameraman_choosen)
        {
            backpack_contents ??= Dm.list();
            backpack_contents[typeof(obj_item_broadcast_camera)] = 1;
            TgState.Current.datum_outfit_job_curator__cameraman_choosen = true;
        }
        await base.pre_equip(H, visuals_only);
    }

    // tg: code/modules/jobs/job_types/curator.dm:70
    public override async DmTask post_equip(mob_living_carbon_human translator = null, float? visuals_only = null)
    {
        await base.post_equip(translator, visuals_only);

        if (Dm.truthy(visuals_only))
            return;

        translator.grant_all_languages(source: LANGUAGE_CURATOR);
        translator.remove_blocked_language(GLOB.all_languages, source: LANGUAGE_ALL);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/jobs/job_types/curator.dm:63  var/static/cameraman_choosen = FALSE
    internal bool datum_outfit_job_curator__cameraman_choosen;
}
