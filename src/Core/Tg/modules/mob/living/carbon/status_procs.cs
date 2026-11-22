// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/status_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated in M2 by P2 (scope.md 2e "carbon": the carbon's Life reads get_traumas every tick): IsParalyzed, the disgust
// setters and the trauma wrappers over the brain (the traumas themselves are M8).

// tg: code/modules/mob/living/carbon/status_procs.dm:17
public partial class mob_living_carbon : mob_living
{
    //Here are the procs used to modify status effects of a mob.
    //The effects include: stun, knockdown, unconscious, sleeping, resting

    // tg: code/modules/mob/living/carbon/status_procs.dm:5
    [DmParams("include_stamcrit")]
    public override object IsParalyzed(params object[] __extra)
    {
        float? include_stamcrit = Dm.arg<float?>(__extra, 0);
        include_stamcrit ??= TRUE;
        return (base.IsParalyzed() is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.num(Dm.truthy(include_stamcrit) && HAS_TRAIT_FROM(this, TRAIT_INCAPACITATED, STAMINA)));
    }

    // tg: code/modules/mob/living/carbon/status_procs.dm:8
    [DmParams("amount", "max")]
    public override void adjust_disgust(object amount = null, params object[] __extra)
    {
        object max = Dm.arg<object>(__extra, 0);
        max ??= DISGUST_LEVEL_MAXEDOUT;
        disgust = clamp(disgust + Dm.num(amount), 0, Dm.num(max));
    }

    // tg: code/modules/mob/living/carbon/status_procs.dm:11
    public override void set_disgust(object amount = null)
    {
        disgust = clamp(Dm.num(amount), 0, DISGUST_LEVEL_MAXEDOUT);
    }

    ////////////////////////////////////////TRAUMAS/////////////////////////////////////////

    // tg: code/modules/mob/living/carbon/status_procs.dm:17
    public virtual object get_traumas()
    {
        object dot = null;
        dot = Dm.list();
        obj_item_organ_brain B = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (B != null)
            dot = B.traumas;
        return dot;
    }

    // tg: code/modules/mob/living/carbon/status_procs.dm:23
    public virtual object has_trauma_type(object brain_trauma_type = null, object resilience = null)
    {
        object dot = null;
        obj_item_organ_brain B = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (B != null)
            dot = B.has_trauma_type(brain_trauma_type as Type, resilience == null ? null : Dm.num(resilience));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/status_procs.dm:28
    public virtual async DmTask<object> gain_trauma(object trauma = null, object resilience = null, params object[] __extra)
    {
        object dot = null;
        obj_item_organ_brain B = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (B != null)
        {
            DmList arguments = Dm.list();
            DmList args__1 = Dm.args(trauma, resilience, __extra);
            if (args__1.len > 2)
                arguments = args__1.Copy(3);
            dot = await B.brain_gain_trauma(trauma, resilience, arguments);
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/status_procs.dm:36
    public virtual async DmTask<object> gain_trauma_type(Type brain_trauma_type = null, object resilience = null)
    {
        brain_trauma_type ??= typeof(datum_brain_trauma);
        object dot = null;
        obj_item_organ_brain B = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (B != null)
            dot = await B.gain_trauma_type(brain_trauma_type, resilience);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/status_procs.dm:41
    public virtual object cure_trauma_type(Type brain_trauma_type = null, object resilience = null)
    {
        brain_trauma_type ??= typeof(datum_brain_trauma);
        object dot = null;
        obj_item_organ_brain B = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (B != null)
        {
            // . = B.cure_trauma_type(...): /obj/item/organ/brain/proc/cure_trauma_type returns nothing (its slot is void), so
            // . stays null as the DM call's null return would leave it.
            B.cure_trauma_type(brain_trauma_type, resilience == null ? null : Dm.num(resilience));
            dot = null;
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/status_procs.dm:46
    public virtual object cure_all_traumas(object resilience = null)
    {
        object dot = null;
        obj_item_organ_brain B = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (B != null)
            dot = B.cure_all_traumas(resilience == null ? null : Dm.num(resilience));
        return dot;
    }
}
