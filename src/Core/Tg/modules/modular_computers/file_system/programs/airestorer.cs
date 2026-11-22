// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/airestorer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the NtOS AI Manager & Restorer, an intelliCard slot that revives the AI on the card. The var
// block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/airestorer.dm:1
public partial class datum_computer_file_program_ai_restorer : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/airestorer.dm:19
    public override object on_examine(obj_item_modular_computer source = null, mob user = null)
    {
        DmList examine_text = Dm.list();
        if (stored_card == null)
        {
            Dm.append(ref examine_text, I18n.T("It has a slot installed for an intelliCard."));
            return examine_text;
        }

        if (Dm.truthy(Waiting.NoSleep(computer.Adjacent(user))))
            Dm.append(ref examine_text, I18n.T("It has a slot installed for an intelliCard which contains: []", stored_card.name));
        else
            Dm.append(ref examine_text, I18n.T("It has a slot installed for an intelliCard, which appears to be occupied."));
        Dm.append(ref examine_text, span_info(I18n.T("Alt-click to eject the intelliCard.")));
        return examine_text;
    }

    // tg: code/modules/modular_computers/file_system/programs/airestorer.dm:32
    public override object kill_program(mob user = null)
    {
        Waiting.NoSleep(try_eject(forced: TRUE));
        return base.kill_program(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/airestorer.dm:36
    public override object process_tick(object seconds_per_tick = null)
    {
        base.process_tick(seconds_per_tick);
        if (!Dm.truthy(restoring)) //Put the check here so we don't check for an ai all the time
            return null;

        mob_living_silicon_ai A = stored_card.AI;
        if (Dm.truthy(stored_card.flush))
        {
            restoring = FALSE;
            return null;
        }
        Waiting.NoSleep(A.adjust_oxy_loss(-5, FALSE));
        Waiting.NoSleep(A.adjust_fire_loss(-5, FALSE));
        Waiting.NoSleep(A.adjust_brute_loss(-5, FALSE));

        // Please don't forget to update health, otherwise the below if statements will probably always fail.
        Waiting.NoSleep(A.updatehealth());
        if (Dm.num(A.health) >= 0 && Dm.eq(A.stat, DEAD))
        {
            Waiting.NoSleep(A.revive());
            stored_card.update_appearance();
        }

        // Finished restoring
        if (Dm.num(A.health) >= 100)
            restoring = FALSE;

        return TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/airestorer.dm:63
    public override async DmTask<object> application_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (tool is obj_item_aicard card)
            return await aicard_act(user, card);
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/airestorer.dm:67
    public virtual async DmTask<object> aicard_act(mob_living user = null, obj_item_aicard used_aicard = null)
    {
        if (computer == null)
            return NONE;
        if (stored_card != null)
        {
            to_chat(user, span_warning(I18n.T("You try to insert \\the [] into \\the [], but the slot is occupied.", used_aicard, computer.name)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(used_aicard, computer)))
            return ITEM_INTERACT_BLOCKING;

        stored_card = used_aicard;
        to_chat(user, span_notice(I18n.T("You insert \\the [] into \\the [].", used_aicard, computer.name)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/modular_computers/file_system/programs/airestorer.dm:80
    public override async DmTask<object> try_eject(mob_living user = null, object forced = null)
    {
        forced ??= FALSE;
        if (stored_card == null)
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("There is no card in \\the [].", computer.name)));
            return FALSE;
        }

        if (Dm.truthy(restoring) && !Dm.truthy(forced))
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("Safeties prevent you from removing the card until reconstruction is complete...")));
            return FALSE;
        }

        if (user != null && Dm.truthy(await computer.Adjacent(user)))
        {
            to_chat(user, span_notice(I18n.T("You remove [] from [].", stored_card, computer.name)));
            await user.put_in_hands(stored_card);
        }
        else
            await stored_card.forceMove(computer.drop_location() as atom);

        stored_card = null;
        restoring = FALSE;
        return TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/airestorer.dm:104
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (Dm.str(action))
        {
            case "PRG_beginReconstruction":
            {
                if (stored_card == null || stored_card.AI == null)
                    return FALSE;
                mob_living_silicon_ai A = stored_card.AI;
                if (A != null && Dm.num(A.health) < 100)
                {
                    restoring = TRUE;
                    A.notify_revival("Your core files are being restored!", source: computer);
                }
                return TRUE;
            }
            case "PRG_eject":
                if (stored_card != null)
                {
                    await try_eject(usr as mob_living);
                    return TRUE;
                }
                break;
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/airestorer.dm:118
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        data["ejectable"] = TRUE;
        data["AI_present"] = stored_card?.AI != null ? TRUE : FALSE;
        data["error"] = null;

        if (stored_card == null)
            data["error"] = "Please insert an intelliCard.";
        else if (stored_card.AI == null)
            data["error"] = "No AI located...";
        else if (Dm.truthy(stored_card.flush))
            data["error"] = "Flush in progress!";
        else
        {
            data["name"] = stored_card.AI.name;
            data["restoring"] = restoring;
            data["health"] = (Dm.num(stored_card.AI.health) + 100) / 2;
            data["isDead"] = Dm.eq(stored_card.AI.stat, DEAD) ? TRUE : FALSE;
            data["laws"] = stored_card.AI.laws.get_law_list(include_zeroth: TRUE, render_html: FALSE);
        }

        return data;
    }
}
