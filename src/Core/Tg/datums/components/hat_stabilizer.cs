// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/hat_stabilizer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. A hat worn on top of a helmet (the MOD helmet and its hat stabilizer module): a head item
// used on the helmet is put on it and drawn over it, tilted if it sits loose, and adds its cover flags and clothing traits; an
// empty right-click takes it off; a loose hat falls off when the wearer slips, is slapped or thrown, and drops when they hit the
// floor. The var block is generated (D111).

/// Allows players to place hats on the atom this is attached to
// tg: code/datums/components/hat_stabilizer.dm:2
public partial class datum_component_hat_stabilizer : datum_component
{
    // tg: code/datums/components/hat_stabilizer.dm:18
    [DmParams("use_worn_icon", "pixel_z_offset", "loose_hat")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object use_worn_icon = Dm.arg(__extra, 0) ?? FALSE;
        object pixel_z_offset = Dm.arg(__extra, 1) ?? 0;
        object loose_hat = Dm.arg(__extra, 2) ?? FALSE;
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        atom_movable source = parent as atom_movable;
        Dm.combine(ref source.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);

        this.use_worn_icon = Dm.num(use_worn_icon);
        this.pixel_z_offset = pixel_z_offset;
        this.loose_hat = Dm.num(loose_hat);
        // Examine signals
        RegisterSignal(source, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(source, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));

        // Equip signals, used to drop loose hats
        RegisterSignal(source, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
        RegisterSignal(source, COMSIG_ITEM_DROPPED, nameof(on_drop));

        // Wear & Remove
        RegisterSignal(source, COMSIG_ATOM_ATTACKBY, nameof(on_attackby));
        RegisterSignal(source, COMSIG_ATOM_ATTACK_HAND_SECONDARY, nameof(on_secondary_attack_hand));

        // Overlays
        RegisterSignal(source, COMSIG_MODULE_GENERATE_WORN_OVERLAY, nameof(get_worn_overlays));
        RegisterSignal(source, COMSIG_ITEM_GET_SEPARATE_WORN_OVERLAYS, nameof(get_separate_worn_overlays));

        RegisterSignal(source, COMSIG_QDELETING, nameof(on_qdel));
        return null;
    }

    // Inherit the new values passed to the component
    // tg: code/datums/components/hat_stabilizer.dm:47
    [DmParams("use_worn_icon", "pixel_z_offset", "loose_hat")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        object use_worn_icon = Dm.arg(__extra, 0);
        object pixel_z_offset = Dm.arg(__extra, 1);
        object loose_hat = Dm.arg(__extra, 2);
        if (!Dm.truthy(i_am_original))
            return;

        if ((use_worn_icon != null))
            this.use_worn_icon = Dm.num(use_worn_icon);
        if ((use_worn_icon != null))
            this.use_worn_icon = Dm.num(use_worn_icon);
        if ((pixel_z_offset != null))
            this.pixel_z_offset = pixel_z_offset;
        if ((loose_hat != null))
            this.loose_hat = Dm.num(loose_hat);
    }

    // tg: code/datums/components/hat_stabilizer.dm:60
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        if (attached_hat != null)
            await remove_hat();
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_ATOM_ATTACKBY,
            COMSIG_ATOM_ATTACK_HAND_SECONDARY, COMSIG_MODULE_GENERATE_WORN_OVERLAY,
            COMSIG_ITEM_GET_SEPARATE_WORN_OVERLAYS, COMSIG_ATOM_UPDATE_OVERLAYS, COMSIG_QDELETING,
            COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));
    }

    // tg: code/datums/components/hat_stabilizer.dm:68
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob equipper = null, object slot = null)
    {
        if (!Dm.truthy(loose_hat))
            return;

        obj_item our_item = parent as obj_item;
        if (!Dm.truthy(Dm.bitand(slot, our_item.slot_flags)))
            return;
        RegisterSignals(equipper, Dm.list(COMSIG_MOB_SLIPPED, COMSIG_LIVING_SLAPPED, COMSIG_MOVABLE_POST_THROW), nameof(throw_hat));
        RegisterSignal(equipper, COMSIG_LIVING_THUD, nameof(drop_hat));
    }

    // tg: code/datums/components/hat_stabilizer.dm:80
    [SignalHandler]
    public virtual void on_drop(datum source = null, mob dropper = null)
    {
        UnregisterSignal(dropper, Dm.list(COMSIG_MOB_SLIPPED, COMSIG_LIVING_SLAPPED, COMSIG_MOVABLE_POST_THROW, COMSIG_LIVING_THUD));
    }

    // tg: code/datums/components/hat_stabilizer.dm:84
    [SignalHandler]
    public virtual void throw_hat(mob hatless = null)
    {
        if (!Dm.truthy(loose_hat))
            return;
        obj_item hat = Waiting.NoSleep(remove_hat()) as obj_item;
        if (hat == null)
            return;
        hat.visible_message(span_danger(I18n.T("[] goes flying off []'s head!", hat, hatless)));
        Waiting.NoSleep(hat.throw_at(get_edge_target_turf(get_turf(hat), pick(GLOB.alldirs)) as atom, 2, 1, spin: TRUE));
    }

    // tg: code/datums/components/hat_stabilizer.dm:94
    [SignalHandler]
    public virtual void drop_hat(mob hatless = null)
    {
        if (!Dm.truthy(loose_hat))
            return;
        Waiting.NoSleep(remove_hat());
    }

    // tg: code/datums/components/hat_stabilizer.dm:100
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList base_examine = null)
    {
        if (attached_hat != null)
            Dm.append(ref base_examine, span_notice(I18n.T("There's \\a [] [] placed on [].", attached_hat, Dm.truthy(loose_hat) ? I18n.T("loosely") : "", parent)));
        else
            Dm.append(ref base_examine, span_notice(I18n.T("There's nothing placed on []. Yet.", parent)));
    }

    // tg: code/datums/components/hat_stabilizer.dm:107
    [SignalHandler]
    public virtual void get_worn_overlays(atom_movable source = null, DmList overlays = null, mutable_appearance standing = null, object isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        if (Dm.truthy(isinhands))
            return;
        if (attached_hat == null)
            return;
        mutable_appearance worn_overlay = Waiting.NoSleep(attached_hat.build_worn_icon(default_layer: ABOVE_BODY_FRONT_HEAD_LAYER - 0.1f, default_icon_file: Dm.resource("icons/mob/clothing/head/default.dmi"), bodyshape: bodyshape)) as mutable_appearance;
        worn_overlay.appearance_flags = Dm.num(Dm.bitor(worn_overlay.appearance_flags, RESET_COLOR));
        // loose hats are slightly angled
        if (Dm.truthy(loose_hat))
        {
            matrix tilt_trix = Dm.matrix(worn_overlay.transform);
            float angle = 5;
            tilt_trix.Turn(angle * head_angle);
            worn_overlay.transform = tilt_trix;
        }
        worn_overlay.pixel_z = Dm.num(pixel_z_offset) + attached_hat.worn_y_offset;
        Dm.append(ref overlays, worn_overlay);
    }

    // tg: code/datums/components/hat_stabilizer.dm:124
    [SignalHandler]
    public virtual void get_separate_worn_overlays(atom_movable source = null, DmList overlays = null, mutable_appearance standing = null, mutable_appearance draw_target = null, object isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        if (Dm.truthy(isinhands))
            return;
        if (attached_hat == null)
            return;
        mutable_appearance worn_overlay = Waiting.NoSleep(attached_hat.build_worn_icon(default_layer: ABOVE_BODY_FRONT_HEAD_LAYER - 0.1f, default_icon_file: Dm.resource("icons/mob/clothing/head/default.dmi"), bodyshape: bodyshape)) as mutable_appearance;
        foreach (mutable_appearance overlay in Dm.each<mutable_appearance>(worn_overlay.overlays))
            overlay.layer = -ABOVE_BODY_FRONT_HEAD_LAYER + 0.1f;
        // loose hats are slightly angled
        if (Dm.truthy(loose_hat))
        {
            matrix tilt_trix = Dm.matrix(worn_overlay.transform);
            float angle = 5;
            tilt_trix.Turn(angle * head_angle);
            worn_overlay.transform = tilt_trix;
        }
        worn_overlay.pixel_z = Dm.num(pixel_z_offset) + attached_hat.worn_y_offset;
        Dm.append(ref overlays, worn_overlay);
    }

    // tg: code/datums/components/hat_stabilizer.dm:142
    [SignalHandler]
    public virtual void on_qdel(atom_movable source = null)
    {
        if (attached_hat != null)
            QDEL_NULL(ref attached_hat);
    }

    // tg: code/datums/components/hat_stabilizer.dm:148
    [SignalHandler]
    public virtual void on_attackby(datum source = null, obj_item hitting_item = null, mob user = null)
    {
        atom_movable movable_parent = parent as atom_movable;
        if (!Dm.truthy(Dm.bitand(hitting_item.slot_flags, ITEM_SLOT_HEAD)))
            return;

        if (attached_hat != null)
        {
            movable_parent.balloon_alert(user, I18n.T("hat already attached!"));
            return;
        }
        if (isclothing(hitting_item))
        {
            obj_item_clothing hat = hitting_item as obj_item_clothing;
            if (Dm.truthy(Dm.bitand(hat.clothing_flags, STACKABLE_HELMET_EXEMPT)))
            {
                movable_parent.balloon_alert(user, I18n.T("invalid hat!"));
                return;
            }
        }

        if (!Dm.truthy(Waiting.NoSleep(user.transferItemToLoc(hitting_item, parent, force: FALSE, silent: TRUE))))
            return;

        Waiting.NoSleep(attach_hat(hitting_item, user));
    }

    // tg: code/datums/components/hat_stabilizer.dm:169
    public virtual async DmTask attach_hat(obj_item hat = null, mob user = null)
    {
        atom_movable movable_parent = parent as atom_movable;
        attached_hat = hat;
        RegisterSignal(hat, COMSIG_MOVABLE_MOVED, nameof(on_hat_movement));
        head_angle = Dm.num(pick(1, -1));

        if ((user != null))
            movable_parent.balloon_alert(user, I18n.T("hat attached"));

        if (!isclothing(parent))
        {
            movable_parent.update_appearance();
            return;
        }

        obj_item_clothing apparel = parent as obj_item_clothing;
        if (isclothing(attached_hat))
        {
            obj_item_clothing realhat = attached_hat as obj_item_clothing;
            apparel.attach_clothing_traits(realhat.clothing_traits);
            apparel.visor_flags_cover = Dm.num(Dm.bitor(apparel.visor_flags_cover, realhat.visor_flags_cover));
        }
        former_flags = apparel.flags_cover;
        former_visor_flags = apparel.visor_flags_cover;
        apparel.flags_cover = Dm.num(Dm.bitor(apparel.flags_cover, attached_hat.flags_cover));
        apparel.update_appearance();

        if (ismob(apparel.loc))
        {
            mob wearer = apparel.loc as mob;
            await wearer.update_clothing(wearer.get_slot_by_item(apparel));
        }
    }

    // tg: code/datums/components/hat_stabilizer.dm:196
    [SignalHandler]
    public virtual void on_hat_movement(obj hat = null, mob user = null)
    {
        Waiting.NoSleep(remove_hat(ismob(user) ? user : null));
    }

    // tg: code/datums/components/hat_stabilizer.dm:200
    [SignalHandler]
    public virtual object on_secondary_attack_hand(datum source = null, mob user = null)
    {
        object dot = COMPONENT_CANCEL_ATTACK_CHAIN;
        if (attached_hat == null)
            return dot;
        atom_movable movable_parent = parent as atom_movable;
        if (Dm.truthy(Waiting.NoSleep(remove_hat(user))))
            movable_parent.balloon_alert(user, I18n.T("hat removed"));
        else
            movable_parent.balloon_alert_to_viewers(I18n.T("the hat falls to the floor!"));
        return dot;
    }

    // tg: code/datums/components/hat_stabilizer.dm:211
    public virtual void on_retraction()
    {
    }

    // tg: code/datums/components/hat_stabilizer.dm:213
    public virtual async DmTask<object> remove_hat(mob user = null)
    {
        object dot = null;
        if (QDELETED(attached_hat))
            return dot;

        atom_movable movable_parent = parent as atom_movable;
        UnregisterSignal(attached_hat, COMSIG_MOVABLE_MOVED);

        if (attached_hat.loc == parent)
            await attached_hat.forceMove(movable_parent.drop_location() as atom);

        if ((user != null))
            dot = await user.put_in_active_hand(attached_hat);
        else
            movable_parent.balloon_alert_to_viewers(I18n.T("the hat falls to the floor!"));

        if (!isclothing(parent))
        {
            attached_hat = null;
            movable_parent.update_appearance();
            return dot;
        }

        obj_item former_hat = attached_hat;
        obj_item_clothing apparel = parent as obj_item_clothing;
        if (isclothing(attached_hat))
        {
            obj_item_clothing truehat = attached_hat as obj_item_clothing;
            apparel.detach_clothing_traits(truehat.clothing_traits);
        }
        apparel.flags_cover = Dm.num(former_flags);
        apparel.visor_flags_cover = Dm.num(former_visor_flags);
        apparel.update_appearance();
        attached_hat = null;
        if (ismob(apparel.loc))
        {
            mob wearer = apparel.loc as mob;
            await wearer.update_clothing(wearer.get_slot_by_item(apparel));
        }
        if ((user == null))
            return former_hat;
        return dot;
    }

    // tg: code/datums/components/hat_stabilizer.dm:248
    [SignalHandler]
    public virtual object on_requesting_context_from_item(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (attached_hat != null && held_item == null)
        {
            context[SCREENTIP_CONTEXT_RMB] = I18n.T("Remove hat");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (istype(held_item, typeof(obj_item_clothing_head)))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Attach hat");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return NONE;
    }
}
