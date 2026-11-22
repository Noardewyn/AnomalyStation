// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/liver/_liver.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The liver base (M2 P2 its insertion hooks and on_life; M8 slice A the chemical handling, examine, the job traits' transfer, the failure
// stages and the owner's examine). Gaps OG1 (D339, D341): the grinding, the surplus, snail, leaky and distillery livers.

// tg: code/modules/surgery/organs/internal/liver/_liver.dm:5
[DmPath("/obj/item/organ/liver")]
public partial class obj_item_organ_liver : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:1  #define LIVER_DEFAULT_TOX_TOLERANCE 3 (file-local, #undef at
    // :402; rules 11.4: internal on the file's main type, the liver subtypes use it too)
    internal const int LIVER_DEFAULT_TOX_TOLERANCE = 3; //amount of toxins the liver can filter out
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:3  #define LIVER_FAILURE_STAGE_SECONDS 60 //amount of seconds before liver failure reaches a new stage (file-local, #undef at :404)
    internal const int LIVER_FAILURE_STAGE_SECONDS = 60;
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:2  #define LIVER_DEFAULT_TOX_RESISTANCE 1 (file-local, #undef at :403)
    internal const int LIVER_DEFAULT_TOX_RESISTANCE = 1; //lower values lower how harmful toxins are to the liver

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:6-24
    public obj_item_organ_liver()
    {
        name = "liver";
        desc = "Pairing suggestion: chianti and fava beans.";
        icon_state = "liver";

        w_class = WEIGHT_CLASS_SMALL;
        zone = BODY_ZONE_CHEST;
        slot = ORGAN_SLOT_LIVER;

        maxHealth = STANDARD_ORGAN_THRESHOLD;
        healing_factor = STANDARD_ORGAN_HEALING;
        decay_factor = STANDARD_ORGAN_DECAY; // smack in the middle of decay times

        cell_line = CELL_LINE_ORGAN_LIVER;
        cells_minimum = 1;
        cells_maximum = 2;

        visual = FALSE;
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:18  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        food_reagents = Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment_organ_tissue), 5), Dm.kv(typeof(datum_reagent_iron), 5));
    }

    /// Affects how much damage the liver takes from alcohol
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:27
    public float alcohol_tolerance = ALCOHOL_RATE;
    /// The maximum volume of toxins the liver will ignore
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:29
    public float toxTolerance = LIVER_DEFAULT_TOX_TOLERANCE;
    /// Modifies how much damage toxin deals to the liver
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:31
    public float liver_resistance = LIVER_DEFAULT_TOX_RESISTANCE;
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:32
    public float filterToxins = TRUE; //whether to filter toxins
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:33
    public float operated = FALSE; //whether the liver's been repaired with surgery and can be fixed again or not
}

// tg: code/modules/surgery/organs/internal/liver/_liver.dm:35 (interleaved types, rules 1.9)
public partial class obj_item_organ_liver : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // If the liver handles foods like a clown, it honks like a bike horn
        // Don't think about it too much.
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_COMEDY_METABOLISM), nameof(on_add_comedy_metabolism));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_COMEDY_METABOLISM), nameof(on_remove_comedy_metabolism));
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/liver/_liver.dm:54
public partial class obj_item_organ_liver : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:54
    public virtual void on_add_comedy_metabolism()
    {
        // Are clown "bike" horns made from the livers of ex-clowns?
        // Would that make the clown more or less likely to honk it
        Waiting.NoSleep(AddComponent(typeof(datum_component_squeak), Dm.list(Dm.kv(Dm.resource("sound/items/bikehorn.ogg"), 1)), 50, Dm.named("falloff_exponent", 20)));
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:65
    public virtual void on_remove_comedy_metabolism()
    {
        // SIGNAL_HANDLER

        qdel(GetComponent(typeof(datum_component_squeak)));
    }

    /**
     * This proc can be overriden by liver subtypes so they can handle certain chemicals in special ways.
     * Return null to continue running the normal on_mob_life() for that reagent.
     * Return COMSIG_MOB_STOP_REAGENT_TICK to not run the normal metabolism effects.
     *
     * NOTE: If you return COMSIG_MOB_STOP_REAGENT_TICK, that reagent will not be removed like normal! You must handle it manually.
     **/
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:86
    public virtual object handle_chemical(mob_living_carbon organ_owner = null, datum_reagent chem = null, object seconds_per_tick = null, object metabolization_rate = null)
    {
        // SIGNAL_HANDLER
        return null;
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:89
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);

        if (HAS_MIND_TRAIT(user, TRAIT_ENTRAILS_READER) || isobserver(user))
        {
            if (HAS_TRAIT(this, TRAIT_LAW_ENFORCEMENT_METABOLISM))
                Dm.append(ref dot, span_info(I18n.T("Fatty deposits and sprinkle residue, imply that this is the liver of someone in <em>security</em>.")));
            if (HAS_TRAIT(this, TRAIT_CULINARY_METABOLISM))
                Dm.append(ref dot, span_info(I18n.T("The high iron content and slight smell of garlic, implies that this is the liver of a <em>cook</em>.")));
            if (HAS_TRAIT(this, TRAIT_BARTENDER_METABOLISM))
                Dm.append(ref dot, span_info(I18n.T("The decidedly well-used look from periods of prolonged exposure to a wide variety of alcohols, implies that this is the liver of a <em>bartender</em>.")));
            if (HAS_TRAIT(this, TRAIT_COMEDY_METABOLISM))
                Dm.append(ref dot, span_info(I18n.T("A smell of bananas, a slippery sheen and [] when depressed, implies that this is the liver of a <em>clown</em>.", span_clown(I18n.T("honking")))));
            if (HAS_TRAIT(this, TRAIT_MEDICAL_METABOLISM))
                Dm.append(ref dot, span_info(I18n.T("Marks of stress and a faint whiff of medicinal alcohol, imply that this is the liver of a <em>medical worker</em>.")));
            if (HAS_TRAIT(this, TRAIT_ENGINEER_METABOLISM))
                Dm.append(ref dot, span_info(I18n.T("Signs of radiation exposure and space adaption, implies that this is the liver of an <em>engineer</em>.")));
            if (HAS_TRAIT(this, TRAIT_SCIENTIST_LIVER))
                Dm.append(ref dot, span_info(I18n.T("Strange glowing residues, sprinklings of congealed solid plasma, and what seem to be tumors indicate this is the radiated liver of a <em>scientist</em>.")));
            if (HAS_TRAIT(this, TRAIT_MAINTENANCE_METABOLISM))
                Dm.append(ref dot, span_info(I18n.T("A half-digested rat's tail (somehow), disgusting sludge, and the faint smell of Grey Bull imply this is what remains of an <em>assistant</em>'s liver.")));
            if (HAS_TRAIT(this, TRAIT_CORONER_METABOLISM))
                Dm.append(ref dot, span_info(I18n.T("An aroma of pickles and sea water, along with being remarkably well-preserved, imply this is what remains of a <em>coroner</em>'s liver.")));
            if (HAS_TRAIT(this, TRAIT_HUMAN_AI_METABOLISM))
                Dm.append(ref dot, span_info(I18n.T("The liver appears barely human and entirely self-sufficient, implying this is what remains of a <em>human AI</em>'s liver.")));

            // royal trumps pretender royal
            if (HAS_TRAIT(this, TRAIT_ROYAL_METABOLISM))
                Dm.append(ref dot, span_info(I18n.T("A rich diet of luxury food, suppleness from soft beds, implies that this is the liver of a <em>head of staff</em>.")));
            else if (HAS_TRAIT(this, TRAIT_PRETENDER_ROYAL_METABOLISM))
                Dm.append(ref dot, span_info(I18n.T("A diet of imitation caviar, and signs of insomnia, implies that this is the liver of <em>someone who wants to be a head of staff</em>.")));
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:120
    public override async DmTask before_organ_replacement(obj_item_organ replacement = null)
    {
        await base.before_organ_replacement(replacement);
        if (!istype(replacement, type))
            return;

        datum_job owner_job = owner.mind?.assigned_role;
        if (owner_job == null || !Dm.truthy(LAZYLEN(owner_job.liver_traits)))
            return;

        // Transfer over liver traits from jobs, if we should have them
        foreach (object readded_trait in Dm.each(owner_job.liver_traits))
        {
            if (!HAS_TRAIT_FROM(this, readded_trait, JOB_TRAIT))
                continue;
            ADD_TRAIT(replacement, readded_trait, JOB_TRAIT);
        }
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:200
    public virtual void on_owner_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (!ishuman(owner) || !Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            return;

        mob_living_carbon_human humie_owner = (mob_living_carbon_human)owner;
        if (humie_owner.get_organ_slot(ORGAN_SLOT_EYES) == null || Dm.truthy(humie_owner.is_eyes_covered()))
            return;
        if (failure_time >= 0 && failure_time <= 3 * LIVER_FAILURE_STAGE_SECONDS - 1)
            examine_list.Add(span_notice(I18n.T("[]'s eyes are slightly yellow.", owner)));
        else if (failure_time >= 3 * LIVER_FAILURE_STAGE_SECONDS && failure_time <= 4 * LIVER_FAILURE_STAGE_SECONDS - 1)
            examine_list.Add(span_notice(I18n.T("[]'s eyes are completely yellow, and [] [] visibly suffering.", owner, owner.p_they(), owner.p_are())));
        else if (failure_time >= 4 * LIVER_FAILURE_STAGE_SECONDS)
            examine_list.Add(span_danger(I18n.T("[]'s eyes are completely yellow and swelling with pus. [] []n't look like [] will be alive for much longer.", owner, owner.p_They(), owner.p_do(), owner.p_they())));
    }
}

// tg: code/modules/surgery/organs/internal/liver/_liver.dm:70 (interleaved types, rules 1.9)
public partial class obj_item_organ_liver : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:70
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = null;
        dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(organ_owner, COMSIG_MOB_REAGENT_TICK, nameof(handle_chemical));
        RegisterSignal(organ_owner, COMSIG_ATOM_EXAMINE, nameof(on_owner_examine));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:75
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal(organ_owner as datum, Dm.list(COMSIG_MOB_REAGENT_TICK, COMSIG_ATOM_EXAMINE));
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:135
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        //If your liver is failing, then we use the liverless version of metabolize
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) || HAS_TRAIT(owner, TRAIT_LIVERLESS_METABOLISM))
        {
            await ((datum_reagents)owner.reagents).end_metabolization(keep_liverless: TRUE);
            await ((datum_reagents)owner.reagents).metabolize(owner, seconds_per_tick, can_overdose: TRUE, liverless: TRUE);
            return;
        }

        if (owner.reagents != null)
            await ((datum_reagents)owner.reagents).metabolize(owner, seconds_per_tick, can_overdose: TRUE);
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:145
    public override async DmTask handle_failing_organs(object seconds_per_tick = null)
    {
        if (HAS_TRAIT(owner, TRAIT_STABLELIVER) || HAS_TRAIT(owner, TRAIT_LIVERLESS_METABOLISM))
            return;
        await base.handle_failing_organs(seconds_per_tick);
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:150
    public override async DmTask organ_failure(object seconds_per_tick = null)
    {
        object sw__1 = Dm.div(failure_time, LIVER_FAILURE_STAGE_SECONDS);
        if (Dm.eq(sw__1, 1))
            to_chat(owner, span_userdanger(I18n.T("You feel stabbing pain in your abdomen!")));
        else if (Dm.eq(sw__1, 2))
        {
            to_chat(owner, span_userdanger(I18n.T("You feel a burning sensation in your gut!")));
            await owner.vomit(VOMIT_CATEGORY_DEFAULT);
        }
        else if (Dm.eq(sw__1, 3))
        {
            to_chat(owner, span_userdanger(I18n.T("You feel painful acid in your throat!")));
            await owner.vomit(VOMIT_CATEGORY_BLOOD);
        }
        else if (Dm.eq(sw__1, 4))
        {
            to_chat(owner, span_userdanger(I18n.T("Overwhelming pain knocks you out!")));
            await owner.vomit(VOMIT_CATEGORY_BLOOD, distance: rand(1, 2));
            await owner.emote("Scream");
            await owner.AdjustUnconscious(2.5f * SECONDS);
        }
        else if (Dm.eq(sw__1, 5))
        {
            to_chat(owner, span_userdanger(I18n.T("You feel as if your guts are about to melt!")));
            await owner.vomit(VOMIT_CATEGORY_BLOOD, distance: rand(1, 3));
            await owner.emote("Scream");
            await owner.AdjustUnconscious(5 * SECONDS);
        }

        //After 60 seconds we begin to feel the effects
        if (failure_time >= 1 * LIVER_FAILURE_STAGE_SECONDS && failure_time <= 2 * LIVER_FAILURE_STAGE_SECONDS - 1)
        {
            await owner.adjust_tox_loss(0.2f * Dm.num(seconds_per_tick), forced: TRUE);
            owner.adjust_disgust(0.1f * Dm.num(seconds_per_tick));
        }
        else if (failure_time >= 2 * LIVER_FAILURE_STAGE_SECONDS && failure_time <= 3 * LIVER_FAILURE_STAGE_SECONDS - 1)
        {
            await owner.adjust_tox_loss(0.4f * Dm.num(seconds_per_tick), forced: TRUE);
            await owner.adjust_drowsiness(0.5f * SECONDS * Dm.num(seconds_per_tick));
            owner.adjust_disgust(0.3f * Dm.num(seconds_per_tick));
        }
        else if (failure_time >= 3 * LIVER_FAILURE_STAGE_SECONDS && failure_time <= 4 * LIVER_FAILURE_STAGE_SECONDS - 1)
        {
            await owner.adjust_tox_loss(0.6f * Dm.num(seconds_per_tick), forced: TRUE);
            await owner.adjust_organ_loss(pick(ORGAN_SLOT_HEART, ORGAN_SLOT_LUNGS, ORGAN_SLOT_STOMACH, ORGAN_SLOT_EYES, ORGAN_SLOT_EARS), 0.2f * Dm.num(seconds_per_tick));
            await owner.adjust_drowsiness(1 * SECONDS * Dm.num(seconds_per_tick));
            owner.adjust_disgust(0.6f * Dm.num(seconds_per_tick));

            if (SPT_PROB(1.5f, seconds_per_tick))
                await owner.emote("drool");
        }
        else if (failure_time >= 4 * LIVER_FAILURE_STAGE_SECONDS)
        {
            await owner.adjust_tox_loss(0.8f * Dm.num(seconds_per_tick), forced: TRUE);
            await owner.adjust_organ_loss(pick(ORGAN_SLOT_HEART, ORGAN_SLOT_LUNGS, ORGAN_SLOT_STOMACH, ORGAN_SLOT_EYES, ORGAN_SLOT_EARS), 0.5f * Dm.num(seconds_per_tick));
            await owner.adjust_drowsiness(1.6f * SECONDS * Dm.num(seconds_per_tick));
            owner.adjust_disgust(1.2f * Dm.num(seconds_per_tick));

            if (SPT_PROB(3, seconds_per_tick))
                await owner.emote("drool");
        }
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:215
    public override object get_availability(datum_species owner_species = null, mob_living owner_mob = null)
    {
        return owner_species.mutantliver;
    }
}

// tg: code/modules/surgery/organs/internal/liver/_liver.dm:218 (M6 slice 2: the self-check's organ lines)
public partial class obj_item_organ_liver : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:218
    public override object feel_for_damage(object self_aware = null)
    {
        if (Dm.num(damage) < low_threshold)
            return null;
        if (Dm.num(damage) < high_threshold)
            return span_warning(I18n.T("Your [] feels sore.", Dm.truthy(self_aware) ? I18n.T("liver") : I18n.T("lower abdomen")));
        return span_boldwarning(I18n.T("Your [] feels like it's on fire!", Dm.truthy(self_aware) ? I18n.T("liver") : I18n.T("lower abdomen")));
    }
}

// tg: code/modules/surgery/organs/internal/liver/_liver.dm:240  (silicons SI3: the exosuit fabricator prints it)
public partial class obj_item_organ_liver_cybernetic : obj_item_organ_liver
{
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:252
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        // COOLDOWN_FINISHED(src, severe_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(Dm.num(severe_cooldown) <= world.time)) //So we cant just spam emp to kill people.
        {
            await owner.adjust_tox_loss(10 / Dm.num(severity));
            severe_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START(src, severe_cooldown, 10 SECONDS)
        }
        if (prob(emp_vulnerability / Dm.num(severity))) //Chance of permanent effects
            organ_flags = Dm.bitor(organ_flags, ORGAN_EMP); //Starts organ faliure - gonna need replacing soon.
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/liver/_liver.dm:300 (gaps P3b: the plant infusion)
public partial class obj_item_organ_liver_pod : obj_item_organ_liver
{
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:306
    public override object handle_chemical(mob_living_carbon organ_owner = null, datum_reagent chem = null, object seconds_per_tick = null, object metabolization_rate = null)
    {
        object dot = base.handle_chemical(organ_owner, chem, seconds_per_tick, metabolization_rate);
        if (Dm.truthy(Dm.bitand(dot, COMSIG_MOB_STOP_REAGENT_TICK)) || Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            return dot;
        if (!Dm.truthy(Dm.bitand(organ_owner.mob_biotypes, MOB_PLANT)))
            return dot;
        if (chem.type == typeof(datum_reagent_toxin_plantbgone))
            Waiting.NoSleep(organ_owner.adjust_tox_loss(1.5f * Dm.num(seconds_per_tick)));
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/liver/_liver.dm:42 (gaps OG1)
public partial class obj_item_organ_liver : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:42
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment_peptides), 5));
    }
}

// tg: code/modules/surgery/organs/internal/liver/_liver.dm:282 (gaps OG1)
public partial class obj_item_organ_liver_cybernetic_surplus : obj_item_organ_liver_cybernetic
{
    //surplus organs are so awful that they explode when removed, unless failing
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:296
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dangerous_organ_removal), /*surgical = */ TRUE));
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/liver/_liver.dm:315 (gaps OG1)
public partial class obj_item_organ_liver_snail : obj_item_organ_liver
{
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:323
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        await organ_owner.AddElement(typeof(datum_element_lube_walking), Dm.named("require_resting", TRUE));
        await organ_owner.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_snail), multiplicative_slowdown: snail_speed_mod);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:328
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon organ_owner__carbon = organ_owner as mob_living_carbon;
        organ_owner__carbon.remove_movespeed_modifier(typeof(datum_movespeed_modifier_snail));
        organ_owner__carbon.RemoveElement(typeof(datum_element_lube_walking), Dm.named("require_resting", TRUE));
    }

    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:333
    [DmParams("organ_owner", "chem", "seconds_per_tick")]
    public override object handle_chemical(mob_living_carbon organ_owner = null, datum_reagent chem = null, object seconds_per_tick = null, object metabolization_rate = null)
    {
        object dot = base.handle_chemical(organ_owner, chem, seconds_per_tick, metabolization_rate);
        if (Dm.truthy(Dm.bitand(dot, COMSIG_MOB_STOP_REAGENT_TICK)) || Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            return dot;
        if (istype(chem, typeof(datum_reagent_consumable_salt)))
        {
            playsound(organ_owner, SFX_SEAR, 30, TRUE);
            Waiting.NoSleep(organ_owner.adjust_fire_loss(1 * Dm.num(seconds_per_tick)));
            Waiting.NoSleep(((datum_reagents)organ_owner.reagents).remove_reagent(chem.type, REAGENTS_METABOLISM * Dm.num(seconds_per_tick)));
            return COMSIG_MOB_STOP_REAGENT_TICK;
        }
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/liver/_liver.dm:354 (gaps OG1)
public partial class obj_item_organ_liver_bloody : obj_item_organ_liver
{
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:365
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        owner.adjust_blood_volume(4 * Dm.num(seconds_per_tick), maximum: BLOOD_VOLUME_NORMAL);
    }
}

/// Convert all non-alcoholic drinks into alcohol
// tg: code/modules/surgery/organs/internal/liver/_liver.dm:371 (gaps OG1)
public partial class obj_item_organ_liver_distillery : obj_item_organ_liver
{
    // tg: code/modules/surgery/organs/internal/liver/_liver.dm:386
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        foreach (object reagent__it in Dm.each(((datum_reagents)owner.reagents).reagent_list))
        {
            datum_reagent reagent = reagent__it as datum_reagent;
            // Already alcohol
            if (istype(reagent, convert_into))
                continue;

            await ((datum_reagents)owner.reagents).convert_reagent(reagent.type, convert_into, ethanol_conversion);
        }
    }
}
