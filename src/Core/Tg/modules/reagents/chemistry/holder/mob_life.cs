// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/holder/mob_life.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice A; the mind's addiction points since gaps RG2, D339).

// tg: code/modules/reagents/chemistry/holder/mob_life.dm:16
public partial class datum_reagents : datum
{
    // tg: code/modules/reagents/chemistry/holder/mob_life.dm:1-4  (file-local defines, #undef at :80-83)
    private const int HAS_SILENT_TOXIN = 0; //don't provide a feedback message if this is the only toxin present
    private const int HAS_NO_TOXIN = 1;
    private const int HAS_PAINFUL_TOXIN = 2;
    private const int MAX_TOXIN_LIVER_DAMAGE = 2; //the max damage the liver can receive per second (~1 min at max damage will destroy liver)

    /**
     * Triggers metabolizing for all the reagents in this holder
     *
     * Arguments:
     * * mob/living/carbon/carbon - The mob to metabolize in, if null it uses [/datum/reagents/var/my_atom]
     * * seconds_per_tick - the time in server seconds between proc calls (when performing normally it will be 2)
     * * times_fired - the number of times the owner's life() tick has been called aka The number of times SSmobs has fired
     * * can_overdose - Allows overdosing
     * * liverless - Stops reagents that aren't set as [/datum/reagent/var/self_consuming] from metabolizing
     */
    // tg: code/modules/reagents/chemistry/holder/mob_life.dm:16
    public virtual async DmTask metabolize(mob_living_carbon owner = null, object seconds_per_tick = null, float? can_overdose = null, float? liverless = null, float? dead = null)
    {
        can_overdose ??= FALSE;
        liverless ??= FALSE;
        dead ??= FALSE;
        DmList cached_reagents = reagent_list;
        if (owner != null)
            await expose_temperature(owner.bodytemperature, 0.25f);

        float need_mob_update = FALSE;
        obj_item_organ_stomach belly = owner.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach;
        obj_item_organ_liver liver = owner.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        float liver_tolerance = 0;
        float liver_damage = 0;
        float provide_pain_message = 0; // null in tg; only its truthiness and its HAS_PAINFUL_TOXIN test are read
        float amount = 0;
        if (liver != null)
        {
            float liver_health_percent = (liver.maxHealth - Dm.num(liver.damage)) / liver.maxHealth;
            liver_tolerance = liver.toxTolerance * liver_health_percent;
            provide_pain_message = HAS_NO_TOXIN;
        }

        // An allocation the empty holder of every carbon's every Life tick does not need: nothing reads the list unless a
        // reagent is metabolized (metabolize_reagent writes it only when it is not null).
        DmList reagents_metabolized = length(cached_reagents) > 0 ? Dm.list() : null;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            datum_reagent_toxin toxin = reagent as datum_reagent_toxin;
            // skip metabolizing effects for small units of toxins
            if (toxin != null && liver != null && !Dm.truthy(dead))
            {
                amount = Dm.num(toxin.volume);
                if (belly != null)
                    amount += Dm.num(((datum_reagents)belly.reagents).get_reagent_amount(toxin.type));

                if (amount <= liver_tolerance * toxin.liver_tolerance_multiplier)
                {
                    await ((datum_reagents)owner.reagents).remove_reagent(toxin.type, toxin.metabolization_rate * owner.metabolism_efficiency * Dm.num(seconds_per_tick));
                    continue;
                }
            }

            need_mob_update += Dm.num(await metabolize_reagent(owner, reagent, seconds_per_tick, can_overdose, liverless, dead, reagents_metabolized));

            // If applicable, calculate any toxin-related liver damage
            // Note: we have to do this AFTER metabolize_reagent, because we want handle_reagent to run before we make the determination.
            // The order is really important unfortunately.
            if (toxin != null && !Dm.truthy(liverless) && liver != null && Dm.truthy(liver.filterToxins) && !HAS_TRAIT(owner, TRAIT_TOXINLOVER) && !HAS_TRAIT(owner, TRAIT_TOXIMMUNE))
            {
                if (Dm.truthy(toxin.affected_organ_flags) && !Dm.truthy(Dm.bitand(liver.organ_flags, toxin.affected_organ_flags))) //this particular toxin does not affect this type of organ
                    continue;

                // a 15u syringe is a nice baseline to scale lethality by
                liver_damage += ((amount / 15) * toxin.toxpwr * toxin.liver_damage_multiplier) / liver.liver_resistance;

                if (provide_pain_message != HAS_PAINFUL_TOXIN)
                    provide_pain_message = Dm.truthy(toxin.silent_toxin) ? HAS_SILENT_TOXIN : HAS_PAINFUL_TOXIN;
            }
        }

        // if applicable, apply our liver damage and display the accompanying pain message
        if (Dm.truthy(liver_damage))
            await liver.apply_organ_damage(min(liver_damage * Dm.num(seconds_per_tick), MAX_TOXIN_LIVER_DAMAGE * Dm.num(seconds_per_tick)));

        // HAS_NO_TOXIN is 1, so a damaged liver aches with no toxin at all (tg's own reading of its flag)
        if (Dm.truthy(provide_pain_message) && Dm.num(liver.damage) > 10 && SPT_PROB(Dm.num(liver.damage) / 6, seconds_per_tick)) //the higher the damage the higher the probability
            to_chat(owner, span_warning(I18n.T("You feel a dull pain in your abdomen.")));

        if (owner != null)
        {
            //apply side effects based on reagents metabolized
            if (length(reagents_metabolized) > 1)
            {
                foreach (datum_stacked_metabolization_effect effect in Dm.each_cast<datum_stacked_metabolization_effect>(GLOB.stacked_metabolization_effect))
                    need_mob_update += Dm.num(effect.check_and_apply(reagents_metabolized, owner, seconds_per_tick));
            }

            //some of the metabolized reagents had effects on the mob that requires some updates.
            if (Dm.truthy(need_mob_update))
                await owner.updatehealth();
        }
    }

    /*
     * Metabolises a single reagent for a target owner carbon mob. See above.
     *
     * Arguments:
     * * mob/living/carbon/owner - The mob to metabolize in, if null it uses [/datum/reagents/var/my_atom]
     * * seconds_per_tick - the time in server seconds between proc calls (when performing normally it will be 2)
     * * can_overdose - Allows overdosing
     * * liverless - Stops reagents that aren't set as [/datum/reagent/var/self_consuming] from metabolizing
     * * list/reagents_metabolized - a list to hold the reagent metabolized with the metabolization ratio
     */
    // tg: code/modules/reagents/chemistry/holder/mob_life.dm:95
    public virtual async DmTask<object> metabolize_reagent(mob_living_carbon owner = null, datum_reagent reagent = null, object seconds_per_tick = null, float? can_overdose = null, float? liverless = null, float? dead = null, DmList reagents_metabolized = null)
    {
        can_overdose ??= FALSE;
        liverless ??= FALSE;
        dead ??= FALSE;
        if (QDELETED(reagent.holder))
            return FALSE;

        if (owner == null)
            owner = reagent.holder.my_atom as mob_living_carbon;

        if (owner == null || reagent == null || (Dm.truthy(dead) && !Dm.truthy(Dm.bitand(reagent.chemical_flags, REAGENT_DEAD_PROCESS))))
            return FALSE;

        float metabolized_volume = Dm.num(await reagent.compute_metabolization(owner, seconds_per_tick));
        float metabolization_ratio = REM() * metabolized_volume;

        object tick_return = await owner.reagent_tick(reagent, seconds_per_tick, metabolization_ratio);
        if (Dm.truthy(Dm.bitand(tick_return, COMSIG_MOB_STOP_REAGENT_TICK)))
            return FALSE;

        if (Dm.truthy(liverless) && !Dm.truthy(reagent.self_consuming)) //need to be6 metabolized
            return FALSE;

        float need_mob_update = FALSE;
        if (reagents_metabolized != null)
            reagents_metabolized[reagent.type] = metabolization_ratio;
        if (Dm.truthy(can_overdose) && !HAS_TRAIT(owner, TRAIT_OVERDOSEIMMUNE))
        {
            if (Dm.truthy(reagent.overdose_threshold) && Dm.num(reagent.volume) >= reagent.overdose_threshold && !Dm.truthy(reagent.overdosed))
            {
                reagent.overdosed = TRUE;
                await reagent.overdose_start(owner, metabolization_ratio); // need_mob_update += : overdose_start is a void slot (rules 7.4)
                owner.log_message(Dm.text("has started overdosing on [] at [] units.", reagent.name, reagent.volume), LOG_GAME);
            }

            foreach (object addiction in Dm.each(reagent.addiction_types))
            {
                object threshold = reagent.addiction_types[addiction];
                Type addiction_type = addiction as Type;
                // point gain is scaled based on how much we metabolized per second
                owner.mind?.add_addiction_points(addiction, Dm.div(Dm.initial(addiction_type, (datum_addiction p) => p.addiction_gain_threshold), Dm.div(threshold, metabolized_volume)));
            }

            if (Dm.truthy(reagent.overdosed))
                need_mob_update += Dm.num(await reagent.overdose_process(owner, seconds_per_tick, metabolization_ratio));
        }

        reagent.current_cycle++;
        need_mob_update += Dm.num(await reagent.on_mob_life(owner, seconds_per_tick, metabolization_ratio));

        if (Dm.truthy(dead) && !QDELETED(owner) && !QDELETED(reagent))
            need_mob_update += Dm.num(await reagent.on_mob_dead(owner, seconds_per_tick, metabolization_ratio));

        if (!QDELETED(owner) && !QDELETED(reagent) && !Dm.truthy(Dm.bitand(tick_return, COMSIG_MOB_STOP_REAGENT_METABOLISM)))
            await reagent.metabolize_reagent(owner, seconds_per_tick, metabolized_volume);

        return need_mob_update;
    }

    /**
     * Signals that metabolization has stopped, triggering the end of trait-based effects
     * Arguments
     *
     * * [C][mob/living/carbon] - the mob to end metabolization on
     * * keep_liverless - if true will work without a liver
     */
    // tg: code/modules/reagents/chemistry/holder/mob_life.dm:150
    public virtual async DmTask end_metabolization(mob_living_carbon C = null, float? keep_liverless = null)
    {
        keep_liverless ??= TRUE;
        DmList cached_reagents = reagent_list;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(cached_reagents))
        {
            if (QDELETED(reagent.holder))
                continue;
            if (Dm.truthy(keep_liverless) && Dm.truthy(reagent.self_consuming)) //Will keep working without a liver
                continue;
            if (C == null)
                C = reagent.holder.my_atom as mob_living_carbon;
            if (Dm.truthy(reagent.metabolizing))
            {
                reagent.metabolizing = FALSE;
                await reagent.on_mob_end_metabolize(C);
            }
        }
    }

    /**
     * Processes the reagents in the holder and converts them, only called in a mob/living/carbon on addition
     *
     * Arguments:
     * * reagent - the added reagent datum/object
     * * added_volume - the volume of the reagent that was added (since it can already exist in a mob)
     * * added_purity - the purity of the added volume
     * * list/reagent_datum - a holder that will contain the inverse reagent datum that got added if this returns FALSE
     * returns the volume of the original, pure, reagent to add / keep
     */
    // tg: code/modules/reagents/chemistry/holder/mob_life.dm:173
    public virtual async DmTask<object> process_mob_reagent_purity(datum_reagent reagent = null, object added_volume = null, object added_purity = null, DmList reagent_datum = null)
    {
        // PRIVATE_PROC(TRUE)

        //no splitting when purity is perfect
        if (Dm.eq(added_purity, 1))
            return added_volume;

        //Turns all of a added reagent into the inverse chem
        if (reagent.inverse_chem_val > Dm.num(added_purity) && reagent.inverse_chem != null)
        {
            reagent_datum ??= Dm.list();
            object added_amount = await add_reagent(reagent.inverse_chem, added_volume, null, added_purity: reagent.get_inverse_purity(reagent.creation_purity), reagent_added: reagent_datum);
            datum_reagent inverse_reagent = reagent_datum[length(reagent_datum)] as datum_reagent;
            if (Dm.truthy(Dm.bitand(inverse_reagent.chemical_flags, REAGENT_SNEAKYNAME)))
                inverse_reagent.name = reagent.name; //Negative effects are hidden
            return Dm.num(added_amount) * -1;
        }

        //reagent did not split because purity was above inverse value
        return added_volume;
    }

    /**
     * Processes any chems that have the REAGENT_IGNORE_STASIS bitflag ONLY
     * Arguments
     *
     * * [owner][mob/living/carbon] - the mob we are doing stasis handlng on
     * * seconds_per_tick - passed from process
     */
    // tg: code/modules/reagents/chemistry/holder/mob_life.dm:200
    public virtual async DmTask handle_stasis_chems(mob_living_carbon owner = null, object seconds_per_tick = null)
    {
        float need_mob_update = FALSE;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagent_list))
        {
            if (!Dm.truthy(Dm.bitand(reagent.chemical_flags, REAGENT_IGNORE_STASIS)))
                continue;
            need_mob_update += Dm.num(await metabolize_reagent(owner, reagent, seconds_per_tick, can_overdose: TRUE));
        }
        if (owner != null && Dm.truthy(need_mob_update)) //some of the metabolized reagents had effects on the mob that requires some updates.
            await owner.updatehealth();
    }
}
