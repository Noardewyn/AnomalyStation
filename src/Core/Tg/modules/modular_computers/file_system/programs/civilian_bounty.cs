// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/civilian_bounty.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file, the Civilian Bounties app (its NtosCivCargoHoldTerminal window). The var block is generated (D111).

///Everything within this file is an edited form from this file, stripping it of some various components because they are not needed for the PDA app: code/game/machinery/civilian_bounties.dm
// tg: code/modules/modular_computers/file_system/programs/civilian_bounty.dm:6
public partial class datum_computer_file_program_civilianbounties : datum_computer_file_program
{
    ///Percentage of a civilian bounty the civilian will make.
    // tg: code/modules/modular_computers/file_system/programs/civilian_bounty.dm:4  (file-local, #undef at the end)
    private const float CIV_BOUNTY_SPLIT = 30;

    // tg: code/modules/modular_computers/file_system/programs/civilian_bounty.dm:20
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["points"] = points;
        data["status_report"] = status_report;
        data["id_inserted"] = computer.stored_id;
        datum_bank_account account = computer.stored_id?.registered_account;
        if (account != null)
        {
            if (account.civilian_bounty != null)
            {
                data["id_bounty_info"] = account.civilian_bounty.description;
                data["id_bounty_num"] = account.bounty_num();
                data["id_bounty_value"] = Dm.num(account.civilian_bounty.get_bounty_reward()) * (CIV_BOUNTY_SPLIT / 100);
            }
            if (Dm.truthy(account.bounties))
            {
                data["picking"] = TRUE;
                DmList names = Dm.list();
                DmList infos = Dm.list();
                DmList values = Dm.list();
                foreach (datum_bounty bounty in Dm.each_cast<datum_bounty>(account.bounties))
                {
                    Dm.append(ref names, bounty.name);
                    Dm.append(ref infos, bounty.description);
                    Dm.append(ref values, Dm.num(bounty.get_bounty_reward()) * (CIV_BOUNTY_SPLIT / 100));
                }
                data["id_bounty_names"] = names;
                data["id_bounty_infos"] = infos;
                data["id_bounty_values"] = values;
            }
            else
                data["picking"] = FALSE;
        }

        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/civilian_bounty.dm:45
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        switch (Dm.str(action))
        {
            case "pick":
                pick_bounty(@params["value"]);
                break;
            case "bounty":
                await add_bounties(user, 0);
                break;
        }
        return dot;
    }

    ///Here is where cargo bounties are added to the player's bank accounts, then adjusted and scaled into a civilian bounty.
    // tg: code/modules/modular_computers/file_system/programs/civilian_bounty.dm:55
    public virtual async DmTask<object> add_bounties(mob user = null, float cooldown_reduction = 0)
    {
        datum_bank_account id_account = computer.stored_id?.registered_account;
        if (id_account == null)
            return null;
        if ((id_account.civilian_bounty != null || Dm.truthy(id_account.bounties)) && !(world.time >= id_account.bounty_timer)) // COOLDOWN_FINISHED
        {
            string time_left = DisplayTimeText(id_account.bounty_timer - world.time, round_seconds_to: 1); // COOLDOWN_TIMELEFT
            computer.balloon_alert(user, I18n.T("try again in []!", time_left));
            return FALSE;
        }
        if (computer.stored_id.trim == null)
        {
            await computer.say(I18n.T("Requesting ID card has no job assignment registered!"));
            return FALSE;
        }

        DmList crumbs = ((datum_id_trim)computer.stored_id.trim).generate_bounty_list();
        id_account.bounty_timer = world.time + ((5 * MINUTES) - cooldown_reduction); // COOLDOWN_START
        id_account.bounties = crumbs;
        return null;
    }

    /**
     * Proc that assigned a civilian bounty to an ID card, from the list of potential bounties that that bank account currently has available.
     * Available choices are assigned during add_bounties, and one is locked in here.
     *
     * @param choice The index of the bounty in the list of bounties that the player can choose from.
     */
    // tg: code/modules/modular_computers/file_system/programs/civilian_bounty.dm:77
    public virtual datum_bounty pick_bounty(object choice = null)
    {
        datum_bank_account id_account = computer.stored_id?.registered_account;
        if (!Dm.truthy(id_account?.bounties?[choice]))
        {
            playsound(computer.loc, Dm.resource("sound/machines/synth/synth_no.ogg"), 40, TRUE);
            return null;
        }
        id_account.set_bounty(id_account.bounties[choice] as datum_bounty, computer.stored_id);
        SSblackbox.record_feedback("tally", "bounties_assigned", 1, id_account.civilian_bounty.type);
        return id_account.civilian_bounty;
    }
}
