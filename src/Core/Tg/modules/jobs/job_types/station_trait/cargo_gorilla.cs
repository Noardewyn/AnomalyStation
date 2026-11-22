// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/station_trait/cargo_gorilla.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R2 (D327): the whole file: the Cargo Gorilla's job, which the Cargo Gorilla station trait opens: the player who signs up in
// the lobby spawns as the cargorilla (gaps C6) at the gorilla's start, named from tg's list, with a bank account and the gorilla's
// ID. The var block is generated (D111).

// tg: code/modules/jobs/job_types/station_trait/cargo_gorilla.dm:1
public partial class datum_job_cargo_gorilla : datum_job
{
    // tg: code/modules/jobs/job_types/station_trait/cargo_gorilla.dm:21
    public override async DmTask<object> get_roundstart_spawn_point()
    {
        if (Dm.truthy(length(GLOB.gorilla_start)))
            return pick(GLOB.gorilla_start);
        return await base.get_roundstart_spawn_point();
    }

    // tg: code/modules/jobs/job_types/station_trait/cargo_gorilla.dm:26
    public override async DmTask<object> get_spawn_mob(client player_client = null, atom spawn_point = null)
    {
        if (player_client == null)
            return null;
        var the_big_man = Dm.@new(spawn_type, get_turf(spawn_point)) as mob_living;
        await the_big_man.fully_replace_character_name(the_big_man.real_name, pick(GLOB.cargorilla_names));
        return the_big_man;
    }

    // tg: code/modules/jobs/job_types/station_trait/cargo_gorilla.dm:33
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        await base.after_spawn(spawned, player_client);
        // Gorilla with a wage, what's he buyin?
        var bank_account = Dm.@new<datum_bank_account>(spawned.real_name, this);
        await bank_account.payday(STARTING_PAYCHECKS, free: TRUE);
        bank_account.replaceable = FALSE;
        spawned.add_mob_memory(typeof(datum_memory_key_account), Dm.named("remembered_id", bank_account.account_id));

        var gorilla_id = Dm.@new<obj_item_card_id_advanced_cargo_gorilla>(spawned.loc);
        gorilla_id.registered_name = spawned.name;
        gorilla_id.update_label();
        gorilla_id.set_account(bank_account);
        await spawned.put_in_hands(gorilla_id, del_on_fail: TRUE);

        to_chat(spawned, span_boldnotice(I18n.T("You are Cargorilla, a pacifist friend of the station and carrier of freight.")));
        to_chat(spawned, span_notice(I18n.T("You can pick up crates by clicking on them, and drop them by clicking on the ground.")));
        LAZYADD(ref spawned.mind.special_roles, "Cargorilla");
    }
}
