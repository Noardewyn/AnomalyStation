// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (the reagent container base: initial reagents, the transfer amount, splashing, the fill
// overlay, the liquid sounds, refilling and draining). Deferred leaves: the admins' splash message (admin logs, M10). The
// fantasy bonuses are live-partial rows (M10). The var block is generated.

// tg: code/modules/reagents/reagent_containers.dm:1
public partial class obj_item_reagent_containers : obj_item
{
    // tg: code/modules/reagents/reagent_containers.dm:73
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object vol = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, vol));
        if (isnum(vol) && Dm.num(vol) > 0)
            volume = Dm.num(vol);
        if (!Dm.truthy(force))
            item_flags = Dm.num(Dm.bitor(item_flags, NOBLUDGEON));
        create_reagents(volume, initial_reagent_flags);
        if (spawned_disease != null)
        {
            datum_disease F = (datum_disease)Dm.@new(spawned_disease); // gaps VI1: the culture bottles' infected blood
            DmList data = Dm.list(Dm.kv("viruses", Dm.list(F)));
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_blood), disease_amount, data));
        }
        Waiting.NoSleep(add_initial_reagents());
        Waiting.NoSleep(AddElement(typeof(datum_element_reagents_exposed_on_fire)));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers.dm:88
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(has_variable_transfer_amount))
        {
            if (possible_transfer_amounts.len > 1)
                Dm.append(ref dot, span_notice(I18n.T("Left-click or right-click in-hand to increase or decrease its transfer amount. It is currently set to [] units.", amount_per_transfer_from_this)));
            else if (Dm.truthy(possible_transfer_amounts.len))
                Dm.append(ref dot, span_notice(I18n.T("Left-click or right-click in-hand to view its transfer amount.")));
        }

        if (isliving(user) && HAS_TRAIT(user, TRAIT_REMOTE_TASTING))
        {
            mob_living living_user = (mob_living)user;
            living_user.taste_container((datum_reagents)reagents);
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers.dm:100
    public override void create_reagents(object max_vol = null, object flags = null)
    {
        base.create_reagents(max_vol, flags);
        RegisterSignal((datum)reagents, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_reagent_change));
    }

    // tg: code/modules/reagents/reagent_containers.dm:104
    public virtual async DmTask add_initial_reagents()
    {
        if (Dm.truthy(list_reagents))
            await ((datum_reagents)reagents).add_reagent_list(list_reagents, added_purity: list_reagents_purity);
    }

    // tg: code/modules/reagents/reagent_containers.dm:108
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        // DEVIATION(DV27): an inert container (a soda can, a drinking glass, the CMO's hypospray) has no reagents: its Initialize did not run (D112)
        if (InertSeam.MissingInitialize(this, "attack_self", user))
            return TRUE;
        if (Dm.truthy(Dm.bitand(((datum_reagents)reagents).flags, SEALED_CONTAINER)))
            return TRUE;
        if (Dm.truthy(has_variable_transfer_amount))
        {
            change_transfer_amount(user, FORWARD);
            return TRUE;
        }
        return null;
    }

    // tg: code/modules/reagents/reagent_containers.dm:115
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        if (InertSeam.MissingInitialize(this, "attack_self_secondary", user)) // DEVIATION(DV27): as attack_self
            return TRUE;
        if (Dm.truthy(has_variable_transfer_amount))
            change_transfer_amount(user, BACKWARD);
        return null;
    }

    // tg: code/modules/reagents/reagent_containers.dm:119
    public virtual void mode_change_message(mob user = null)
    {
        return;
    }

    // tg: code/modules/reagents/reagent_containers.dm:122
    public virtual void change_transfer_amount(mob user = null, object direction = null)
    {
        direction ??= FORWARD;
        float list_len = Dm.length(possible_transfer_amounts);
        if (!Dm.truthy(list_len))
            return;
        float index = possible_transfer_amounts.Find(amount_per_transfer_from_this) is var or__1 && Dm.truthy(or__1) ? Dm.num(or__1) : 1;
        object sw__1 = direction;
        if (Dm.eq(sw__1, FORWARD))
            index = (index % list_len) + 1;
        else if (Dm.eq(sw__1, BACKWARD))
            index = index - 1 is var or__2 && Dm.truthy(or__2) ? or__2 : list_len;
        else
            throw Dm.CRASH("change_transfer_amount() called with invalid direction value");
        amount_per_transfer_from_this = Dm.num(possible_transfer_amounts[index]);
        balloon_alert(user, I18n.T("transferring []u", amount_per_transfer_from_this));
        mode_change_message(user);
    }

    // tg: code/modules/reagents/reagent_containers.dm:138
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(user.combat_mode))
            return NONE; // non-combat-mode-rmb allows for stuff like opening containers or attacking (bottle breaking)
        if (Dm.truthy(await try_splash(user, interacting_with)))
            return ITEM_INTERACT_SUCCESS;
        return NONE;
    }

    /// Tries to splash the target, called when right-clicking with a reagent container.
    // tg: code/modules/reagents/reagent_containers.dm:146
    public virtual async DmTask<object> try_splash(mob user = null, atom target = null)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(is_open_container()) || Dm.truthy(Dm.bitand(holder.flags, NO_SPLASH)))
            return FALSE;
        if (!Dm.truthy(holder?.total_volume))
            return FALSE;

        string punctuation = ismob(target) ? "!" : ".";

        user.changeNext_move(CLICK_CD_MELEE);
        user.visible_message(
            span_danger(I18n.T("[] splashes the contents of [] onto [][]", user, this, target, punctuation)),
            span_danger(I18n.T("You splash the contents of [] onto [][]", this, target, punctuation)),
            ignored_mobs: target
        );
        if (Dm.truthy(target._listen_lookup?[COMSIG_ATOM_SPLASHED]))
            target._SendSignal(COMSIG_ATOM_SPLASHED, Dm.list(target));

        if (ismob(target))
        {
            mob target_mob = (mob)target;
            target_mob.show_message(
                span_userdanger(I18n.T("[] splashes the contents of [] onto you!", user, this)),
                MSG_VISUAL,
                span_userdanger(I18n.T("You feel drenched!"))
            );
        }

        playsound(target, Dm.resource("sound/effects/slosh.ogg"), 25, TRUE);

        mutable_appearance splash_animation = (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "splash");
        if (isturf(target))
            splash_animation.icon_state = "splash_floor";
        splash_animation.color = mix_color_from_reagents(holder.reagent_list);
        target.flick_overlay_view(splash_animation, 1 * SECONDS);

        await holder.expose(target, TOUCH);
        log_combat(user, target, "splashed", holder.get_reagent_log_string());
        await holder.clear_reagents();
        return TRUE;
    }

    // tg: code/modules/reagents/reagent_containers.dm:182
    public virtual object canconsume(mob eater = null, mob user = null)
    {
        if (!iscarbon(eater))
            return FALSE;
        datum_reagents holder = (datum_reagents)reagents;
        if (holder == null || !Dm.truthy(holder.total_volume))
        {
            to_chat(user, span_warning(I18n.T("[] is empty!", this)));
            return FALSE;
        }

        mob_living_carbon as_carbon = (mob_living_carbon)eater;
        string covered = "";
        if (Dm.truthy(as_carbon.is_mouth_covered(ITEM_SLOT_HEAD)))
            covered = "headgear";
        else if (Dm.truthy(as_carbon.is_mouth_covered(ITEM_SLOT_MASK)))
            covered = "mask";
        if (Dm.truthy(covered))
        {
            object who = (user == null || eater == user) ? "your" : Dm.text("[]", eater.p_their());
            to_chat(user, span_warning(I18n.T("You have to remove [] [] first!", who, covered)));
            return FALSE;
        }
        return TRUE;
    }

    /// Sets reagent flags to the passed flags outright
    // tg: code/modules/reagents/reagent_containers.dm:203
    public virtual void update_container_flags(object new_flags = null)
    {
        ((datum_reagents)reagents).flags = new_flags;
    }

    /// Adds the passed flags to the current reagent flags
    // tg: code/modules/reagents/reagent_containers.dm:207
    public virtual void add_container_flags(object new_flags = null)
    {
        datum_reagents holder = (datum_reagents)reagents;
        holder.flags = Dm.bitor(holder.flags, new_flags);
    }

    /// Resets to base flags
    // tg: code/modules/reagents/reagent_containers.dm:211
    public virtual void reset_container_flags()
    {
        ((datum_reagents)reagents).flags = initial_reagent_flags;
    }

    /*
     * On accidental consumption, transfer a portion of the reagents to the eater and the item it's in, then continue to the base proc (to deal with shattering glass containers)
     */
    // tg: code/modules/reagents/reagent_containers.dm:217
    public override async DmTask<object> on_accidental_consumption(mob_living_carbon victim = null, mob_living_carbon user = null, obj_item source_item = null, float? discover_after = null)
    {
        discover_after ??= TRUE;
        victim.losebreath += 2;
        datum_reagents holder = (datum_reagents)reagents;
        if (holder != null)
            await holder.trans_to(victim, min(15, holder.total_volume / rand(5, 10)), transferred_by: user, methods: INGEST);
        if (source_item?.reagents != null)
            await holder.trans_to(source_item, min(((datum_reagents)source_item.reagents).total_volume / 2, holder.total_volume / 5), transferred_by: user, methods: TOUCH);
        return await base.on_accidental_consumption(victim, user, source_item, discover_after);
    }

    // tg: code/modules/reagents/reagent_containers.dm:224
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object do_splash = Dm.arg(__extra, 0) ?? TRUE;
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (Dm.truthy(do_splash))
            Waiting.NoSleep(splash_reagents(hit_atom, throwingdatum?.get_thrower() as mob, was_thrown: TRUE, allow_closed_splash: FALSE));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers.dm:229
    public virtual async DmTask<object> bartender_check(atom target = null, mob thrown_by = null)
    {
        object dot = FALSE;
        if (Dm.truthy(await target.CanPass(this, get_dir(target, this))) && thrown_by != null && HAS_TRAIT(thrown_by, TRAIT_BOOZE_SLIDER))
            dot = TRUE;
        return dot;
    }

    /**
     * Attempts to splash the reagents in the container onto the target.
     *
     * * target - The target to splash the reagents onto.
     * * throwingdatum - The throwingdatum behind the throw if the
     */
    // tg: code/modules/reagents/reagent_containers.dm:241
    public virtual async DmTask splash_reagents(atom target = null, mob splasher = null, float? was_thrown = null, float? allow_closed_splash = null)
    {
        was_thrown ??= FALSE;
        allow_closed_splash ??= FALSE;
        datum_reagents holder = (datum_reagents)reagents;
        if (holder == null || !Dm.truthy(holder.total_volume) || (!Dm.truthy(is_open_container()) && !Dm.truthy(allow_closed_splash)) || Dm.truthy(Dm.bitand(holder.flags, NO_SPLASH)))
            return;

        if (ismob(target) && target.reagents != null)
        {
            float splash_multiplier = 1;
            if (Dm.truthy(was_thrown))
                splash_multiplier *= (rand(5, 10) * 0.1f); //Not all of it makes contact with the target
            float turf_splash_multiplier = 1 - splash_multiplier;
            mob M = (mob)target;
            turf target_turf = get_turf(target);
            target.visible_message(span_danger(I18n.T("[] is splashed with something!", M)),
                            span_userdanger(I18n.T("[] is splashed with something!", M)));
            if (splasher != null)
                log_combat(splasher, M, "splashed", this, Dm.text("containing [] []", holder.get_reagent_log_string(), Dm.truthy(was_thrown) ? "(thrown)" : ""));
            await holder.expose(target, TOUCH, splash_multiplier);
            if (turf_splash_multiplier > 0)
                await holder.expose(target_turf, TOUCH, turf_splash_multiplier); // 1 - splash_multiplier because it's what didn't hit the target
        }
        else if (Dm.truthy(await bartender_check(target, splasher)) && Dm.truthy(was_thrown))
        {
            visible_message(span_notice(I18n.T("[] lands onto \\the [] without spilling a single drop.", this, target)));
            return;
        }
        else
        {
            if (isturf(target) && Dm.truthy(Dm.length(holder.reagent_list)) && splasher != null)
            {
                log_combat(splasher, target, Dm.text("splashed []", english_list(holder.reagent_list)), this, Dm.text("in [] []", AREACOORD(target), Dm.truthy(was_thrown) ? "(thrown)" : ""));
                message_admins(Dm.text("[] splashed (thrown) [] on [] in [].", ADMIN_LOOKUPFLW(splasher), english_list(((datum_reagents)reagents).reagent_list), target, ADMIN_VERBOSEJMP(target)));
            }
            visible_message(span_notice(I18n.T("[] spills its contents all over [].", this, target)));
            await holder.expose(target, TOUCH);
            if (QDELETED(this))
                return;
        }

        playsound(target, Dm.resource("sound/effects/slosh.ogg"), 25, TRUE);

        mutable_appearance splash_animation = (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "splash");
        if (isturf(target))
            splash_animation.icon_state = "splash_floor";
        splash_animation.color = mix_color_from_reagents(holder.reagent_list);
        target.flick_overlay_view(splash_animation, 1.0f * SECONDS);

        await holder.clear_reagents();
    }

    /// Updates the icon of the container when the reagents change. Eats signal args
    // tg: code/modules/reagents/reagent_containers.dm:280
    public virtual void on_reagent_change(datum holder = null, params object[] __extra)
    {
        update_appearance();
    }

    // tg: code/modules/reagents/reagent_containers.dm:284
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (fill_icon_thresholds == null)
            return dot;
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.total_volume))
            return dot;

        object fill_name = Dm.truthy(fill_icon_state) ? fill_icon_state : icon_state;
        mutable_appearance filling = (mutable_appearance)mutable_appearance(fill_icon, Dm.text("[][]", fill_name, fill_icon_thresholds[1]));

        float percent = round((holder.total_volume / Dm.num(volume)) * 100);
        for (float i = 1; i <= fill_icon_thresholds.len; i++)
        {
            float threshold = Dm.num(fill_icon_thresholds[i]);
            float threshold_end = (i == fill_icon_thresholds.len) ? INFINITY : Dm.num(fill_icon_thresholds[i + 1]);
            if (threshold <= percent && percent < threshold_end)
                filling.icon_state = Dm.text("[][]", fill_name, fill_icon_thresholds[i]);
        }

        if (!Dm.truthy(adjust_color_contrast))
        {
            filling.color = mix_color_from_reagents(holder.reagent_list);
            Dm.append(ref dot, filling);
            return dot;
        }

        DmList mix_colors = Dm.rgb2num(mix_color_from_reagents(holder.reagent_list) as string);
        //reagent color red
        float float_r = Dm.num(mix_colors[1]) / 255;
        //reagent color green
        float float_g = Dm.num(mix_colors[2]) / 255;
        //reagent color blue
        float float_b = Dm.num(mix_colors[3]) / 255;
        //reagent color alpha
        float float_a = mix_colors.len > 3 ? Dm.num(mix_colors[4]) / 255 : 1;
        //value, used to make modifications depending on if our reagent color is light or dark.
        float float_v = (float_r + float_g + float_b) / 3;
        //max result of float_b - float_v is 0.6666 so we multiply with 1.5 to get something close to 1 at max blueness.
        float blue_mod = max(float_b - float_v, 0) * 1.5f;
        //red multiplier
        float red_scale = 1.6f;
        //green_multiplier
        float green_scale = 1.5f;
        //blue scale
        float blue_scale = 1.1f * (1 + 0.60f * blue_mod);
        //additive red - modifies red across the board by val * 255
        float red_base = -0.07f - (0.035f * float_v);
        //additive green - modifies green across the board by val * 255
        float green_base = -0.06f - (0.03f * float_v);
        //additive blue - modifies blue across the board by val * 255
        float blue_base = 0.10f - (0.050f * float_v) - (0.40f * blue_mod);

        DmList reagent_color_and_contrast_matrix = Dm.list(
            //Red - RR, RG, RB, RA
            float_r * red_scale, 0, 0, 0,
            //Green - GR - GG - GB - GA
            0, float_g * green_scale, 0, 0,
            ///Blue - BR, BG, BB, BA
            0.25f * blue_mod, 0.33f * blue_mod, float_b * blue_scale, 0,
            //Alpha - AR, AG, AB, AA
            0, 0, 0, float_a,
            //Constant - CR, CG, CB, CA
            red_base, green_base, blue_base, 0);
        filling.color = reagent_color_and_contrast_matrix;
        Dm.append(ref dot, filling);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers.dm:350
    public virtual object reagent_container_sound_chain(object filled_sound = null, object empty_sound = null, object target = null, object volume = null)
    {
        datum_reagents holder = (datum_reagents)reagents;
        atom target_atom = target as atom;
        // an inert container (a type whose Initialize is not translated) never made its holder: it sounds as an empty one
        if (holder == null || holder.total_volume <= round((holder.maximum_volume * 0.2f), 1))
        {
            if (Dm.truthy(empty_sound))
            {
                playsound(target_atom, empty_sound, volume, vary: sound_vary, ignore_walls: FALSE);
                return TRUE;
            }
            return FALSE;
        }

        if (Dm.truthy(reagent_container_liquid_sound))
            playsound(target_atom, reagent_container_liquid_sound, LIQUID_SLOSHING_SOUND_VOLUME, vary: TRUE, ignore_walls: FALSE);

        if (Dm.truthy(filled_sound))
        {
            playsound(target_atom, filled_sound, volume, vary: sound_vary, ignore_walls: FALSE);
            return TRUE;
        }

        if (Dm.truthy(empty_sound))
        {
            playsound(target_atom, empty_sound, volume, vary: sound_vary, ignore_walls: FALSE);
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/reagents/reagent_containers.dm:372
    public override object play_pickup_sound(float? volume = null)
    {
        volume ??= PICKUP_SOUND_VOLUME;
        return reagent_container_sound_chain(filled_pickup_sound, pickup_sound, this, volume);
    }

    // tg: code/modules/reagents/reagent_containers.dm:375
    public override object play_drop_sound(float? volume = null)
    {
        volume ??= DROP_SOUND_VOLUME;
        return reagent_container_sound_chain(filled_drop_sound, drop_sound, this, volume);
    }

    // tg: code/modules/reagents/reagent_containers.dm:378
    public override object play_throw_drop_sound(float? volume = null)
    {
        volume ??= YEET_SOUND_VOLUME;
        return reagent_container_sound_chain(filled_throw_drop_sound, throw_drop_sound, this, volume);
    }

    // tg: code/modules/reagents/reagent_containers.dm:381
    public override object play_mob_throw_hit_sound(object target = null, float? volume = null)
    {
        volume ??= DROP_SOUND_VOLUME;
        return reagent_container_sound_chain(filled_throw_hit_sound, mob_throw_hit_sound, target, volume);
    }

    // tg: code/modules/reagents/reagent_containers.dm:384
    public override object play_hit_sound(object target = null, float? volume = null)
    {
        volume ??= HALFWAY_SOUND_VOLUME;
        return reagent_container_sound_chain(filled_hitsound, filled_hitsound, target, volume);
    }

    // tg: code/modules/reagents/reagent_containers.dm:387
    public override object play_equip_sound(float? volume = null)
    {
        volume ??= EQUIP_SOUND_VOLUME;
        return reagent_container_sound_chain(filled_equip_sound, equip_sound, this, volume);
    }

    // tg: code/modules/reagents/reagent_containers.dm:390
    public override async DmTask used_in_craft(atom result = null, datum_crafting_recipe current_recipe = null)
    {
        await base.used_in_craft(result, current_recipe);
        // If consumed in crafting, we should dump contents out before qdeling them.
        if (!Dm.truthy(is_type_in_list(this, current_recipe.parts)))
            await ((datum_reagents)reagents).expose(loc, TOUCH);
    }

    // tg: code/modules/reagents/reagent_containers.dm:396
    public virtual async DmTask<object> try_refill(atom target = null, mob_living user = null)
    {
        datum_reagents holder = (datum_reagents)reagents;
        datum_reagents target_holder = (datum_reagents)target.reagents;
        if (!Dm.truthy(holder.total_volume))
        {
            to_chat(user, span_warning(I18n.T("[] is empty!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(target_holder.holder_full()))
        {
            to_chat(user, span_warning(I18n.T("[] is full.", target)));
            return ITEM_INTERACT_BLOCKING;
        }

        float trans = round(Dm.num(await holder.trans_to(target, amount_per_transfer_from_this, transferred_by: user)), CHEMICAL_VOLUME_ROUNDING);
        playsound(target.loc, SFX_LIQUID_POUR, 50, TRUE);
        if (Dm.truthy(trans))
            to_chat(user, span_notice(I18n.T("You transfer [] unit\\s of the solution to [].", trans, target)));
        if (Dm.truthy(_listen_lookup?[COMSIG_REAGENTS_CUP_TRANSFER_TO]))
            _SendSignal(COMSIG_REAGENTS_CUP_TRANSFER_TO, Dm.list(this, target));
        target.update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers.dm:413
    public virtual async DmTask<object> try_drain(atom target = null, mob_living user = null)
    {
        datum_reagents holder = (datum_reagents)reagents;
        datum_reagents target_holder = (datum_reagents)target.reagents;
        if (!Dm.truthy(target_holder.total_volume))
        {
            to_chat(user, span_warning(I18n.T("[] is empty and can't be refilled!", target)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(holder.holder_full()))
        {
            to_chat(user, span_warning(I18n.T("[] is full.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        float trans = round(Dm.num(await target_holder.trans_to(this, amount_per_transfer_from_this, transferred_by: user)), CHEMICAL_VOLUME_ROUNDING);
        playsound(target.loc, SFX_LIQUID_POUR, 50, TRUE);
        to_chat(user, span_notice(I18n.T("You fill [] with [] unit\\s of the contents of [].", this, trans, target)));
        if (Dm.truthy(_listen_lookup?[COMSIG_REAGENTS_CUP_TRANSFER_FROM]))
            _SendSignal(COMSIG_REAGENTS_CUP_TRANSFER_FROM, Dm.list(this, target));
        target.update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers.dm:429
    public override object is_chem_container()
    {
        return (is_open_container() is var and__1 && !Dm.truthy(and__1) ? and__1 : (object)(!Dm.truthy(Dm.bitand(item_flags, ABSTRACT)) && !Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1))));
    }
}
