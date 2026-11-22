// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/character/_assignment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks and the visiting trims are generated (D111).

/// Possible assignments corpses can have, both for flavor and to push them towards contributing to the round
// tg: code/modules/lost_crew/character/_assignment.dm:2
public partial class datum_corpse_assignment : datum
{
    // tg: code/modules/lost_crew/character/_assignment.dm:14
    public virtual async DmTask apply_assignment(mob_living_carbon_human working_dead = null, DmList protected_job_gear = null, DmList recovered_job_gear = null, DmList on_revive_and_player_occupancy = null)
    {
        if (protected_job_gear == null && recovered_job_gear == null && trim == null)
            return;

        foreach (object item in Dm.each(protected_job_stuffs))
            Dm.append(ref protected_job_gear, Dm.@new((Type)item));

        foreach (object item in Dm.each(recovered_job_stuffs))
            Dm.append(ref recovered_job_gear, Dm.@new((Type)item));

        if (job_datum != null)
            Dm.append(ref on_revive_and_player_occupancy, Dm.@new<datum_callback>(this, nameof(assign_job), working_dead)); //this needs to happen once the body has been successfully occupied and revived

        if (trim != null)
        {
            var card = Dm.@new<obj_item_card_id_advanced>();
            card.registered_name = working_dead.name;
            card.registered_age = working_dead.age;
            await SSid_access.apply_trim_to_card(card, trim);
            Dm.append(ref protected_job_gear, card);
        }
    }

    // tg: code/modules/lost_crew/character/_assignment.dm:34
    public virtual void assign_job(mob_living_carbon_human working_undead = null)
    {
        working_undead.mind.set_assigned_role_with_greeting(Dm.@new(job_datum) as datum_job, working_undead.client);
    }
}

// tg: code/modules/lost_crew/character/_assignment.dm:74
public partial class datum_corpse_assignment_security : datum_corpse_assignment
{
    // tg: code/modules/lost_crew/character/_assignment.dm:81
    [DmParams("working_dead", "job_gear")]
    public override async DmTask apply_assignment(mob_living_carbon_human working_dead = null, DmList protected_job_gear = null, DmList recovered_job_gear = null, DmList on_revive_and_player_occupancy = null)
    {
        await base.apply_assignment(working_dead, protected_job_gear, recovered_job_gear, on_revive_and_player_occupancy);

        var shield = Dm.@new<obj_item_implant_mindshield>();
        await shield.implant(working_dead);
    }
}
