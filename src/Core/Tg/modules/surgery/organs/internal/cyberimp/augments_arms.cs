// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/cyberimp/augments_arms.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps package P6 (D298): the whole file: the arm implants' hand link, the toolkits (extending a tool into the hand, retracting it, the
// radial of a multi-tool kit, the drop key, the EMP), the engineering, surgical, paperwork, laser, taser, energy blade, medical beam, flash,
// baton and combat kits, and the Strong-Arm implant with its two-arm set bonus. The var blocks are generated (D111).
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:1
public partial class obj_item_organ_cyberimp_arm : obj_item_organ_cyberimp
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:15
    public override async DmTask swap_zone(object target_zone = null)
    {
        await base.swap_zone(target_zone);
        await update_overlay_state();
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:19
    public override object get_overlay_state()
    {
        return Dm.text("[][]", aug_overlay, Dm.eq(zone, BODY_ZONE_L_ARM) ? "_left" : "_right");
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:22
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(organ_owner, COMSIG_CARBON_POST_ATTACH_LIMB, nameof(on_limb_attached));
        on_limb_attached(organ_owner, organ_owner.hand_bodyparts[Dm.eq(zone, BODY_ZONE_R_ARM) ? RIGHT_HANDS : LEFT_HANDS] as obj_item_bodypart);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:27
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal((datum)organ_owner, COMSIG_CARBON_POST_ATTACH_LIMB);
        on_limb_detached(hand as obj_item_bodypart);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:32
    [SignalHandler]
    public virtual void on_limb_attached(mob_living_carbon source = null, obj_item_bodypart limb = null)
    {
        if (limb == null || QDELETED(limb) || !Dm.eq(limb.body_zone, zone))
            return;
        handle_attachment(limb);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:38
    public virtual void handle_attachment(obj_item_bodypart limb = null)
    {
        if (hand != null)
            on_limb_detached(hand as obj_item_bodypart);
        RegisterSignal(limb, COMSIG_BODYPART_REMOVED, nameof(on_limb_detached));
        hand = limb;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:44
    [SignalHandler]
    public virtual void on_limb_detached(obj_item_bodypart source = null)
    {
        if (source != hand || QDELETED(hand))
            return;
        UnregisterSignal(hand, COMSIG_BODYPART_REMOVED);
        hand = null;
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:51
public partial class obj_item_organ_cyberimp_arm_toolkit : obj_item_organ_cyberimp_arm
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (ispath(active_item))
        {
            obj_item made = Dm.@new(active_item as Type, this) as obj_item;
            active_item = made;
            Waiting.NoSleep(made.set_custom_materials(null));
            items_list.Add(WEAKREF(made));
        }

        foreach (object typepath in Dm.each(items_to_create))
        {
            atom new_item = Dm.@new(typepath as Type, this) as atom;
            Waiting.NoSleep(new_item.set_custom_materials(null));
            items_list.Add(WEAKREF(new_item));
        }

        if (Dm.truthy(hand_state))
        {
            // Overlay is done in two layers, "[state]" and "[state]_hand"
            bodypart_aug?.add_layer("hand", BODYPARTS_HIGH_LAYER);
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:85
    public override object Destroy(float? force = null)
    {
        hand = null;
        active_item = null;
        foreach (datum_weakref @ref in Dm.each_cast<datum_weakref>(items_list))
        {
            obj_item to_del = @ref.resolve() as obj_item;
            if (to_del == null)
                continue;
            qdel(to_del);
        }
        items_list.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:99
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(organ_owner, COMSIG_KB_MOB_DROPITEM_DOWN, nameof(dropkey)); //We're nodrop, but we'll watch for the drop hotkey anyway and then stow if possible.
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:103
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal((datum)organ_owner, COMSIG_KB_MOB_DROPITEM_DOWN);
        await Retract();
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:108
    public override void handle_attachment(obj_item_bodypart limb = null)
    {
        base.handle_attachment(limb);
        RegisterSignal(limb, COMSIG_ITEM_ATTACK_SELF, nameof(on_item_attack_self));
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:112
    [SignalHandler]
    public override void on_limb_detached(obj_item_bodypart source = null)
    {
        if (source != hand || QDELETED(hand))
            return;
        UnregisterSignal(hand, Dm.list(COMSIG_BODYPART_REMOVED, COMSIG_ITEM_ATTACK_SELF));
        hand = null;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:118
    [SignalHandler]
    public virtual void on_item_attack_self()
    {
        Waiting.Detach(ui_action_click()); // INVOKE_ASYNC(src, PROC_REF(ui_action_click))
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:122
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)) || !Dm.truthy(IS_ROBOTIC_ORGAN(this)))
            return dot;
        if (prob(15 / Dm.num(severity)) && owner != null)
        {
            to_chat(owner, span_warning(I18n.T("The electromagnetic pulse causes [] to malfunction!", this)));
            // give the owner an idea about why his implant is glitching
            await Retract();
        }
        return dot;
    }

    /**
     * Called when the mob uses the "drop item" hotkey
     *
     * Items inside toolset implants have TRAIT_NODROP, but we can still use the drop item hotkey as a
     * quick way to store implant items. In this case, we check to make sure the user has the correct arm
     * selected, and that the item is actually owned by us, and then we'll hand off the rest to Retract()
     */
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:138
    [SignalHandler]
    public virtual object dropkey(mob_living_carbon host = null)
    {
        if (host == null)
            return null; //How did we even get here
        if (hand != host.hand_bodyparts[host.active_hand_index])
            return null; //wrong hand
        if (Dm.truthy(Waiting.NoSleep(Retract())))
            return COMSIG_KB_ACTIVATED;
        return null;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:147
    public virtual async DmTask<object> Retract()
    {
        obj_item item = active_item as obj_item;
        if (item == null || Dm.@in(item, this))
            return FALSE;
        item.resistance_flags = Dm.num(Dm.initial(item, (obj_item p) => p.resistance_flags));
        if (owner != null)
        {
            owner.visible_message(
                span_notice(I18n.T("[] retracts [] back into [] [].", owner, item, owner.p_their(), parse_zone(zone))),
                span_notice(I18n.T("[] snaps back into your [].", item, parse_zone(zone))),
                span_hear(I18n.T("You hear a short mechanical noise.")));

            await owner.transferItemToLoc(item, this, TRUE);
        }
        else
        {
            await item.forceMove(this);
        }

        UnregisterSignal(item, COMSIG_ITEM_ATTACK_SELF);
        UnregisterSignal(item, COMSIG_ITEM_ATTACK_SELF_SECONDARY);
        active_item = null;
        playsound(get_turf(owner), retract_sound, 50, TRUE);
        return TRUE;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:168
    public virtual async DmTask Extend(obj_item augment = null)
    {
        if (!Dm.@in(augment, this))
            return;

        active_item = augment;
        augment.resistance_flags = Dm.num(Dm.bitor(Dm.bitor(Dm.bitor(INDESTRUCTIBLE, LAVA_PROOF), Dm.bitor(FIRE_PROOF, UNACIDABLE)), ACID_PROOF));
        ADD_TRAIT(augment, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT);
        augment.slot_flags = null;
        await augment.set_custom_materials(null);

        float side = Dm.eq(zone, BODY_ZONE_R_ARM) ? RIGHT_HANDS : LEFT_HANDS;
        object hand__1 = owner.get_empty_held_index_for_side(side); // the local hand of tg's proc (it shadows the var)
        if (Dm.truthy(hand__1))
        {
            await owner.put_in_hand(augment, hand__1);
        }
        else
        {
            DmList hand_items = owner.get_held_items_for_side(side, all: TRUE) as DmList;
            object success = FALSE;
            DmList failure_message = Dm.list();
            for (int i = 1; i <= hand_items.len; i++) //Can't just use *in* here.
            {
                obj_item hand_item = hand_items[i] as obj_item;
                if (!Dm.truthy(await owner.dropItemToGround(hand_item)))
                {
                    failure_message.Add(span_warning(I18n.T("Your [] interferes with []!", hand_item, this)));
                    continue;
                }
                to_chat(owner, span_notice(I18n.T("You drop [] to activate []!", hand_item, this)));
                success = await owner.put_in_hand(augment, owner.get_empty_held_index_for_side(side));
                break;
            }
            if (!Dm.truthy(success))
            {
                foreach (object failure in Dm.each(failure_message))
                    to_chat(owner, failure);
                return;
            }
        }
        owner.visible_message(span_notice(I18n.T("[] extends [] from [] [].", owner, augment, owner.p_their(), parse_zone(zone))),
            span_notice(I18n.T("You extend [] from your [].", augment, parse_zone(zone))),
            span_hear(I18n.T("You hear a short mechanical noise.")));
        playsound(get_turf(owner), extend_sound, 50, TRUE);

        if (length(items_list) > 1)
            RegisterSignals(augment, Dm.list(COMSIG_ITEM_ATTACK_SELF, COMSIG_ITEM_ATTACK_SELF_SECONDARY), nameof(swap_tools)); // secondary for welders
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:206
    [SignalHandler]
    public virtual void swap_tools(object active_item = null)
    {
        Waiting.NoSleep(Retract());
        Waiting.Detach(ui_action_click()); // INVOKE_ASYNC(src, PROC_REF(ui_action_click))
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:211
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) || (active_item == null && !Dm.truthy(contents.len)))
        {
            to_chat(owner, span_warning(I18n.T("The implant doesn't respond. It seems to be broken...")));
            return null;
        }

        if (active_item == null || Dm.@in(active_item, this))
        {
            active_item = null;
            if (contents.len == 1)
            {
                await Extend(contents[1] as obj_item);
            }
            else
            {
                DmList choice_list = Dm.list();
                foreach (datum_weakref augment_ref in Dm.each_cast<datum_weakref>(items_list.Copy()))
                {
                    obj_item augment_item = augment_ref.resolve() as obj_item;
                    if (augment_item == null)
                    {
                        items_list.Remove(augment_ref);
                        continue;
                    }
                    choice_list[augment_item] = image(augment_item);
                }
                obj_item choice = await show_radial_menu(owner, owner, choice_list) as obj_item;
                if (owner != null && owner == usr && owner.stat != DEAD && Dm.@in(this, owner.organs) && active_item == null && Dm.@in(choice, contents))
                {
                    // This monster sanity check is a nice example of how bad input is.
                    await Extend(choice);
                }
            }
        }
        else
        {
            await Retract();
        }
        return null;
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:235
public partial class obj_item_organ_cyberimp_arm_toolkit_gun : obj_item_organ_cyberimp_arm_toolkit
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:235
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (prob(30 / Dm.num(severity)) && owner != null && !Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
        {
            await Retract();
            owner.visible_message(span_danger(I18n.T("A loud bang comes from []'s []!", owner, parse_zone(zone))));
            playsound(get_turf(owner), Dm.resource("sound/items/weapons/flashbang.ogg"), 100, TRUE);
            to_chat(owner, span_userdanger(I18n.T("You feel an explosion erupt inside your [] as your implant breaks!", parse_zone(zone))));
            await owner.adjust_fire_stacks(20);
            owner.ignite_mob();
            await owner.adjust_fire_loss(25);
            organ_flags = Dm.bitor(organ_flags, ORGAN_FAILING);
        }
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:278
public partial class obj_item_organ_cyberimp_arm_toolkit_paperwork : obj_item_organ_cyberimp_arm_toolkit
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:296
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        foreach (datum_weakref created_item in Dm.each_cast<datum_weakref>(items_list))
        {
            obj potential_tool = created_item.resolve() as obj;
            if (istype(potential_tool, typeof(obj_item_stamp_chameleon)))
                return FALSE;
        }

        balloon_alert(user, I18n.T("experimental stamp unlocked"));
        items_list.Add(WEAKREF(Dm.@new<obj_item_stamp_chameleon>(this)));
        return TRUE;
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:261
public partial class obj_item_organ_cyberimp_arm_toolkit_toolset : obj_item_organ_cyberimp_arm_toolkit
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:306
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        foreach (datum_weakref created_item in Dm.each_cast<datum_weakref>(items_list))
        {
            obj potential_knife = created_item.resolve() as obj;
            if (istype(potential_knife, typeof(obj_item_knife_combat_cyborg)))
                return FALSE;
        }

        balloon_alert(user, I18n.T("integrated knife unlocked"));
        items_list.Add(WEAKREF(Dm.@new<obj_item_knife_combat_cyborg>(this)));
        return TRUE;
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:328
public partial class obj_item_organ_cyberimp_arm_toolkit_flash : obj_item_organ_cyberimp_arm_toolkit
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:334
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (datum_weakref created_item in Dm.each_cast<datum_weakref>(items_list))
        {
            obj potential_flash = created_item.resolve() as obj;
            if (!istype(potential_flash, typeof(obj_item_assembly_flash_armimplant)))
                continue;
            obj_item_assembly_flash_armimplant flash = (obj_item_assembly_flash_armimplant)potential_flash;
            flash.arm = WEAKREF(this) as datum_weakref;
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:343
    public override async DmTask Extend(obj_item augment = null)
    {
        await base.Extend(augment);
        if (active_item is not obj_item item)
            return; // a runtime in tg: the augment was not in the implant (active_item stays null)
        item.set_light_range(7);
        await item.set_light_on(TRUE);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:348
    public override async DmTask<object> Retract()
    {
        if (active_item is obj_item item)
            await item.set_light_on(FALSE);
        return await base.Retract();
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:359
public partial class obj_item_organ_cyberimp_arm_toolkit_combat : obj_item_organ_cyberimp_arm_toolkit
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:370
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (datum_weakref created_item in Dm.each_cast<datum_weakref>(items_list))
        {
            obj potential_flash = created_item.resolve() as obj;
            if (!istype(potential_flash, typeof(obj_item_assembly_flash_armimplant)))
                continue;
            obj_item_assembly_flash_armimplant flash = (obj_item_assembly_flash_armimplant)potential_flash;
            flash.arm = WEAKREF(this) as datum_weakref;
        }
        return dot;
    }
}

// Strong-Arm Implant //

// tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:431
public partial class obj_item_organ_cyberimp_arm_strongarm : obj_item_organ_cyberimp_arm
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:427  #define DOAFTER_SOURCE_STRONGARM_INTERACTION "strongarm interaction"
    internal const string DOAFTER_SOURCE_STRONGARM_INTERACTION = "strongarm interaction";

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:471
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_strongarm)));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:475
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        if (ishuman(organ_owner)) //Sorry, only humans
            RegisterSignal(organ_owner, COMSIG_LIVING_EARLY_UNARMED_ATTACK, nameof(on_attack_hand));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:480
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal((datum)organ_owner, COMSIG_LIVING_EARLY_UNARMED_ATTACK);
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:484
    public override async DmTask on_bodypart_insert(obj_item_bodypart limb = null)
    {
        await base.on_bodypart_insert(limb);
        limb.unarmed_damage_low += lower_punch_damage;
        limb.unarmed_damage_high += upper_punch_damage;
        limb.unarmed_effectiveness += punch_effectiveness_added;
        limb.unarmed_grab_damage_bonus += bonus_grab_damage;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:491
    public override async DmTask on_bodypart_remove(obj_item_bodypart limb = null, object movement_flags = null)
    {
        await base.on_bodypart_remove(limb, movement_flags);
        limb.unarmed_damage_low -= lower_punch_damage;
        limb.unarmed_damage_high -= upper_punch_damage;
        limb.unarmed_effectiveness -= punch_effectiveness_added;
        limb.unarmed_grab_damage_bonus -= bonus_grab_damage;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:498
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (owner == null)
            return dot; // a runtime in tg: an EMP on a loose implant (owner.balloon_alert on null)
        owner.balloon_alert(owner, I18n.T("your arm spasms wildly!"));
        organ_flags = Dm.bitor(organ_flags, ORGAN_FAILING);
        _addtimer(Dm.@new<datum_callback>(this, nameof(reboot)), 90 / Dm.num(severity), file: "code/modules/surgery/organs/internal/cyberimp/augments_arms.dm", line: 504);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:506
    public virtual void reboot()
    {
        organ_flags = Dm.bitand(organ_flags, Dm.bitnot(ORGAN_FAILING));
        owner?.balloon_alert(owner, I18n.T("your arm stops spasming!")); // a runtime in tg once the implant is out (owner null)
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:510
    [SignalHandler]
    public virtual object on_attack_hand(mob_living_carbon_human source = null, atom target = null, object proximity = null, object modifiers = null)
    {
        if (source.get_active_hand() != hand || !Dm.truthy(proximity))
            return NONE;
        if (!Dm.truthy(source.combat_mode) || Dm.truthy(LAZYACCESS(modifiers as DmList, RIGHT_CLICK)))
            return NONE;
        if (!isliving(target))
            return NONE;
        if (HAS_TRAIT(source, TRAIT_HULK)) //NO HULK
            return NONE;
        if (!(slam_cooldown <= world.time) && ishuman(target)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
            return NONE;
        if (!Dm.truthy(source.can_unarmed_attack()))
            return COMPONENT_SKIP_ATTACK;

        mob_living living_target = (mob_living)target;
        object picked_hit_type = pick("punch", "smash", "pummel", "bash", "slam");

        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
        {
            if (Dm.num(source.body_position) != LYING_DOWN && living_target != source && prob(50))
            {
                to_chat(source, span_danger(I18n.T("You try to [] [], but lose your balance and fall!", picked_hit_type, living_target)));
                Waiting.NoSleep(source.Knockdown(3 * SECONDS));
                Waiting.NoSleep(source.forceMove(get_turf(living_target)));
            }
            else
            {
                to_chat(source, span_danger(I18n.T("Your muscles spasm!")));
                Waiting.NoSleep(source.Paralyze(1 * SECONDS));
            }
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        bool ground_bounce = false; // funny flavor. if you hit someone who's floored you slam them into the ground, breaking tiles
        turf target_turf = get_turf(living_target);

        /* Damage calculations operate on the same math used in /datum/species/proc/harm():
        * Unarmed damage is randomized between an upper and lower value.
        * The lower value is determined by taking the damage value from the limb, and then increasing that value based on athletics level (min upper value)
        * The upper value is determiend by taking the damage value from the limb, and then seeing if we have the strength trait, providing extra damage.
        * The end result is that the more investment into athletics, the more precise the damage is, without necessarily increasing the potential damage.
        */

        // Our attacking limb
        obj_item_bodypart attacking_bodypart = hand as obj_item_bodypart;
        // The upper damage, calculated first as it will be used to cap our potential lower damage.
        float potential_upper_damage = attacking_bodypart.unarmed_damage_high + (HAS_TRAIT(source, TRAIT_STRENGTH) ? 2 : 0);
        // The lower damage, which is capped by potential_upper_damage
        float potential_lower_damage = Dm.num(Dm.min(Dm.add(attacking_bodypart.unarmed_damage_low, (source.mind?.get_skill_level(typeof(datum_skill_athletics)) is var or__1 && Dm.truthy(or__1) ? or__1 : 0)), potential_upper_damage));
        // Finally, we determine the actual damage roll.
        float potential_damage = rand(potential_lower_damage, potential_upper_damage);
        // This value is used to determine armour penetration.
        float potential_effectiveness = attacking_bodypart.unarmed_effectiveness;
        // This is a damage and penetration multiplier if our target is grabbed when we deliver our punch.
        float potential_pummel_bonus = attacking_bodypart.unarmed_pummeling_bonus;

        if (living_target.pulledby != null && living_target.pulledby.grab_state >= GRAB_AGGRESSIVE) // get pummeled idiot
        {
            potential_damage *= potential_pummel_bonus;
            potential_effectiveness *= potential_pummel_bonus;
            if (Dm.num(living_target.body_position) == LYING_DOWN)
                ground_bounce = true;
        }

        object is_correct_biotype = Dm.bitand(living_target.mob_biotypes, biotype_bonus_targets);
        if (Dm.truthy(biotype_bonus_targets) && Dm.truthy(is_correct_biotype)) //If we are punching one of our special biotype targets, increase the damage floor by a factor of two.
            potential_damage += biotype_bonus_damage;

        if (ishuman(target))
        {
            mob_living_carbon_human human_target = (mob_living_carbon_human)target;
            if (Dm.truthy(Waiting.NoSleep(human_target.check_block(source, potential_damage, Dm.text("[]'s' []", source, picked_hit_type)))))
            {
                Waiting.NoSleep(source.do_attack_animation(target));
                playsound(living_target.loc, Dm.resource("sound/items/weapons/punchmiss.ogg"), 25, TRUE, -1);
                log_combat(source, target, Dm.text("attempted to []", picked_hit_type), "muscle implant");
                return COMPONENT_CANCEL_ATTACK_CHAIN;
            }
        }

        Waiting.NoSleep(source.do_attack_animation(target, ATTACK_EFFECT_SMASH));
        playsound(living_target.loc, Dm.resource("sound/items/weapons/punch1.ogg"), 25, TRUE, -1);

        // Some mobs gib when killed, so we're logging early. At this point, we're definitely hitting, so...
        living_target.visible_message(
            span_danger(I18n.T("[] []ed [][]!", source, picked_hit_type, living_target, ground_bounce ? I18n.T(" into []", target_turf) : "")),
            span_userdanger(I18n.T("You're []ed by [][]!", picked_hit_type, source, ground_bounce ? I18n.T(" into []", target_turf) : "")),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            COMBAT_MESSAGE_RANGE,
            source);

        to_chat(source, span_danger(I18n.T("You [] [][]!", picked_hit_type, target, ground_bounce ? I18n.T(" into []", target_turf) : "")));

        log_combat(source, target, Dm.text("[]ed", picked_hit_type), "muscle implant");

        if (ishuman(target))
            slam_cooldown = world.time + slam_cooldown_duration; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline

        object target_zone = living_target.get_random_valid_zone(source.zone_selected);
        object armor_block = living_target.run_armor_check(target_zone, MELEE, armour_penetration: potential_effectiveness);
        Waiting.NoSleep(living_target.apply_damage(potential_damage * 2, attacking_bodypart.attack_type as string, target_zone, Dm.num(armor_block)));

        if (Dm.num(source.body_position) != LYING_DOWN && !QDELETED(living_target)) //Throw them if we are standing and we didn't somehow just completely obliterate the target
        {
            atom throw_target = get_edge_target_turf(living_target, source.dir) as atom;
            Waiting.NoSleep(living_target.throw_at(throw_target, attack_throw_range, rand(throw_power_min, throw_power_max), source, gentle: non_harmful_throw));
            if (ground_bounce)
            {
                if (isfloorturf(target_turf))
                {
                    turf_open_floor crunched = (turf_open_floor)target_turf;
                    crunched.crush(); // crunch
                }
            }
        }
        else if (ground_bounce) // Just in case our target mob somehow evaporated during this process, we still leave an obliterated tile in their wake
        {
            if (isfloorturf(target_turf))
            {
                turf_open_floor crunched = (turf_open_floor)target_turf;
                crunched.crush(); // again, crunch
            }
        }

        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }
}

// tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:617
public partial class datum_status_effect_organ_set_bonus_strongarm : datum_status_effect_organ_set_bonus
{
    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:624
    public override async DmTask<object> enable_bonus(obj_item_organ inserted_organ = null, object visuals_only = null)
    {
        object dot = await base.enable_bonus(inserted_organ, visuals_only);
        if (!Dm.truthy(dot))
            return dot;
        await owner.AddElement(typeof(datum_element_door_pryer), 6 * SECONDS, obj_item_organ_cyberimp_arm_strongarm.DOAFTER_SOURCE_STRONGARM_INTERACTION); // pry_time, interaction_key
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/cyberimp/augments_arms.dm:630
    public override async DmTask disable_bonus(obj_item_organ removed_organ = null, object visuals_only = null)
    {
        await base.disable_bonus(removed_organ, visuals_only);
        owner.RemoveElement(typeof(datum_element_door_pryer), 6 * SECONDS, obj_item_organ_cyberimp_arm_strongarm.DOAFTER_SOURCE_STRONGARM_INTERACTION);
    }
}
