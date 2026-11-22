// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/aifixer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the AI system integrity restorer console. The var block is generated (D111).

// tg: code/game/machinery/computer/aifixer.dm:1
public partial class obj_machinery_computer_aifixer : obj_machinery_computer
{
    // tg: code/game/machinery/computer/aifixer.dm:16
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (occupier != null)
        {
            if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
                to_chat(user, span_warning(I18n.T("The screws on []'s screen won't budge.", name)));
            else
                to_chat(user, span_warning(I18n.T("The screws on []'s screen won't budge and it emits a warning beep.", name)));
            return null;
        }
        else
            return await base.screwdriver_act(user, tool);
    }

    // tg: code/game/machinery/computer/aifixer.dm:25
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "AiRestorer", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/aifixer.dm:32
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        data["ejectable"] = FALSE;
        data["AI_present"] = FALSE;
        data["error"] = null;
        if (occupier == null)
            data["error"] = "Please transfer an AI unit.";
        else
        {
            data["AI_present"] = TRUE;
            data["name"] = occupier.name;
            data["restoring"] = restoring;
            data["health"] = (Dm.num(occupier.health) + 100) / 2;
            data["isDead"] = Dm.eq(occupier.stat, DEAD) ? TRUE : FALSE;
            data["laws"] = occupier.laws.get_law_list(include_zeroth: TRUE, render_html: FALSE);
        }

        return data;
    }

    // tg: code/game/machinery/computer/aifixer.dm:51
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (occupier == null)
            restoring = FALSE;

        switch (Dm.str(action))
        {
            case "PRG_beginReconstruction":
                if (occupier != null && Dm.num(occupier.health) < 100)
                {
                    to_chat(usr, span_notice(I18n.T("Reconstruction in progress. This will take several minutes.")));
                    playsound(this, Dm.resource("sound/machines/terminal/terminal_prompt_confirm.ogg"), 25, FALSE);
                    restoring = TRUE;
                    occupier.notify_revival("Your core files are being restored!", source: this);
                    dot = TRUE;
                }
                break;
        }
        return dot;
    }

    // tg: code/game/machinery/computer/aifixer.dm:67
    public virtual async DmTask<bool> Fix()
    {
        if (!Dm.truthy(await use_energy(active_power_usage, force: TRUE)))
        {
            await say("Not enough energy. Restoration cancelled.");
            return false;
        }
        float need_mob_update = 0;
        need_mob_update += Dm.num(await occupier.adjust_oxy_loss(-5, updating_health: FALSE));
        need_mob_update += Dm.num(await occupier.adjust_fire_loss(-5, updating_health: FALSE));
        need_mob_update += Dm.num(await occupier.adjust_brute_loss(-5, updating_health: FALSE));
        if (need_mob_update != 0)
            await occupier.updatehealth();
        if (Dm.num(occupier.health) >= 0 && Dm.eq(occupier.stat, DEAD))
        {
            await occupier.revive();
            if (!Dm.truthy(occupier.radio_enabled))
            {
                occupier.radio_enabled = TRUE;
                to_chat(occupier, span_warning(I18n.T("Your Subspace Transceiver has been enabled!")));
            }
        }
        return Dm.num(occupier.health) < 100;
    }

    // tg: code/game/machinery/computer/aifixer.dm:85
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(base.process(seconds_per_tick, __extra)))
        {
            if (Dm.truthy(restoring))
            {
                object oldstat = occupier.stat;
                restoring = Waiting.NoSleep(Fix()) ? TRUE : FALSE;
                if (!Dm.eq(oldstat, occupier.stat))
                    update_appearance();
            }
        }
        return null;
    }

    // tg: code/game/machinery/computer/aifixer.dm:93
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return dot;

        if (Dm.truthy(restoring))
            Dm.append(ref dot, "ai-fixer-on");

        if (occupier == null)
        {
            Dm.append(ref dot, "ai-fixer-empty");
            return dot;
        }

        if (Dm.eq(occupier.stat, HARD_CRIT) || IS_UNCONSCIOUS(occupier))
            Dm.append(ref dot, "ai-fixer-404");
        else if (!IS_UNCONSCIOUS_OR_CRIT(occupier))
            Dm.append(ref dot, "ai-fixer-full");
        return dot;
    }

    // tg: code/game/machinery/computer/aifixer.dm:110
    public override async DmTask<object> transfer_ai(object interaction = null, mob user = null, mob_living_silicon_ai AI = null, obj_item_aicard card = null)
    {
        if (!Dm.truthy(await base.transfer_ai(interaction, user, AI, card)))
            return null;
        //Downloading AI from card to terminal.
        if (Dm.eq(interaction, AI_TRANS_FROM_CARD))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            {
                to_chat(user, span_alert(I18n.T("[] is offline and cannot take an AI at this time.", this)));
                return null;
            }
            await AI.forceMove(this);
            occupier = AI;
            AI.set_control_disabled(TRUE);
            AI.radio_enabled = FALSE;
            to_chat(AI, span_alert(I18n.T("You have been uploaded to a stationary terminal. Sadly, there is no remote access from here.")));
            to_chat(user, Dm.text("[]: ", span_notice(I18n.T("Transfer successful"))) + I18n.T("[] ([].exe) installed and executed successfully. Local copy has been removed.", AI.name, rand(1000, 9999)));
            card.AI = null;
            update_appearance();
        }
        else //Uploading AI from terminal to card
        {
            if (occupier != null && !Dm.truthy(restoring))
            {
                to_chat(occupier, span_notice(I18n.T("You have been downloaded to a mobile storage device. Still no remote access.")));
                to_chat(user, Dm.text("[]: ", span_notice(I18n.T("Transfer successful"))) + I18n.T("[] ([].exe) removed from host terminal and stored within local memory.", occupier.name, rand(1000, 9999)));
                await occupier.forceMove(card);
                card.AI = occupier;
                occupier = null;
                update_appearance();
            }
            else if (Dm.truthy(restoring))
                to_chat(user, span_alert(I18n.T("ERROR: Reconstruction in progress.")));
            else if (occupier == null)
                to_chat(user, span_alert(I18n.T("ERROR: Unable to locate artificial intelligence.")));
        }
        return null;
    }

    // tg: code/game/machinery/computer/aifixer.dm:139
    public override object Destroy(float? force = null)
    {
        if (occupier != null)
            QDEL_NULL(ref occupier);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/computer/aifixer.dm:144
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        await DmTask.CompletedTask;
        if (occupier != null)
            QDEL_NULL(ref occupier);
        return null;
    }
}
