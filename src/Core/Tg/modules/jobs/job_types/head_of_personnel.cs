// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/head_of_personnel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice G2, D274): the captaincy announcement and the outfit's holiday undershirt; syndicate A2: the traitor objective
// (the captain replacement); syndicate G4a (D316): the mail goodies.

// tg: code/modules/jobs/job_types/head_of_personnel.dm:1
public partial class datum_job_head_of_personnel : datum_job
{
    // tg: code/modules/jobs/job_types/head_of_personnel.dm:50
    public override object get_captaincy_announcement(mob_living captain = null)
    {
        return Dm.text("Due to staffing shortages, newly promoted Acting Captain [] on deck!", captain.real_name);
    }

    // tg: code/modules/jobs/job_types/head_of_personnel.dm:53
    public override object generate_traitor_objective()
    {
        datum_objective_assassinate_captain_replacement promotion = Dm.@new<datum_objective_assassinate_captain_replacement>();
        promotion.target = Waiting.NoSleep(promotion.find_target()) as datum_mind;
        if (promotion.target == null)
        {
            qdel(promotion);
            return null;
        }

        promotion.update_explanation_text();
        return promotion;
    }
}

/// Special assassination objective to kill the cap, take their id, and become the new captain
// tg: code/modules/jobs/job_types/head_of_personnel.dm:65
public partial class datum_objective_assassinate_captain_replacement : datum_objective_assassinate
{
    // tg: code/modules/jobs/job_types/head_of_personnel.dm:68
    public override void update_explanation_text()
    {
        base.update_explanation_text();
        explanation_text = Dm.text("Assassinate [], the Captain, and steal [] ID card.", target.name, target.p_their());
    }

    // tg: code/modules/jobs/job_types/head_of_personnel.dm:72
    public override object check_completion()
    {
        if (Dm.truthy(completed))
            return TRUE;
        if (!Dm.truthy(base.check_completion()))
            return FALSE;

        foreach (datum_mind hop in Dm.each<datum_mind>(get_owners()))
        {
            if (!isliving(hop.current))
                continue;

            if (Dm.locate<obj_item_card_id_advanced_gold>(hop.current.get_all_contents()) != null)
                return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/jobs/job_types/head_of_personnel.dm:87
    public override async DmTask<object> find_target(object dupe_search_range = null, DmList blacklist = null)
    {
        foreach (datum_mind fellow_head in Dm.each<datum_mind>(Dm.sub(SSjob.get_all_heads(), blacklist)))
        {
            if (is_captain_job(fellow_head.assigned_role))
                return fellow_head;
        }
        return null;
    }
}

// tg: code/modules/jobs/job_types/head_of_personnel.dm:76
public partial class datum_outfit_job_hop : datum_outfit_job
{
    // tg: code/modules/jobs/job_types/head_of_personnel.dm:116
    public override async DmTask pre_equip(mob_living_carbon_human H = null, float? visuals_only = null)
    {
        await base.pre_equip(H, visuals_only);
        if (Dm.truthy(check_holidays(IAN_HOLIDAY)))
            undershirt = typeof(datum_sprite_accessory_clothing_undershirt_ian);
    }
}

// tg: code/modules/jobs/job_types/head_of_personnel.dm:122  (syndicate G4a, D316: the mail)
public partial class datum_job_head_of_personnel : datum_job
{
    // tg: code/modules/jobs/job_types/head_of_personnel.dm:122
    public override object get_mail_goodies(mob recipient = null)
    {
        object dot = base.get_mail_goodies(recipient);
        // Strange Reagent if the pet is dead.
        foreach (mob_living_basic_pet_dog_corgi_ian staff_pet in Dm.each<mob_living_basic_pet_dog_corgi_ian>(GLOB.dead_mob_list))
        {
            Dm.append(ref dot, Dm.list(Dm.kv(typeof(datum_reagent_medicine_strange_reagent), 20)));
            break;
        }
        return dot;
    }
}
