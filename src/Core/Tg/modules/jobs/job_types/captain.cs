// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/captain.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G2 (D274): the whole file (the MODsuit outfit, an admin's pick, has only vars; its MODsuit is live since gaps MO1). The station charter is an inert
// item (its rename of the station waits).

// tg: code/modules/jobs/job_types/captain.dm:1
public partial class datum_job_captain : datum_job
{
    // tg: code/modules/jobs/job_types/captain.dm:55
    public override object get_captaincy_announcement(mob_living captain = null)
    {
        return Dm.text("Captain [] on deck!", captain.real_name);
    }

    // tg: code/modules/jobs/job_types/captain.dm:58
    public override object get_radio_information()
    {
        object dot = base.get_radio_information();
        return Dm.text("[]\nYou have access to all radio channels, but they are not automatically tuned. Check your radio for more information.", dot);
    }
}

// tg: code/modules/jobs/job_types/captain.dm:63
public partial class datum_outfit_job_captain : datum_outfit_job
{
    // tg: code/modules/jobs/job_types/captain.dm:99
    public override async DmTask pre_equip(mob_living_carbon_human H = null, float? visuals_only = null)
    {
        await base.pre_equip(H, visuals_only);
        // CHECK_MAP_JOB_CHANGE(JOB_CAPTAIN, "special_charter") (__DEFINES/maps.dm:243), expanded inline (rules 11.1)
        special_charter = (SSmapping?.current_map?.job_changes?[JOB_CAPTAIN] as DmList)?["special_charter"];
        if (!Dm.truthy(special_charter))
            return;

        Dm.remove(ref backpack_contents, typeof(obj_item_station_charter));

        if (l_hand == null)
            l_hand = typeof(obj_item_station_charter_banner);
        else if (r_hand == null)
            r_hand = typeof(obj_item_station_charter_banner);
    }

    // tg: code/modules/jobs/job_types/captain.dm:112
    public override async DmTask post_equip(mob_living_carbon_human equipped = null, float? visuals_only = null)
    {
        await base.post_equip(equipped, visuals_only);
        if (Dm.truthy(visuals_only) || !Dm.truthy(special_charter))
            return;

        var celestial_charter = Dm.locate<obj_item_station_charter_banner>(equipped.held_items);
        if (celestial_charter == null)
        {
            // failed to give out the unique charter, plop on the ground
            celestial_charter = Dm.@new<obj_item_station_charter_banner>(get_turf(equipped));
        }

        celestial_charter.name_type = Dm.str(special_charter);
    }
}
