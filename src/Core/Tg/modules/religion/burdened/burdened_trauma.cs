// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/burdened/burdened_trauma.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the burdened trauma, whose level rises with each lost organ or limb, addiction, bad mutation and severe trauma and
// grants telepathy, telekinesis and at nine the psyker's transformation. The var block is generated (D111).

///Burdened grants some mutations upon injuring yourself sufficiently
// tg: code/modules/religion/burdened/burdened_trauma.dm:2
public partial class datum_brain_trauma_special_burdened : datum_brain_trauma_special
{
    /// Static list of organ slots that add burden when lost
    // tg: code/modules/religion/burdened/burdened_trauma.dm:16  var/static/list/burden_organ_slots
    public static DmList burden_organ_slots => TgState.Current.datum_brain_trauma_special_burdened__burden_organ_slots ??= Dm.list(
        ORGAN_SLOT_BRAIN,
        ORGAN_SLOT_HEART,
        ORGAN_SLOT_LUNGS,
        ORGAN_SLOT_EYES,
        ORGAN_SLOT_EARS,
        ORGAN_SLOT_TONGUE,
        ORGAN_SLOT_LIVER,
        ORGAN_SLOT_STOMACH);

    // tg: code/modules/religion/burdened/burdened_trauma.dm:27
    public override async DmTask<object> on_gain()
    {
        RegisterSignal(owner, COMSIG_CARBON_GAIN_ORGAN, nameof(organ_added_burden));
        RegisterSignal(owner, COMSIG_CARBON_LOSE_ORGAN, nameof(organ_removed_burden));

        RegisterSignal(owner, COMSIG_CARBON_ATTACH_LIMB, nameof(limbs_added_burden));
        RegisterSignal(owner, COMSIG_CARBON_REMOVE_LIMB, nameof(limbs_removed_burden));

        RegisterSignal(owner, COMSIG_CARBON_GAIN_ADDICTION, nameof(addict_added_burden));
        RegisterSignal(owner, COMSIG_CARBON_LOSE_ADDICTION, nameof(addict_removed_burden));

        RegisterSignal(owner, COMSIG_CARBON_GAIN_MUTATION, nameof(mutation_added_burden));
        RegisterSignal(owner, COMSIG_CARBON_LOSE_MUTATION, nameof(mutation_removed_burden));

        RegisterSignal(owner, COMSIG_CARBON_GAIN_TRAUMA, nameof(trauma_added_burden));
        RegisterSignal(owner, COMSIG_CARBON_LOSE_TRAUMA, nameof(trauma_removed_burden));

        foreach (object burden_slot in Dm.each(burden_organ_slots))
        {
            if (!Dm.truthy(owner.get_organ_slot(burden_slot)) && Dm.truthy(is_burdensome_organ(owner, burden_slot, null, FALSE)))
                await update_burden(TRUE, silent: TRUE);
        }

        DmList missing_limbs = owner.get_missing_limbs() as DmList;
        for (float i = 1, i__end = length(missing_limbs) + length(owner.mind?.active_addictions); i <= i__end; i++)
            await update_burden(TRUE, silent: TRUE);

        if (owner.dna != null)
        {
            foreach (datum_mutation mutation in Dm.each<datum_mutation>(owner.dna.mutations))
            {
                if (Dm.eq(mutation.quality, NEGATIVE))
                    await update_burden(TRUE, silent: TRUE);
            }
        }

        foreach (datum_brain_trauma_severe trauma in Dm.each<datum_brain_trauma_severe>(owner.get_traumas()))
            await update_burden(TRUE, silent: TRUE);

        return await base.on_gain();
    }

    // tg: code/modules/religion/burdened/burdened_trauma.dm:61
    public override async DmTask on_lose(object silent = null)
    {
        UnregisterSignal(owner, Dm.list(
            COMSIG_CARBON_GAIN_ORGAN,
            COMSIG_CARBON_LOSE_ORGAN,
            COMSIG_CARBON_ATTACH_LIMB,
            COMSIG_CARBON_REMOVE_LIMB,
            COMSIG_CARBON_GAIN_ADDICTION,
            COMSIG_CARBON_LOSE_ADDICTION,
            COMSIG_CARBON_GAIN_MUTATION,
            COMSIG_CARBON_LOSE_MUTATION,
            COMSIG_CARBON_GAIN_TRAUMA,
            COMSIG_CARBON_LOSE_TRAUMA));

        for (float i = 1, i__end = burden_level; i <= i__end; i++)
            await update_burden(FALSE, silent: TRUE);

        await base.on_lose(silent);
    }

    /**
     * Called by hooked signals whenever burden_level var needs to go up or down by 1.
     * Sends messages on burden level, gives powers and takes them if needed, etc
     *
     * Arguments:
     * * increase: whether to tick burden_level up or down 1
     * * silent: if TRUE, this does not print out any to_chats
     */
    // tg: code/modules/religion/burdened/burdened_trauma.dm:88
    public virtual async DmTask update_burden(object increase = null, float? silent = null)
    {
        silent ??= FALSE;
        datum_dna dna = owner?.dna;
        if (dna == null)
        {
            qdel(this);
            return;
        }
        //adjust burden
        burden_level = Dm.truthy(increase) ? burden_level + 1 : burden_level - 1;
        if (burden_level < 0)
            burden_level = 0;
        //send a message and handle rewards
        switch (burden_level)
        {
            case 0:
                if (!Dm.truthy(silent))
                    to_chat(owner, span_warning(I18n.T("You feel no weight on your shoulders. You are not feeling []'s suffering.", GLOB.deity)));
                break;
            case 1:
                if (!Dm.truthy(silent))
                {
                    if (Dm.truthy(increase))
                        to_chat(owner, span_notice(I18n.T("You begin to feel the scars on []. You must continue to burden yourself.", GLOB.deity)));
                    else
                        to_chat(owner, span_warning(I18n.T("The weight on your shoulders feels lighter. You are barely feeling []'s suffering.", GLOB.deity)));
                }
                break;
            case 2:
                if (Dm.truthy(increase))
                {
                    if (!Dm.truthy(silent))
                        to_chat(owner, span_notice(I18n.T("You have done well to understand []. You are almost at a breakthrough.", GLOB.deity)));
                }
                else
                {
                    if (!Dm.truthy(silent))
                        to_chat(owner, span_warning(I18n.T("The weight on your shoulders feels lighter. You have lost some universal truths.")));
                    await dna.remove_mutation(typeof(datum_mutation_telepathy), MUTATION_SOURCE_BURDENED_TRAUMA);
                    await dna.remove_mutation(typeof(datum_mutation_unintelligible), MUTATION_SOURCE_BURDENED_TRAUMA);
                    owner.remove_filter("burden_outline");
                }
                break;
            case 3:
                if (Dm.truthy(increase))
                {
                    if (!Dm.truthy(silent))
                        to_chat(owner, span_notice(I18n.T("Your suffering is only a fraction of []'s, and yet the universal truths are coming to you.", GLOB.deity)));
                    await dna.add_mutation(typeof(datum_mutation_telepathy), MUTATION_SOURCE_BURDENED_TRAUMA);
                    await dna.add_mutation(typeof(datum_mutation_unintelligible), MUTATION_SOURCE_BURDENED_TRAUMA);
                    owner.add_filter("burden_outline", 9, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", "#6c6eff")));
                }
                else
                {
                    if (!Dm.truthy(silent))
                        to_chat(owner, span_warning(I18n.T("The weight on your shoulders feels lighter. You feel like you're about to forget.")));
                }
                break;
            case 4:
                if (!Dm.truthy(silent))
                {
                    if (Dm.truthy(increase))
                        to_chat(owner, span_notice(I18n.T("It hurts, each ounce of pain a lesson told. How does [] bear this weight?", GLOB.deity)));
                    else
                        to_chat(owner, span_warning(I18n.T("The weight on your shoulders feels lighter. You're growing further from your goal.")));
                }
                break;
            case 5:
                if (Dm.truthy(increase))
                {
                    if (!Dm.truthy(silent))
                        to_chat(owner, span_notice(I18n.T("Your body is a canvas of loss. You are almost at a breakthrough.")));
                }
                else
                {
                    if (!Dm.truthy(silent))
                        to_chat(owner, span_warning(I18n.T("The weight on your shoulders feels lighter. You have lost some universal truths.")));
                    await dna.remove_mutation(typeof(datum_mutation_telekinesis), MUTATION_SOURCE_BURDENED_TRAUMA);
                    await dna.remove_mutation(typeof(datum_mutation_mindreader), MUTATION_SOURCE_BURDENED_TRAUMA);
                }
                break;
            case 6:
                if (Dm.truthy(increase))
                {
                    if (!Dm.truthy(silent))
                        to_chat(owner, span_notice(I18n.T("Your suffering is respectful, your scars immaculate. More universal truths are clear, but you do not fully understand yet.")));
                    await dna.add_mutation(typeof(datum_mutation_telekinesis), MUTATION_SOURCE_BURDENED_TRAUMA);
                    await dna.add_mutation(typeof(datum_mutation_mindreader), MUTATION_SOURCE_BURDENED_TRAUMA);
                }
                else
                {
                    if (!Dm.truthy(silent))
                        to_chat(owner, span_warning(I18n.T("The weight on your shoulders feels lighter. You feel like you're about to forget.")));
                }
                break;
            case 7:
                if (!Dm.truthy(silent))
                {
                    if (Dm.truthy(increase))
                        to_chat(owner, span_notice(I18n.T("The weight on your shoulders is immense. [] is shattered across the cosmos.", GLOB.deity)));
                    else
                        to_chat(owner, span_warning(I18n.T("The weight on your shoulders feels lighter. You're growing further from your goal.")));
                }
                break;
            case 8:
                if (!Dm.truthy(silent))
                {
                    if (Dm.truthy(increase))
                        to_chat(owner, span_notice(I18n.T("You're on the cusp of another breakthrough. [] lost everything.", GLOB.deity)));
                    else
                        to_chat(owner, span_warning(I18n.T("The weight on your shoulders feels lighter. You have lost some universal truths.")));
                }
                break;
            case 9:
                if (Dm.truthy(increase))
                {
                    if (!Dm.truthy(silent))
                        to_chat(owner, span_notice(I18n.T("You have finally broken yourself enough to understand []. It's all so clear to you.", GLOB.deity)));
                    mob_living_carbon_human knower = owner as mob_living_carbon_human;
                    if (!(knower is mob_living_carbon_human))
                        return;
                    Waiting.Detach(knower.slow_psykerize());
                }
                break;
        }
    }

    // tg: code/modules/religion/burdened/burdened_trauma.dm:173
    public virtual object is_burdensome_organ(mob burdened = null, object organ_slot = null, obj_item_organ organ = null, object special = null)
    {
        if (Dm.truthy(special)) //aheals
            return null;
        if (!ishuman(burdened))
            return null; //mobs that don't care about organs
        mob_living_carbon_human burdened_human = burdened as mob_living_carbon_human;
        datum_species burdened_species = burdened_human.dna?.species as datum_species;
        if (burdened_species == null)
            return null;

        /// only organs that are slotted in these count. because there's a lot of useless organs to cheese with.
        DmList critical_slots = burden_organ_slots.Copy();
        if (burdened_species.mutantheart == null)
            Dm.remove(ref critical_slots, ORGAN_SLOT_HEART);
        if (burdened_species.mutanttongue == null)
            Dm.remove(ref critical_slots, ORGAN_SLOT_TONGUE);
        if (burdened_species.mutantlungs == null)
            Dm.remove(ref critical_slots, ORGAN_SLOT_LUNGS);
        if (burdened_species.mutantstomach == null)
            Dm.remove(ref critical_slots, ORGAN_SLOT_STOMACH);
        if (burdened_species.mutantliver == null)
            Dm.remove(ref critical_slots, ORGAN_SLOT_LIVER);

        if (!Dm.@in(organ_slot, critical_slots))
            return FALSE;
        if (organ is obj_item_organ_eyes)
        {
            obj_item_organ_eyes eyes = organ as obj_item_organ_eyes;
            if (eyes.tint < TINT_BLIND) //unless you were already blinded by them (flashlight eyes), this is adding burden!
                return TRUE;
            return FALSE;
        }
        return TRUE;
    }

    /// Signal to decrease burden_level (see update_burden proc) if an organ is added
    // tg: code/modules/religion/burdened/burdened_trauma.dm:206
    [SignalHandler]
    public virtual void organ_added_burden(mob burdened = null, obj_item_organ new_organ = null, object special = null)
    {
        if (Dm.truthy(is_burdensome_organ(burdened, new_organ.slot, new_organ, special)))
            Waiting.NoSleep(update_burden(increase: FALSE));//working organ
    }

    /// Signal to increase burden_level (see update_burden proc) if an organ is removed
    // tg: code/modules/religion/burdened/burdened_trauma.dm:213
    [SignalHandler]
    public virtual void organ_removed_burden(mob burdened = null, obj_item_organ old_organ = null, object special = null)
    {
        if (Dm.truthy(is_burdensome_organ(burdened, old_organ.slot, old_organ, special)))
            Waiting.NoSleep(update_burden(increase: TRUE)); //lost organ
    }

    /// Signal to decrease burden_level (see update_burden proc) if a limb is added
    // tg: code/modules/religion/burdened/burdened_trauma.dm:220
    [SignalHandler]
    public virtual void limbs_added_burden(datum source = null, obj_item_bodypart new_limb = null, object special = null)
    {
        if (Dm.truthy(special)) //something we don't wanna consider, like instaswapping limbs
            return;
        Waiting.NoSleep(update_burden(increase: FALSE));
    }

    /// Signal to increase burden_level (see update_burden proc) if a limb is removed
    // tg: code/modules/religion/burdened/burdened_trauma.dm:228
    [SignalHandler]
    public virtual void limbs_removed_burden(datum source = null, obj_item_bodypart old_limb = null, object special = null, object dismembered = null)
    {
        if (Dm.truthy(special)) //something we don't wanna consider, like instaswapping limbs
            return;
        Waiting.NoSleep(update_burden(increase: TRUE));
    }

    /// Signal to increase burden_level (see update_burden proc) if an addiction is added
    // tg: code/modules/religion/burdened/burdened_trauma.dm:236
    [SignalHandler]
    public virtual void addict_added_burden(datum_addiction new_addiction = null, datum_mind addict_mind = null)
    {
        Waiting.NoSleep(update_burden(increase: TRUE));
    }

    /// Signal to decrease burden_level (see update_burden proc) if an addiction is removed
    // tg: code/modules/religion/burdened/burdened_trauma.dm:242
    [SignalHandler]
    public virtual void addict_removed_burden(datum_addiction old_addiction = null, datum_mind nonaddict_mind = null)
    {
        Waiting.NoSleep(update_burden(increase: FALSE));
    }

    /// Signal to increase burden_level (see update_burden proc) if a mutation is added
    // tg: code/modules/religion/burdened/burdened_trauma.dm:248
    [SignalHandler]
    public virtual void mutation_added_burden(mob_living_carbon burdened = null, Type mutation_type = null, object @class = null) // a typepath: the signal sends the mutation's type (rules 3.2)
    {
        if (Dm.eq(Dm.initial(mutation_type, (datum_mutation p) => p.quality), NEGATIVE))
            Waiting.NoSleep(update_burden(increase: TRUE));
    }

    /// Signal to decrease burden_level (see update_burden proc) if a mutation is removed
    // tg: code/modules/religion/burdened/burdened_trauma.dm:255
    [SignalHandler]
    public virtual void mutation_removed_burden(mob_living_carbon burdened = null, Type mutation_type = null) // a typepath: the signal sends the mutation's type (rules 3.2)
    {
        if (Dm.eq(Dm.initial(mutation_type, (datum_mutation p) => p.quality), NEGATIVE))
            Waiting.NoSleep(update_burden(increase: FALSE));
    }

    /// Signal to increase burden_level (see update_burden proc) if a trauma is added
    // tg: code/modules/religion/burdened/burdened_trauma.dm:262
    [SignalHandler]
    public virtual void trauma_added_burden(mob_living_carbon burdened = null, datum_brain_trauma trauma_added = null)
    {
        if (trauma_added is datum_brain_trauma_severe)
            Waiting.NoSleep(update_burden(increase: TRUE));
    }

    /// Signal to decrease burden_level (see update_burden proc) if a trauma is removed
    // tg: code/modules/religion/burdened/burdened_trauma.dm:269
    [SignalHandler]
    public virtual void trauma_removed_burden(mob_living_carbon burdened = null, datum_brain_trauma trauma_removed = null)
    {
        if (trauma_removed is datum_brain_trauma_severe)
            Waiting.NoSleep(update_burden(increase: FALSE));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/religion/burdened/burdened_trauma.dm:16  /datum/brain_trauma/special/burdened's var/static/list/burden_organ_slots
    internal DmList datum_brain_trauma_special_burdened__burden_organ_slots;
}
