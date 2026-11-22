// Translated from tgstation (snapshot 1d99bd7f): code/datums/wounds/_wound_static_data.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 2, the wound engine). The var block is generated (D111).

// tg: code/datums/wounds/_wound_static_data.dm:8
public partial class datum_wound_pregen_data : datum
{
    // tg: code/datums/wounds/_wound_static_data.dm:51
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);

        if (!Dm.truthy(@abstract))
        {
            if (required_limb_biostate == null)
                stack_trace(Dm.text("required_limb_biostate null - please set it! occurred on: []", type), "code/datums/wounds/_wound_static_data.dm", 56);
            if (wound_path_to_generate == null)
                stack_trace(Dm.text("wound_path_to_generate null - please set it! occurred on: []", type), "code/datums/wounds/_wound_static_data.dm", 58);
        }

        scar_priorities = generate_scar_priorities() as DmList;
    }

    /// Should return a list of BIO_ biostate priorities, in order. See [scar_priorities] for further documentation.
    // tg: code/datums/wounds/_wound_static_data.dm:63
    public virtual object generate_scar_priorities()
    {
        DmList priorities = Dm.list(
            Dm.text("[]", BIO_FLESH),
            Dm.text("[]", BIO_BONE));

        return priorities;
    }

    // this proc is the primary reason this datum exists - a singleton instance so we can always run this proc even without the wound existing
    /**
     * Args:
     * * obj/item/bodypart/limb: The limb we are considering.
     * * suggested_wounding_typs: The wounding type to be checked against the wounding type we require. Defaults to required_wounding_type.
     * * datum/wound/old_wound: If we would replace a wound, this would be said wound. Nullable.
     * * random_roll = FALSE: If this is in the context of a random wound generation, and this wound wasn't specifically checked.
     *
     * Returns:
     * FALSE if the limb cannot be wounded, if the wounding types don't match ours (via wounding_types_valid()), if we have a higher severity wound already in our series,
     * if we have a biotype mismatch, if the limb isn't in a viable zone, or if there's any duplicate wound types.
     * TRUE otherwise.
     */
    // tg: code/datums/wounds/_wound_static_data.dm:86
    public virtual object can_be_applied_to(obj_item_bodypart limb = null, object suggested_wounding_type = null, datum_wound old_wound = null, float? random_roll = null, object duplicates_allowed = null, float? care_about_existing_wounds = null)
    {
        suggested_wounding_type ??= required_wounding_type;
        random_roll ??= FALSE;
        duplicates_allowed ??= this.duplicates_allowed;
        care_about_existing_wounds ??= TRUE;

        if (limb == null)
            return FALSE;

        if (Dm.truthy(random_roll) && !Dm.truthy(can_be_randomly_generated))
            return FALSE;

        if (!Dm.truthy(wounding_types_valid(suggested_wounding_type)))
            return FALSE;

        if (Dm.truthy(care_about_existing_wounds))
        {
            foreach (datum_wound preexisting_wound in Dm.each_cast<datum_wound>(limb.wounds))
            {
                datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[preexisting_wound.type];
                if (Dm.eq(pregen_data.wound_series, wound_series))
                {
                    if (preexisting_wound.severity >= Dm.initial(wound_path_to_generate, (datum_wound p) => p.severity))
                        return FALSE;
                }
            }
        }

        if (!Dm.truthy(ignore_cannot_bleed) && (Dm.truthy(Dm.bitand(required_limb_biostate, BIO_BLOODED)) && !Dm.truthy(limb.can_bleed())))
            return FALSE;

        if (!Dm.truthy(biostate_valid(limb.biological_state)))
            return FALSE;

        if (!Dm.@in(limb.body_zone, viable_zones))
            return FALSE;

        // we accept promotions and demotions, but no point in redundancy. This should have already been checked wherever the wound was rolled and applied for (see: bodypart damage code), but we do an extra check
        // in case we ever directly add wounds
        if (!Dm.truthy(duplicates_allowed))
        {
            foreach (datum_wound preexisting_wound in Dm.each_cast<datum_wound>(limb.wounds))
            {
                if (preexisting_wound.type == wound_path_to_generate && (preexisting_wound != old_wound))
                    return FALSE;
            }
        }
        return TRUE;
    }

    /// Returns true if we have the given biostates, or any biostate in it if check_for_any is true. False otherwise.
    // tg: code/datums/wounds/_wound_static_data.dm:123
    public virtual object biostate_valid(object biostate = null)
    {
        if (Dm.truthy(require_any_biostate))
        {
            if (!Dm.truthy(Dm.bitand(biostate, required_limb_biostate)))
                return FALSE;
        }
        else if (!Dm.eq(Dm.bitand(biostate, required_limb_biostate), required_limb_biostate)) // check for all
            return FALSE;

        return TRUE;
    }

    /**
     * A simple getter for [weight], with arguments supplied to allow custom behavior.
     *
     * Args:
     * * obj/item/bodypart/limb: The limb we are contemplating being added to. Nullable.
     * * woundtype: The woundtype of the assumed attack that would generate us. Nullable.
     * * damage: The raw damage that would cause us. Nullable.
     * * attack_direction: The direction of the attack that'd cause us. Nullable.
     * * damage_source: The entity that would cause us. Nullable.
     *
     * Returns:
     * Our weight.
     */
    // tg: code/datums/wounds/_wound_static_data.dm:145
    public virtual object get_weight(obj_item_bodypart limb = null, object woundtype = null, object damage = null, object attack_direction = null, object damage_source = null)
    {
        return weight;
    }

    /// Returns TRUE if we use WOUND_ALL or our wounding type
    // tg: code/datums/wounds/_wound_static_data.dm:149
    public virtual object wounding_types_valid(object suggested_wounding_type = null)
    {
        if (Dm.eq(required_wounding_type, WOUND_ALL))
            return TRUE;
        return Dm.eq(suggested_wounding_type, required_wounding_type) ? TRUE : FALSE;
    }

    /**
     * A simple getter for [threshold_minimum], with arguments supplied to allow custom behavior.
     *
     * Args:
     * * obj/item/bodypart/part: The limb we are contemplating being added to.
     * * attack_direction: The direction of the attack that'd generate us. Nullable.
     * * damage_source: The source of the damage that'd cause us. Nullable.
     */
    // tg: code/datums/wounds/_wound_static_data.dm:162
    public virtual object get_threshold_for(obj_item_bodypart part = null, object attack_direction = null, object damage_source = null)
    {
        return threshold_minimum;
    }

    /// Returns a new instance of our wound datum.
    // tg: code/datums/wounds/_wound_static_data.dm:166
    public virtual async DmTask<object> generate_instance(obj_item_bodypart limb = null, params object[] __extra)
    {
        return await Dm.new_async(wound_path_to_generate);
    }

    // tg: code/datums/wounds/_wound_static_data.dm:171
    public override object Destroy(float? force = null)
    {
        object dot = null;
        string error_message = Dm.text("[], a singleton wound pregen data instance, was destroyed! This should not happen!", this);
        if (Dm.truthy(force))
            error_message += " NOTE: This Destroy() was called with force == TRUE. This instance will be deleted and replaced with a new one.";
        stack_trace(error_message, "code/datums/wounds/_wound_static_data.dm", 175);

        if (!Dm.truthy(force))
            return QDEL_HINT_LETMELIVE;

        dot = base.Destroy(force);

        GLOB.all_wound_pregen_data[wound_path_to_generate] = Dm.@new(type); //recover
        return dot;
    }
}
