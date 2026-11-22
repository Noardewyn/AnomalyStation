// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/accessories/_accessories.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the screentip (add_context, a live-partial row): an accessory is pinned onto a jumpsuit, worn with it, drawn on the
// wearer and taken off again (M4a clothing: can_attach_accessory, update_greyscale, try_attach, attach, generate_accessory_overlay,
// detach and the uniform's signal procs on_uniform_equipped, on_uniform_dropped, accessory_equipped, accessory_dropped,
// on_uniform_adjusted, on_uniform_update); J1 (D298): Initialize, the right-click in hand (above or below the suit) and examine.

// tg: code/modules/clothing/under/accessories/_accessories.dm:11
public partial class obj_item_clothing_accessory : obj_item_clothing
{
    // tg: code/modules/clothing/under/accessories/_accessories.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    /**
     * Can we be attached to the passed clothing article?
     */
    // tg: code/modules/clothing/under/accessories/_accessories.dm:39
    public virtual object can_attach_accessory(obj_item_clothing_under attach_to = null, mob_living user = null)
    {
        if (!(attach_to is obj_item_clothing_under))
            throw Dm.CRASH(Dm.text("[] - can_attach_accessory called with an invalid item to attach to. (got: [])", type, attach_to), "code/modules/clothing/under/accessories/_accessories.dm", 41);

        if (Dm.truthy(atom_storage) && Dm.truthy(attach_to.atom_storage))
        {
            if (Dm.truthy(user))
                attach_to.balloon_alert(user, I18n.T("isn't compatible!"));
            return FALSE;
        }

        if (Dm.truthy(attachment_slot) && !Dm.truthy(Dm.bitand(attach_to.body_parts_covered, attachment_slot)))
        {
            if (Dm.truthy(user))
                attach_to.balloon_alert(user, I18n.T("can't attach there!"));
            return FALSE;
        }

        if (length(attach_to.attached_accessories) >= attach_to.max_number_of_accessories)
        {
            if (Dm.truthy(user))
                attach_to.balloon_alert(user, I18n.T("too many accessories!"));
            return FALSE;
        }

        return TRUE;
    }

    // If accessory is being worn, make sure it updates on the player
    // tg: code/modules/clothing/under/accessories/_accessories.dm:61
    public override async DmTask update_greyscale()
    {
        await base.update_greyscale();
        obj_item_clothing_under attached_to = loc as obj_item_clothing_under;
        if (!(attached_to is obj_item_clothing_under))
            return;
        mob_living_carbon_human wearer = attached_to.loc as mob_living_carbon_human;
        if (wearer is mob_living_carbon_human && Dm.eq(wearer.get_item_by_slot(ITEM_SLOT_ICLOTHING), attached_to))
            await wearer.update_clothing(attached_to.slot_flags);
    }

    /**
     * Try to attach this accessory to the passed clothing article.
     *
     * The accessory is not yet within the clothing's loc at this point, this hapens after success.
     */
    // tg: code/modules/clothing/under/accessories/_accessories.dm:75
    [ShouldCallParent]
    public virtual async DmTask<object> try_attach(obj_item_clothing_under attach_to = null, mob_living attacher = null)
    {
        if (Dm.truthy(atom_storage))
        {
            atom_storage.close_all();
            await attach_to.clone_storage(atom_storage);
            await attach_to.atom_storage.set_real_location(this);
            attach_to.atom_storage.do_rustle = TRUE; // it's on the suit now
        }

        float num_other_accessories = LAZYLEN(attach_to.attached_accessories);
        layer = FLOAT_LAYER + clamp(attach_to.max_number_of_accessories - num_other_accessories, 0, 10);
        plane = FLOAT_PLANE;

        if (Dm.truthy(minimize_when_attached))
        {
            transform = Dm.mul(transform, 0.5f);
            pixel_w += 8;
            pixel_z += (-8 + LAZYLEN(attach_to.attached_accessories) * 2);
        }

        RegisterSignal(attach_to, COMSIG_ITEM_EQUIPPED, nameof(on_uniform_equipped));
        RegisterSignal(attach_to, COMSIG_ITEM_DROPPED, nameof(on_uniform_dropped));
        RegisterSignal(attach_to, COMSIG_CLOTHING_UNDER_ADJUSTED, nameof(on_uniform_adjusted));
        RegisterSignal(attach_to, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_uniform_update));

        return TRUE;
    }

    /// Called after try_attach returns TRUE and thus the accessory can be finally be moved into its target
    // tg: code/modules/clothing/under/accessories/_accessories.dm:101
    [ShouldCallParent]
    public virtual async DmTask attach(obj_item_clothing_under attached_to = null)
    {
        LAZYADD(ref attached_to.attached_accessories, this);
        await forceMove(attached_to);

        // Do on-equip effects if we're already equipped
        mob worn_on = attached_to.loc as mob;
        if (worn_on is mob)
            on_uniform_equipped(attached_to, worn_on as mob_living, worn_on.get_slot_by_item(attached_to), update: TRUE);

        if (Dm.truthy(this._listen_lookup?[COMSIG_ACCESSORY_ATTACHED]))
            this._SendSignal(COMSIG_ACCESSORY_ATTACHED, Dm.list(this, attached_to));
        if (Dm.truthy(attached_to._listen_lookup?[COMSIG_CLOTHING_ACCESSORY_ATTACHED]))
            attached_to._SendSignal(COMSIG_CLOTHING_ACCESSORY_ATTACHED, Dm.list(attached_to, this));
    }

    // tg: code/modules/clothing/under/accessories/_accessories.dm:115
    [ShouldCallParent]
    public virtual object generate_accessory_overlay(obj_item_clothing_under attached_to = null)
    {
        mutable_appearance appearance = mutable_appearance(worn_icon, (Dm.truthy(icon_state_is_worn) ? icon_state : (string)worn_icon_state)) as mutable_appearance;
        Dm.append(ref appearance.overlays, worn_overlays(appearance, FALSE, worn_icon)); // we're assuming it's being worn.
        appearance.alpha = alpha;
        appearance.color = color;
        return appearance;
    }

    /**
     * Detach this accessory from the passed clothing article
     *
     * We may have exited the clothing's loc at this point
     */
    // tg: code/modules/clothing/under/accessories/_accessories.dm:128
    [ShouldCallParent]
    public virtual object detach(obj_item_clothing_under detach_from = null, float? update = null)
    {
        update ??= TRUE;

        if (detach_from.atom_storage?.real_location == this)
        {
            // Ensure void items do not stick around
            detach_from.atom_storage.close_all();
            // And clean up the storage we made
            QDEL_NULL(ref detach_from.atom_storage);
        }

        UnregisterSignal(detach_from, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED, COMSIG_CLOTHING_UNDER_ADJUSTED, COMSIG_ATOM_UPDATE_OVERLAYS));
        mob dropped_from = detach_from.loc as mob;
        if (dropped_from is mob)
            on_uniform_dropped(detach_from, dropped_from as mob_living, update: update);

        if (Dm.truthy(this._listen_lookup?[COMSIG_ACCESSORY_DETACHED]))
            this._SendSignal(COMSIG_ACCESSORY_DETACHED, Dm.list(this, detach_from));
        if (Dm.truthy(detach_from._listen_lookup?[COMSIG_CLOTHING_ACCESSORY_DETACHED]))
            detach_from._SendSignal(COMSIG_CLOTHING_ACCESSORY_DETACHED, Dm.list(detach_from, this));

        if (Dm.truthy(minimize_when_attached))
        {
            transform = Dm.mul(transform, 2);
            // Reset our applied offset
            pixel_w = 0;
            pixel_z = 0;
            // just randomize position
            pixel_x = rand(4, -4);
            pixel_y = rand(4, -4);
        }

        layer = Dm.initial(this, (atom p) => p.layer);
        SET_PLANE_IMPLICIT(this, Dm.initial(this, (atom p) => p.plane));
        return TRUE;
    }

    /// Signal proc for [COMSIG_ITEM_EQUIPPED] on the uniform we're pinned to
    // tg: code/modules/clothing/under/accessories/_accessories.dm:159
    [SignalHandler]
    public virtual void on_uniform_equipped(obj_item_clothing_under source = null, mob_living user = null, object slot = null, float? update = null)
    {
        update ??= FALSE;

        if (!Dm.truthy(Dm.bitand(slot, source.slot_flags)))
            return;
        Waiting.NoSleep(accessory_equipped(source, user));
        if (Dm.truthy(update)) // Don't update_clothing twice if we were already attached to our holder
            Waiting.NoSleep(user.update_clothing(source.slot_flags));
    }

    /// Signal proc for [COMSIG_ITEM_DROPPED] on the uniform we're pinned to
    // tg: code/modules/clothing/under/accessories/_accessories.dm:169
    [SignalHandler]
    public virtual void on_uniform_dropped(obj_item_clothing_under source = null, mob_living user = null, float? update = null)
    {
        update ??= FALSE;

        Waiting.NoSleep(accessory_dropped(source, user));
        if (Dm.truthy(update))
            Waiting.NoSleep(user.update_clothing(source.slot_flags));
    }

    /// Called when the uniform this accessory is pinned to is equipped in a valid slot
    // tg: code/modules/clothing/under/accessories/_accessories.dm:177
    public virtual async DmTask accessory_equipped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        await equipped__proc(user, user.get_slot_by_item(clothes)); // so we get any actions, item_flags get set, etc
        foreach (object trait in Dm.each(clothing_traits)) // Accessory don't have slot flags by def, but they still apply clothing traits when the suit is equipped in the right slot.
        {
            // ADD_CLOTHING_TRAIT(user, trait) (__DEFINES/clothing.dm:39), expanded inline (rules 11.1 (d): it reads src)
            ADD_TRAIT(user, trait, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this)));
        }
    }

    /// Called when the uniform this accessory is pinned to is dropped
    // tg: code/modules/clothing/under/accessories/_accessories.dm:183
    public virtual async DmTask accessory_dropped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        await dropped(user); //This handles removing clothing traits from the user by default everytime.
    }

    /// Signal proc for [COMSIG_CLOTHING_UNDER_ADJUSTED] on the uniform we're pinned to
    /// Checks if we can no longer be attached to the uniform, and if so, drops us
    // tg: code/modules/clothing/under/accessories/_accessories.dm:188
    [SignalHandler]
    public virtual void on_uniform_adjusted(obj_item_clothing_under source = null)
    {
        if (Dm.truthy(can_attach_accessory(source)))
            return;

        Waiting.NoSleep(forceMove(source.drop_location() as atom)); //This calls remove_accessory()
        source.visible_message(span_warning(I18n.T("[] falls off of []!", this, source)));
    }

    /// Signal proc for [COMSIG_ATOM_UPDATE_OVERLAYS] on the uniform we're pinned to to add our overlays to the inventory icon
    // tg: code/modules/clothing/under/accessories/_accessories.dm:198
    [SignalHandler]
    public virtual void on_uniform_update(obj_item source = null, DmList overlays = null)
    {
        // overlays += appearance: SHIM-GAP(lead): BYOND's /atom/var/appearance is not in the shim. The atom itself goes in
        // instead; an overlay list turns an atom into the snapshot of its appearance (IDmAppearance), which is what
        // appearance reads.
        Dm.append(ref overlays, this);
    }

    // tg: code/modules/clothing/under/accessories/_accessories.dm:202
    [DmParams("user")]
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
        {
            above_suit = Dm.truthy(above_suit) ? FALSE : TRUE;
            balloon_alert(user, I18n.T("wearing [] suits", Dm.truthy(above_suit) ? I18n.T("above") : I18n.T("below")));
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/clothing/under/accessories/_accessories.dm:211
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("It can be attached to a uniform."));
        Dm.append(ref dot, I18n.T("It can be worn above or below your suit. Right-click to toggle."));
        return dot;
    }
}
