// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/prosthetic_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The prosthetic replacement operation makes an arbitrary item into an arm (the
// surplus-prosthetic look of a robotic arm holding the item) that may fall off when swung. The var block is generated.

/**
 * This makes an arbitrary item into a "prosthetic limb"
 */
// tg: code/datums/components/prosthetic_item.dm:4
public partial class datum_component_item_as_prosthetic_limb : datum_component
{
    // tg: code/datums/components/prosthetic_item.dm:11
    [DmParams("prosthetic_item", "drop_prob")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        obj_item prosthetic_item = Dm.arg<obj_item>(__extra, 0);
        float drop_prob = Dm.arg(__extra, 1) is var dp && dp != null ? Dm.num(dp) : 100;
        if (!isbodypart(parent))
            return COMPONENT_INCOMPATIBLE;
        obj_item_bodypart bodyparent = (obj_item_bodypart)parent;
        if (!Dm.eq(bodyparent.body_zone, BODY_ZONE_L_ARM) && !Dm.eq(bodyparent.body_zone, BODY_ZONE_R_ARM))
            return COMPONENT_INCOMPATIBLE;
        if (bodyparent.owner == null)
            return COMPONENT_INCOMPATIBLE;

        Dm.combine(ref bodyparent.bodypart_flags, Dm.bitor(BODYPART_PSEUDOPART, BODYPART_IMPLANTED));
        this.item_limb = prosthetic_item; // calls register_item() in registerWithParent()
        this.drop_prob = drop_prob;
        return null;
    }

    // tg: code/datums/components/prosthetic_item.dm:25
    [DmParams("new_comp", "i_am_original", "prosthetic_item", "drop_prob")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        obj_item prosthetic_item = Dm.arg<obj_item>(__extra, 0);
        float drop_prob = Dm.arg(__extra, 1) is var dp && dp != null ? Dm.num(dp) : 100;
        if (prosthetic_item != null)
        {
            unregister_item(item_limb);
            await register_item(prosthetic_item);
        }

        this.drop_prob = drop_prob;
    }

    // tg: code/datums/components/prosthetic_item.dm:32
    public virtual async DmTask register_item(obj_item prosthetic_item = null)
    {
        // SEND_SIGNAL(prosthetic_item, COMSIG_ITEM_PRE_USED_AS_PROSTHETIC, parent) (rules 11.1 (e))
        if (Dm.truthy(prosthetic_item._listen_lookup?[COMSIG_ITEM_PRE_USED_AS_PROSTHETIC]))
            prosthetic_item._SendSignal(COMSIG_ITEM_PRE_USED_AS_PROSTHETIC, Dm.list(prosthetic_item, parent));
        item_limb = prosthetic_item;
        RegisterSignal(prosthetic_item, COMSIG_QDELETING, nameof(qdel_limb));
        RegisterSignal(prosthetic_item, COMSIG_MOVABLE_MOVED, nameof(limb_moved));
        // adds a bunch of flags to the item to make it act like a limb
        Dm.combine(ref prosthetic_item.item_flags, Dm.bitor(HAND_ITEM, ABSTRACT));
        Dm.mask(ref prosthetic_item.interaction_flags_item, Dm.bitnot(INTERACT_ITEM_ATTACK_HAND_PICKUP));
        ADD_TRAIT(prosthetic_item, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT);
        // update the name of the real limb to match the item, so you see "their chainsaw arm is wounded"
        obj_item_bodypart bodyparent = (obj_item_bodypart)parent;
        // ensures the item is in the proper place
        object sw__1 = bodyparent.body_zone;
        if (Dm.eq(sw__1, BODY_ZONE_R_ARM))
        {
            bodyparent.name = I18n.T("right []", prosthetic_item.name);
            bodyparent.plaintext_zone = I18n.T("right []", prosthetic_item.name);
            await bodyparent.owner.put_in_r_hand(prosthetic_item);
        }
        else if (Dm.eq(sw__1, BODY_ZONE_L_ARM))
        {
            bodyparent.name = I18n.T("left []", prosthetic_item.name);
            bodyparent.plaintext_zone = I18n.T("left []", prosthetic_item.name);
            await bodyparent.owner.put_in_l_hand(prosthetic_item);
        }
        // SEND_SIGNAL(prosthetic_item, COMSIG_ITEM_POST_USED_AS_PROSTHETIC, parent)
        if (Dm.truthy(prosthetic_item._listen_lookup?[COMSIG_ITEM_POST_USED_AS_PROSTHETIC]))
            prosthetic_item._SendSignal(COMSIG_ITEM_POST_USED_AS_PROSTHETIC, Dm.list(prosthetic_item, parent));
    }

    // tg: code/datums/components/prosthetic_item.dm:56
    public virtual void unregister_item(obj_item prosthetic_item = null)
    {
        item_limb = null;
        UnregisterSignal(prosthetic_item, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        // nothing to be done if it's being thrown away
        if (QDELING(prosthetic_item))
            return;

        // reset all the flags
        Dm.mask(ref prosthetic_item.item_flags, Dm.bitnot(Dm.bitor(HAND_ITEM, ABSTRACT)));
        Dm.combine(ref prosthetic_item.interaction_flags_item, INTERACT_ITEM_ATTACK_HAND_PICKUP);
        REMOVE_TRAIT(prosthetic_item, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT);
        // note: default behavior is to keep the item in the contents of the arm
        // so if the limb is deleted by some unknown means (supermatter?), the item will also be deleted
        // SEND_SIGNAL(prosthetic_item, COMSIG_ITEM_DROPPED_FROM_PROSTHETIC, parent)
        if (Dm.truthy(prosthetic_item._listen_lookup?[COMSIG_ITEM_DROPPED_FROM_PROSTHETIC]))
            prosthetic_item._SendSignal(COMSIG_ITEM_DROPPED_FROM_PROSTHETIC, Dm.list(prosthetic_item, parent));
    }

    // tg: code/datums/components/prosthetic_item.dm:72
    public virtual void register_limb(obj_item_bodypart bodyparent = null)
    {
        RegisterSignal(bodyparent, COMSIG_BODYPART_REMOVED, nameof(clear_comp));
        RegisterSignals(bodyparent.owner, Dm.list(COMSIG_MOB_ITEM_ATTACK, COMSIG_LIVING_UNARMED_ATTACK), nameof(pop_limb));
    }

    // tg: code/datums/components/prosthetic_item.dm:76
    public virtual void unregister_limb(obj_item_bodypart bodyparent = null)
    {
        UnregisterSignal(bodyparent, COMSIG_BODYPART_REMOVED);
        if (bodyparent.owner != null) // may be null frim removal
            UnregisterSignal(bodyparent.owner, Dm.list(COMSIG_MOB_ITEM_ATTACK, COMSIG_LIVING_UNARMED_ATTACK));
    }

    // tg: code/datums/components/prosthetic_item.dm:81
    public override async DmTask RegisterWithParent()
    {
        register_limb((obj_item_bodypart)parent);
        await register_item(item_limb);
    }

    // tg: code/datums/components/prosthetic_item.dm:85
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        unregister_limb((obj_item_bodypart)parent);
        unregister_item(item_limb);
    }

    /// If the fake limb (the item) is deleted, the real limb goes with it.
    // tg: code/datums/components/prosthetic_item.dm:90
    [SignalHandler]
    public virtual void qdel_limb(obj_item source = null)
    {
        if (QDELING(parent))
            return;
        qdel(parent); // which nulls all the references and signals
    }

    /// If the item is removed from our hands somehow, the real limb has to go
    // tg: code/datums/components/prosthetic_item.dm:98
    [SignalHandler]
    public virtual void limb_moved(obj_item source = null)
    {
        if (QDELING(parent))
            return;
        obj_item_bodypart bodyparent = (obj_item_bodypart)parent;
        if (source.loc == bodyparent.owner)
            return;
        qdel(parent); // which nulls all the references and signals
    }

    /// When the bodypart is removed, we will drop the item on the ground, and then delete the the real limb.
    // tg: code/datums/components/prosthetic_item.dm:109
    [SignalHandler]
    public virtual void clear_comp(datum source = null, mob_living_carbon owner = null)
    {
        UnregisterSignal(owner, Dm.list(COMSIG_MOB_ITEM_ATTACK, COMSIG_LIVING_UNARMED_ATTACK));
        if (item_limb.loc == owner)
            Waiting.NoSleep(item_limb.forceMove(owner.drop_location() as atom));
        if (QDELING(parent))
            return;
        qdel(parent); // which nulls all the references and signals
    }

    /// Attacking with the fake limb (the item) can cause it to fall off, which in turn will result in the real limb being deleted.
    // tg: code/datums/components/prosthetic_item.dm:120
    [SignalHandler]
    public virtual object pop_limb(mob_living source = null, params object[] __extra)
    {
        if (source.get_active_hand() != parent)
            return NONE;
        if (!prob(drop_prob))
            return NONE;

        obj_item_bodypart bodyparent = (obj_item_bodypart)parent;
        source.visible_message(
            span_warning(I18n.T("As [] attempts to swing with [] [], it falls right off!", source, source.p_their(), bodyparent.name)),
            span_warning(I18n.T("As you attempt to swing with [] [], it falls right off!", source.p_their(), bodyparent.name)),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        Waiting.NoSleep(bodyparent.dismember(silent: TRUE)); // which removes the limb, which qdels us (which nulls all the references and signals)
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }
}

// tg: code/datums/components/prosthetic_item.dm:135
public partial class mob_living_carbon : mob_living
{
    /**
     * Makes the passed item into a "prosthetic limb" of this mod, replacing any existing arm
     *
     * Returns the created pseudopart
     */
    // tg: code/datums/components/prosthetic_item.dm:139
    public virtual async DmTask<object> make_item_prosthetic(obj_item some_thing = null, string target_zone = null, float? fall_prob = null)
    {
        target_zone ??= BODY_ZONE_R_ARM;
        fall_prob ??= 0;
        if (HAS_TRAIT_FROM(some_thing, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT))
            throw Dm.CRASH("make_item_prosthetic given an item that is already a prosthetic limb!", "code/datums/components/prosthetic_item.dm", 141);

        obj_item_bodypart existing = get_bodypart(target_zone) as obj_item_bodypart;
        if (existing != null)
            await existing.drop_limb(special: TRUE);

        obj_item_bodypart bodypart_to_attach = await newBodyPart(target_zone) as obj_item_bodypart;
        await bodypart_to_attach.change_appearance(icon: Dm.resource("icons/mob/augmentation/surplus_augments.dmi"), id: BODYPART_ID_ROBOTIC, greyscale: FALSE, dimorphic: FALSE);
        await bodypart_to_attach.try_attach_limb(this);
        await bodypart_to_attach.AddComponent(typeof(datum_component_item_as_prosthetic_limb), some_thing, fall_prob);

        if (bodypart_to_attach.owner != this)
        {
            stack_trace("make_item_prosthetic failed to attach to owner!", "code/datums/components/prosthetic_item.dm", 151);
            qdel(bodypart_to_attach);
            return null;
        }

        return bodypart_to_attach;
    }
}
