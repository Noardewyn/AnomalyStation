// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/digitigrade_limb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP1 (D338): the whole file: a lizard's digitigrade legs, squashed into the plain lizard leg's picture by the clothes that cover
// them with no digitigrade variant. The var block is generated (D111).

/// Updates the limb id of a bodypart if the mob is wearing digitigrade squishing clothing
// tg: code/datums/components/digitigrade_limb.dm:2
public partial class datum_component_digitigrade_limb : datum_component
{
    // tg: code/datums/components/digitigrade_limb.dm:11
    [DmParams("squashed_id", "free_id")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object squashed_id = Dm.arg(__extra, 0), free_id = Dm.arg(__extra, 1);
        if (parent is not obj_item_bodypart_leg)
            return COMPONENT_INCOMPATIBLE;

        obj_item_bodypart limb = (obj_item_bodypart)parent;
        limb.bodytype = Dm.bitor(limb.bodytype, BODYTYPE_DIGITIGRADE);

        this.squashed_id = squashed_id;
        this.free_id = Dm.truthy(free_id) ? free_id : Dm.initial(limb, (obj_item_bodypart p) => p.limb_id);

        RegisterSignal(parent, COMSIG_BODYPART_UPDATED, nameof(update_limb_id_comsig));
        RegisterSignal(parent, COMSIG_BODYPART_ATTACHED, nameof(on_attach));
        RegisterSignal(parent, COMSIG_BODYPART_REMOVED, nameof(on_remove));
        RegisterSignal(parent, COMSIG_BODYPART_BUTCHERED, nameof(on_butchered));

        if (ishuman(limb.owner))
            on_attach(limb, limb.owner);
        return null;
    }

    // tg: code/datums/components/digitigrade_limb.dm:29
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(parent, COMSIG_BODYPART_UPDATED);
        UnregisterSignal(parent, COMSIG_BODYPART_ATTACHED);
        UnregisterSignal(parent, COMSIG_BODYPART_REMOVED);

        UnregisterSignal(parent, COMSIG_BODYPART_BUTCHERED);
        obj_item_bodypart limb = (obj_item_bodypart)parent;
        if (!QDELING(parent) && ishuman(limb.owner))
            on_remove(limb, limb.owner);
        limb.bodytype = Dm.bitand(limb.bodytype, Dm.bitnot(BODYTYPE_DIGITIGRADE));
        return base.Destroy(force);
    }

    // tg: code/datums/components/digitigrade_limb.dm:41
    [SignalHandler]
    public virtual void on_attach(obj_item_bodypart limb = null, mob_living_carbon new_limb_owner = null)
    {
        RegisterSignal(new_limb_owner, COMSIG_MOB_EQUIPPED_ITEM, nameof(equipped_item));
        RegisterSignal(new_limb_owner, COMSIG_MOB_DROPPED_ITEM, nameof(unequipped_item));
        RegisterSignal(new_limb_owner, COMSIG_CARBON_ITEM_COVERAGE_CHANGED, nameof(coverage_changed));
        // tg: if(equipped_item(...)) LAZYOR(squashing_us, REF(equipped)): the handler returns null, so only its own LAZYOR runs
        foreach (obj_item equipped in Dm.each_cast<obj_item>(new_limb_owner.get_equipped_items()))
            equipped_item(new_limb_owner, equipped, new_limb_owner.get_slot_by_item(equipped));
    }

    // tg: code/datums/components/digitigrade_limb.dm:51
    [SignalHandler]
    public virtual void on_remove(obj_item_bodypart limb = null, mob_living_carbon old_limb_owner = null)
    {
        UnregisterSignal(old_limb_owner, COMSIG_MOB_EQUIPPED_ITEM);
        UnregisterSignal(old_limb_owner, COMSIG_MOB_DROPPED_ITEM);
        UnregisterSignal(old_limb_owner, COMSIG_CARBON_ITEM_COVERAGE_CHANGED);
        LAZYNULL(ref squashing_us);
        Waiting.NoSleep(update_limb_id());
    }

    // tg: code/datums/components/digitigrade_limb.dm:60
    [SignalHandler]
    public virtual void equipped_item(mob_living_carbon equipper = null, obj_item equipped_item = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, equipped_item.slot_flags)) && Dm.truthy(item_squishes_limb(equipped_item, equipper)))
        {
            LAZYOR(ref squashing_us, REF(equipped_item));
            Waiting.NoSleep(update_limb_id());
        }
    }

    // tg: code/datums/components/digitigrade_limb.dm:67
    [SignalHandler]
    public virtual void unequipped_item(mob_living_carbon equipper = null, obj_item equipped_item = null)
    {
        LAZYREMOVE(ref squashing_us, REF(equipped_item));
        Waiting.NoSleep(update_limb_id());
    }

    // Snowflake case for updating whether our jumpsuit should squish us
    // tg: code/datums/components/digitigrade_limb.dm:74
    [SignalHandler]
    public virtual void coverage_changed(mob_living_carbon equipper = null, object added_slots = null, object removed_slots = null)
    {
        if (!Dm.truthy(Dm.bitand(Dm.bitor(added_slots, removed_slots), HIDEJUMPSUIT)))
            return;

        if (equipper.get_item_by_slot(ITEM_SLOT_ICLOTHING) is not obj_item_clothing uniform)
            return;

        object uniform_ref = REF(uniform);
        if (Dm.@in(uniform_ref, squashing_us))
        {
            if (Dm.truthy(Dm.bitand(added_slots, HIDEJUMPSUIT)))
            {
                LAZYREMOVE(ref squashing_us, uniform_ref);
                Waiting.NoSleep(update_limb_id());
            }
        }

        else
        {
            if (Dm.truthy(Dm.bitand(removed_slots, HIDEJUMPSUIT)) && Dm.truthy(item_squishes_limb(uniform, equipper)))
            {
                LAZYOR(ref squashing_us, uniform_ref);
                Waiting.NoSleep(update_limb_id());
            }
        }
    }

    // tg: code/datums/components/digitigrade_limb.dm:95
    public virtual object item_squishes_limb(obj_item equipped_item = null, mob_living_carbon equipper = null)
    {
        if (!Dm.truthy(Dm.bitand(equipped_item.body_parts_covered, Dm.bitor(LEGS, FEET))))
            return FALSE;

        object sw__1 = equipper.get_slot_by_item(equipped_item);
        if (Dm.eq(sw__1, ITEM_SLOT_FEET) || Dm.eq(sw__1, ITEM_SLOT_OCLOTHING))
            return Dm.num(!Dm.truthy(Dm.bitand(equipped_item.supports_variations_flags, DIGITIGRADE_VARIATIONS)));
        if (Dm.eq(sw__1, ITEM_SLOT_ICLOTHING)) // If the jumpsuit is obscured, it shouldn't contribute to squishing
            return Dm.num(!Dm.truthy(Dm.bitand(equipped_item.supports_variations_flags, DIGITIGRADE_VARIATIONS)) && !Dm.truthy(Dm.bitand(equipper.obscured_slots, HIDEJUMPSUIT)));

        return FALSE;
    }

    /// This is just ran on update_limb() to ensure we always have the correct ID
    // tg: code/datums/components/digitigrade_limb.dm:108
    [SignalHandler]
    public virtual void update_limb_id_comsig()
    {
        Waiting.NoSleep(update_limb_id(sprite_update: FALSE));
    }

    /// Digitigrade limbs that are butchered add the component to the replacement limb
    // tg: code/datums/components/digitigrade_limb.dm:113
    [SignalHandler]
    public virtual void on_butchered(datum source = null, obj_item_bodypart replacement = null)
    {
        squashed_id = Dm.text("[]_[]", Dm.initial(replacement, (obj_item_bodypart p) => p.limb_id), BODYPART_ID_DIGITIGRADE);
        free_id = Dm.initial(replacement, (obj_item_bodypart p) => p.limb_id);
        Waiting.NoSleep(replacement.TakeComponent(this));
    }

    // tg: code/datums/components/digitigrade_limb.dm:119
    public virtual async DmTask update_limb_id(float? sprite_update = null)
    {
        sprite_update ??= TRUE;
        obj_item_bodypart limb = (obj_item_bodypart)parent;
        string old_id = limb.limb_id;
        if (Dm.truthy(LAZYLEN(squashing_us)))
        {
            limb.limb_id = squashed_id as string;
            limb.remove_bodyshape(BODYSHAPE_DIGITIGRADE);
        }

        else
        {
            limb.limb_id = free_id as string;
            limb.add_bodyshape(BODYSHAPE_DIGITIGRADE);
        }

        if (!Dm.truthy(sprite_update) || Dm.eq(old_id, limb.limb_id))
            return;

        if (limb.owner == null)
        {
            await limb.update_icon_dropped();
            return;
        }

        // Ensures any items that with a variation are updated
        foreach (obj_item thing in Dm.each_cast<obj_item>(limb.owner.get_equipped_items(Dm.bitor(INCLUDE_PROSTHETICS, INCLUDE_ABSTRACT))))
        {
            if (Dm.truthy(Dm.bitand(thing.supports_variations_flags, DIGITIGRADE_VARIATIONS)))
                thing.update_slot_icon();
        }
        // Updates underwear and mob sprites
        await limb.owner.update_body();
    }
}
