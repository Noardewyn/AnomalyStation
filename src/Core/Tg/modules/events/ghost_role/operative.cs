// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/ghost_role/operative.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the lone operative, whose weight the nuclear disk raises while it is left alone). The var blocks
// are generated (D111).

// tg: code/modules/events/ghost_role/operative.dm:1
public partial class datum_round_event_control_operative : datum_round_event_control
{
    // tg: code/modules/events/ghost_role/operative.dm:9
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        return Dm.truthy(base.can_spawn_event(players_amt, allow_magic)) && Dm.truthy(SSdynamic.antag_events_enabled) ? TRUE : FALSE;
    }
}

// tg: code/modules/events/ghost_role/operative.dm:12
public partial class datum_round_event_ghost_role_operative : datum_round_event_ghost_role
{
    // tg: code/modules/events/ghost_role/operative.dm:17
    public override async DmTask<object> spawn_role()
    {
        var chosen_one = await SSpolling.poll_ghost_candidates(check_jobban: ROLE_OPERATIVE, role: ROLE_LONE_OPERATIVE, alert_pic: typeof(obj_machinery_nuclearbomb), amount_to_pick: 1) as mob;
        if (chosen_one == null)
            return NOT_ENOUGH_PLAYERS;
        var spawn_location = find_space_spawn() as turf;
        if (spawn_location == null)
            return MAP_ERROR;
        var operative = Dm.@new<mob_living_carbon_human>(spawn_location);
        await operative.randomize_human_appearance(Dm.bitnot(RANDOMIZE_SPECIES));
        operative.dna.update_dna_identity();
        var Mind = Dm.@new<datum_mind>(chosen_one.key);
        Mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_lone_operative)) as datum_job);
        Mind.active = TRUE;
        await Mind.transfer_to(operative);
        if (!Dm.truthy(operative.client?.prefs.read_preference(typeof(datum_preference_toggle_nuke_ops_species))))
        {
            object species_type = operative.client.prefs.read_preference(typeof(datum_preference_choiced_species));
            operative.set_species(species_type); //Apply the preferred species to our freshly-made body.
        }

        await Mind.add_antag_datum(typeof(datum_antagonist_nukeop_lone));

        message_admins(Dm.text("[] has been made into lone operative by an event.", key_name_admin(operative))); // ADMIN_LOOKUPFLW: its admin links are the admin tools' (M10)
        operative.log_message("was spawned as a lone operative by an event.", LOG_GAME);
        Dm.append(ref spawned_mobs, operative);
        return SUCCESSFUL_SPAWN;
    }
}
