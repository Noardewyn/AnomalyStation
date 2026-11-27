// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/perceptomatrix.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the perceptomatrix helm. Without a hallucination core it is a blind (infinite tint) helmet that refuses its
// wearer's psychic spells; a core (three seconds to insert) clears the tint, gives the wearer its perception and mind protection
// traits, heavy ear protection, mind anti-magic and its pink screen colour, and lets them cast its Hallucinate spell: a pink
// lightning beam and quantum sparks that blur, stagger and make a human hallucinate (or blow up pancakes). The fishing difficulty
// element waits for fishing. The var blocks are generated (D111).

// tg: code/modules/clothing/head/perceptomatrix.dm:2
public partial class obj_item_clothing_head_helmet_perceptomatrix : obj_item_clothing_head_helmet
{
    // tg: code/modules/clothing/head/perceptomatrix.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_ICON_STATE);
        Waiting.NoSleep(update_anomaly_state());
        Dm.Deferred("code/modules/clothing/head/perceptomatrix.dm:73", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -7), the fishing difficulty (fishing, M10+)");
        Waiting.NoSleep(AddComponent(typeof(datum_component_hat_stabilizer), Dm.named("loose_hat", TRUE)));
        return dot;
    }

    // tg: code/modules/clothing/head/perceptomatrix.dm:76
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HEAD)))
            RegisterSignal(user, COMSIG_MOB_BEFORE_SPELL_CAST, nameof(pre_cast_core_check));
    }

    // tg: code/modules/clothing/head/perceptomatrix.dm:81
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        UnregisterSignal(user, COMSIG_MOB_BEFORE_SPELL_CAST);
        await base.dropped(user, silent, __extra);
        return null;
    }

    // Prevent casting the spell w/o the core.
    // tg: code/modules/clothing/head/perceptomatrix.dm:86
    [SignalHandler]
    public virtual object pre_cast_core_check(mob caster = null, datum_action_cooldown_spell spell = null)
    {
        if (!Dm.truthy(core_installed) && Dm.eq(spell.school, SCHOOL_PSYCHIC))
        {
            to_chat(caster, span_warning(I18n.T("You can't zap minds through []'s shielding without a core installed!", this)));
            return SPELL_CANCEL_CAST;
        }
        return null;
    }

    // tg: code/modules/clothing/head/perceptomatrix.dm:92
    public virtual async DmTask update_anomaly_state()
    {
        // If the core isn't installed, or it's temporarily deactivated, disable special functions.
        if (!Dm.truthy(core_installed))
        {
            REMOVE_TRAIT(this, TRAIT_CASTING_CLOTHING, INNATE_TRAIT);
            detach_clothing_traits(additional_clothing_traits);
            QDEL_LIST(active_components);
            RemoveElement(typeof(datum_element_wearable_client_colour), typeof(datum_client_colour_perceptomatrix), ITEM_SLOT_HEAD, HELMET_TRAIT, Dm.named("forced", TRUE));
            tint = INFINITY;
            mob_living_carbon carbon_loc = astype(loc, typeof(mob_living_carbon)) as mob_living_carbon;
            if (carbon_loc != null)
                await carbon_loc.update_tint();
            return;
        }

        ADD_TRAIT(this, TRAIT_CASTING_CLOTHING, INNATE_TRAIT);
        attach_clothing_traits(additional_clothing_traits);
        tint = 0;
        mob_living_carbon wearer = astype(loc, typeof(mob_living_carbon)) as mob_living_carbon;
        if (wearer != null)
            await wearer.update_tint();

        // When someone makes TRAIT_DEAF an element, or status effect, or whatever, give this item a way to bypass said deafness.
        // just blocking future instances of deafness isn't what the item is meant to do but there's no proper way to do it otherwise at the moment.
        Dm.append(ref active_components, await AddComponent(typeof(datum_component_wearertargeting_earprotection), EAR_PROTECTION_HEAVY)); // should be same as highest value
        Dm.append(ref active_components, await AddComponent(
            typeof(datum_component_anti_magic),
            Dm.named("antimagic_flags", MAGIC_RESISTANCE_MIND),
            Dm.named("inventory_flags", ITEM_SLOT_HEAD)
        ));
        await AddElement(typeof(datum_element_wearable_client_colour), typeof(datum_client_colour_perceptomatrix), ITEM_SLOT_HEAD, HELMET_TRAIT, Dm.named("forced", TRUE));

        await update_icon_state();
    }

    // tg: code/modules/clothing/head/perceptomatrix.dm:121
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(active_components);
        return base.Destroy(force);
    }

    // tg: code/modules/clothing/head/perceptomatrix.dm:125
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(core_installed))
            Dm.append(ref dot, span_warning(I18n.T("It requires a hallucination anomaly core in order to function.")));
        return dot;
    }

    // tg: code/modules/clothing/head/perceptomatrix.dm:130
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = base_icon_state + (Dm.truthy(core_installed) ? "" : "_inactive");
        worn_icon_state = base_icon_state + (Dm.truthy(core_installed) ? "" : "_inactive");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/clothing/head/perceptomatrix.dm:135
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item weapon = tool;
        if (!istype(weapon, typeof(obj_item_assembly_signaler_anomaly_hallucination)))
            return NONE;
        balloon_alert(user, "inserting...");
        if (!Dm.truthy(await do_after(user, delay: 3 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;
        qdel(weapon);
        core_installed = TRUE;
        await update_anomaly_state();
        update_appearance(UPDATE_ICON_STATE);
        playsound(this, "sound/machines/crate/crate_open.ogg", 50, FALSE);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/clothing/head/perceptomatrix.dm:151
public partial class datum_action_cooldown_spell_pointed_percept_hallucination : datum_action_cooldown_spell_pointed
{
    // tg: code/modules/clothing/head/perceptomatrix.dm:174
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        object dot = await base.is_valid_target(cast_on);
        if (!Dm.truthy(dot))
            return FALSE;
        if (ishuman(cast_on))
            return TRUE;
        if (istype(cast_on, typeof(obj_item_food_pancakes)))
            return TRUE;

        return FALSE;
    }

    // tg: code/modules/clothing/head/perceptomatrix.dm:185
    public virtual async DmTask blows_up_pancakes_with_mind(obj_item_food_pancakes pancakes = null)
    {
        owner.visible_message(
            span_userdanger(I18n.T("[] blows up [] with [] mind!", owner, pancakes, owner.p_their())),
            span_userdanger(I18n.T("You blow up [] with your mind!", pancakes))
        );

        foreach (mob chef in Dm.each_cast<mob>(get_hearers_in_view(7, pancakes)))
        {
            if (chef.mind == null)
                continue;
            // if cooked by chef, or if EITHER 5% chance OR its april fools. a || (b || c)
            if (HAS_TRAIT_FROM(pancakes, TRAIT_HANDMADE, REF(chef.mind)) || (prob(5) || Dm.truthy(check_holidays(APRIL_FOOLS))))
                await chef.say("Ma fuckin' pancakes!");
        }

        playsound(pancakes, "sound/effects/fuse.ogg", 80);
        animate(pancakes, Dm.named("time", 1), Dm.named("pixel_z", 12), Dm.named("easing", ELASTIC_EASING));
        animate(Dm.named("time", 1), Dm.named("pixel_z", 0), Dm.named("easing", BOUNCE_EASING));
        for (float i = 1; i <= 15; i++)
            animate(Dm.named("color", Dm.truthy(i % 2) ? "#ffffff" : "#ff6739"), Dm.named("time", 1), Dm.named("easing", QUAD_EASING), Dm.named("flags", ANIMATION_CONTINUE));

        _addtimer(Dm.@new<datum_callback>(this, nameof(pancake_explosion), pancakes), 1.5f * SECONDS, file: "code/modules/clothing/head/perceptomatrix.dm", line: 205);
    }

    // tg: code/modules/clothing/head/perceptomatrix.dm:207
    public virtual async DmTask pancake_explosion(obj pancakes = null)
    {
        await explosion(pancakes, devastation_range: -1, heavy_impact_range: -1, light_impact_range: 1, flame_range: 2);
        qdel(pancakes);
        StartCooldown();
    }

    // tg: code/modules/clothing/head/perceptomatrix.dm:212
    public virtual void cast_fx(atom cast_on = null)
    {
        owner.Beam(cast_on, icon_state: "greyscale_lightning", beam_color: COLOR_FADED_PINK, time: 0.5f * SECONDS);
        do_sparks(2, TRUE, get_turf(owner), spark_type: typeof(datum_effect_system_basic_spark_spread_quantum));
        do_sparks(4, TRUE, get_turf(owner), spark_type: typeof(datum_effect_system_basic_spark_spread_quantum));
    }

    // tg: code/modules/clothing/head/perceptomatrix.dm:217
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = await base.cast(cast_on);

        cast_fx(cast_on);

        if (istype(cast_on, typeof(obj_item_food_pancakes)))
        {
            await blows_up_pancakes_with_mind(cast_on as obj_item_food_pancakes);
            return dot;
        }

        mob_living_carbon_human target = cast_on as mob_living_carbon_human;
        if (Dm.truthy(target.can_block_magic(antimagic_flags)))
        {
            to_chat(target, span_notice(I18n.T("You feel psychic energies reflecting off you.")));
            to_chat(owner, span_warning(I18n.T("[] deflects the energy!", target)));
            return dot;
        }

        to_chat(target, span_warning(I18n.T("Your brain feels like it's on fire!")));
        await target.emote("scream");
        await target.set_eye_blur_if_lower(eye_blur_duration);
        await target.adjust_staggered(stagger_duration);
        await target.apply_status_effect(typeof(datum_status_effect_hallucination_perceptomatrix), hallucination_duration, HALLUCINATION_TIER_RARE);
        return dot;
    }
}
