// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/syringes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (injecting and drawing, the blood sample, the fill states and overlay, the syringe's
// embedding with its slow injection, the station subtypes). Deferred leaves: the self-injection's attack log (game logs,
// M10) and the foam dart insert (the toy foam darts, later). The var blocks are generated.

// tg: code/modules/reagents/reagent_containers/syringes.dm:1
public partial class obj_item_reagent_containers_syringe : obj_item_reagent_containers
{
    // tg: code/modules/reagents/reagent_containers/syringes.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        // AddComponent(/datum/component/dart_insert, dart_insert_icon, dart_insert_casing_icon_state, dart_insert_icon,
        // dart_insert_projectile_icon_state, CALLBACK(src, PROC_REF(get_dart_var_modifiers)))
        Dm.Deferred("code/modules/reagents/reagent_containers/syringes.dm:29", "Initialize: AddComponent(/datum/component/dart_insert): a syringe loaded into a toy foam dart (toys, later)");
        RegisterSignal(this, COMSIG_ITEM_IN_UNWRAPPED_TRAITOR_MAIL, nameof(on_mail_unwrap));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/syringes.dm:38
    public virtual object try_syringe(atom target = null, mob user = null)
    {
        if (target.reagents == null)
            return FALSE;
        if (isliving(target))
        {
            mob_living living_target = (mob_living)target;
            if (!Dm.truthy(living_target.try_inject(user, injection_flags: Dm.bitor(INJECT_TRY_SHOW_ERROR_MESSAGE, inject_flags))))
                return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/reagents/reagent_containers/syringes.dm:48
    public override async DmTask<object> interact_with_atom(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        if (target.reagents == null)
            return NONE;
        if (!Dm.truthy(try_syringe(target, user)))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(target._listen_lookup?[COMSIG_LIVING_TRY_SYRINGE_INJECT]))
            target._SendSignal(COMSIG_LIVING_TRY_SYRINGE_INJECT, Dm.list(target, user));

        datum_reagents holder = (datum_reagents)reagents;
        datum_reagents target_holder = (datum_reagents)target.reagents;
        object contained = holder.get_reagent_log_string();
        log_combat(user, target, "attempted to inject", this, addition: Dm.text("which had []", contained));

        if (!Dm.truthy(holder.total_volume))
        {
            to_chat(user, span_warning(I18n.T("[] is empty! Right-click to draw.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!isliving(target) && !Dm.truthy(target.is_injectable(user)))
        {
            to_chat(user, span_warning(I18n.T("You cannot directly fill []!", target)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(target_holder.holder_full()))
        {
            to_chat(user, span_notice(I18n.T("[] is full.", target)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (isliving(target))
        {
            mob_living living_target = (mob_living)target;
            if (living_target != user)
            {
                living_target.visible_message(
                    span_danger(I18n.T("[] is trying to inject []!", user, living_target)),
                    span_userdanger(I18n.T("[] is trying to inject you!", user))
                );
                if (!Dm.truthy(await do_after(user, CHEM_INTERACT_DELAY(3 * SECONDS, user), living_target, extra_checks: Dm.@new<datum_callback>(this, nameof(try_syringe), living_target, user))))
                    return ITEM_INTERACT_BLOCKING;
                if (!Dm.truthy(holder.total_volume))
                    return ITEM_INTERACT_BLOCKING;
                if (Dm.truthy(((datum_reagents)living_target.reagents).holder_full()))
                    return ITEM_INTERACT_BLOCKING;
                living_target.visible_message(
                    span_danger(I18n.T("[] injects [] with the syringe!", user, living_target)),
                    span_userdanger(I18n.T("[] injects you with the syringe!", user))
                );
            }

            if (living_target == user)
            {
                living_target.log_message(Dm.text("injected themselves ([]) with []", contained, name), LOG_ATTACK, color: "orange");
            }
            else
                log_combat(user, living_target, "injected", this, addition: Dm.text("which had []", contained));
        }

        if (Dm.truthy(await holder.trans_to(target, amount_per_transfer_from_this, transferred_by: user, methods: INJECT)))
        {
            to_chat(user, span_notice(I18n.T("You inject [] units of the solution. The syringe now contains [] units.", amount_per_transfer_from_this, holder.total_volume)));
            target.update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/reagents/reagent_containers/syringes.dm:96
    public override async DmTask<object> interact_with_atom_secondary(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        if (target.reagents == null)
            return NONE;
        if (!Dm.truthy(try_syringe(target, user)))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(target._listen_lookup?[COMSIG_LIVING_TRY_SYRINGE_WITHDRAW]))
            target._SendSignal(COMSIG_LIVING_TRY_SYRINGE_WITHDRAW, Dm.list(target, user));

        datum_reagents holder = (datum_reagents)reagents;
        if (Dm.truthy(holder.holder_full()))
        {
            to_chat(user, span_notice(I18n.T("[] is full.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (isliving(target))
        {
            mob_living living_target = (mob_living)target;
            float drawn_amount = holder.maximum_volume - holder.total_volume;
            if (target != user)
            {
                target.visible_message(
                    span_danger(I18n.T("[] is trying to take a blood sample from []!", user, target)),
                    span_userdanger(I18n.T("[] is trying to take a blood sample from you!", user))
                );
                if (!Dm.truthy(await do_after(user, CHEM_INTERACT_DELAY(3 * SECONDS, user), target, extra_checks: Dm.@new<datum_callback>(this, nameof(try_syringe), living_target, user))))
                    return ITEM_INTERACT_BLOCKING;
                if (Dm.truthy(holder.holder_full()))
                    return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(await living_target.transfer_blood_to(this, drawn_amount)))
                user.visible_message(span_notice(I18n.T("[] takes a blood sample from [].", user, living_target)));
            else
                to_chat(user, span_warning(I18n.T("You are unable to draw any blood from []!", living_target)));
            return ITEM_INTERACT_SUCCESS;
        }

        datum_reagents target_holder = (datum_reagents)target.reagents;
        if (!Dm.truthy(target_holder.total_volume))
        {
            to_chat(user, span_warning(I18n.T("[] is empty!", target)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(target.is_drawable(user)))
        {
            to_chat(user, span_warning(I18n.T("You cannot directly remove reagents from []!", target)));
            return ITEM_INTERACT_BLOCKING;
        }

        object trans = await target_holder.trans_to(this, amount_per_transfer_from_this, transferred_by: user); // transfer from, transfer to - who cares?
        if (Dm.truthy(trans))
            to_chat(user, span_notice(I18n.T("You fill [] with [] units of the solution. It now contains [] units.", this, trans, holder.total_volume)));
        target.update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    /*
     * On accidental consumption, inject the eater with 2/3rd of the syringe and reveal it
     */
    // tg: code/modules/reagents/reagent_containers/syringes.dm:146
    public override async DmTask<object> on_accidental_consumption(mob_living_carbon victim = null, mob_living_carbon user = null, obj_item source_item = null, float? discover_after = null)
    {
        discover_after ??= TRUE;
        if (source_item != null)
            to_chat(victim, span_boldwarning(I18n.T("There's \\a [] in []!!", this, source_item)));
        else
            to_chat(victim, span_boldwarning(I18n.T("[] injects you!", this)));

        await victim.apply_damage(5, BRUTE, BODY_ZONE_HEAD);
        if (reagents is datum_reagents holder)
            await holder.trans_to(victim, round(holder.total_volume * (2f / 3)), transferred_by: user, methods: INJECT);

        return discover_after;
    }

    // tg: code/modules/reagents/reagent_containers/syringes.dm:157
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object rounded_vol = get_rounded_vol();
        inhand_icon_state = Dm.text("[]_[]", base_icon_state, rounded_vol);
        icon_state = (string)inhand_icon_state;
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/reagents/reagent_containers/syringes.dm:162
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        object reagent_overlays = update_reagent_overlay();
        if (Dm.truthy(reagent_overlays))
            Dm.append(ref dot, reagent_overlays);
        return dot;
    }

    /// Returns a list of overlays to add that relate to the reagents inside the syringe
    // tg: code/modules/reagents/reagent_containers/syringes.dm:169
    public virtual object update_reagent_overlay()
    {
        object dot = null;
        if (Dm.truthy((reagents as datum_reagents)?.total_volume))
        {
            mutable_appearance filling_overlay = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/medical/reagent_fillings.dmi"), Dm.text("syringe[]", get_rounded_vol()));
            filling_overlay.color = mix_color_from_reagents(((datum_reagents)reagents).reagent_list);
            Dm.append(ref dot, filling_overlay);
        }
        return dot;
    }

    ///Used by update_appearance() and update_overlays()
    // tg: code/modules/reagents/reagent_containers/syringes.dm:176
    public virtual object get_rounded_vol()
    {
        datum_reagents holder = reagents as datum_reagents;
        if (!Dm.truthy(holder?.total_volume))
            return 0;
        return clamp(round((holder.total_volume / Dm.num(volume) * 15), 5), 1, 15);
    }

    // tg: code/modules/reagents/reagent_containers/syringes.dm:181
    public virtual async DmTask<object> get_dart_var_modifiers(obj_projectile projectile = null)
    {
        datum_embedding embed_data = (datum_embedding)((datum_embedding)get_embed()).create_copy();
        embed_data.rip_time += Dm.num((projectile.get_embed() as datum_embedding)?.rip_time);
        return Dm.list(
            Dm.kv("damage", max(6, Dm.num(volume) / 5)), // Scales with size?
            Dm.kv("speed", max(0, Dm.num(throw_speed) - 3)),
            Dm.kv("embedding", embed_data),
            Dm.kv("armour_penetration", armour_penetration),
            Dm.kv("wound_bonus", wound_bonus),
            Dm.kv("exposed_wound_bonus", exposed_wound_bonus),
            Dm.kv("demolition_mod", demolition_mod)
        );
    }

    // tg: code/modules/reagents/reagent_containers/syringes.dm:194
    [SignalHandler]
    public virtual object on_mail_unwrap(atom source = null, mob_living user = null, obj_item letter = null)
    {
        if (!Dm.truthy(((datum_reagents)reagents).total_volume) || user.reagents == null || !Dm.truthy(user.try_inject(user, user.get_active_hand())))
            return null;
        to_chat(user, span_danger(I18n.T("As you open [], you prick yourself on a syringe inside!", letter)));
        Waiting.NoSleep(((datum_reagents)reagents).trans_to(user, min(((datum_reagents)reagents).total_volume, 5)));
        Waiting.NoSleep(forceMove(user.loc));
        return COMPONENT_TRAITOR_MAIL_HANDLED;
    }
}

// tg: code/modules/reagents/reagent_containers/syringes.dm:203
public partial class datum_embedding_syringe : datum_embedding
{
    // tg: code/modules/reagents/reagent_containers/syringes.dm:214
    public override async DmTask<object> process_effect(object seconds_per_tick = null)
    {
        obj_item_reagent_containers syringe = parent as obj_item_reagent_containers;
        if (syringe == null)
        {
            syringe = Dm.locate<obj_item_reagent_containers>(parent);
            if (syringe == null && isammocasing(parent))
            {
                obj_item_ammo_casing casing = (obj_item_ammo_casing)parent;
                syringe = Dm.locate<obj_item_reagent_containers>(casing.loaded_projectile);
            }
            if (syringe == null)
                return null;
        }

        if (!Dm.truthy(IS_ORGANIC_LIMB(owner_limb)))
            return null;

        if (owner.reagents == null || !Dm.truthy(((datum_reagents)syringe.reagents).total_volume))
            return null;

        await ((datum_reagents)syringe.reagents).trans_to(owner, transfer_per_second * Dm.num(seconds_per_tick), methods: INJECT, show_message: SPT_PROB(15, seconds_per_tick) ? TRUE : FALSE);
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/syringes.dm:233
    public override void setup_shrapnel(obj_projectile source = null, mob_living_carbon victim = null)
    {
        if (!istype(source, typeof(obj_projectile_bullet_dart_syringe)))
        {
            base.setup_shrapnel(source, victim);
            return;
        }

        obj_projectile_bullet_dart_syringe syringe_dart = (obj_projectile_bullet_dart_syringe)source;
        obj_item_reagent_containers_syringe syringe = syringe_dart.inner_syringe;
        if (syringe == null)
        {
            base.setup_shrapnel(source, victim);
            return;
        }

        syringe_dart.inner_syringe = null;
        source.set_embed(null, dont_delete: TRUE);
        register_on(syringe);
        syringe.set_embed(this);
    }

    // tg: code/modules/reagents/reagent_containers/syringes.dm:248
    public override async DmTask fall_out()
    {
        await base.fall_out();
        embed_chance = Dm.num(Dm.initial(this, (datum_embedding_syringe p) => p.embed_chance));
    }
}

// tg: code/modules/reagents/reagent_containers/syringes.dm:363
public partial class obj_item_reagent_containers_syringe_crude : obj_item_reagent_containers_syringe
{
    // tg: code/modules/reagents/reagent_containers/syringes.dm:385
    public override object update_reagent_overlay()
    {
        return null;
    }
}

// tg: code/modules/reagents/reagent_containers/syringes.dm:388
public partial class obj_item_reagent_containers_syringe_crude_tribal : obj_item_reagent_containers_syringe_crude
{
    // tg: code/modules/reagents/reagent_containers/syringes.dm:392
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object toxin_to_get = pick(
            typeof(datum_reagent_ants),
            typeof(datum_reagent_consumable_liquidelectricity),
            typeof(datum_reagent_toxin_amanitin),
            typeof(datum_reagent_toxin_bungotoxin),
            typeof(datum_reagent_toxin_coniine)
        );
        list_reagents = Dm.list(Dm.kv(toxin_to_get, 5));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/reagents/reagent_containers/syringes.dm:431
public partial class obj_item_reagent_containers_syringe_contraband : obj_item_reagent_containers_syringe
{
    // tg: code/modules/reagents/reagent_containers/syringes.dm:435
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}
