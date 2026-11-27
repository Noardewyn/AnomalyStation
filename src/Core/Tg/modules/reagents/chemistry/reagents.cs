// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the /datum/reagent base's name (the list, sort and comparator helpers name it), New, Destroy, on_new and on_merge
// (M4a P4a: the welder's fuel), the expose procs and intercept_reagents_transfer (M4b: the fire extinguisher's water),
// on_burn_wound_processing (M6 slice 3), and the metabolism hooks, burn, on_spark_act, the overdose procs, the taste
// description, the purity helpers and pretty_string_from_reagent_list (M8 slice A); on_hydroponics_apply (gaps P2, D298).
// used_on_fish (fishing, M10+) is not declared. The other vars of the block are generated (D111).

// tg: code/modules/reagents/chemistry/reagents.dm:2
[DmPath("/datum/reagent")]
public partial class datum_reagent : datum
{
    // tg: code/modules/reagents/chemistry/reagents.dm:6
    public string name = "";
    /// Are we from a material? We might wanna know that for special stuff. Like metalgen. Is replaced with a ref of the material on New()
    // tg: code/modules/reagents/chemistry/reagents.dm:46  var/datum/material/material (a typepath, then New makes it the
    // material datum: declared here as object, which the generated subtypes' typepath defaults fit)
    public object material;
}

// tg: code/modules/reagents/chemistry/reagents.dm:100 (M4a P4a: New, Destroy, on_new, on_merge; M4b: the expose procs and
// intercept_reagents_transfer; M6 slice 3: on_burn_wound_processing; the reagent's metabolism and the rest of the base are
// chemistry, M8)
public partial class datum_reagent : datum
{
    // tg: code/modules/reagents/chemistry/reagents.dm:100
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);

        if (material != null)
            material = await SSmaterials.get_material(material);
        if (Dm.truthy(glass_price))
            Waiting.NoSleep(AddElement(typeof(datum_element_venue_price), glass_price));
        if (!Dm.truthy(mass))
            mass = rand(10, 800);
    }

    /// This should only be called by the holder, so it's already handled clearing its references
    // tg: code/modules/reagents/chemistry/reagents.dm:112
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        holder = null;
        return dot;
    }

    /// Applies this reagent to an [/atom]
    // tg: code/modules/reagents/chemistry/reagents.dm:117
    [ShouldCallParent]
    public virtual async DmTask<object> expose_atom(atom exposed_atom = null, object reac_volume = null, float? methods = null)
    {
        methods ??= TOUCH;
        object dot = 0;
        dot = Dm.bitor(dot, !Dm.truthy(_listen_lookup?[COMSIG_REAGENT_EXPOSE_ATOM]) ? NONE : _SendSignal(COMSIG_REAGENT_EXPOSE_ATOM, Dm.list(this, exposed_atom, reac_volume, methods)));
        dot = Dm.bitor(dot, !Dm.truthy(exposed_atom._listen_lookup?[COMSIG_ATOM_EXPOSE_REAGENT]) ? NONE : exposed_atom._SendSignal(COMSIG_ATOM_EXPOSE_REAGENT, Dm.list(exposed_atom, this, reac_volume, methods)));
        return dot;
    }

    /// Applies this reagent to a [/mob/living]
    // tg: code/modules/reagents/chemistry/reagents.dm:125
    [ShouldCallParent]
    public virtual async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_REAGENT_EXPOSE_MOB]) ? NONE : _SendSignal(COMSIG_REAGENT_EXPOSE_MOB, Dm.list(this, exposed_mob, methods, reac_volume, show_message, touch_protection)), COMPONENT_NO_EXPOSE_REAGENTS)))
            return;

        if (exposed_mob.reagents == null) // lots of simple mobs do not have a reagents holder
            return;

        if (Dm.truthy(Dm.bitand(await exposed_mob.reagent_expose(this, methods, reac_volume, show_message, touch_protection), COMPONENT_NO_EXPOSE_REAGENTS)))
            return;

        if (Dm.truthy(Dm.bitand(penetrates_skin, methods))) // models things like vapors which penetrate the skin
        {
            float amount = round(Dm.num(reac_volume) * clamp((1 - Dm.num(touch_protection)), 0, 1), 0.1f);
            if (amount >= 0.5f)
                await ((datum_reagents)exposed_mob.reagents).add_reagent(type, amount, data as DmList, holder.chem_temp, purity);
        }
    }

    /// Applies this reagent to an [/obj]
    // tg: code/modules/reagents/chemistry/reagents.dm:143
    [ShouldCallParent]
    public virtual async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        return !Dm.truthy(_listen_lookup?[COMSIG_REAGENT_EXPOSE_OBJ]) ? NONE : _SendSignal(COMSIG_REAGENT_EXPOSE_OBJ, Dm.list(this, exposed_obj, reac_volume, methods, show_message));
    }

    /// Applies this reagent to a [/turf]
    // tg: code/modules/reagents/chemistry/reagents.dm:149
    [ShouldCallParent]
    public virtual async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        return !Dm.truthy(_listen_lookup?[COMSIG_REAGENT_EXPOSE_TURF]) ? NONE : _SendSignal(COMSIG_REAGENT_EXPOSE_TURF, Dm.list(this, exposed_turf, reac_volume));
    }

    // tg: code/modules/reagents/chemistry/reagents.dm:155
    public virtual async DmTask burn(datum_reagents holder = null)
    {
        return;
    }

    ///Called to begin metabolization and return the volume of reagent to metabolize
    // tg: code/modules/reagents/chemistry/reagents.dm:160
    public virtual async DmTask<object> compute_metabolization(mob_living_carbon affected_mob = null, object seconds_per_tick = null)
    {
        float metabolizing_out = metabolization_rate * Dm.num(seconds_per_tick);
        if (!Dm.truthy(Dm.bitand(chemical_flags, REAGENT_UNAFFECTED_BY_METABOLISM)))
        {
            if (Dm.truthy(Dm.bitand(chemical_flags, REAGENT_REVERSE_METABOLISM)))
                metabolizing_out = Dm.num(Dm.div(metabolizing_out, affected_mob.metabolism_efficiency));
            else
                metabolizing_out *= affected_mob.metabolism_efficiency;
        }

        if (!Dm.truthy(metabolizing))
        {
            metabolizing = TRUE;
            await on_mob_metabolize(affected_mob);
        }

        return min(metabolizing_out, Dm.num(volume));
    }

    /**
     * Ticks on mob Life() for as long as the reagent remains in the mob's reagents.
     *
     * Usage: Parent should be called first using . = ..()
     *
     * Exceptions: If the holder var needs to be accessed, call the parent afterward that as it can become null if the reagent is fully removed.
     *
     * Returns: UPDATE_MOB_HEALTH only if you need to update the health of a mob (this is only needed when damage is dealt to the mob)
     *
     * Arguments
     * * mob/living/carbon/affected_mob - the mob which the reagent currently is inside of
     * * seconds_per_tick - the time in server seconds between proc calls (when performing normally it will be 2)
     * * times_fired - the number of times the owner's Life() tick has been called aka The number of times SSmobs has fired
     *
     */
    // tg: code/modules/reagents/chemistry/reagents.dm:189
    [ShouldCallParent]
    public virtual async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        return null;
    }

    ///Metabolizes a portion of the reagent after on_mob_life() is called
    // tg: code/modules/reagents/chemistry/reagents.dm:193
    public virtual async DmTask metabolize_reagent(mob_living_carbon affected_mob = null, object seconds_per_tick = null, object metabolized_volume = null)
    {
        if (holder == null)
            return;
        volume = Dm.num(volume) - Dm.num(metabolized_volume);
        await holder.update_total();
    }

    /// Called in burns.dm *if* the reagent has the REAGENT_AFFECTS_WOUNDS process flag
    // tg: code/modules/reagents/chemistry/reagents.dm:200
    public virtual async DmTask on_burn_wound_processing(datum_wound_burn_flesh burn_wound = null)
    {
        return;
    }

    /**
     * Intercepts the reagent transfer/copy operation to do some work before it takes place.
     * Used to perform some reaction work. Return TRUE To cancel the operation
     *
     * Arguments
     *
     * * datum/reagents/target - the target holder we are being transferred to
     * * amount - the amount of reagent being transferred
     * * copy_only - if TRUE we don't remove ourself from the holder because its a reagent copy & not transfer operation
    */
    // tg: code/modules/reagents/chemistry/reagents.dm:213
    public virtual async DmTask<object> intercept_reagents_transfer(datum_reagents target = null, object amount = null, object copy_only = null)
    {
        return FALSE;
    }

    /// Called when this reagent is first added to a mob
    // tg: code/modules/reagents/chemistry/reagents.dm:217
    public virtual async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        // Scale the overdose threshold of the chem by the difference between the default and creation purity.
        overdose_threshold += (creation_purity - Dm.initial(this, (datum_reagent p) => p.purity)) * overdose_threshold;
        if (Dm.truthy(added_traits))
            affected_mob.add_traits(added_traits, Dm.text("base:[]", type));
    }

    /// Called when this reagent is removed while inside a mob
    // tg: code/modules/reagents/chemistry/reagents.dm:224
    public virtual async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        affected_mob.clear_mood_event(Dm.text("[]_overdose", type));
        REMOVE_TRAITS_IN(affected_mob, Dm.text("base:[]", type));
    }

    /// Called when this reagent first starts being metabolized by a liver
    // tg: code/modules/reagents/chemistry/reagents.dm:229
    [ShouldCallParent]
    public virtual async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        if (Dm.truthy(metabolized_traits))
            affected_mob.add_traits(metabolized_traits, METABOLIZATION_TRAIT(type));
    }

    /// Called when this reagent stops being metabolized by a liver
    // tg: code/modules/reagents/chemistry/reagents.dm:235
    [ShouldCallParent]
    public virtual async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        REMOVE_TRAITS_IN(affected_mob, METABOLIZATION_TRAIT(type));
    }

    /**
     * Called when a reagent is inside of a mob when they are dead if the reagent has the REAGENT_DEAD_PROCESS flag
     * Returning UPDATE_MOB_HEALTH will cause updatehealth() to be called on the holder mob by /datum/reagents/proc/metabolize.
     */
    // tg: code/modules/reagents/chemistry/reagents.dm:243
    [ShouldCallParent]
    public virtual async DmTask<object> on_mob_dead(mob_living_carbon affected_mob = null, object seconds_per_tick = null, params object[] __extra)
    {
        return null;
    }

    /*
     * Called when a reagent is exposed to electric current, rapidly heated or smashed, something that would cause explosives to get easily set off
     * Returning a SPARK_ACT_ flag will signal that an action has occurred as a result, for parent behavior and logging purposes
     * Probably shouldn't be called from within a mob's bloodstream, unless you're ready for some very explosive results
     * Arguments:
     * * power_charge - If we were triggered from electric current, how much power was dumped into us?
     * * spark_flags - Flags specific to the interaction, is it in an enclosed space, should we nerf common reagents, etc.
     */
    // tg: code/modules/reagents/chemistry/reagents.dm:254
    public virtual async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        power_charge ??= 0;
        spark_flags ??= NONE;
        return NONE;
    }

    /**
     * New, standard way of applying a reagent to the reagent holder
     * Arguments
     * * data - if not null, contains reagent data which will be applied to the newly created reagent (this will override any pre-set data).
     */
    // tg: code/modules/reagents/chemistry/reagents.dm:264
    public virtual async DmTask on_new(object data = null)
    {
        if (Dm.truthy(data))
            this.data = data;
    }

    /// Called when two reagents of the same are mixing.
    // tg: code/modules/reagents/chemistry/reagents.dm:269
    public virtual async DmTask on_merge(DmList mix_data = null, object amount = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_REAGENT_ON_MERGE]))
            _SendSignal(COMSIG_REAGENT_ON_MERGE, Dm.list(this, mix_data, amount));
    }

    /// Called if the reagent has passed the overdose threshold and is set to be triggering overdose effects. Returning UPDATE_MOB_HEALTH will cause updatehealth() to be called on the holder mob by /datum/reagents/proc/metabolize.
    // tg: code/modules/reagents/chemistry/reagents.dm:274
    public virtual async DmTask<object> overdose_process(mob_living affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        return null;
    }

    /// Called when an overdose starts. Returning UPDATE_MOB_HEALTH will cause updatehealth() to be called on the holder mob by /datum/reagents/proc/metabolize.
    // tg: code/modules/reagents/chemistry/reagents.dm:278
    public virtual async DmTask overdose_start(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        to_chat(affected_mob, span_userdanger(I18n.T("You feel like you took too much of []!", name)));
        await affected_mob.add_mood_event(Dm.text("[]_overdose", type), typeof(datum_mood_event_overdose), name);
        return;
    }

    /**
     * Called when this chemical is processed in a hydroponics tray.
     *
     * Can affect plant's health, stats, or cause the plant to react in certain ways.
     */
    // tg: code/modules/reagents/chemistry/reagents.dm:288
    public virtual DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        return DmTask.CompletedTask;
    }

    /// Should return a associative list where keys are taste descriptions and values are strength ratios
    // tg: code/modules/reagents/chemistry/reagents.dm:292
    public virtual object get_taste_description(mob_living taster = null)
    {
        if (taster == null || !HAS_TRAIT(taster, TRAIT_DETECTIVES_TASTE))
            return Dm.list(Dm.kv(Dm.text("[]", taste_description), 1));
        return Dm.list(Dm.kv(Dm.text("[]", LOWER_TEXT(name)), 1));
    }

    /**
     * Used when you want the default reagents purity to be equal to the normal effects
     * (i.e. if default purity is 0.75, and your reacted purity is 1, then it will return 1.33)
     *
     * Arguments
     * * normalise_num_to - what number/purity value you're normalising to. If blank it will default to the compile value of purity for this chem
     * * creation_purity - creation_purity override, if desired. This is the purity of the reagent that you're normalising from.
     */
    // tg: code/modules/reagents/chemistry/reagents.dm:305
    public virtual object normalise_creation_purity(object normalise_num_to = null, object creation_purity = null)
    {
        if (!Dm.truthy(normalise_num_to))
            normalise_num_to = Dm.initial(this, (datum_reagent p) => p.purity);
        if (!Dm.truthy(creation_purity))
            creation_purity = this.creation_purity;
        return Dm.div(creation_purity, normalise_num_to);
    }

    /**
     * Gets the inverse purity of this reagent. Mostly used when converting from a normal reagent to its inverse one.
     *
     * Arguments
     * * purity - Overrides the purity used for determining the inverse purity.
     */
    // tg: code/modules/reagents/chemistry/reagents.dm:318
    public virtual object get_inverse_purity(object purity = null)
    {
        if (inverse_chem == null || !Dm.truthy(inverse_chem_val))
            return 0;
        if (!Dm.truthy(purity))
            purity = this.purity;
        return min(1 - inverse_chem_val + Dm.num(purity) + 0.01f, 1); //Gives inverse reactions a 1% purity threshold for being 100% pure to appease players with OCD.
    }
}

// tg: code/modules/reagents/chemistry/reagents.dm:350 (a global proc after the /datum/reagent procs)
public static partial class GlobalProcs
{
    /**
     * Input a reagent_list, outputs pretty readable text!
     * Default output will be formatted as
     * * water, 5 | silicon, 6 | soup, 4 | space lube, 8
     *
     * * names_only will remove the amount displays, showing
     * * water | silicon | soup | space lube
     *
     * * join_text will alter the text between reagents
     * * setting to ", " will result in
     * * water, 5, silicon, 6, soup, 4, space lube, 8
     *
     * * final_and should be combined with the above. will format as
     * * water, 5, silicon, 6, soup, 4, and space lube, 8
     *
     * * capitalize_names will result in
     * * Water, 5 | Silicon, 6 | Soup, 4 | Space lube, 8
     *
     * * * use (reagents.reagent_list, names_only, join_text = ", ", final_and, capitalize_names) for the formatting
     * * * Water, Silicon, Soup, and Space Lube
     */
    // tg: code/modules/reagents/chemistry/reagents.dm:350
    public static object pretty_string_from_reagent_list(DmList reagent_list = null, object names_only = null, string join_text = null, object final_and = null, object capitalize_names = null)
    {
        join_text ??= " | ";
        //Convert reagent list to a printable string for logging etc
        DmList reagent_strings = Dm.list();
        float reagents_left = length(reagent_list);
        float intial_list_length = reagents_left;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagent_list))
        {
            reagents_left--;
            if (Dm.truthy(final_and) && intial_list_length > 1 && reagents_left == 0)
                reagent_strings.Add(Dm.text("and [][]", Dm.truthy(capitalize_names) ? capitalize(reagent.name) : reagent.name, Dm.truthy(names_only) ? null : Dm.text(", []", reagent.volume)));
            else
                reagent_strings.Add(Dm.text("[][]", Dm.truthy(capitalize_names) ? capitalize(reagent.name) : reagent.name, Dm.truthy(names_only) ? null : Dm.text(", []", reagent.volume)));
        }

        return reagent_strings.Join(join_text);
    }
}
