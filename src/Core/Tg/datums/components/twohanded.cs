// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/twohanded.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 5 (windows, grilles, the fire axe): the whole file, the two handed component and its offhand item. The var block
// is generated (D111).
//
// GAP(types): tg's untyped `wieldsound`, `unwieldsound`, `attacksound` and `icon_wielded` (:18, :20, :22, :26) default to
// FALSE, so the generator types them float (V4); tg stores sound files and icon state text in them through Initialize's
// arguments (the fire axe's icon_wielded = "fireaxe1"). The component keeps what tg stores in the *_value fields below.

/**
 * Two Handed Component
 *
 * When applied to an item it will make it two handed
 * Only one of the component can exist on an item.
 */
// tg: code/datums/components/twohanded.dm:7
public partial class datum_component_two_handed : datum_component
{
    /// GAP(types): tg's `var/wieldsound` (:18).
    public object wieldsound_value = FALSE;

    /// GAP(types): tg's `var/unwieldsound` (:20).
    public object unwieldsound_value = FALSE;

    /// GAP(types): tg's `var/attacksound` (:22).
    public object attacksound_value = FALSE;

    /// GAP(types): tg's `var/icon_wielded` (:26).
    public object icon_wielded_value = FALSE;

    /**
     * Two Handed component
     *
     * vars:
     * * require_twohands (optional) Does the item need both hands to be carried
     * * wieldsound (optional) The sound to play when wielded
     * * unwieldsound (optional) The sound to play when unwielded
     * * attacksound (optional) The sound to play when wielded and attacking
     * * force_multiplier (optional) The force multiplier when wielded, do not use with force_wielded, and force_unwielded
     * * force_wielded (optional) The force setting when the item is wielded, do not use with force_multiplier
     * * force_unwielded (optional) The force setting when the item is unwielded, do not use with force_multiplier
     * * icon_wielded (optional) The icon to be used when wielded
     */
    // tg: code/datums/components/twohanded.dm:50
    [DmParams("require_twohands", "wieldsound", "unwieldsound", "attacksound", "force_multiplier", "force_wielded", "force_unwielded", "icon_wielded", "wield_callback", "unwield_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        float? require_twohands = Dm.arg<float?>(__extra, 0);
        object wieldsound = Dm.arg(__extra, 1);
        object unwieldsound = Dm.arg(__extra, 2);
        object attacksound = Dm.arg(__extra, 3);
        float? force_multiplier = Dm.arg<float?>(__extra, 4);
        float? force_wielded = Dm.arg<float?>(__extra, 5);
        float? force_unwielded = Dm.arg<float?>(__extra, 6);
        object icon_wielded = Dm.arg(__extra, 7);
        datum_callback wield_callback = Dm.arg<datum_callback>(__extra, 8);
        datum_callback unwield_callback = Dm.arg<datum_callback>(__extra, 9);
        require_twohands ??= FALSE;
        wieldsound ??= FALSE;
        unwieldsound ??= FALSE;
        attacksound ??= FALSE;
        force_multiplier ??= 1;
        force_wielded ??= 0;
        force_unwielded ??= 0;
        icon_wielded ??= FALSE;
        object dot = await base.Initialize(Dm.forward(__extra, require_twohands, wieldsound, unwieldsound, attacksound, force_multiplier, force_wielded, force_unwielded, icon_wielded, wield_callback, unwield_callback));
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        this.require_twohands = require_twohands;
        this.wieldsound_value = wieldsound;
        this.unwieldsound_value = unwieldsound;
        this.attacksound_value = attacksound;
        this.force_multiplier = force_multiplier;
        this.force_wielded = force_wielded;
        this.force_unwielded = force_unwielded;
        this.icon_wielded_value = icon_wielded;
        this.wield_callback = wield_callback;
        this.unwield_callback = unwield_callback;

        if (Dm.truthy(require_twohands))
            ADD_TRAIT(parent, TRAIT_NEEDS_TWO_HANDS, ABSTRACT_ITEM_TRAIT);
        return dot;
    }

    // tg: code/datums/components/twohanded.dm:79
    public override object Destroy(float? force = null)
    {
        offhand_item = null;
        wield_callback = null;
        unwield_callback = null;
        return base.Destroy(force);
    }

    // Inherit the new values passed to the component
    // tg: code/datums/components/twohanded.dm:86
    [DmParams("new_comp", "original", "require_twohands", "wieldsound", "unwieldsound", "attacksound", "force_multiplier", "force_wielded", "force_unwielded", "icon_wielded", "wield_callback", "unwield_callback")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        object original = i_am_original;
        object require_twohands = Dm.arg(__extra, 0);
        object wieldsound = Dm.arg(__extra, 1);
        object unwieldsound = Dm.arg(__extra, 2);
        object attacksound = Dm.arg(__extra, 3);
        object force_multiplier = Dm.arg(__extra, 4);
        object force_wielded = Dm.arg(__extra, 5);
        object force_unwielded = Dm.arg(__extra, 6);
        object icon_wielded = Dm.arg(__extra, 7);
        datum_callback wield_callback = Dm.arg<datum_callback>(__extra, 8);
        datum_callback unwield_callback = Dm.arg<datum_callback>(__extra, 9);
        if (!Dm.truthy(original))
            return;
        obj_item parent_item = parent as obj_item;
        if (Dm.truthy(wielded))
        {
            if (Dm.truthy(sharpened_increase))
                parent_item.force -= Dm.num(sharpened_increase);
            if (Dm.truthy(force_multiplier))
                parent_item.force /= Dm.num(force_multiplier);
            else if (force_unwielded != null)
                parent_item.force = Dm.num(force_unwielded);
        }
        if (require_twohands != null)
            this.require_twohands = Dm.num_or_null(require_twohands);
        if (Dm.truthy(wieldsound))
            this.wieldsound_value = wieldsound;
        if (Dm.truthy(unwieldsound))
            this.unwieldsound_value = unwieldsound;
        if (Dm.truthy(attacksound))
            this.attacksound_value = attacksound;
        if (Dm.truthy(force_multiplier))
            this.force_multiplier = Dm.num_or_null(force_multiplier);
        if (force_wielded != null)
            this.force_wielded = Dm.num_or_null(force_wielded);
        if (force_unwielded != null)
            this.force_unwielded = Dm.num_or_null(force_unwielded);
        if (Dm.truthy(icon_wielded))
            this.icon_wielded_value = icon_wielded;
        if (Dm.truthy(wield_callback))
            this.wield_callback = wield_callback;
        if (Dm.truthy(unwield_callback))
            this.unwield_callback = unwield_callback;
        if (!Dm.truthy(wielded))
            return;
        if (force_multiplier != null)
            parent_item.force *= Dm.num(force_multiplier);
        else if (force_wielded != null)
            parent_item.force = Dm.num(force_wielded);
        if (sharpened_increase != null)
            parent_item.force += Dm.num(sharpened_increase);
    }

    // register signals withthe parent item
    // tg: code/datums/components/twohanded.dm:140
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_POST_EQUIPPED, nameof(on_equip));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_drop));
        RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(on_attack_self));
        RegisterSignal(parent, COMSIG_ITEM_ATTACK, nameof(on_attack));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_ICON, nameof(on_update_icon));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(parent, COMSIG_ITEM_SHARPEN_ACT, nameof(on_sharpen));
        RegisterSignal(parent, COMSIG_ITEM_APPLY_FANTASY_BONUSES, nameof(apply_fantasy_bonuses));
        RegisterSignal(parent, COMSIG_ITEM_REMOVE_FANTASY_BONUSES, nameof(remove_fantasy_bonuses));
        RegisterSignal(parent, COMSIG_ATOM_FINALIZE_MATERIAL_EFFECTS, nameof(on_materials_updated));
        RegisterSignal(parent, COMSIG_ATOM_FINALIZE_REMOVE_MATERIAL_EFFECTS, nameof(on_materials_updated));
        RegisterSignal(parent, COMSIG_ATOM_SINGLE_MATERIAL_EFFECT_APPLY, nameof(on_material_apply));
        RegisterSignal(parent, COMSIG_ATOM_SINGLE_MATERIAL_EFFECT_REMOVE, nameof(on_material_remove));
    }

    // Remove all siginals registered to the parent item
    // tg: code/datums/components/twohanded.dm:156
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ITEM_POST_EQUIPPED,
            COMSIG_ITEM_DROPPED,
            COMSIG_ITEM_ATTACK_SELF,
            COMSIG_ITEM_ATTACK,
            COMSIG_ATOM_UPDATE_ICON,
            COMSIG_MOVABLE_MOVED,
            COMSIG_ITEM_SHARPEN_ACT,
            COMSIG_ITEM_APPLY_FANTASY_BONUSES,
            COMSIG_ITEM_REMOVE_FANTASY_BONUSES,
            COMSIG_ATOM_FINALIZE_MATERIAL_EFFECTS,
            COMSIG_ATOM_FINALIZE_REMOVE_MATERIAL_EFFECTS));
    }

    /// Triggered on equip of the item containing the component
    // tg: code/datums/components/twohanded.dm:172
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob user = null, object slot = null)
    {
        if (HAS_TRAIT(parent, TRAIT_NEEDS_TWO_HANDS) && Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS))) // force equip the item
            Waiting.NoSleep(wield(user));
        if (!Dm.truthy(user.is_holding(parent as obj_item)) && Dm.truthy(wielded) && !HAS_TRAIT(parent, TRAIT_NEEDS_TWO_HANDS))
            Waiting.NoSleep(unwield(user));
    }

    /// Triggered on drop of item containing the component
    // tg: code/datums/components/twohanded.dm:181
    [SignalHandler]
    public virtual void on_drop(datum source = null, mob user = null)
    {
        if (HAS_TRAIT(parent, TRAIT_NEEDS_TWO_HANDS)) //Don't let the item fall to the ground and cause bugs if it's actually being equipped on another slot.
            Waiting.NoSleep(unwield(user, FALSE, FALSE));
        if (Dm.truthy(wielded))
            Waiting.NoSleep(unwield(user));
        if (source == offhand_item && !QDELETED(source))
        {
            offhand_item = null;
            qdel(source);
        }
    }

    /// Triggered on destroy of the component's offhand
    // tg: code/datums/components/twohanded.dm:193
    [SignalHandler]
    public virtual void on_destroy(datum source = null)
    {
        offhand_item = null;
    }

    /// Triggered on attack self of the item containing the component
    // tg: code/datums/components/twohanded.dm:198
    [SignalHandler]
    public virtual void on_attack_self(datum source = null, mob user = null)
    {
        if (!HAS_TRAIT(parent, TRAIT_NEEDS_TWO_HANDS))
        {
            if (Dm.truthy(wielded))
                Waiting.NoSleep(unwield(user));
            else if (Dm.truthy(user.is_holding(parent as obj_item)))
                Waiting.NoSleep(wield(user));
        }
    }

    /**
     * Wield the two handed item in both hands
     *
     * vars:
     * * user The mob/living/carbon that is wielding the item
     */
    // tg: code/datums/components/twohanded.dm:213
    public virtual async DmTask<object> wield(object user = null)
    {
        mob_living_carbon carbon_user = user as mob_living_carbon;
        mob as_mob = user as mob;
        if (Dm.truthy(wielded))
            return null;

        atom atom_parent = parent as atom;
        if (HAS_TRAIT(user, TRAIT_NO_TWOHANDING))
        {
            if (HAS_TRAIT(parent, TRAIT_NEEDS_TWO_HANDS))
            {
                atom_parent.balloon_alert(as_mob, I18n.T("can't wield!"));
                await as_mob.dropItemToGround(parent as obj_item, force: TRUE);
            }
            else
                atom_parent.balloon_alert(as_mob, I18n.T("can't wield with both hands!"));
            return COMPONENT_EQUIPPED_FAILED;
        }
        if (Dm.truthy(as_mob.get_inactive_held_item()))
        {
            if (HAS_TRAIT(parent, TRAIT_NEEDS_TWO_HANDS))
            {
                atom_parent.balloon_alert(as_mob, I18n.T("can't carry in one hand!"));
                await as_mob.dropItemToGround(parent as obj_item, force: TRUE);
            }
            else
                atom_parent.balloon_alert(as_mob, I18n.T("holding something in other hand!"));
            return COMPONENT_EQUIPPED_FAILED;
        }
        if (Dm.lt(carbon_user.usable_hands, 2))
        {
            if (HAS_TRAIT(parent, TRAIT_NEEDS_TWO_HANDS))
                await as_mob.dropItemToGround(parent as obj_item, force: TRUE);
            atom_parent.balloon_alert(as_mob, I18n.T("not enough hands!"));
            return COMPONENT_EQUIPPED_FAILED;
        }

        // wield update status
        if (Dm.truthy(Dm.bitand((!Dm.truthy(parent._listen_lookup?[COMSIG_TWOHANDED_WIELD]) ? NONE : parent._SendSignal(COMSIG_TWOHANDED_WIELD, Dm.list(parent, user))), COMPONENT_TWOHANDED_BLOCK_WIELD)))
        {
            await as_mob.dropItemToGround(parent as obj_item, force: TRUE);
            return COMPONENT_EQUIPPED_FAILED; // blocked wield from item
        }
        if (Dm.truthy(Dm.bitand(wield_callback != null ? await wield_callback.Invoke(parent, user) : null, COMPONENT_TWOHANDED_BLOCK_WIELD)))
            return null;
        wielded = TRUE;
        ADD_TRAIT(parent, TRAIT_WIELDED, REF(this));
        RegisterSignal(as_mob, COMSIG_MOB_SWAPPING_HANDS, nameof(on_swapping_hands));

        // update item stats and name
        obj_item parent_item = parent as obj_item;
        if (!Dm.eq(force_multiplier, 1))
            parent_item.force *= Dm.num(force_multiplier);
        else if (!Dm.eq(force_wielded, force_unwielded))
            parent_item.force = Dm.num(force_wielded);
        if (Dm.truthy(sharpened_increase))
            parent_item.force += Dm.num(sharpened_increase);
        parent_item.name = Dm.text("[] (Wielded)", parent_item.name);
        parent_item.update_appearance();

        if (iscyborg(user))
            to_chat(user, span_notice(I18n.T("You dedicate your module to [].", parent)));
        else
            to_chat(user, span_notice(I18n.T("You grab [] with both hands.", parent)));

        // Play sound if one is set
        if (Dm.truthy(wieldsound_value))
            playsound(parent_item.loc, wieldsound_value, 50, TRUE);

        // Let's reserve the other hand
        offhand_item = Dm.@new<obj_item_offhand>(user);
        offhand_item.name = Dm.text("[] - offhand", parent_item.name);
        offhand_item.desc = Dm.text("Your second grip on [].", parent_item);
        offhand_item.wielded = TRUE;
        RegisterSignal(offhand_item, COMSIG_ITEM_DROPPED, nameof(on_drop));
        RegisterSignal(offhand_item, COMSIG_QDELETING, nameof(on_destroy));
        await as_mob.put_in_inactive_hand(offhand_item);
        return null;
    }

    /**
     * Unwield the two handed item
     *
     * vars:
     * * user The mob/living/carbon that is unwielding the item
     * * show_message (option) show a message to chat on unwield
     * * can_drop (option) whether 'dropItemToGround' can be called or not.
     */
    // tg: code/datums/components/twohanded.dm:285
    public virtual async DmTask unwield(object user = null, float? show_message = null, float? can_drop = null)
    {
        show_message ??= TRUE;
        can_drop ??= TRUE;
        if (!Dm.truthy(wielded))
            return;

        // wield update status
        wielded = FALSE;
        UnregisterSignal(user as datum, COMSIG_MOB_SWAPPING_HANDS);
        if (Dm.truthy(parent._listen_lookup?[COMSIG_TWOHANDED_UNWIELD]))
            parent._SendSignal(COMSIG_TWOHANDED_UNWIELD, Dm.list(parent, user));
        REMOVE_TRAIT(parent, TRAIT_WIELDED, REF(this));
        if (unwield_callback != null)
            await unwield_callback.Invoke(parent, user);

        // update item stats
        obj_item parent_item = parent as obj_item;
        if (Dm.truthy(sharpened_increase))
            parent_item.force -= Dm.num(sharpened_increase);
        if (!Dm.eq(force_multiplier, 1))
            parent_item.force /= Dm.num(force_multiplier);
        else if (!Dm.eq(force_unwielded, force_wielded))
            parent_item.force = Dm.num(force_unwielded);

        // update the items name to remove the wielded status
        float sf = findtext(parent_item.name, " (Wielded)", -10); // 10 == length(" (Wielded)")
        if (Dm.truthy(sf))
            parent_item.name = copytext(parent_item.name, 1, sf);
        else
            parent_item.name = Dm.text("[]", Dm.initial(parent_item, (obj_item p) => p.name));

        // Update icons
        parent_item.update_appearance();

        if (istype(user, typeof(mob_living_carbon))) // tk showed that we might not have a mob here
        {
            mob_living_carbon carbon_user = (mob_living_carbon)user;
            if (carbon_user.get_item_by_slot(ITEM_SLOT_BACK) == parent)
                await carbon_user.update_worn_back();
            else
                await carbon_user.update_held_items();

            // if the item requires two handed drop the item on unwield
            if (HAS_TRAIT(parent, TRAIT_NEEDS_TWO_HANDS) && Dm.truthy(can_drop))
                await carbon_user.dropItemToGround(parent as obj_item, force: TRUE);

            // Show message if requested
            if (Dm.truthy(show_message))
            {
                if (iscyborg(user))
                    to_chat(user, span_notice(I18n.T("You free up your module.")));
                else if (HAS_TRAIT(parent, TRAIT_NEEDS_TWO_HANDS))
                    to_chat(user, span_notice(I18n.T("You drop [].", parent)));
                else
                    to_chat(user, span_notice(I18n.T("You are now carrying [] with one hand.", parent)));
            }
        }

        // Play sound if set
        if (Dm.truthy(unwieldsound_value))
            playsound(parent_item.loc, unwieldsound_value, 50, TRUE);

        // Remove the object in the offhand
        if (offhand_item != null)
        {
            UnregisterSignal(offhand_item, Dm.list(COMSIG_ITEM_DROPPED, COMSIG_QDELETING));
            qdel(offhand_item);
        }
        // Clear any old refrence to an item that should be gone now
        offhand_item = null;
    }

    /**
     * on_attack triggers on attack with the parent item
     */
    // tg: code/datums/components/twohanded.dm:348
    [SignalHandler]
    public virtual void on_attack(obj_item source = null, mob_living target = null, mob_living user = null)
    {
        if (Dm.truthy(wielded) && Dm.truthy(attacksound_value))
        {
            obj_item parent_item = parent as obj_item;
            playsound(parent_item.loc, attacksound_value, 50, TRUE);
        }
    }

    /**
     * on_update_icon triggers on call to update parent items icon
     *
     * Updates the icon using icon_wielded if set
     */
    // tg: code/datums/components/twohanded.dm:359
    [SignalHandler]
    public virtual object on_update_icon(obj_item source = null)
    {
        if (!Dm.truthy(wielded))
            return NONE;
        if (!Dm.truthy(icon_wielded_value))
            return NONE;
        source.icon_state = Dm.str(icon_wielded_value);
        return COMSIG_ATOM_NO_UPDATE_ICON_STATE;
    }

    /**
     * on_moved Triggers on item moved
     */
    // tg: code/datums/components/twohanded.dm:371
    [SignalHandler]
    public virtual void on_moved(datum source = null, mob user = null, object dir = null)
    {
        Waiting.NoSleep(unwield(user, can_drop: FALSE));
    }

    /**
     * on_swap_hands Triggers on swapping hands, blocks swap if the other hand is busy
     */
    // tg: code/datums/components/twohanded.dm:379
    [SignalHandler]
    public virtual object on_swapping_hands(mob user = null, obj_item held_item = null)
    {
        if (held_item == null)
            return null;
        if (held_item == parent)
            return COMPONENT_BLOCK_SWAP;
        return null;
    }

    /**
     * on_sharpen Triggers on usage of a sharpening stone on the item
     */
    // tg: code/datums/components/twohanded.dm:390
    [SignalHandler]
    public virtual object on_sharpen(obj_item item = null, object amount = null, object max_amount = null)
    {
        if (item == null)
            return COMPONENT_BLOCK_SHARPEN_BLOCKED;
        if (Dm.truthy(sharpened_increase))
            return COMPONENT_BLOCK_SHARPEN_ALREADY;
        float wielded_val = 0;
        if (Dm.truthy(force_multiplier))
        {
            obj_item parent_item = parent as obj_item;
            if (Dm.truthy(wielded))
                wielded_val = parent_item.force;
            else
                wielded_val = parent_item.force * Dm.num(force_multiplier);
        }
        else
            wielded_val = Dm.num(force_wielded);
        if (Dm.gt(wielded_val, max_amount))
            return COMPONENT_BLOCK_SHARPEN_MAXED;
        sharpened_increase = min(Dm.num(amount), Dm.num(max_amount) - wielded_val);
        return COMPONENT_BLOCK_SHARPEN_APPLIED;
    }

    // tg: code/datums/components/twohanded.dm:411
    [SignalHandler]
    public virtual void apply_fantasy_bonuses(obj_item source = null, object bonus = null)
    {
        force_wielded = Dm.num_or_null(source.modify_fantasy_variable("force_wielded", force_wielded, bonus));
        force_unwielded = Dm.num_or_null(source.modify_fantasy_variable("force_unwielded", force_unwielded, bonus));
        if (Dm.truthy(wielded) && ismob(source.loc))
            Waiting.NoSleep(unwield(source.loc));
        if (Dm.truthy(force_multiplier))
            force_multiplier = Dm.num_or_null(source.modify_fantasy_variable("force_multiplier", force_multiplier, Dm.div(bonus, 10), minimum: 1));
    }

    // tg: code/datums/components/twohanded.dm:420
    [SignalHandler]
    public virtual void remove_fantasy_bonuses(obj_item source = null, object bonus = null)
    {
        force_wielded = Dm.num_or_null(source.reset_fantasy_variable("force_wielded", force_wielded));
        force_unwielded = Dm.num_or_null(source.reset_fantasy_variable("force_unwielded", force_unwielded));
        if (Dm.truthy(wielded) && ismob(source.loc))
            Waiting.NoSleep(unwield(source.loc));
        force_multiplier = Dm.num_or_null(source.reset_fantasy_variable("force_multiplier", force_multiplier));
    }

    // tg: code/datums/components/twohanded.dm:428
    [SignalHandler]
    public virtual void on_materials_updated(obj_item source = null, DmList materials = null, datum_material main_material = null)
    {
        // With materials assigned we need to update our forces.
        if (Dm.truthy(wielded))
            force_wielded = source.force;
        else
            force_unwielded = source.force;
    }

    // tg: code/datums/components/twohanded.dm:436
    [SignalHandler]
    public virtual void on_material_apply(obj_item source = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        // Opposite state's force needs to be calculated for each material's effect
        if (Dm.truthy(wielded))
            force_unwielded *= GET_MATERIAL_MODIFIER(source.get_material_force_modifier(material, source.sharpness), multiplier);
        else
            force_wielded *= GET_MATERIAL_MODIFIER(source.get_material_force_modifier(material, source.sharpness), multiplier);
    }

    // tg: code/datums/components/twohanded.dm:444
    [SignalHandler]
    public virtual void on_material_remove(obj_item source = null, datum_material material = null, object amount = null, object multiplier = null)
    {
        // Same as appliation but inversed
        if (Dm.truthy(wielded))
            force_unwielded /= GET_MATERIAL_MODIFIER(source.get_material_force_modifier(material, source.sharpness), multiplier);
        else
            force_wielded /= GET_MATERIAL_MODIFIER(source.get_material_force_modifier(material, source.sharpness), multiplier);
    }
}

/**
 * The offhand dummy item for two handed items
 */
// tg: code/datums/components/twohanded.dm:455
public partial class obj_item_offhand : obj_item
{
    // tg: code/datums/components/twohanded.dm:464
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, ABSTRACT_ITEM_TRAIT);
        return dot;
    }

    // tg: code/datums/components/twohanded.dm:468
    public override object Destroy(float? force = null)
    {
        wielded = FALSE;
        return base.Destroy(force);
    }

    // tg: code/datums/components/twohanded.dm:472
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(wielded) && !Dm.truthy(user.is_holding(this)) && !QDELETED(this))
            qdel(this);
    }
}
