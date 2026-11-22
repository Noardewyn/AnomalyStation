// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/ling_decoy_brain.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file (the changeling's vestigial brain made a decoy: not vital, a decoy for the MMI, and the MMI Talk
// action granted while it sits in an MMI). The var block is generated (D111).

/// Component applied to ling brains to make them into decoy brains, as ling brains are vestigial and don't do anything
// tg: code/datums/components/ling_decoy_brain.dm:2
public partial class datum_component_ling_decoy_brain : datum_component
{
    // tg: code/datums/components/ling_decoy_brain.dm:8
    [DmParams("ling")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        await DmTask.CompletedTask;
        datum_antagonist_changeling ling = Dm.arg(__extra, 0) as datum_antagonist_changeling;
        if (parent is not obj_item_organ_brain)
            return COMPONENT_INCOMPATIBLE;
        if (ling == null)
        {
            stack_trace(Dm.text("[] instantiated without a changeling to link to.", type), "code/datums/components/ling_decoy_brain.dm", 12);
            return COMPONENT_INCOMPATIBLE;
        }

        parent_ling = ling;
        RegisterSignal(parent_ling, COMSIG_QDELETING, nameof(clear_decoy));
        return null;
    }

    // tg: code/datums/components/ling_decoy_brain.dm:18
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(parent_ling, COMSIG_QDELETING);
        parent_ling = null;
        QDEL_NULL(ref talk_action);
        return base.Destroy(force);
    }

    // tg: code/datums/components/ling_decoy_brain.dm:24
    public override async DmTask RegisterWithParent()
    {
        await DmTask.CompletedTask;
        obj_item_organ_brain ling_brain = parent as obj_item_organ_brain;
        Dm.mask(ref ling_brain.organ_flags, Dm.bitnot(ORGAN_VITAL));
        ling_brain.decoy_override = TRUE;
        RegisterSignal(ling_brain, COMSIG_ATOM_ENTERING, nameof(entered_mmi));
    }

    // tg: code/datums/components/ling_decoy_brain.dm:30
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await DmTask.CompletedTask;
        obj_item_organ_brain ling_brain = parent as obj_item_organ_brain;
        Dm.combine(ref ling_brain.organ_flags, ORGAN_VITAL);
        ling_brain.decoy_override = FALSE;
        UnregisterSignal(ling_brain, COMSIG_ATOM_ENTERING); // tg passes PROC_REF(entered_mmi) as a third argument, which UnregisterSignal ignores
    }

    /**
     * Signal proc for [COMSIG_ATOM_ENTERING], when the brain enters an MMI grant the MMI talk action to the ling
     *
     * Unfortunately this is hooked on Entering rather than its own dedicated MMI signal becuase MMI code is a fuck
     */
    // tg: code/datums/components/ling_decoy_brain.dm:41
    [SignalHandler]
    public virtual void entered_mmi(obj_item_organ_brain source = null, atom entering = null, atom old_loc = null, params object[] __extra)
    {
        mob_living the_real_ling = parent_ling.owner.current as mob_living;
        if (the_real_ling is not mob_living)
            return;

        if (source.loc is obj_item_brain_processor_organic && talk_action?.owner != the_real_ling)
        {
            if (talk_action == null)
            {
                talk_action = Dm.@new<datum_action_changeling_mmi_talk>(); // Not linked to anything, we manage the reference (and don't want it disappearing on us)
                talk_action.brain_ref = source;
            }

            if (Dm.truthy(the_real_ling.key))
                to_chat(the_real_ling, span_ghostalert(I18n.T("We detect our decoy brain has been placed within a Man-Machine Interface. We can use the \"MMI Talk\" action to command it to speak.")));
            else
                the_real_ling.notify_revival(I18n.T("Your decoy brain has been placed in an MMI, re-enter your body to talk via it!"), source: the_real_ling, flashwindow: TRUE);
            Waiting.NoSleep(talk_action.Grant(the_real_ling));
        }
        else if (talk_action?.owner == the_real_ling)
        {
            to_chat(the_real_ling, span_ghostalert(I18n.T("We can no longer detect our decoy brain.")));
            Waiting.NoSleep(talk_action.Remove(the_real_ling));
        }
    }

    /// Clear up the decoy if the ling is de-linged
    // tg: code/datums/components/ling_decoy_brain.dm:65
    [SignalHandler]
    public virtual void clear_decoy(datum source = null)
    {
        qdel(this);
    }
}
