// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/init_signals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated in M2 by P2 (scope.md 2e "carbon": init_signals.dm): the carbon's trait signals and their handlers. Gender
// identity blocks and traumas are M8-M9 (GAPs below); the addictions since gaps RG2 (D339), the diseases since gaps VI1.

// tg: code/modules/mob/living/carbon/init_signals.dm:33
public partial class mob_living_carbon : mob_living
{
    //Called on /mob/living/carbon/Initialize(mapload), for the carbon mobs to register relevant signals.
    // tg: code/modules/mob/living/carbon/init_signals.dm:2
    public override async DmTask register_init_signals()
    {
        await base.register_init_signals();

        //Traits that register add and remove
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_AGENDER), nameof(on_agender_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_AGENDER), nameof(on_agender_trait_loss));
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_NOBLOOD), nameof(on_noblood_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_NOBLOOD), nameof(on_noblood_trait_loss));

        //Traits that register add only
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_NOBREATH), nameof(on_nobreath_trait_gain));
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_LIVERLESS_METABOLISM), nameof(on_liverless_metabolism_trait_gain));
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_VIRUSIMMUNE), nameof(on_virusimmune_trait_gain));
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_TOXIMMUNE), nameof(on_toximmune_trait_gain));
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_GENELESS), nameof(on_geneless_trait_gain));
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_NO_SPLIT_PERSONALITY), nameof(on_no_split_personality_trait_gain));

        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_NO_OXYLOSS_PASSOUT), SIGNAL_REMOVETRAIT(TRAIT_NO_OXYLOSS_PASSOUT)), nameof(check_passout));

        RegisterSignals(this, Dm.list(
            SIGNAL_ADDTRAIT(TRAIT_NOHARDCRIT),
            SIGNAL_REMOVETRAIT(TRAIT_NOHARDCRIT),
            SIGNAL_ADDTRAIT(TRAIT_NOSOFTCRIT),
            SIGNAL_REMOVETRAIT(TRAIT_NOSOFTCRIT)
        ), nameof(update_stat));
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:33
    [SignalHandler]
    public virtual void on_agender_trait_gain(datum source = null)
    {
        gender = PLURAL;
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:43
    [SignalHandler]
    public virtual void on_agender_trait_loss(datum source = null)
    {
        // :49-50  var/datum/dna_block/identity/gender/to_update = GLOB.dna_identity_blocks[/datum/dna_block/identity/gender]
        //         to_update.apply_to_mob(src, src.dna.unique_identity)
        // GAP(lead): the dna identity block types (datums/dna/blocks/*.dm) are not declared, and dna.unique_identity is not
        // made (dna.cs: the identity hashes are M8); nothing in M2 gives or takes TRAIT_AGENDER.
        Dm.NotTranslated("code/modules/mob/living/carbon/init_signals.dm:49", "on_agender_trait_loss: the gender identity block (dna blocks, M8)");
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:54
    [SignalHandler]
    public virtual void on_noblood_trait_gain(datum source = null)
    {
        update_blood_status();
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:64
    [SignalHandler]
    public virtual void on_noblood_trait_loss(datum source = null)
    {
        update_blood_status();
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:74
    [SignalHandler]
    public virtual void on_nobreath_trait_gain(datum source = null)
    {
        Waiting.NoSleep(set_oxy_loss(0, updating_health: TRUE, forced: TRUE));
        losebreath = 0;
        failed_last_breath = FALSE;

        clear_alert(ALERT_TOO_MUCH_OXYGEN);
        clear_alert(ALERT_NOT_ENOUGH_OXYGEN);

        clear_alert(ALERT_TOO_MUCH_PLASMA);
        clear_alert(ALERT_NOT_ENOUGH_PLASMA);

        clear_alert(ALERT_TOO_MUCH_NITRO);
        clear_alert(ALERT_NOT_ENOUGH_NITRO);

        clear_alert(ALERT_TOO_MUCH_CO2);
        clear_alert(ALERT_NOT_ENOUGH_CO2);

        clear_alert(ALERT_TOO_MUCH_N2O);
        clear_alert(ALERT_NOT_ENOUGH_N2O);

        clear_mood_event("chemical_euphoria");
        clear_mood_event("smell");
        clear_mood_event("suffocation");
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:105
    [SignalHandler]
    public virtual void on_liverless_metabolism_trait_gain(datum source = null)
    {
        foreach (object addiction_type in Dm.each(GLOB.addictions))
            mind?.remove_addiction_points(addiction_type, MAX_ADDICTION_POINTS); //Remove the addiction!

        Waiting.NoSleep(((datum_reagents)reagents).end_metabolization(keep_liverless: TRUE));
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:118
    [SignalHandler]
    public virtual void on_virusimmune_trait_gain(datum source = null)
    {
        // SIGNAL_HANDLER

        foreach (datum_disease disease in Dm.each_cast<datum_disease>(diseases))
            Waiting.NoSleep(disease.cure(FALSE)); // gaps VI1
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:129
    [SignalHandler]
    public virtual void on_toximmune_trait_gain(datum source = null)
    {
        Waiting.NoSleep(set_tox_loss(0, updating_health: TRUE, forced: TRUE));
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:139
    [SignalHandler]
    public virtual void on_geneless_trait_gain(datum source = null)
    {
        if (dna != null)
            Waiting.NoSleep(dna.remove_all_mutations());
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:149
    [SignalHandler]
    public virtual void on_no_split_personality_trait_gain(datum source = null)
    {
        cure_trauma_type(typeof(datum_brain_trauma_severe_split_personality), TRAUMA_LIMIT_ABSOLUTE);
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:158
    public override void on_hearing_loss(params object[] __extra)
    {
        base.on_hearing_loss(__extra);
        breathing_loop.stop();
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:162
    public override void on_stasis_trait_gain(datum source = null)
    {
        base.on_stasis_trait_gain(source);
        Waiting.NoSleep(update_bodypart_bleed_overlays());
    }

    // tg: code/modules/mob/living/carbon/init_signals.dm:166
    public override void on_stasis_trait_loss(datum source = null)
    {
        base.on_stasis_trait_loss(source);
        Waiting.NoSleep(update_bodypart_bleed_overlays());
    }
}
