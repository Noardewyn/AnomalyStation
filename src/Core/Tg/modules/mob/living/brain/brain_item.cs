// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/brain/brain_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the brain's M2 subset translated (M2 P2, scope.md 2e "organs": Initialize, on_mob_insert, on_mob_remove,
// transfer_identity's mindless return, Destroy, on_life, the bodypart hooks, apply_organ_damage, roll_for_brain_trauma,
// check_damage_thresholds); M6 slice 5 (gibbing): transfer_identity whole (a gibbed player's mind goes to the brain mob).
// Silicons SI2: on_mob_insert's brain mob transfer (the mind back into a body) and the changeling's kept brain.
// The trauma procs that gaining a given trauma runs (can_gain_trauma, gain_trauma, brain_gain_trauma, the traumas list and
// the traumas' application on insert and removal) are translated for the blunt head trauma's concussion (item_attack.dm:442).
// Skillchips, repairs and examine come with M8 and M10 (their roots stay Dm.NotTranslated); the random trauma roll's
// gain_trauma_type came with the brain traumas (gaps B1).

// tg: code/modules/mob/living/brain/brain_item.dm:1
[DmPath("/obj/item/organ/brain")]
public partial class obj_item_organ_brain : obj_item_organ
{
    // tg: code/modules/mob/living/brain/brain_item.dm:2-20
    public obj_item_organ_brain()
    {
        name = "brain";
        desc = "A piece of juicy meat found in a person's head.";
        icon_state = "brain";
        visual = TRUE;
        throw_speed = 3;
        throw_range = 5;
        layer = ABOVE_MOB_LAYER;
        zone = BODY_ZONE_HEAD;
        slot = ORGAN_SLOT_BRAIN;
        organ_flags = Dm.bitor(Dm.bitor(ORGAN_ORGANIC, ORGAN_VITAL), ORGAN_PROMINENT);

        ///The brain's organ variables are significantly more different than the other organs, with half the decay rate for balance reasons, and twice the maxHealth
        decay_factor = STANDARD_ORGAN_DECAY * 0.5f; //30 minutes of decaying to result in a fully damaged brain, since a fast decay rate would be unfun gameplay-wise

        maxHealth = BRAIN_DAMAGE_DEATH;
        low_threshold = 45;
        high_threshold = 120;
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:12-30  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        attack_verb_continuous = Dm.list("attacks", "slaps", "whacks");
        attack_verb_simple = Dm.list("attack", "slap", "whack");

        organ_traits = Dm.list(TRAIT_ADVANCEDTOOLUSER, TRAIT_LITERATE, TRAIT_CAN_STRIP);

        traumas = Dm.list();
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:24
    public float suicided = FALSE;
    // tg: code/modules/mob/living/brain/brain_item.dm:25
    public mob_living_brain brainmob = null;
    /// If it's a fake brain with no brainmob assigned. Feedback messages will be faked as if it does have a brainmob. See changelings & dullahans.
    // tg: code/modules/mob/living/brain/brain_item.dm:27
    public float decoy_override = FALSE;


    // tg: code/modules/mob/living/brain/brain_item.dm:30
    public DmList traumas;

    /// List of skillchip items, their location should be this brain.
    // tg: code/modules/mob/living/brain/brain_item.dm:33
    public DmList skillchips;
    /// Maximum skillchip complexity we can support before they stop working. Do not reference this var directly and instead call get_max_skillchip_complexity()
    // tg: code/modules/mob/living/brain/brain_item.dm:35
    public float max_skillchip_complexity = 3;
    /// Maximum skillchip slots available. Do not reference this var directly and instead call get_max_skillchip_slots()
    // tg: code/modules/mob/living/brain/brain_item.dm:37
    public float max_skillchip_slots = 5;

    /// Size modifier for the sprite
    // tg: code/modules/mob/living/brain/brain_item.dm:40
    public float brain_size = 1;
    /// Can this brain become smooth after it gets washed
    // tg: code/modules/mob/living/brain/brain_item.dm:42
    public float can_smoothen_out = TRUE;
    /// We got smooth from being washed
    // tg: code/modules/mob/living/brain/brain_item.dm:44
    public float smooth_brain = FALSE;
    /// Variance in brain traits added by subtypes
    // tg: code/modules/mob/living/brain/brain_item.dm:46
    public object variant_traits_added;
    /// Variance in brain traits removed by subtypes
    // tg: code/modules/mob/living/brain/brain_item.dm:48
    public object variant_traits_removed;
    /// The color of this brain. Fluff, only used when repairing (shade of...).
    // tg: code/modules/mob/living/brain/brain_item.dm:50
    public string shade_color = "pink";
}

// tg: code/modules/mob/living/brain/brain_item.dm:52 (interleaved types, rules 1.9)
public partial class obj_item_organ_brain : obj_item_organ
{
    // tg: code/modules/mob/living/brain/brain_item.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // Brain size logic
        transform = ((matrix)transform).Scale(brain_size);
        organ_traits.Remove(variant_traits_removed);
        Dm.combine(ref organ_traits, variant_traits_added);
        return dot;
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:59
    [DmParams("brain_owner", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon brain_owner = organ_owner;
        special ??= FALSE;
        object dot = null;
        dot = await base.on_mob_insert(brain_owner, special, movement_flags);

        name = Dm.initial(this, (obj_item_organ_brain p) => p.name);

        // Special check for if you're trapped in a body you can't control because it's owned by a ling.
        if (Dm.truthy(IS_CHANGELING(brain_owner)) && !Dm.truthy(Dm.bitand(movement_flags, NO_ID_TRANSFER)))
        {
            if (brainmob != null && !IS_DEAD_OR_FAKING(brain_owner))
                to_chat(brainmob, span_danger(I18n.T("You can't feel your body! You're still just a brain!")));
            await forceMove(brain_owner);
            await brain_owner.update_body_parts();
            return dot;
        }

        if (brainmob != null)
        {
            // If it's a ling decoy brain, nothing to transfer, just throw it out
            if (Dm.truthy(decoy_override))
            {
                if (Dm.truthy(brainmob?.key))
                    stack_trace("Decoy override brain with a key assigned - This should never happen.", "code/modules/mob/living/brain/brain_item.dm", 76);
            }
            // Not a ling - assume direct control
            else
            {
                if (Dm.truthy(brain_owner.key))
                    brain_owner.ghostize();

                if (brainmob.mind != null)
                    await brainmob.mind.transfer_to(brain_owner);
                else
                    brain_owner.PossessByPlayer(brainmob.key);

                brain_owner.set_suicide(HAS_TRAIT(brainmob, TRAIT_SUICIDED) ? TRUE : FALSE);
            }

            QDEL_NULL(ref brainmob);
        }
        else
            brain_owner.set_suicide(suicided);

        foreach (datum_brain_trauma trauma in Dm.each_cast<datum_brain_trauma>(traumas))
        {
            if (trauma.owner != null)
            {
                if (trauma.owner == brain_owner)
                {
                    // if we're being special replaced, the trauma is already applied, so this is expected
                    // but if we're not... this is likely a bug, and should be reported
                    if (!Dm.truthy(special))
                        stack_trace(Dm.text("A brain trauma ([]) is being re-applied to its owning mob ([])!", trauma, brain_owner), "code/modules/mob/living/brain/brain_item.dm", 100);
                    continue;
                }

                stack_trace(Dm.text("A brain trauma ([]) is being applied to a new mob ([]) when it's owned by someone else ([])!", trauma, brain_owner, trauma.owner), "code/modules/mob/living/brain/brain_item.dm", 103);
                continue;
            }

            trauma.owner = brain_owner;
            if (!Dm.truthy(await trauma.on_gain()))
                qdel(trauma);
        }

        //Update the body's icon so it doesnt appear debrained anymore
        if (!Dm.truthy(special) && !Dm.truthy(Dm.bitand(brain_owner.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS)))
            await brain_owner.update_body_parts();
        return dot;
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:114
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon organ_owner__carbon = organ_owner as mob_living_carbon;
        await base.on_mob_remove(organ_owner, special, movement_flags);
        // Delete skillchips first as parent proc sets owner to null, and skillchips need to know the brain's owner.
        if (!QDELETED(organ_owner__carbon) && Dm.truthy(length(skillchips)))
        {
            if (!Dm.truthy(special))
            {
                to_chat(organ_owner__carbon, span_notice(I18n.T("You feel your skillchips enable emergency power saving mode, deactivating as your brain leaves your body...")));
                foreach (object chip in Dm.each(skillchips))
                {
                    obj_item_skillchip skillchip = (obj_item_skillchip)chip;
                    // Run the try_ proc with force = TRUE.
                    await skillchip.try_deactivate_skillchip(silent: special, force: TRUE, brain_owner: organ_owner__carbon);
                }
            }
        }

        foreach (object X in Dm.each(traumas))
        {
            datum_brain_trauma BT = X as datum_brain_trauma;
            await BT.on_lose(TRUE);
            BT.owner = null;
        }

        if ((!QDELETED(this) || !QDELETED(owner)) && !Dm.truthy(Dm.bitand(movement_flags, NO_ID_TRANSFER)))
            await transfer_identity(organ_owner__carbon);
        if (!Dm.truthy(special))
        {
            organ_owner__carbon.clear_mood_event("brain_damage");
            organ_owner__carbon.med_hud_set_status();
        }
    }
}

// tg: code/modules/mob/living/brain/brain_item.dm:140
public partial class obj_item_organ_brain : obj_item_organ
{
    // tg: code/modules/mob/living/brain/brain_item.dm:140
    public virtual async DmTask transfer_identity(mob_living L = null)
    {
        name = I18n.T("[]'s []", L.name, Dm.initial(this, (obj_item_organ_brain p) => p.name));
        if (brainmob != null)
        {
            if (!Dm.truthy(decoy_override))
                return;

            // it's just a dummy, throw it out
            QDEL_NULL(ref brainmob);
        }

        if (L.mind == null)
            return;
        // M6 slice 5: a gibbed player's brain keeps their mind in the brain mob (their ghost can return to it)
        brainmob = Dm.@new<mob_living_brain>(this);
        brainmob.name = (string)L.real_name;
        brainmob.real_name = L.real_name;
        brainmob.timeofdeath = L.timeofdeath;

        if (Dm.truthy(suicided))
            ADD_TRAIT(brainmob, TRAIT_SUICIDED, REF(this));

        if (Dm.truthy(L.has_dna()))
        {
            mob_living_carbon C = (mob_living_carbon)L;
            if (brainmob.stored_dna == null)
                brainmob.stored_dna = Dm.@new<datum_dna_stored>(brainmob);
            await ((datum_dna)C.dna).copy_dna(brainmob.stored_dna);
            // Hack, fucked dna needs to follow the brain to prevent memes, so we need to copy over the trait sources and shit
            foreach (object source in Dm.each(GET_TRAIT_SOURCES(L, TRAIT_BADDNA)))
                ADD_TRAIT(brainmob, TRAIT_BADDNA, source);
        }

        if (L.mind != null && L.mind.current != null && !Dm.truthy(decoy_override))
        {
            await L.mind.transfer_to(brainmob);
            to_chat(brainmob, span_notice(I18n.T("You feel slightly disoriented. That's normal when you're just a brain.")));
        }
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:217
    public virtual async DmTask<object> check_for_repair(obj_item item = null, mob user = null)
    {
        if (Dm.truthy(damage) && Dm.truthy(item.is_drainable()) && Dm.truthy(((datum_reagents)item.reagents).has_reagent(typeof(datum_reagent_medicine_mannitol))) && Dm.truthy(Dm.bitand(organ_flags, ORGAN_ORGANIC))) //attempt to heal the brain
        {
            if (brainmob != null && brainmob.health <= brainmob.dead_threshold) //if the brain is fucked anyway, do nothing
            {
                to_chat(user, span_warning(I18n.T("[] is far too damaged, there's nothing else we can do for it!", this)));
                return TRUE;
            }

            user.visible_message(span_notice(I18n.T("[] starts to slowly pour the contents of [] onto [].", user, item, this)), span_notice(I18n.T("You start to slowly pour the contents of [] onto [].", item, this)));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
            {
                to_chat(user, span_warning(I18n.T("You failed to pour the contents of [] onto []!", item, this)));
                return TRUE;
            }
            string and_bright_shade = !Dm.truthy(shade_color) ? "" : Dm.text(" and turn a slightly brighter shade of []", shade_color);
            user.visible_message(span_notice(I18n.T("[] pours the contents of [] onto [], causing it to reform its original shape[].", user, item, this, and_bright_shade)), span_notice(I18n.T("You pour the contents of [] onto [], causing it to reform its original shape[].", item, this, and_bright_shade)));
            float amount = Dm.num(((datum_reagents)item.reagents).get_reagent_amount(typeof(datum_reagent_medicine_mannitol)));
            float healto = Dm.num(Dm.max(0, Dm.num(damage) - amount * 2));
            await ((datum_reagents)item.reagents).remove_all(ROUND_UP(Dm.num(((datum_reagents)item.reagents).total_volume) / amount * (Dm.num(damage) - healto) * 0.5f)); //only removes however much solution is needed while also taking into account how much of the solution is mannitol
            await set_organ_damage(healto); //heals 2 damage per unit of mannitol, and by using "set_organ_damage", we clear the failing variable if that was up
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:172
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_borg_apparatus_organ_storage)))
            return NONE; //Borg organ bags shouldn't be killing brains

        if (Dm.truthy(await check_for_repair(tool, user)))
            return ITEM_INTERACT_SUCCESS;

        // Cutting out skill chips.
        if (!Dm.truthy(length(skillchips)) || !Dm.eq(tool.get_sharpness(), SHARP_EDGED))
            return NONE;

        to_chat(user, span_notice(I18n.T("You begin to excise skillchips from [].", this)));
        if (!Dm.truthy(await do_after(user, 15 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;

        foreach (object chip in Dm.each(skillchips))
        {
            if (!(chip is obj_item_skillchip))
            {
                stack_trace(Dm.text("Item of type [] qdel'd from [] skillchip list.", ((datum)chip).type, this), "code/modules/mob/living/brain/brain_item.dm", 189);
                qdel(chip);
                continue;
            }
            obj_item_skillchip skillchip = (obj_item_skillchip)chip;

            await remove_skillchip(skillchip);

            if (Dm.truthy(skillchip.removable))
            {
                await skillchip.forceMove(drop_location() as atom);
                continue;
            }

            qdel(skillchip);
        }

        skillchips = null;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:445
    public override async DmTask before_organ_replacement(obj_item_organ replacement = null)
    {
        await base.before_organ_replacement(replacement);
        obj_item_organ_brain replacement_brain = replacement as obj_item_organ_brain;
        if (replacement_brain == null)
            return;

        // Transfer over skillcips to the new brain

        // If we have some sort of brain type or subtype change and have skillchips, engage the failsafe procedure!
        if (owner != null && Dm.truthy(length(skillchips)) && replacement_brain.type != type)
            await activate_skillchip_failsafe();

        // Check through all our skillchips, remove them from this brain, add them to the replacement brain.
        foreach (object chip in Dm.each(skillchips))
        {
            obj_item_skillchip skillchip = (obj_item_skillchip)chip;

            // We're technically doing a little hackery here by bypassing the procs, but I'm the one who wrote them
            // and when you know the rules, you can break the rules.

            // Technically the owning mob is the same. We don't need to activate or deactivate the skillchips.
            // All the skillchips themselves care about is what brain they're in.
            // Because the new brain will ultimately be owned by the same body, we can safely leave skillchip logic alone.

            // Directly change the new holding_brain.
            skillchip.holding_brain = replacement_brain;
            //And move the actual obj into the new brain (contents)
            await skillchip.forceMove(replacement_brain);

            // Directly add them to the skillchip list in the new brain.
            LAZYADD(ref replacement_brain.skillchips, skillchip);
        }

        // Any skillchips has been transferred over, time to empty the list.
        LAZYCLEARLIST(skillchips);

        // Transfer over traumas as well
        foreach (datum_brain_trauma trauma in Dm.each_cast<datum_brain_trauma>(traumas))
        {
            remove_trauma_from_traumas(trauma);
            replacement_brain.add_trauma_to_traumas(trauma);
        }
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:236
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.length(skillchips)))
            Dm.append(ref dot, span_info(I18n.T("It has a skillchip embedded in it.")));
        Dm.append(ref dot, brain_damage_examine());
        if (Dm.truthy(smooth_brain))
            Dm.append(ref dot, span_notice(I18n.T("All the pesky wrinkles are gone. Now it just needs a good drying...")));
        if (brain_size < 1)
            Dm.append(ref dot, span_notice(I18n.T("It is a bit on the smaller side...")));
        if (brain_size > 1)
            Dm.append(ref dot, span_notice(I18n.T("It is bigger than average...")));
        return dot;
    }

    /// Needed so subtypes can override examine text while still calling parent
    // tg: code/modules/mob/living/brain/brain_item.dm:249
    public virtual object brain_damage_examine()
    {
        if (Dm.truthy(suicided))
            return span_info(I18n.T("It's started turning slightly grey. They must not have been able to handle the stress of it all."));
        if (brainmob != null && (Dm.truthy(decoy_override) || brainmob.client != null || Dm.truthy(brainmob.get_ghost())))
        {
            if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
                return span_info(I18n.T("It seems to still have a bit of energy within it, but it's rather damaged... You may be able to restore it with some <b>mannitol</b>."));
            else if (damage >= BRAIN_DAMAGE_DEATH * 0.5f)
                return span_info(I18n.T("You can feel the small spark of life still left in this one, but it's got some bruises. You may be able to restore it with some <b>mannitol</b>."));
            else
                return span_info(I18n.T("You can feel the small spark of life still left in this one."));
        }
        else
            return span_info(I18n.T("This one is completely devoid of life."));
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:375
    public virtual async DmTask roll_for_brain_trauma(object delta_dam = null)
    {
        if (prob(Dm.num(delta_dam) * (1 + max(0, (Dm.num(damage) - BRAIN_DAMAGE_MILD) / 100)))) //Base chance is the hit damage; for every point of damage past the threshold the chance is increased by 1% //learn how to do your bloody math properly goddamnit
            await gain_trauma_type(typeof(datum_brain_trauma_mild) /* BRAIN_TRAUMA_MILD, __DEFINES/mobs.dm:287 (a typepath define, inline) */, natural_gain: TRUE);

        float is_boosted = Dm.num(owner != null && HAS_TRAIT(this, TRAIT_SPECIAL_TRAUMA_BOOST));
        if (Dm.num(damage) < BRAIN_DAMAGE_SEVERE)
            return;
        if (prob(Dm.num(delta_dam) * (1 + max(0, (Dm.num(damage) - BRAIN_DAMAGE_SEVERE) / 100)))) //Base chance is the hit damage; for every point of damage past the threshold the chance is increased by 1%
        {
            if (prob(20 + (is_boosted * 30)))
                await gain_trauma_type(typeof(datum_brain_trauma_special) /* BRAIN_TRAUMA_SPECIAL */, (Dm.truthy(is_boosted) ? (object)TRAUMA_RESILIENCE_SURGERY : null), natural_gain: TRUE);
            else
                await gain_trauma_type(typeof(datum_brain_trauma_severe) /* BRAIN_TRAUMA_SEVERE */, natural_gain: TRUE);
        }
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:391
    public virtual void update_brain_color(float? animate = null)
    {
        animate ??= TRUE;
        // M6 slice 7b (D156): the colour filter and its transition reach the client (protocol versions 25 and 26)
        if (damage <= 0)
        {
            if (Dm.truthy(get_filter(BRAIN_DAMAGE_FILTER)))
            {
                if (Dm.truthy(animate))
                {
                    transition_filter(BRAIN_DAMAGE_FILTER, color_matrix_filter("#ffffff") as DmList, time: 1 * SECONDS);
                    _addtimer(Dm.@new<datum_callback>(this, nameof(remove_filter), "brain_damage_color_filter"), 1.2f * SECONDS, TIMER_UNIQUE, file: "code/modules/mob/living/brain/brain_item.dm", line: 396);
                }
                else
                    remove_filter(BRAIN_DAMAGE_FILTER);
            }
            return;
        }

        // var/gradient = rgb_gradient(round(damage / maxHealth, 0.01), 0, "#ffffff", 1, "#7f7f7f"): rgb_gradient(index, args...) is
        // BYOND's gradient(args, index) (__DEFINES/gradient.dm:2), which the shim does not have; written inline as BlendRGB does
        // (__HELPERS/icons.cs): a gradient that does not loop holds its end colours outside 0 to 1, and each channel goes linearly
        // from white to #7f7f7f between them (BYOND ref /proc/gradient).
        float index = Math.Clamp(round(Dm.num(damage) / Dm.num(maxHealth), 0.01f), 0f, 1f);
        int grey = (int)MathF.Round(255f + (127f - 255f) * index);
        string gradient = Dm.rgb(grey, grey, grey);
        if (Dm.truthy(animate))
        {
            if (!Dm.truthy(get_filter(BRAIN_DAMAGE_FILTER)))
                add_filter(BRAIN_DAMAGE_FILTER, 1, color_matrix_filter("#ffffff") as DmList);
            transition_filter(BRAIN_DAMAGE_FILTER, color_matrix_filter(gradient) as DmList, time: 1 * SECONDS);
        }
        else if (Dm.truthy(get_filter(BRAIN_DAMAGE_FILTER)))
            modify_filter(BRAIN_DAMAGE_FILTER, color_matrix_filter(gradient) as DmList);
        else
            add_filter(BRAIN_DAMAGE_FILTER, 1, color_matrix_filter(gradient) as DmList);
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:388  #define BRAIN_DAMAGE_FILTER "brain_damage_color_filter" (#undef at :411)
    private const string BRAIN_DAMAGE_FILTER = "brain_damage_color_filter";

    // tg: code/modules/mob/living/brain/brain_item.dm:606
    public virtual object has_trauma_type(Type brain_trauma_type = null, float? resilience = null)
    {
        brain_trauma_type ??= typeof(datum_brain_trauma);
        resilience ??= TRAUMA_RESILIENCE_ABSOLUTE;
        foreach (object X in Dm.each(traumas))
        {
            datum_brain_trauma BT = X as datum_brain_trauma;
            if (istype(BT, brain_trauma_type) && (Dm.num(BT.resilience) <= Dm.num(resilience)))
                return BT;
        }
        return null;
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:612
    public virtual object get_traumas_type(Type brain_trauma_type = null, float? resilience = null)
    {
        brain_trauma_type ??= typeof(datum_brain_trauma);
        resilience ??= TRAUMA_RESILIENCE_ABSOLUTE;
        DmList dot = null;
        dot = Dm.list();
        foreach (object X in Dm.each(traumas))
        {
            datum_brain_trauma BT = X as datum_brain_trauma;
            if (istype(BT, brain_trauma_type) && (Dm.num(BT.resilience) <= Dm.num(resilience)))
                Dm.append(ref dot, BT);
        }
        return dot;
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:619
    public virtual object can_gain_trauma(object trauma = null, object resilience = null, float? natural_gain = null)
    {
        natural_gain ??= FALSE;
        if (HAS_TRAIT(this, TRAIT_BRAIN_TRAUMA_IMMUNITY))
            return FALSE;

        if (!ispath(trauma))
            trauma = ((datum_brain_trauma)trauma).GetType();
        if (!Dm.truthy(Dm.initial((Type)trauma, (datum_brain_trauma p) => p.can_gain)))
            return FALSE;
        if (!Dm.truthy(resilience))
            resilience = Dm.initial((Type)trauma, (datum_brain_trauma p) => p.resilience);

        float resilience_tier_count = 0;
        foreach (object X in Dm.each(traumas))
        {
            if (istype(X, (Type)trauma))
                return FALSE;
            datum_brain_trauma T = X as datum_brain_trauma;
            if (Dm.eq(resilience, T.resilience))
                resilience_tier_count++;
        }

        object max_traumas = null;
        object sw__1 = resilience;
        if (Dm.eq(sw__1, TRAUMA_RESILIENCE_BASIC))
            max_traumas = TRAUMA_LIMIT_BASIC;
        else if (Dm.eq(sw__1, TRAUMA_RESILIENCE_SURGERY))
            max_traumas = TRAUMA_LIMIT_SURGERY;
        else if (Dm.eq(sw__1, TRAUMA_RESILIENCE_WOUND))
            max_traumas = TRAUMA_LIMIT_WOUND;
        else if (Dm.eq(sw__1, TRAUMA_RESILIENCE_LOBOTOMY))
            max_traumas = TRAUMA_LIMIT_LOBOTOMY;
        else if (Dm.eq(sw__1, TRAUMA_RESILIENCE_MAGIC))
            max_traumas = TRAUMA_LIMIT_MAGIC;
        else if (Dm.eq(sw__1, TRAUMA_RESILIENCE_ABSOLUTE))
            max_traumas = TRAUMA_LIMIT_ABSOLUTE;

        if (Dm.truthy(natural_gain) && resilience_tier_count >= Dm.num(max_traumas))
            return FALSE;
        return TRUE;
    }

    //Proc to use when directly adding a trauma to the brain, so extra args can be given
    // tg: code/modules/mob/living/brain/brain_item.dm:658
    public virtual async DmTask<object> gain_trauma(object trauma = null, object resilience = null, params object[] __extra)
    {
        object dot = null;
        DmList arguments = Dm.list();
        DmList args__1 = Dm.args(trauma, resilience, __extra);
        if (args__1.len > 2)
            arguments = args__1.Copy(3);
        dot = await brain_gain_trauma(trauma, resilience, arguments);
        return dot;
    }

    //Direct trauma gaining proc. Necessary to assign a trauma to its brain. Avoid using directly.
    // tg: code/modules/mob/living/brain/brain_item.dm:670
    public virtual async DmTask<object> brain_gain_trauma(object trauma = null, object resilience = null, DmList arguments = null)
    {
        if (!Dm.truthy(can_gain_trauma(trauma, resilience)))
            return null;

        datum_brain_trauma actual_trauma = null;
        if (ispath(trauma))
        {
            if (!Dm.truthy(LAZYLEN(arguments)))
                actual_trauma = Dm.@new((Type)trauma) as datum_brain_trauma; //arglist with an empty list runtimes for some reason
            else
                actual_trauma = Dm.@new((Type)trauma, Dm.arglist(arguments)) as datum_brain_trauma;
        }
        else
        {
            actual_trauma = trauma as datum_brain_trauma;
        }

        if (actual_trauma.brain != null) //we don't accept used traumas here
        {
            // WARNING(MSG) (__HELPERS/logging/_logging.dm:2), expanded inline (rules 11.1 (d), (f): src, usr, __FILE__, __LINE__)
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", "gain_trauma was given an already active trauma.", "code/modules/mob/living/brain/brain_item.dm", 684, this, usr));
            return null;
        }

        add_trauma_to_traumas(actual_trauma);
        if (owner != null)
        {
            actual_trauma.owner = owner;
            if (Dm.truthy(Dm.bitand((!Dm.truthy(owner._listen_lookup?[COMSIG_CARBON_GAIN_TRAUMA]) ? (object)NONE : owner._SendSignal(COMSIG_CARBON_GAIN_TRAUMA, Dm.list(owner, trauma, resilience))), COMSIG_CARBON_BLOCK_TRAUMA)))
            {
                qdel(actual_trauma);
                return null;
            }
            if (!Dm.truthy(await actual_trauma.on_gain()))
            {
                qdel(actual_trauma);
                return null;
            }
            // tg reads trauma.type, which is the typepath itself for a path (BYOND gives a path's type as the path)
            log_game(Dm.text("[] has gained the following brain trauma: []", key_name_and_tag(owner), ispath(trauma) ? trauma : trauma.GetType()));
        }
        if (Dm.truthy(resilience))
            actual_trauma.resilience = Dm.num_or_null(resilience);
        SSblackbox.record_feedback("tally", "traumas", 1, actual_trauma.GetType());
        return actual_trauma;
    }

    /// Adds the passed trauma instance to our list of traumas and links it to our brain.
    /// DOES NOT handle setting up the trauma, that's done by [proc/brain_gain_trauma]!
    // tg: code/modules/mob/living/brain/brain_item.dm:704
    public virtual void add_trauma_to_traumas(datum_brain_trauma trauma = null)
    {
        trauma.brain = this;
        Dm.append(ref traumas, trauma);
    }

    /// Removes the passed trauma instance to our list of traumas and links it to our brain
    /// DOES NOT handle removing the trauma's effects, that's done by [/datum/brain_trauma/Destroy()]!
    // tg: code/modules/mob/living/brain/brain_item.dm:710
    public virtual void remove_trauma_from_traumas(datum_brain_trauma trauma = null)
    {
        trauma.brain = null;
        Dm.remove(ref traumas, trauma);
    }

    //Add a random trauma of a certain subtype
    // tg: code/modules/mob/living/brain/brain_item.dm:715
    public virtual async DmTask<object> gain_trauma_type(Type brain_trauma_type = null, object resilience = null, float? natural_gain = null)
    {
        brain_trauma_type ??= typeof(datum_brain_trauma);
        natural_gain ??= FALSE;
        DmList possible_traumas = Dm.list();
        foreach (object T in Dm.each(subtypesof(brain_trauma_type)))
        {
            Type BT = (Type)T;
            if (Dm.truthy(can_gain_trauma(BT, resilience, natural_gain)) && Dm.truthy(Dm.initial(BT, (datum_brain_trauma p) => p.random_gain)))
                Dm.append(possible_traumas, BT);
        }

        if (!Dm.truthy(LAZYLEN(possible_traumas)))
            return null;

        object trauma_type = pick(possible_traumas);
        return await gain_trauma(trauma_type, resilience);
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:729
    public virtual void cure_trauma_type(Type brain_trauma_type = null, float? resilience = null)
    {
        brain_trauma_type ??= typeof(datum_brain_trauma);
        resilience ??= TRAUMA_RESILIENCE_BASIC;
        DmList traumas = get_traumas_type(brain_trauma_type, resilience) as DmList;
        if (Dm.truthy(LAZYLEN(traumas)))
            qdel(pick(traumas));
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:734
    public virtual object cure_all_traumas(float? resilience = null)
    {
        resilience ??= TRAUMA_RESILIENCE_BASIC;
        float amount_cured = 0;
        DmList traumas = get_traumas_type(resilience: resilience) as DmList;
        foreach (object X in Dm.each(traumas))
        {
            qdel(X);
            amount_cured++;
        }
        return amount_cured;
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:743
    public virtual object get_attacking_limb(mob_living_carbon_human target = null)
    {
        obj_item_bodypart_arm active_hand = owner.get_active_hand() as obj_item_bodypart_arm;
        if (HAS_TRAIT(owner, TRAIT_FERAL_BITER) || (HAS_TRAIT(owner, TRAIT_REFINED_BITER) && prob(50))) //Feral biters will always prefer biting. Refined biters pick a bite 50% of the time.
        {
            obj_item_bodypart_head found_head = owner.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
            return (object)found_head ?? active_hand; // If we are a biter, return a usable head.
        }
        if (target.pulledby == owner) // if we're grabbing our target we're beating them to death with our bare hands
            return active_hand;
        if (target.body_position == LYING_DOWN && Dm.truthy(owner.usable_legs))
        {
            obj_item_bodypart found_bodypart = owner.get_bodypart(IS_LEFT_INDEX(active_hand.held_index) ? BODY_ZONE_L_LEG : BODY_ZONE_R_LEG) as obj_item_bodypart;
            return (object)found_bodypart ?? active_hand;
        }
        return active_hand;
    }
}

// tg: code/modules/mob/living/brain/brain_item.dm:332 (interleaved types, rules 1.9)
public partial class obj_item_organ_brain : obj_item_organ
{
    // tg: code/modules/mob/living/brain/brain_item.dm:332
    public override object Destroy(float? force = null) //copypasted from MMIs.
    {
        QDEL_NULL(ref brainmob);
        QDEL_LIST(traumas);

        Waiting.NoSleep(destroy_all_skillchips());
        owner?.mind?.set_current(null); //You aren't allowed to return to brains that don't exist
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:340
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        if (HAS_TRAIT(this, TRAIT_BRAIN_DAMAGE_NODEATH))
            return;
        if (Dm.num(damage) >= BRAIN_DAMAGE_DEATH) //rip
        {
            to_chat(owner, span_userdanger(I18n.T("The last spark of life in your brain fizzles out...")));
            owner.investigate_log("has been killed by brain damage.", INVESTIGATE_DEATHS);
            await owner.death();
        }
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:350
    public override async DmTask on_bodypart_insert(obj_item_bodypart limb = null)
    {
        await base.on_bodypart_insert(limb);
        if (ishuman(limb.owner))
            limb.owner.update_hair();
        else
            await limb.update_icon_dropped();
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:357
    public override async DmTask on_bodypart_remove(obj_item_bodypart limb = null, object movement_flags = null)
    {
        await base.on_bodypart_remove(limb, movement_flags);
        if (ishuman(limb.owner))
            limb.owner.update_hair();
        else
            await limb.update_icon_dropped();
        // once it's out in the world we need to make sure it's the right color
        update_brain_color(animate: FALSE);
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:366
    public override async DmTask<object> apply_organ_damage(object damage_amount = null, object maximum = null, float? required_organ_flag = null)
    {
        maximum ??= maxHealth;
        required_organ_flag ??= NONE;
        object dot = null;
        dot = await base.apply_organ_damage(damage_amount, maximum, required_organ_flag);
        object delta_dam = dot; //for the sake of clarity
        if (bodypart_owner == null) // no need to color it if it's in someone's noggin
            update_brain_color();
        if (Dm.lt(delta_dam, 0) && Dm.num(damage) > BRAIN_DAMAGE_MILD)
            await roll_for_brain_trauma(Dm.neg(delta_dam)); // parent call returns negative numbers if take damage and positive if we heal
        return dot;
    }

    // tg: code/modules/mob/living/brain/brain_item.dm:413
    public override async DmTask<object> check_damage_thresholds()
    {
        object dot = null;
        dot = await base.check_damage_thresholds();
        if (owner == null)
            return dot;
        // If we crossed blinking brain damage thresholds either way, update our blinking
        if ((prev_damage >= BRAIN_DAMAGE_ASYNC_BLINKING && Dm.num(damage) < BRAIN_DAMAGE_ASYNC_BLINKING) || (prev_damage < BRAIN_DAMAGE_ASYNC_BLINKING && Dm.num(damage) >= BRAIN_DAMAGE_ASYNC_BLINKING))
        {
            obj_item_organ_eyes eyes = owner.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
            if (Dm.truthy(eyes?.blink_animation))
                eyes.animate_eyelids(owner as mob_living_carbon_human);
        }
        if (Dm.num(damage) >= 60 && prev_damage < 60)
            await owner.add_mood_event("brain_damage", typeof(datum_mood_event_brain_damage));
        else if (prev_damage >= 60 && Dm.num(damage) < 60)
            owner.clear_mood_event("brain_damage");

        // If we're not more injured than before, return without gambling for a trauma
        if (Dm.num(damage) <= prev_damage)
            return dot;

        // Conscious or soft-crit
        object brain_message = null;
        if (prev_damage < BRAIN_DAMAGE_MILD && Dm.num(damage) >= BRAIN_DAMAGE_MILD)
            brain_message = span_warning(I18n.T("You feel lightheaded."));
        else if (prev_damage < BRAIN_DAMAGE_SEVERE && Dm.num(damage) >= BRAIN_DAMAGE_SEVERE)
            brain_message = span_warning(I18n.T("You feel less in control of your thoughts."));
        else if (prev_damage < (BRAIN_DAMAGE_DEATH - 20) && Dm.num(damage) >= (BRAIN_DAMAGE_DEATH - 20))
            brain_message = span_warning(I18n.T("You can feel your mind flickering on and off..."));

        if (Dm.truthy(dot))
            Dm.append(ref dot, Dm.text("\n[]", brain_message));
        else
            return brain_message;
        return dot;
    }
}

// tg: code/modules/mob/living/brain/brain_item.dm:552 (gaps SP1: the felinid's brain)
public partial class obj_item_organ_brain_felinid : obj_item_organ_brain
{
    // Sometimes, felinids go a bit haywire and bite people. Based entirely on mania and hunger.
    // tg: code/modules/mob/living/brain/brain_item.dm:560
    public override object get_attacking_limb(mob_living_carbon_human target = null)
    {
        float starving_cat_bonus = Dm.num(owner.nutrition) <= NUTRITION_LEVEL_HUNGRY ? 1 : 10;
        float crazy_feral_cat = clamp(Dm.num(Dm.mul(starving_cat_bonus, owner.mob_mood?.sanity_level)), 0, 100);
        if (prob(crazy_feral_cat) || HAS_TRAIT(owner, TRAIT_FERAL_BITER))
            return owner.get_bodypart(BODY_ZONE_HEAD) is var head && Dm.truthy(head) ? head : base.get_attacking_limb(target);
        return base.get_attacking_limb(target);
    }
}

