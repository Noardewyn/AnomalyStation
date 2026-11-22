// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/robot/laws.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI1 and SI2, D326).

// tg: code/modules/mob/living/silicon/robot/laws.dm:35
public partial class mob_living_silicon : mob_living
{
    /**
     * For AIs, iterates over connected cyborg and calls try_sync_laws
     * For cyborgs, checks if we have a master AI and, if lawupdate is set, syncs law and misc. with it
     */
    // tg: code/modules/mob/living/silicon/robot/laws.dm:35
    public virtual async DmTask<object> try_sync_laws()
    {
        return null;
    }
}

// tg: code/modules/mob/living/silicon/robot/laws.dm:1
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/robot/laws.dm:1
    public override void deadchat_lawchange()
    {
        if (Dm.truthy(lawupdate))
            return;

        base.deadchat_lawchange();
    }

    // tg: code/modules/mob/living/silicon/robot/laws.dm:7
    public override async DmTask show_laws(mob show_to = null)
    {
        if (Dm.truthy(lawupdate))
        {
            if (!QDELETED(connected_ai))
            {
                if (IS_UNCONSCIOUS_OR_CRIT(connected_ai) || Dm.truthy(connected_ai.control_disabled))
                    to_chat(this, span_bold(I18n.T("AI signal lost, unable to sync laws.")));

                else
                {
                    await lawsync();
                    to_chat(this, span_bold(I18n.T("Laws synced with AI, be sure to note any changes.")));
                }
            }
            else
            {
                to_chat(this, span_bold(I18n.T("No AI selected to sync laws with, disabling lawsync protocol.")));
                lawupdate = FALSE;
            }
        }

        await base.show_laws(show_to);

        if (Dm.truthy(shell)) //AI shell
            to_chat(this, span_bold(I18n.T("Remember, you are an AI remotely controlling your shell, other AIs can be ignored.")));
        else if (connected_ai != null)
            to_chat(this, span_bold(I18n.T("Remember, [] is your master, other AIs can be ignored.", connected_ai.name)));
        else if (Dm.truthy(emagged))
            to_chat(this, span_bold(I18n.T("Remember, you are not required to listen to the AI.")));
        else
            to_chat(this, span_bold(I18n.T("Remember, you are not bound to any AI, you are not required to listen to them.")));
    }

    // tg: code/modules/mob/living/silicon/robot/laws.dm:38
    public override async DmTask<object> try_sync_laws()
    {
        if (QDELETED(connected_ai) || !Dm.truthy(lawupdate))
            return FALSE;

        await sync_to_ai();
        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/robot/laws.dm:45
    public virtual async DmTask sync_to_ai()
    {
        picturesync();
        await lawsync();
    }

    // tg: code/modules/mob/living/silicon/robot/laws.dm:49
    public virtual void picturesync()
    {
        if (connected_ai?.aicamera == null || aicamera == null)
            return;
        foreach (object i in Dm.each(aicamera.stored))
            connected_ai.aicamera.stored[i] = TRUE;
        foreach (object i in Dm.each(connected_ai.aicamera.stored))
            aicamera.stored[i] = TRUE;
    }

    // tg: code/modules/mob/living/silicon/robot/laws.dm:57
    public virtual async DmTask lawsync()
    {
        connected_ai?.laws?.ai_to_cyborg(laws);

        datum_computer_file_program_robotact program = modularInterface?.get_robotact() as datum_computer_file_program_robotact;
        if (program?.computer != null)
            await program.computer.update_static_data_for_all_viewers();
    }

    // tg: code/modules/mob/living/silicon/robot/laws.dm:63
    public override void announce_law_change(float? announce = null)
    {
        base.announce_law_change(announce);
        _addtimer(Dm.@new<datum_callback>(this, nameof(logevent), "Law update processed."), 0, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/mob/living/silicon/robot/laws.dm", line: 65); //Post_Lawchange gets spammed by some law boards, so let's wait it out
    }
}
