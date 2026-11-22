// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/organ_set_bonus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package P6, D298: the Strong-Arm implants' two-arm bonus; the infused organs' bonuses keep their own procs and
// stay inert with the DNA infuser). The status effect's var block is generated (D111); the element's bonus_type is declared here, since
// it holds a typepath (the generator types it as the status effect).

/**
 * organ set bonus element; which makes organs in the same set, all in one person, provide a unique bonus!
 *
 * Used for infused organs!
 */
// tg: code/datums/elements/organ_set_bonus.dm:6
public partial class datum_element_organ_set_bonus : datum_element
{
    /// Status Effect typepath to instantiate and apply to the mob.
    // tg: code/datums/elements/organ_set_bonus.dm:10  (a typepath: the hand-written member)
    public Type bonus_type;

    // tg: code/datums/elements/organ_set_bonus.dm:7  (the generated constructor, kept with the hand-written var above)
    public datum_element_organ_set_bonus()
    {
        element_flags = 2;
        argument_hash_start_idx = 2;
    }

    // tg: code/datums/elements/organ_set_bonus.dm:12
    [DmParams("target", "bonus_type")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);

        if (!isorgan(target))
            return ELEMENT_INCOMPATIBLE;
        this.bonus_type = Dm.arg(__extra, 0) as Type;
        RegisterSignal(target, COMSIG_ORGAN_IMPLANTED, nameof(on_implanted));
        RegisterSignal(target, COMSIG_ORGAN_REMOVED, nameof(on_removed));
        return dot;
    }

    // tg: code/datums/elements/organ_set_bonus.dm:21
    public override object Detach(datum source = null, params object[] __extra)
    {
        obj_item_organ target = source as obj_item_organ;
        UnregisterSignal(target, Dm.list(COMSIG_ORGAN_IMPLANTED, COMSIG_ORGAN_REMOVED));
        if (target.owner != null)
            UnregisterSignal(target.owner, COMSIG_ATOM_EXAMINE);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/organ_set_bonus.dm:27
    [SignalHandler]
    public virtual void on_implanted(obj_item_organ target = null, mob_living_carbon receiver = null)
    {
        datum_status_effect_organ_set_bonus set_bonus = receiver.has_status_effect(bonus_type) as datum_status_effect_organ_set_bonus;
        object visuals_only = isdummy(receiver) ? TRUE : FALSE; //todo: COMSIG_ORGAN_IMPLANTED signals: movement_flags & VISUAL_ONLY
        if (set_bonus == null)
            set_bonus = Waiting.NoSleep(receiver.apply_status_effect(bonus_type)) as datum_status_effect_organ_set_bonus;
        set_bonus.set_organs(set_bonus.organs + 1, target, visuals_only);
    }

    // tg: code/datums/elements/organ_set_bonus.dm:36
    [SignalHandler]
    public virtual void on_removed(obj_item_organ target = null, mob_living_carbon loser = null)
    {
        //get status effect or remove it
        datum_status_effect_organ_set_bonus set_bonus = loser.has_status_effect(bonus_type) as datum_status_effect_organ_set_bonus;
        object visuals_only = isdummy(loser) ? TRUE : FALSE;
        if (set_bonus != null)
            set_bonus.set_organs(set_bonus.organs - 1, target, visuals_only);
    }
}

// tg: code/datums/elements/organ_set_bonus.dm:45
public partial class datum_status_effect_organ_set_bonus : datum_status_effect
{
    // tg: code/datums/elements/organ_set_bonus.dm:71
    public virtual void set_organs(object new_value = null, obj_item_organ organ = null, object visuals_only = null)
    {
        organs = Dm.num(new_value);
        if (!Dm.truthy(organs)) //initial value but won't kick in without calling the setter
            qdel(this);
        if (organs >= organs_needed)
        {
            if (!Dm.truthy(bonus_active))
                Waiting.Detach(enable_bonus(organ, visuals_only)); // INVOKE_ASYNC(src, PROC_REF(enable_bonus), organ, visuals_only)
        }
        else if (Dm.truthy(bonus_active))
        {
            Waiting.Detach(disable_bonus(organ, visuals_only)); // INVOKE_ASYNC(src, PROC_REF(disable_bonus), organ, visuals_only)
        }
    }

    // tg: code/datums/elements/organ_set_bonus.dm:81
    public virtual async DmTask<object> enable_bonus(obj_item_organ inserted_organ = null, object visuals_only = null)
    {
        if (Dm.truthy(required_biotype))
        {
            if (!Dm.truthy(Dm.bitand(owner.mob_biotypes, required_biotype)))
                return FALSE;
            if (required_biotype == MOB_ORGANIC && !Dm.truthy(owner.can_mutate()))
                return FALSE;
        }
        bonus_active = TRUE;
        // Add traits
        if (Dm.truthy(length(bonus_traits)))
            owner.add_traits(bonus_traits, TRAIT_STATUS_EFFECT(id));

        // Add biotype
        if (Dm.truthy(bonus_biotype))
        {
            biotype_added = Dm.num(Dm.bitand(bonus_biotype, Dm.bitnot(owner.mob_biotypes)));
            owner.mob_biotypes = Dm.num(Dm.bitor(owner.mob_biotypes, biotype_added));
            RegisterSignal(owner, COMSIG_SPECIES_LOSS, nameof(on_species_loss));
            RegisterSignal(owner, COMSIG_SPECIES_GAIN, nameof(on_species_gain));
        }

        if (Dm.truthy(bonus_activate_text))
            to_chat(owner, bonus_activate_text);

        // Add limb texture
        if (limb_texture == null)
            return TRUE;

        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        RegisterSignal(owner, COMSIG_CARBON_ATTACH_LIMB, nameof(texture_limb));
        RegisterSignal(owner, COMSIG_CARBON_REMOVE_LIMB, nameof(untexture_limb));

        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(carbon_owner.get_bodyparts() as DmList))
        {
            if (!Dm.truthy(Dm.bitand(limb.bodytype, BODYTYPE_ORGANIC)))
                continue;
            await limb.add_bodypart_texture(limb_texture, update: FALSE);
            if (Dm.truthy(color_overlay_priority))
                limb.add_color_override(COLOR_WHITE, color_overlay_priority);
        }

        await carbon_owner.update_body();
        return TRUE;
    }

    // tg: code/datums/elements/organ_set_bonus.dm:120
    public virtual async DmTask disable_bonus(obj_item_organ removed_organ = null, object visuals_only = null)
    {
        bonus_active = FALSE;

        // Remove traits
        if (Dm.truthy(length(bonus_traits)))
            owner.remove_traits(bonus_traits, TRAIT_STATUS_EFFECT(id));
        // Remove biotype (if added)
        if (Dm.truthy(biotype_added))
        {
            owner.mob_biotypes = Dm.num(Dm.bitand(owner.mob_biotypes, Dm.bitnot(biotype_added)));
            biotype_added = NONE;
        }

        if (Dm.truthy(bonus_deactivate_text))
            to_chat(owner, bonus_deactivate_text);

        // Remove limb overlay
        if (limb_texture == null)
            return;

        UnregisterSignal(owner, Dm.list(COMSIG_CARBON_ATTACH_LIMB, COMSIG_CARBON_REMOVE_LIMB, COMSIG_SPECIES_LOSS, COMSIG_SPECIES_GAIN));

        if (QDELETED(owner))
            return;

        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(carbon_owner.get_bodyparts() as DmList))
        {
            await limb.remove_bodypart_texture(limb_texture, update: FALSE);
            if (Dm.truthy(color_overlay_priority))
                limb.remove_color_override(color_overlay_priority);
        }

        await carbon_owner.update_body();
    }

    ///We need to recalculate the mob biotypes, so first, remove the added biotypes from the mob before the new species changes the standard biotypes.
    // tg: code/datums/elements/organ_set_bonus.dm:152
    [SignalHandler]
    public virtual void on_species_loss(mob_living_carbon human = null, datum_species new_species = null, datum_species old_species = null)
    {
        human.mob_biotypes = Dm.num(Dm.bitand(human.mob_biotypes, Dm.bitnot(biotype_added)));
        biotype_added = NONE;
    }

    ///After the new species has added its biotypes to the mob, check if they already have or don't have the bonus biotype now.
    // tg: code/datums/elements/organ_set_bonus.dm:158
    [SignalHandler]
    public virtual void on_species_gain(mob_living_carbon human = null, datum_species new_species = null, datum_species old_species = null)
    {
        biotype_added = Dm.num(Dm.bitand(bonus_biotype, Dm.bitnot(owner.mob_biotypes)));
        owner.mob_biotypes = Dm.num(Dm.bitor(owner.mob_biotypes, biotype_added));
    }

    // tg: code/datums/elements/organ_set_bonus.dm:163
    [SignalHandler]
    public virtual void texture_limb(atom source = null, obj_item_bodypart limb = null)
    {
        if (!Dm.truthy(Dm.bitand(limb.bodytype, BODYTYPE_ORGANIC)))
            return;

        // Not updating because enable/disable_bonus(obj/item/organ/removed_organ) call it down the line, and calls coming from comsigs update the owner's body themselves
        Waiting.NoSleep(limb.add_bodypart_texture(limb_texture, update: FALSE));
        if (Dm.truthy(color_overlay_priority))
            limb.add_color_override(COLOR_WHITE, color_overlay_priority);
    }

    // tg: code/datums/elements/organ_set_bonus.dm:174
    [SignalHandler]
    public virtual void untexture_limb(atom source = null, obj_item_bodypart limb = null)
    {
        Waiting.NoSleep(limb.remove_bodypart_texture(limb_texture, update: FALSE));
        if (Dm.truthy(color_overlay_priority))
            limb.remove_color_override(color_overlay_priority);
    }
}
