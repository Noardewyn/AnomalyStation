// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/transforming.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice E (the telescopic baton): the whole file but for the detective scan note (the detective scanner is M10: a deferred
// leaf). The var block is generated (D111).

/*
 * Transforming weapon component. For weapons that swap between states.
 * For example: Energy swords, cleaving saws, switch blades.
 *
 * Used to easily make an item that can be attack_self'd to gain force or change mode.
 *
 * Only values passed on initialize will update when the item is activated (except the icon_state).
 * The icon_state of the item will swap between "[icon_state]" and "[icon_state]_on".
 */
// tg: code/datums/components/transforming.dm:10
public partial class datum_component_transforming : datum_component
{
    // tg: code/datums/components/transforming.dm:56
    [DmParams("start_transformed", "transform_cooldown_time", "force_on", "throwforce_on", "throw_speed_on", "sharpness_on", "hitsound_on", "w_class_on", "clumsy_check", "clumsy_damage", "attack_verb_continuous_on", "attack_verb_simple_on", "inhand_icon_change")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object start_transformed = Dm.arg(__extra, 0) ?? FALSE;
        object transform_cooldown_time = Dm.arg(__extra, 1) ?? 0;
        object force_on = Dm.arg(__extra, 2) ?? 0;
        object throwforce_on = Dm.arg(__extra, 3) ?? 0;
        object throw_speed_on = Dm.arg(__extra, 4) ?? 2;
        object sharpness_on = Dm.arg(__extra, 5) ?? NONE;
        object hitsound_on = Dm.arg(__extra, 6) ?? Dm.resource("sound/items/weapons/blade1.ogg");
        object w_class_on = Dm.arg(__extra, 7) ?? WEIGHT_CLASS_BULKY;
        object clumsy_check = Dm.arg(__extra, 8) ?? TRUE;
        object clumsy_damage = Dm.arg(__extra, 9) ?? 10;
        DmList attack_verb_continuous_on = Dm.arg<DmList>(__extra, 10);
        DmList attack_verb_simple_on = Dm.arg<DmList>(__extra, 11);
        object inhand_icon_change = Dm.arg(__extra, 12) ?? TRUE;
        object dot = await base.Initialize(Dm.forward(__extra, start_transformed, transform_cooldown_time, force_on, throwforce_on, throw_speed_on, sharpness_on, hitsound_on, w_class_on, clumsy_check, clumsy_damage, attack_verb_continuous_on, attack_verb_simple_on, inhand_icon_change));

        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        obj_item item_parent = parent as obj_item;

        this.transform_cooldown_time = transform_cooldown_time;
        this.force_on = force_on;
        this.throwforce_on = throwforce_on;
        this.throw_speed_on = throw_speed_on;
        this.sharpness_on = sharpness_on;
        this.hitsound_on = hitsound_on;
        this.w_class_on = w_class_on;
        this.clumsy_check = clumsy_check;
        this.clumsy_damage = clumsy_damage;
        this.inhand_icon_change = Dm.num(inhand_icon_change);

        this.force_off = item_parent.force;
        this.throwforce_off = item_parent.throwforce;
        this.throw_speed_off = item_parent.throw_speed;
        this.sharpness_off = item_parent.sharpness; // Raw value, not via the getter
        this.w_class_off = item_parent.w_class;

        if (attack_verb_continuous_on != null)
        {
            this.attack_verb_continuous_on = attack_verb_continuous_on;
            attack_verb_continuous_off = item_parent.attack_verb_continuous;
        }
        if (attack_verb_simple_on != null)
        {
            this.attack_verb_simple_on = attack_verb_simple_on;
            attack_verb_simple_off = item_parent.attack_verb_simple;
        }

        if (Dm.truthy(start_transformed))
            toggle_active(parent as obj_item);
        return dot;
    }

    // tg: code/datums/components/transforming.dm:104
    public override async DmTask RegisterWithParent()
    {
        obj_item item_parent = parent as obj_item;

        RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(on_attack_self));
        if (Dm.truthy(item_parent.sharpness) || Dm.truthy(sharpness_on))
            RegisterSignal(parent, COMSIG_ITEM_SHARPEN_ACT, nameof(on_sharpen));

        RegisterSignal(parent, COMSIG_DETECTIVE_SCANNED, nameof(on_scan));
        RegisterSignal(parent, COMSIG_ITEM_APPLY_FANTASY_BONUSES, nameof(apply_fantasy_bonuses));
        RegisterSignal(parent, COMSIG_ITEM_REMOVE_FANTASY_BONUSES, nameof(remove_fantasy_bonuses));
        RegisterSignal(parent, COMSIG_ATOM_FINALIZE_MATERIAL_EFFECTS, nameof(on_materials_updated));
        RegisterSignal(parent, COMSIG_ATOM_FINALIZE_REMOVE_MATERIAL_EFFECTS, nameof(on_materials_updated));
        RegisterSignal(parent, COMSIG_ATOM_SINGLE_MATERIAL_EFFECT_APPLY, nameof(on_material_apply));
        RegisterSignal(parent, COMSIG_ATOM_SINGLE_MATERIAL_EFFECT_REMOVE, nameof(on_material_remove));
    }

    // tg: code/datums/components/transforming.dm:119
    [SignalHandler]
    public virtual void apply_fantasy_bonuses(obj_item source = null, object bonus = null)
    {
        active = FALSE;
        set_inactive(source);
        force_on = Dm.num_or_null(source.modify_fantasy_variable("force_on", force_on, bonus));
        throwforce_on = Dm.num_or_null(source.modify_fantasy_variable("throwforce_on", throwforce_on, bonus));
    }

    // tg: code/datums/components/transforming.dm:126
    [SignalHandler]
    public virtual void remove_fantasy_bonuses(obj_item source = null, object bonus = null)
    {
        active = FALSE;
        set_inactive(source);
        force_on = Dm.num_or_null(source.reset_fantasy_variable("force_on", force_on));
        throwforce_on = Dm.num_or_null(source.reset_fantasy_variable("throwforce_on", throwforce_on));
    }

    // tg: code/datums/components/transforming.dm:133
    [SignalHandler]
    public virtual void on_material_apply(obj_item source = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        // Opposite state's force needs to be calculated for each material's effect
        if (Dm.truthy(active))
        {
            force_off = Dm.num(force_off) * GET_MATERIAL_MODIFIER(source.get_material_force_modifier(material, Dm.initial(source, (obj_item p) => p.sharpness)), multiplier);
            throwforce_off = Dm.num(throwforce_off) * GET_MATERIAL_MODIFIER(source.get_material_throwforce_modifier(material, Dm.initial(source, (obj_item p) => p.sharpness)), multiplier);
        }
        else
        {
            force_on = Dm.num(force_on) * GET_MATERIAL_MODIFIER(source.get_material_force_modifier(material, sharpness_on), multiplier);
            throwforce_on = Dm.num(throwforce_on) * GET_MATERIAL_MODIFIER(source.get_material_throwforce_modifier(material, sharpness_on), multiplier);
        }
    }

    // tg: code/datums/components/transforming.dm:143
    [SignalHandler]
    public virtual void on_material_remove(obj_item source = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        // Same as appliation but inversed
        if (Dm.truthy(active))
        {
            force_off = Dm.num(force_off) / GET_MATERIAL_MODIFIER(source.get_material_force_modifier(material, Dm.initial(source, (obj_item p) => p.sharpness)), multiplier);
            throwforce_off = Dm.num(throwforce_off) / GET_MATERIAL_MODIFIER(source.get_material_throwforce_modifier(material, Dm.initial(source, (obj_item p) => p.sharpness)), multiplier);
        }
        else
        {
            force_on = Dm.num(force_on) / GET_MATERIAL_MODIFIER(source.get_material_force_modifier(material, sharpness_on), multiplier);
            throwforce_on = Dm.num(throwforce_on) / GET_MATERIAL_MODIFIER(source.get_material_throwforce_modifier(material, sharpness_on), multiplier);
        }
    }

    // tg: code/datums/components/transforming.dm:153
    [SignalHandler]
    public virtual void on_materials_updated(obj_item source = null, DmList materials = null, datum_material main_material = null)
    {
        // Current force can be set directly
        if (Dm.truthy(active))
        {
            force_on = source.force;
            throwforce_on = source.throwforce;
        }
        else
        {
            force_off = source.force;
            throwforce_off = source.throwforce;
        }
    }

    // tg: code/datums/components/transforming.dm:163
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_ATTACK_SELF, COMSIG_ITEM_SHARPEN_ACT, COMSIG_DETECTIVE_SCANNED, COMSIG_ATOM_FINALIZE_MATERIAL_EFFECTS, COMSIG_ATOM_FINALIZE_REMOVE_MATERIAL_EFFECTS));
    }

    // tg: code/datums/components/transforming.dm:166
    [SignalHandler]
    public virtual void on_scan(datum source = null, mob user = null, object entry = null)
    {
        // entry.add_data_entry(DETSCAN_CATEGORY_NOTES, "Readings suggest some form of state changing."): the detective scanner
        // (/datum/detective_scanner_log) is not translated
        Dm.Deferred("code/datums/components/transforming.dm:169", "on_scan: entry.add_data_entry(DETSCAN_CATEGORY_NOTES, ...), the detective scanner's note on a transforming item (the detective scanner, M10+)");
    }

    /*
     * Called on [COMSIG_ITEM_ATTACK_SELF].
     *
     * Check if we can transform our weapon, and if so, call [do_transform].
     * Sends signal [COMSIG_TRANSFORMING_PRE_TRANSFORM], and stops the transform action if it returns [COMPONENT_BLOCK_TRANSFORM].
     * And, if [do_transform] was successful, do a clumsy effect from [clumsy_transform_effect].
     *
     * source - source of the signal, the item being transformed / parent
     * user - the mob transforming the weapon
     */
    // tg: code/datums/components/transforming.dm:181
    [SignalHandler]
    public virtual object on_attack_self(obj_item source = null, mob user = null)
    {
        if (!(transform_cooldown <= world.time))
        {
            to_chat(user, span_warning(I18n.T("Wait a bit before trying to use [] again!", source)));
            return null;
        }

        if (Dm.truthy(Dm.bitand(!Dm.truthy(source._listen_lookup?[COMSIG_TRANSFORMING_PRE_TRANSFORM]) ? (object)NONE : source._SendSignal(COMSIG_TRANSFORMING_PRE_TRANSFORM, Dm.list(source, user, active)), COMPONENT_BLOCK_TRANSFORM)))
            return null;

        if (Dm.truthy(do_transform(source, user)))
        {
            clumsy_transform_effect(user as mob_living);
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        return null;
    }

    /*
     * Transform the weapon into its alternate form, calling [toggle_active].
     *
     * Sends signal [COMSIG_TRANSFORMING_ON_TRANSFORM], and calls [default_transform_message] if it does not return [COMPONENT_NO_DEFAULT_MESSAGE].
     * Also starts the [transform_cooldown] if we have a set [transform_cooldown_time].
     *
     * source - the item being transformed / parent
     * user - the mob transforming the item (can be null)
     *
     * returns TRUE.
     */
    // tg: code/datums/components/transforming.dm:206
    public virtual object do_transform(obj_item source = null, mob user = null)
    {
        toggle_active(source);
        if (!Dm.truthy(Dm.bitand(!Dm.truthy(source._listen_lookup?[COMSIG_TRANSFORMING_ON_TRANSFORM]) ? (object)NONE : source._SendSignal(COMSIG_TRANSFORMING_ON_TRANSFORM, Dm.list(source, user, active)), COMPONENT_NO_DEFAULT_MESSAGE)))
            default_transform_message(source, user);
        if (user != null && Dm.truthy(user._listen_lookup?[COMSIG_MOB_TRANSFORMING_ITEM]))
            user._SendSignal(COMSIG_MOB_TRANSFORMING_ITEM, Dm.list(user, source, active));
        if (isnum(transform_cooldown_time))
            transform_cooldown = world.time + Dm.num(transform_cooldown_time);
        if (user != null)
            source.add_fingerprint(user);
        return TRUE;
    }

    /*
     * The default feedback message and sound effect for an item transforming.
     *
     * source - the item being transformed / parent
     * user - the mob transforming the item
     */
    // tg: code/datums/components/transforming.dm:224
    public virtual void default_transform_message(obj_item source = null, mob user = null)
    {
        if (user != null)
            source.balloon_alert(user, I18n.T("[] []", Dm.truthy(active) ? "enabled" : "disabled", source));
        playsound(source, Dm.resource("sound/items/weapons/batonextend.ogg"), 50, TRUE);
    }

    /*
     * Toggle active between true and false, and call
     * either set_active or set_inactive depending on whichever state is toggled.
     *
     * source - the item being transformed / parent
     */
    // tg: code/datums/components/transforming.dm:235
    public virtual void toggle_active(obj_item source = null)
    {
        active = Dm.truthy(active) ? FALSE : TRUE;
        if (Dm.truthy(active))
            set_active(source);
        else
            set_inactive(source);
    }

    /*
     * Set our transformed item into its active state.
     * Updates all the values that were passed from init and the icon_state.
     *
     * source - the item being transformed / parent
     */
    // tg: code/datums/components/transforming.dm:248
    public virtual void set_active(obj_item source = null)
    {
        ADD_TRAIT(source, TRAIT_TRANSFORM_ACTIVE, REF(this));
        if (sharpness_on != null)
            source.sharpness = Dm.num(sharpness_on);
        if (force_on != null)
            source.force = Dm.num(force_on);
        if (throwforce_on != null)
            source.throwforce = Dm.num(throwforce_on);
        if (throw_speed_on != null)
            source.throw_speed = Dm.num(throw_speed_on);

        if (Dm.truthy(LAZYLEN(attack_verb_continuous_on)))
            source.attack_verb_continuous = attack_verb_continuous_on;
        if (Dm.truthy(LAZYLEN(attack_verb_simple_on)))
            source.attack_verb_simple = attack_verb_simple_on;

        source.hitsound = hitsound_on;
        source.update_weight_class(w_class_on);
        source.icon_state = Dm.text("[]_on", source.icon_state);
        if (Dm.truthy(inhand_icon_change) && Dm.truthy(source.inhand_icon_state))
            source.inhand_icon_state = Dm.text("[]_on", source.inhand_icon_state);
        source.update_appearance();
        Waiting.NoSleep(source.update_inhand_icon());
    }

    /*
     * Set our transformed item into its inactive state.
     * Updates all the values back to the item's initial values.
     *
     * source - the item being un-transformed / parent
     */
    // tg: code/datums/components/transforming.dm:278
    public virtual void set_inactive(obj_item source = null)
    {
        REMOVE_TRAIT(source, TRAIT_TRANSFORM_ACTIVE, REF(this));
        if (sharpness_on != null)
            source.sharpness = Dm.num(sharpness_off);
        if (force_on != null)
            source.force = Dm.num(force_off);
        if (throwforce_on != null)
            source.throwforce = Dm.num(throwforce_off);
        if (throw_speed_on != null)
            source.throw_speed = Dm.num(throw_speed_off);

        if (Dm.truthy(LAZYLEN(attack_verb_continuous_on)))
            source.attack_verb_continuous = attack_verb_continuous_off;
        if (Dm.truthy(LAZYLEN(attack_verb_simple_off)))
            source.attack_verb_simple = attack_verb_simple_off;

        source.hitsound = Dm.initial(source, (obj_item p) => p.hitsound);
        source.update_weight_class(w_class_off);
        source.icon_state = Dm.initial(source, (obj_item p) => p.icon_state);
        source.inhand_icon_state = Dm.initial(source, (obj_item p) => p.inhand_icon_state);
        source.update_appearance();
        Waiting.NoSleep(source.update_inhand_icon());
    }

    /*
     * If [clumsy_check] is set to TRUE, attempt to cause a side effect for clumsy people activating this item.
     * Called after the transform is done, meaning [active] var has already updated.
     *
     * user - the clumsy mob, transforming our item (parent)
     *
     * Returns TRUE if side effects happened, FALSE otherwise
     */
    // tg: code/datums/components/transforming.dm:309
    public virtual object clumsy_transform_effect(mob_living user = null)
    {
        if (!Dm.truthy(clumsy_check))
            return FALSE;

        if (user == null || !HAS_TRAIT(user, TRAIT_CLUMSY))
            return FALSE;

        if (Dm.truthy(active) && prob(50))
        {
            object hurt_self_verb_simple = Dm.truthy(LAZYLEN(attack_verb_simple_on)) ? pick(attack_verb_simple_on) : "hit";
            object hurt_self_verb_continuous = Dm.truthy(LAZYLEN(attack_verb_continuous_on)) ? pick(attack_verb_continuous_on) : "hits";
            user.visible_message(
                span_warning(I18n.T("[] triggers [] while holding it backwards and [] themself, like a doofus!", user, parent, hurt_self_verb_continuous)),
                span_warning(I18n.T("You trigger [] while holding it backwards and [] yourself, like a doofus!", parent, hurt_self_verb_simple))
            );
            obj_item item_parent = parent as obj_item;
            object sw__1 = item_parent.damtype;
            if (Dm.eq(sw__1, STAMINA))
                Waiting.NoSleep(user.adjust_stamina_loss(Dm.num(clumsy_damage)));
            else if (Dm.eq(sw__1, OXY))
                Waiting.NoSleep(user.adjust_oxy_loss(Dm.num(clumsy_damage)));
            else if (Dm.eq(sw__1, TOX))
                Waiting.NoSleep(user.adjust_tox_loss(Dm.num(clumsy_damage)));
            else if (Dm.eq(sw__1, BRUTE))
                Waiting.NoSleep(user.take_bodypart_damage(brute: Dm.num(clumsy_damage)));
            else if (Dm.eq(sw__1, BURN))
                Waiting.NoSleep(user.take_bodypart_damage(burn: Dm.num(clumsy_damage)));

            return TRUE;
        }

        return FALSE;
    }

    /*
     * Called on [COMSIG_ITEM_SHARPEN_ACT].
     * We need to track our sharpened bonus here, so we correctly apply and unapply it
     * if our item's sharpness state changes from transforming.
     *
     * source - the item being sharpened / parent
     * increment - the amount of force added
     * max - the maximum force that the item can be adjusted to.
     *
     * Does not return naturally [COMPONENT_BLOCK_SHARPEN_APPLIED] as this is only to track our sharpened bonus between transformation.
     */
    // tg: code/datums/components/transforming.dm:351
    [SignalHandler]
    public virtual object on_sharpen(obj_item source = null, object increment = null, object max = null)
    {
        if (Dm.truthy(sharpened_bonus))
            return COMPONENT_BLOCK_SHARPEN_ALREADY;
        if (Dm.num(force_on) + Dm.num(increment) > Dm.num(max))
            return COMPONENT_BLOCK_SHARPEN_MAXED;
        sharpened_bonus = Dm.num(increment);
        force_on = Dm.num(force_on) + sharpened_bonus;
        throwforce_on = Dm.num(throwforce_on) + sharpened_bonus;
        force_off = Dm.num(force_off) + sharpened_bonus;
        throwforce_off = Dm.num(throwforce_off) + sharpened_bonus;
        // Mimics base whetstone effect for the on state
        sharpness_on = SHARP_EDGED;
        if (!Dm.truthy(active))
            return COMPONENT_BLOCK_SHARPEN_SHARPNESS;
        return null;
    }
}
