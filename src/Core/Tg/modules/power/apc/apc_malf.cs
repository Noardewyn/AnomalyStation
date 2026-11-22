// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/apc/apc_malf.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, standing on an AI that is not ported yet (no AI mob plays): the AI's own procs it calls are rows of
// docs/ai/specs/live-partial.txt. get_malf_status is read by the APC window for every user.

// tg: code/modules/power/apc/apc_malf.dm:1
public partial class obj_machinery_power_apc : obj_machinery_power
{
    // tg: code/modules/power/apc/apc_malf.dm:1
    public virtual object get_malf_status(mob_living_silicon_ai malf = null)
    {
        if (!(malf is mob_living_silicon_ai) || !Dm.truthy(malf.malf_picker))
            return APC_AI_NO_MALF;
        if (malfai != malf)
            return APC_AI_NO_HACK;
        if (occupier == malf)
            return APC_AI_HACK_SHUNT_HERE;
        if (malf.loc is obj_machinery_power_apc)
            return APC_AI_HACK_SHUNT_ANOTHER;
        return APC_AI_HACK_NO_SHUNT;
    }

    // tg: code/modules/power/apc/apc_malf.dm:12
    public virtual void malfhack__proc(mob_living_silicon_ai malf = null)
    {
        if (!(malf is mob_living_silicon_ai))
            return;
        if (!Dm.eq(get_malf_status(malf), APC_AI_NO_HACK))
            return;
        if (Dm.truthy(malf.malfhacking))
        {
            to_chat(malf, span_warning(I18n.T("You are already hacking an APC!")));
            return;
        }
        to_chat(malf, span_notice(I18n.T("Beginning override of APC systems. This takes some time, and you cannot perform other actions during the process.")));
        malf.malfhack = this;
        malf.malfhacking = (_addtimer(Dm.@new<datum_callback>(malf, nameof(mob_living_silicon_ai.malfhacked), this), 30 * SECONDS + 10 * malf.hacked_apcs.len * SECONDS, TIMER_STOPPABLE, file: "code/modules/power/apc/apc_malf.dm", line: 22));

        atom_movable_screen_alert_hackingapc hacking_apc = malf.throw_alert(ALERT_HACKING_APC, typeof(atom_movable_screen_alert_hackingapc)) as atom_movable_screen_alert_hackingapc;
        hacking_apc.target = this;
    }

    // tg: code/modules/power/apc/apc_malf.dm:28
    public virtual void malfoccupy(mob_living_silicon_ai malf = null)
    {
        if (!(malf is mob_living_silicon_ai))
            return;
        if (malf.loc is obj_machinery_power_apc) // Already in an APC
        {
            to_chat(malf, span_warning(I18n.T("You must evacuate your current APC first!")));
            return;
        }
        if (!Dm.truthy(malf.can_shunt))
        {
            to_chat(malf, span_warning(I18n.T("You cannot shunt!")));
            return;
        }
        if (!Dm.truthy(is_station_level(z)))
            return;
        Waiting.Detach(malfshunt(malf));
    }

    // tg: code/modules/power/apc/apc_malf.dm:41
    public virtual async DmTask malfshunt(mob_living_silicon_ai malf = null)
    {
        object confirm = await tgui_alert(malf, "Are you sure that you want to shunt? This will take you out of your core!", Dm.text("Shunt to []?", name), Dm.list("Yes", "No"));
        if (!Dm.eq(confirm, "Yes"))
            return;
        malf.ShutOffDoomsdayDevice();
        occupier = malf;
        if (isturf(malf.loc)) // create a deactivated AI core if the AI isn't coming from an emergency mech shunt
            malf.create_core_link(Dm.@new<obj_structure_ai_core>(malf.loc, CORE_STATE_FINISHED, malf.make_mmi()));
        await malf.forceMove(this); // move INTO the APC, not to its tile
        if (!Dm.truthy(findtext(occupier.name, "APC Copy")))
            occupier.name = Dm.text("[] APC Copy", malf.name);
        malf.shunted = TRUE;
        occupier.eyeobj.name = Dm.text("[] (AI Eye)", occupier.name);
        await occupier.eyeobj.forceMove(loc);
        foreach (obj_item_pinpointer_nuke disk_pinpointers in Dm.each<obj_item_pinpointer_nuke>(GLOB.pinpointer_list))
            disk_pinpointers.switch_mode_to(TRACK_MALF_AI); //Pinpointer will track the shunted AI
        datum_action_innate_core_return return_action = Dm.@new<datum_action_innate_core_return>();
        await return_action.Grant(occupier);
        await occupier.cancel_camera();
    }

    // tg: code/modules/power/apc/apc_malf.dm:61
    public virtual async DmTask malfvacate(object forced = null)
    {
        if (occupier == null)
            return;
        if (Dm.truthy(occupier._listen_lookup?[COMSIG_SILICON_AI_VACATE_APC]))
            occupier._SendSignal(COMSIG_SILICON_AI_VACATE_APC, Dm.list(occupier, occupier));
        if (Dm.truthy(_listen_lookup?[COMSIG_SILICON_AI_VACATE_APC]))
            _SendSignal(COMSIG_SILICON_AI_VACATE_APC, Dm.list(this, occupier));
        if (Dm.truthy(forced))
        {
            await occupier.forceMove(drop_location() as atom);
            Waiting.Detach(occupier.death());
            await occupier.gib(DROP_ALL_REMAINS);
            occupier = null;
            return;
        }

        if (!Dm.truthy(occupier.nuking)) //Pinpointers go back to tracking the nuke disk, as long as the AI (somehow) isn't mid-nuking.
        {
            foreach (obj_item_pinpointer_nuke disk_pinpointers in Dm.each<obj_item_pinpointer_nuke>(GLOB.pinpointer_list))
            {
                disk_pinpointers.switch_mode_to(TRACK_NUKE_DISK);
                disk_pinpointers.alert = FALSE;
            }
        }

        if (occupier.linked_core != null)
        {
            occupier.shunted = FALSE;
            await occupier.resolve_core_link();
            occupier = null;
        }
        else
            stack_trace(Dm.text("An AI: [] has vacated an APC with no linked core and without being gibbed.", occupier), "code/modules/power/apc/apc_malf.dm", 84);
    }

    // tg: code/modules/power/apc/apc_malf.dm:86
    public override async DmTask<object> transfer_ai(object interaction = null, mob user = null, mob_living_silicon_ai AI = null, obj_item_aicard card = null)
    {
        object dot = await base.transfer_ai(interaction, user, AI, card);
        if (!Dm.truthy(dot))
            return dot;
        if (card.AI != null)
        {
            to_chat(user, span_warning(I18n.T("[] is already occupied!", card)));
            return FALSE;
        }
        if (occupier == null)
        {
            to_chat(user, span_warning(I18n.T("There's nothing in [] to transfer!", this)));
            return FALSE;
        }
        if (occupier.mind == null || occupier.client == null)
        {
            to_chat(user, span_warning(I18n.T("[] is either inactive or destroyed!", occupier)));
            return FALSE;
        }
        if (occupier.linked_core != null) //if they have an active linked_core, they can't be transferred from an APC
        {
            to_chat(user, span_warning(I18n.T("[] is refusing all attempts at transfer!", occupier)));
            return FALSE;
        }
        if (Dm.truthy(transfer_in_progress))
        {
            to_chat(user, span_warning(I18n.T("There's already a transfer in progress!")));
            return FALSE;
        }
        if (!Dm.eq(interaction, AI_TRANS_TO_CARD) || IS_UNCONSCIOUS_OR_CRIT(occupier))
            return FALSE;
        turf user_turf = get_turf(user);
        if (user_turf == null)
            return FALSE;
        transfer_in_progress = TRUE;
        user.visible_message(span_notice(I18n.T("[] slots [] into []...", user, card, this)), span_notice(I18n.T("Transfer process initiated. Sending request for AI approval...")));
        playsound(this, "sound/machines/click.ogg", 50, TRUE);
        Dm.output_to(occupier, sound("sound/announcer/notice/notice2.ogg")); //To alert the AI that someone's trying to card them if they're tabbed out
        if (Dm.eq(await tgui_alert(occupier, Dm.text("[] is attempting to transfer you to \\a []. Do you consent to this?", user, card.name), "APC Transfer", Dm.list("Yes - Transfer Me", "No - Keep Me Here")), "No - Keep Me Here"))
        {
            to_chat(user, span_danger(I18n.T("AI denied transfer request. Process terminated.")));
            playsound(this, "sound/machines/buzz/buzz-sigh.ogg", 50, TRUE);
            transfer_in_progress = FALSE;
            return FALSE;
        }
        if (user.loc != user_turf)
        {
            to_chat(user, span_danger(I18n.T("Location changed. Process terminated.")));
            to_chat(occupier, span_warning(I18n.T("[] moved away! Transfer canceled.", user)));
            transfer_in_progress = FALSE;
            return FALSE;
        }
        to_chat(user, span_notice(I18n.T("AI accepted request. Transferring stored intelligence to []...", card)));
        to_chat(occupier, span_notice(I18n.T("Transfer starting. You will be moved to [] shortly.", card)));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
        {
            to_chat(occupier, span_warning(I18n.T("[] was interrupted! Transfer canceled.", user)));
            transfer_in_progress = FALSE;
            return FALSE;
        }
        if (occupier == null || card == null)
        {
            transfer_in_progress = FALSE;
            return FALSE;
        }
        user.visible_message(span_notice(I18n.T("[] transfers [] to []!", user, occupier, card)), span_notice(I18n.T("Transfer complete! [] is now stored in [].", occupier, card)));
        to_chat(occupier, span_notice(I18n.T("Transfer complete! You've been stored in []'s [].", user, card.name)));
        await occupier.forceMove(card);
        card.AI = occupier;
        occupier.shunted = FALSE;
        await occupier.cancel_camera();
        occupier = null;
        transfer_in_progress = FALSE;
        return TRUE;
    }
}
