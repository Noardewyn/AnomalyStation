// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/cyberimp/augments_internal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// The whole file: the cybernetic implant's base with its body overlay (:1-80), the brain implants' EMP (:83-101) and the surgical
// processor implant with its operations (:353-476) came with gaps package P1 (D298); the anti-drop, anti-stun (the CNS rebooter), skillchip
// connector and breathing tube implants (:103-350, :478-496) with gaps package P6.
using System;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:1
public partial class obj_item_organ_cyberimp : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(aug_overlay))
        {
            visual = TRUE;
            bodypart_aug = Dm.@new<datum_bodypart_overlay_simple_augment>();
            bodypart_aug.icon = aug_icon;
            bodypart_aug.icon_state = get_overlay_state();
            bodypart_aug.emissive = emissive_overlay;
            bodypart_aug.set_layer("", overlay_layer);
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:30
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref bodypart_aug); // Do this after Remove() has done its thing, otherwise on_bodypart_remove() will not properly remove the overlay
        return dot;
    }

    /// Returns what icon_state the bodypart overlay should be in.
    /// Defaults to whatever is set in the variable, but can be overridden by subtypes which need multiple states
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:36
    public virtual object get_overlay_state()
    {
        return aug_overlay;
    }

    /// Refreshes the overlay's icon_state, then calls update_bodyparts if necessary
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:40
    public virtual async DmTask update_overlay_state()
    {
        if (bodypart_aug == null)
            return;

        object old_aug_state = bodypart_aug.icon_state;
        bodypart_aug.icon_state = get_overlay_state();
        if (!Dm.eq(old_aug_state, bodypart_aug.icon_state) && owner != null)
            await owner.update_body_parts();
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:49
    public override async DmTask on_bodypart_insert(obj_item_bodypart limb = null)
    {
        await base.on_bodypart_insert(limb);
        if (bodypart_aug == null)
            return;

        await limb.add_bodypart_overlay(bodypart_aug);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:56
    public override async DmTask on_bodypart_remove(obj_item_bodypart limb = null, object movement_flags = null)
    {
        await base.on_bodypart_remove(limb, movement_flags);
        if (bodypart_aug == null)
            return;

        await limb.remove_bodypart_overlay(bodypart_aug);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:77
    public override object feel_for_damage(object self_aware = null)
    {
        // No feeling in implants (yet?)
        return "";
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:63
public partial class datum_bodypart_overlay_simple_augment : datum_bodypart_overlay_simple
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:71
    public override async DmTask<object> get_overlay(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        object dot = await base.get_overlay(limb, layer_index, layer_real);
        if (Dm.truthy(emissive))
            Dm.Deferred("code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:73", "get_overlay: the augment's emissive appearance (emissives, DV12)");
        return dot;
    }
}

//[[[[BRAIN]]]]

// tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:83
public partial class obj_item_organ_cyberimp_brain : obj_item_organ_cyberimp
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:93
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (owner == null || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (emp_immobilize_duration > 0)
            await owner.Immobilize(emp_immobilize_duration / Dm.num(severity));
        if (emp_stun_duration > 0)
        {
            await owner.Stun(emp_stun_duration / Dm.num(severity));
            to_chat(owner, span_warning(I18n.T("Your body seizes up!")));
        }
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:103
public partial class obj_item_organ_cyberimp_brain_anti_drop : obj_item_organ_cyberimp_brain
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:113
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        active = Dm.truthy(active) ? FALSE : TRUE;
        if (Dm.truthy(active))
        {
            DmList hold_list = owner.get_empty_held_indexes() as DmList;
            if (LAZYLEN(hold_list) == owner.held_items.len)
            {
                to_chat(owner, span_notice(I18n.T("You are not holding any items, your hands relax...")));
                active = FALSE;
                return null;
            }
            foreach (obj_item held_item in Dm.each_cast<obj_item>(owner.held_items.Copy()))
            {
                if (held_item == null)
                    continue;
                stored_items.Add(held_item);
                to_chat(owner, span_notice(I18n.T("Your []'s grip tightens.", owner.get_held_index_name(owner.get_held_index_of_item(held_item)))));
                ADD_TRAIT(held_item, TRAIT_NODROP, IMPLANT_TRAIT);
                RegisterSignal(held_item, COMSIG_ITEM_DROPPED, nameof(on_held_item_dropped));
            }
        }
        else
        {
            release_items();
            to_chat(owner, span_notice(I18n.T("Your hands relax...")));
        }
        return null;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:133
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (owner == null || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        float range = 10 / Dm.num(severity);
        atom throw_target;
        if (Dm.truthy(active))
            release_items();
        foreach (obj_item stored_item in Dm.each_cast<obj_item>(stored_items.Copy()))
        {
            DmList seen = oview(range);
            if (seen.len == 0)
                return dot; // a runtime in tg: pick() from an empty oview (no usr to see from) ends the proc
            throw_target = pick(seen) as atom;
            await stored_item.throw_at(throw_target, range, 2);
            to_chat(owner, span_warning(I18n.T("Your [] spasms and throws \\the []!", owner.get_held_index_name(owner.get_held_index_of_item(stored_item)), stored_item)));
        }
        stored_items = Dm.list();
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:148
    public virtual void release_items()
    {
        foreach (obj_item stored_item in Dm.each_cast<obj_item>(stored_items))
        {
            REMOVE_TRAIT(stored_item, TRAIT_NODROP, IMPLANT_TRAIT);
            UnregisterSignal(stored_item, COMSIG_ITEM_DROPPED);
        }
        stored_items = Dm.list();
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:155
    public override async DmTask Remove(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        if (Dm.truthy(active))
            await ui_action_click();
        await base.Remove(organ_owner, special, movement_flags);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:160
    [SignalHandler]
    public virtual void on_held_item_dropped(obj_item source = null, mob user = null)
    {
        REMOVE_TRAIT(source, TRAIT_NODROP, IMPLANT_TRAIT);
        UnregisterSignal(source, COMSIG_ITEM_DROPPED);
        stored_items.Remove(source);
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:166
public partial class obj_item_organ_cyberimp_brain_anti_stun : obj_item_organ_cyberimp_brain
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:173  var/static/list/signalCache (rules 10.6)
    public static ref DmList signalCache => ref TgState.Current.obj_item_organ_cyberimp_brain_anti_stun__signalCache;

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:185
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon implant_owner = (mob_living_carbon)organ_owner;
        UnregisterSignal(implant_owner, signalCache);
        UnregisterSignal(implant_owner, COMSIG_LIVING_ENTER_STAMCRIT);
        remove_stun_buffs(implant_owner);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:191
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignals(organ_owner, signalCache, nameof(on_signal));
        RegisterSignal(organ_owner, COMSIG_LIVING_ENTER_STAMCRIT, nameof(on_stamcrit));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:196
    [SignalHandler]
    public virtual void on_signal(datum source = null, object amount = null)
    {
        if (!Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) && Dm.num(amount) > 0)
            _addtimer(Dm.@new<datum_callback>(this, nameof(clear_stuns)), stun_cap_amount, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/surgery/organs/internal/cyberimp/augments_internal.dm", line: 199);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:201
    [SignalHandler]
    public virtual void on_stamcrit(datum source = null)
    {
        if (!Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            _addtimer(Dm.@new<datum_callback>(this, nameof(clear_stuns)), stun_cap_amount, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/surgery/organs/internal/cyberimp/augments_internal.dm", line: 204);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:206
    public virtual async DmTask clear_stuns()
    {
        if (owner == null || Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) || !(implant_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
            return;

        await owner.SetStun(0);
        await owner.SetKnockdown(0);
        await owner.SetImmobilized(0);
        await owner.SetParalyzed(0);
        await owner.set_stamina_loss(0);
        _addtimer(Dm.@new<datum_callback>(owner, nameof(mob_living.set_stamina_loss), 0), stun_resistance_time, file: "code/modules/surgery/organs/internal/cyberimp/augments_internal.dm", line: 215);

        do_sparks(5, TRUE, this);

        give_stun_buffs(owner);
        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_stun_buffs), owner), stun_resistance_time, file: "code/modules/surgery/organs/internal/cyberimp/augments_internal.dm", line: 220);

        implant_cooldown = world.time + 60 * SECONDS; // COOLDOWN_START, inline
        _addtimer(Dm.@new<datum_callback>(this, nameof(implant_ready)), 60 * SECONDS, file: "code/modules/surgery/organs/internal/cyberimp/augments_internal.dm", line: 223);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:225
    public virtual void implant_ready()
    {
        if (owner != null)
            to_chat(owner, span_purple(I18n.T("Your rebooter implant is ready.")));
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:229
    public virtual void give_stun_buffs(mob_living give_to = null)
    {
        give_to ??= owner;
        give_to.add_traits(Dm.list(TRAIT_STUNIMMUNE, TRAIT_BATON_RESISTANCE), REF(this));
        give_to.add_movespeed_mod_immunities(REF(this), typeof(datum_movespeed_modifier_damage_slowdown));
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:233
    public virtual void remove_stun_buffs(mob_living remove_from = null)
    {
        remove_from ??= owner;
        remove_from.remove_traits(Dm.list(TRAIT_STUNIMMUNE, TRAIT_BATON_RESISTANCE), REF(this));
        remove_from.remove_movespeed_mod_immunities(REF(this), typeof(datum_movespeed_modifier_damage_slowdown));
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:237
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        organ_flags = Dm.bitor(organ_flags, ORGAN_FAILING);
        _addtimer(Dm.@new<datum_callback>(this, nameof(reboot)), 9 * SECONDS / Dm.num(severity), file: "code/modules/surgery/organs/internal/cyberimp/augments_internal.dm", line: 242);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:244
    public virtual void reboot()
    {
        organ_flags = Dm.bitand(organ_flags, Dm.bitnot(ORGAN_FAILING));
        implant_ready();
    }
}

public sealed partial class TgState
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:173  var/static/list/signalCache = list(...)
    internal DmList obj_item_organ_cyberimp_brain_anti_stun__signalCache = Dm.list(
        COMSIG_LIVING_STATUS_STUN,
        COMSIG_LIVING_STATUS_IMMOBILIZE,
        COMSIG_LIVING_STATUS_PARALYZE);
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:248
public partial class obj_item_organ_cyberimp_brain_connector : obj_item_organ_cyberimp_brain
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:256
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        to_chat(owner, span_warning(I18n.T("You start fiddling around with []...", this)));
        playsound(owner, Dm.resource("sound/items/taperecorder/tape_flip.ogg"), 20, vary: TRUE); // asmr

        if (!Dm.truthy(await do_after(owner, 1.5f * SECONDS, owner))) // othwerwise it doesnt appear
        {
            to_chat(owner, span_warning(I18n.T("You were interrupted!")));
            return null;
        }

        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
        {
            // tg passes the slot name ORGAN_SLOT_BRAIN to get_organ_by_type, which finds no organ: remove_brain gets null
            obj_item_organ_brain holy_shit_my_brain = await remove_brain(owner.get_organ_by_type(ORGAN_SLOT_BRAIN) as obj_item_organ_brain) as obj_item_organ_brain;
            if (holy_shit_my_brain != null)
                to_chat(owner, span_warning(I18n.T("You take [] out of []. You stare at it for a moment in confusion.", holy_shit_my_brain, this)));
            return null;
        }

        obj_item held = owner.get_active_held_item() as obj_item;
        if (held != null)
        {
            if (held is obj_item_skillchip skillchip)
                await insert_skillchip(skillchip);
            else
                to_chat(owner, span_warning(I18n.T("You try to insert [] into [], but it won't fit!", owner.get_active_held_item(), this))); // make it kill you if you shove a crayon inside or something
        }
        else // no inhand item, assume removal
        {
            obj_item_organ_brain chippy_brain = owner.get_organ_by_type(typeof(obj_item_organ_brain)) as obj_item_organ_brain;
            if (chippy_brain == null)
                throw Dm.CRASH("we using a brain implant wit no brain", "code/modules/surgery/organs/internal/cyberimp/augments_internal.dm", 280);
            await remove_skillchip(chippy_brain);
        }
        return null;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:283
    public virtual async DmTask insert_skillchip(obj_item_skillchip skillchip = null)
    {
        object fail_string = await owner.implant_skillchip(skillchip, force: FALSE);
        if (Dm.truthy(fail_string))
        {
            to_chat(owner, span_warning(fail_string as string));
            playsound(owner, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 10, vary: TRUE);
            return;
        }

        object refail_string = await skillchip.try_activate_skillchip(silent: FALSE, force: FALSE);
        if (Dm.truthy(refail_string))
        {
            to_chat(owner, span_warning(fail_string as string)); // tg shows fail_string here (null), not refail_string
            playsound(owner, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 10, vary: TRUE);
            return;
        }

        // success!
        playsound(owner, Dm.resource("sound/machines/chime.ogg"), 10, vary: TRUE);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:299
    public virtual async DmTask remove_skillchip(obj_item_organ_brain chippy_brain = null)
    {
        obj_item_skillchip skillchip = await show_radial_menu(owner, owner, chippy_brain.skillchips) as obj_item_skillchip;
        if (skillchip != null)
        {
            await owner.remove_skillchip(skillchip, silent: FALSE);
            await skillchip.forceMove(owner.drop_location() as atom);
            await owner.put_in_hands(skillchip, del_on_fail: FALSE);
            playsound(owner, Dm.resource("sound/machines/click.ogg"), 10, vary: TRUE);
            to_chat(owner, span_warning(I18n.T("You take [] out of [].", skillchip, this)));
            return;
        }

        to_chat(owner, span_warning(I18n.T("Your brain is empty!"))); // heh
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:311
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        organ_flags = Dm.bitor(organ_flags, ORGAN_FAILING);
        if (owner == null)
            return dot; // a runtime in tg: an EMP on a loose connector (owner.visible_message on null) leaves it failing, with no reboot
        int loops = 1;
        if (!Dm.eq(severity, EMP_LIGHT))
            loops = 2;
        for (int i = 1; i <= loops; i++)
        {
            // you either lose a chip or a bit of your brain
            owner.visible_message(span_warning(I18n.T("Something falls to the ground from behind []'s head.", owner)),
                span_boldwarning(I18n.T("You feel something fall off from behind your head.")));
            // tg passes the slot name ORGAN_SLOT_BRAIN to get_organ_by_type, which finds no organ
            obj_item_organ_brain chippy_brain = owner.get_organ_by_type(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
            obj_item_skillchip skillchip = chippy_brain?.skillchips?[1] as obj_item_skillchip;
            if (skillchip != null)
            {
                await owner.remove_skillchip(skillchip, silent: TRUE);
                await skillchip.forceMove(owner.drop_location() as atom);
                playsound(owner, Dm.resource("sound/machines/terminal/terminal_eject.ogg"), 25, TRUE);
            }
            else
            {
                await remove_brain(chippy_brain, Dm.eq(severity, EMP_LIGHT) ? 1 : 2);
            }
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(reboot)), 90 / Dm.num(severity), file: "code/modules/surgery/organs/internal/cyberimp/augments_internal.dm", line: 331);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:333
    public virtual async DmTask<object> remove_brain(obj_item_organ_brain chippy_brain = null, float? severity = null)
    {
        severity ??= 1;
        playsound(owner, Dm.resource("sound/effects/meatslap.ogg"), 25, TRUE);
        if (chippy_brain == null)
            return null;
        await chippy_brain.apply_organ_damage(20 * severity.Value);
        chippy_brain.maxHealth -= 15 * severity.Value; // a bit of your brain fell off. again.
        if (chippy_brain.damage >= chippy_brain.maxHealth)
        {
            await chippy_brain.forceMove(owner.drop_location() as atom);
            owner.visible_message(span_userdanger(I18n.T("[]'s brain falls off the back of [] head!!!", owner, owner.p_their())), span_boldwarning(I18n.T("You feel like you're missing something.")));
            return chippy_brain;
        }

        Type gib_type = typeof(obj_effect_decal_cleanable_blood_gibs_up);
        if (Dm.truthy(IS_ROBOTIC_ORGAN(chippy_brain)))
            gib_type = typeof(obj_effect_decal_cleanable_blood_gibs_robot_debris_up);
        Dm.@new(gib_type, get_turf(owner), await owner.get_static_viruses(), owner.get_blood_dna_list());
        return FALSE;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:350
    public virtual void reboot()
    {
        organ_flags = Dm.bitand(organ_flags, Dm.bitnot(ORGAN_FAILING));
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:353
public partial class obj_item_organ_cyberimp_brain_surgical_processor : obj_item_organ_cyberimp_brain
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:364
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.length(loaded_surgeries) > 0)
        {
            Dm.append(ref dot, span_info(I18n.T("Load surgeries from an operating compuer or a disk containing surgery data. Loaded surgeries:")));
            foreach (datum_surgery_operation downloaded_surgery in Dm.each_cast<datum_surgery_operation>(GLOB.operations.get_instances_from(loaded_surgeries)))
            {
                if (!Dm.truthy(Dm.bitand(downloaded_surgery.operation_flags, OPERATION_LOCKED)))
                    continue;
                // for simplicitly, filters out mechanical subtypes of normal surgeries
                if (Dm.truthy(Dm.bitand(downloaded_surgery.operation_flags, OPERATION_MECHANIC)) && Dm.@in(DmTypes.ParentOf(downloaded_surgery.type), loaded_surgeries))
                    continue;
                Dm.append(ref dot, span_info(I18n.T("&bull; []", capitalize(Dm.truthy(downloaded_surgery.rnd_name) ? downloaded_surgery.rnd_name : downloaded_surgery.name))));
            }
        }
        else
        {
            Dm.append(ref dot, span_info(I18n.T("Load surgeries from an operating compuer or a disk containing surgery data.")));
            Dm.append(ref dot, span_info(I18n.T("No surgeries loaded. Surgeries must be loaded <i>before</i> installation.")));
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:380
    public virtual async DmTask<object> load_surgeries(mob_living user = null, obj design_holder = null)
    {
        balloon_alert(user, I18n.T("copying designs..."));
        playsound(this, Dm.resource("sound/machines/terminal/terminal_processing.ogg"), 25, TRUE);
        if (Dm.truthy(await do_after(user, 1 * SECONDS, target: design_holder)))
        {
            if (design_holder is obj_item_disk_surgery surgery_disk)
                LAZYOR(ref loaded_surgeries, surgery_disk.surgeries);
            else
            {
                obj_machinery_computer_operating surgery_computer = (obj_machinery_computer_operating)design_holder;
                LAZYOR(ref loaded_surgeries, surgery_computer.advanced_surgeries);
            }
            playsound(this, Dm.resource("sound/machines/terminal/terminal_success.ogg"), 25, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:394
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is obj_item_disk_surgery || interacting_with is obj_machinery_computer_operating)
            return await load_surgeries(user, (obj)interacting_with);
        return NONE;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:399
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_disk_surgery)
            return await load_surgeries(user, tool);
        return NONE;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:404
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(organ_owner, COMSIG_LIVING_OPERATING_ON, nameof(check_surgery));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:408
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal((datum)organ_owner, COMSIG_LIVING_OPERATING_ON);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:412
    [SignalHandler]
    public virtual void check_surgery(datum source = null, atom_movable operating_on = null, DmList operations = null)
    {
        if (Dm.truthy(Dm.bitand(organ_flags, Dm.bitor(ORGAN_FAILING, ORGAN_EMP))))
            return;

        Dm.combine(ref operations, loaded_surgeries);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:420
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (owner == null || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;

        obj_item_organ surgeon_brain = owner.get_organ_by_type(typeof(obj_item_organ_brain)) as obj_item_organ;
        await surgeon_brain.apply_organ_damage(20 / Dm.num(severity), maximum: 120);

        float duration = (30 * SECONDS) / Dm.num(severity);
        if (owner.mob_mood != null && owner.mob_mood.mood_modifier > 0)
        {
            // forced insanity - reset to "only a little crazy" after
            await owner.mob_mood.set_sanity(SANITY_INSANE);
            _addtimer(Dm.@new<datum_callback>(owner.mob_mood, nameof(datum_mood.reset_sanity), SANITY_UNSTABLE + 10), duration, TIMER_DELETE_ME, file: "code/modules/surgery/organs/internal/cyberimp/augments_internal.dm", line: 432);
            // and some moodlets to sell the sanity loss
            await owner.add_mood_event("surgery_emp", typeof(datum_mood_event_surgery_emp_active));
            _addtimer(Dm.@new<datum_callback>(owner, nameof(mob_living.add_mood_event), "surgery_emp", typeof(datum_mood_event_surgery_emp_expired)), duration, TIMER_DELETE_ME, file: "code/modules/surgery/organs/internal/cyberimp/augments_internal.dm", line: 436);
        }

        // causes the surgeon to go crazy and start stabbing people
        Dm.Deferred("code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:439", "emp_act: owner.apply_status_effect(/datum/status_effect/forced_combat, ...) (the heretic's forced combat status effect is inert)");
        to_chat(owner, span_boldwarning(I18n.T("Your surgical processor malfunctions, giving you an overwhelming urge to incise, saw, and stitch!")));
        return dot;
    }
}

//[[[[MOUTH]]]]

// tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:481
public partial class obj_item_organ_cyberimp_mouth_breathing_tube : obj_item_organ_cyberimp_mouth
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_internal.dm:490
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (owner == null || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (prob(60 / Dm.num(severity)))
        {
            to_chat(owner, span_warning(I18n.T("Your breathing tube suddenly closes!")));
            owner.losebreath += 2;
        }
        return dot;
    }
}
