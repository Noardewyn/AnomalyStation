// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/tactical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3c, D316): the cardboard cutout's disguise. The holder's override image is drawn in its place as a
// picture (DV114). The var block is generated (D111).

///A simple component that replacess the user's appearance with that of the parent item when equipped.
// tg: code/datums/components/tactical.dm:2
public partial class datum_component_tactical : datum_component
{
    // tg: code/datums/components/tactical.dm:8
    [DmParams("allowed_slots")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object allowed_slots = Dm.arg(__extra, 0);
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        this.allowed_slots = allowed_slots;
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/tactical.dm:14
    public override object Destroy(float? force = null)
    {
        unmodify();
        return base.Destroy(force);
    }

    // tg: code/datums/components/tactical.dm:18
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(modify));
        obj_item item = (obj_item)parent;
        if (ismob(item.loc))
        {
            mob holder = (mob)item.loc;
            modify(item, holder, holder.get_slot_by_item(item));
        }
        await base.RegisterWithParent();
    }

    // tg: code/datums/components/tactical.dm:25
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_EQUIPPED));
        unmodify();
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/tactical.dm:31
    [SignalHandler]
    public virtual void modify(obj_item source = null, mob user = null, object slot = null)
    {
        if (Dm.eq(current_slot, slot))
            return;

        if (Dm.truthy(allowed_slots) && !Dm.truthy(Dm.bitand(slot, allowed_slots)))
        {
            if (Dm.truthy(current_slot))
                unmodify(source, user);
            return;
        }

        RegisterSignal(parent, COMSIG_MOVABLE_Z_CHANGED, nameof(tactical_update));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(unmodify));
        RegisterSignal(parent, COMSIG_ATOM_UPDATED_ICON, nameof(on_icon_update));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(user, COMSIG_HUMAN_GET_VISIBLE_NAME, nameof(on_name_inquiry));
        RegisterSignal(user, COMSIG_HUMAN_GET_FORCED_NAME, nameof(on_name_inquiry));
        // This forces a name update on the user, so we don't need to call name update ourselves
        ADD_TRAIT(user, TRAIT_UNKNOWN_APPEARANCE, REF(this));

        current_slot = slot;
        source.pixel_x = source.base_pixel_x;
        source.pixel_y = source.base_pixel_y;
        source.pixel_w = source.base_pixel_w;
        source.pixel_z = source.base_pixel_z;

        on_icon_update(source);
    }

    // tg: code/datums/components/tactical.dm:57
    [SignalHandler]
    public virtual void on_icon_update(obj_item source = null)
    {
        if (source.loc is not mob user)
            return;

        user.remove_alt_appearance(Dm.text("sneaking_mission[]", REF(this)));
        obj_item master = (obj_item)parent;
        image image = Dm.image(master, loc: user);
        image.copy_overlays(master);
        image.@override = TRUE;
        image.layer = ABOVE_MOB_LAYER;
        image.plane = FLOAT_PLANE;
        user.add_alt_appearance(typeof(datum_atom_hud_alternate_appearance_basic_everyone), Dm.text("sneaking_mission[]", REF(this)), image);
    }

    // tg: code/datums/components/tactical.dm:73
    [SignalHandler]
    public virtual void on_name_inquiry(obj_item source = null, DmList identity = null)
    {
        float tactical_disguise_power = INFINITY; // it's a flawless plan: they'll never look behind this unassuming potted plant
        if (Dm.truthy(identity[VISIBLE_NAME_FORCED]))
        {
            if (Dm.num(identity[VISIBLE_NAME_FORCED]) >= tactical_disguise_power) // my disguise is too powerful for you, traveler! but seriously this is bad
                stack_trace(Dm.text("A name forcing signal ([]) has a priority collision with [].", identity[VISIBLE_NAME_FACE], this), "code/datums/components/tactical.dm", 79);
            else
                identity[VISIBLE_NAME_FORCED] = tactical_disguise_power;
        }
        else
            identity[VISIBLE_NAME_FORCED] = tactical_disguise_power;

        obj_item flawless_disguise = (obj_item)parent;
        identity[VISIBLE_NAME_FACE] = flawless_disguise.name;
        identity[VISIBLE_NAME_ID] = flawless_disguise.name; // for Unknown (as 'potted plant') says
    }

    // tg: code/datums/components/tactical.dm:91
    [SignalHandler]
    public virtual void unmodify(obj_item source = null, mob user = null)
    {
        source ??= (obj_item)parent;
        user ??= source.loc as mob;
        if (user == null)
            return;

        UnregisterSignal(source, Dm.list(COMSIG_MOVABLE_Z_CHANGED, COMSIG_ITEM_DROPPED, COMSIG_MOVABLE_MOVED, COMSIG_ATOM_UPDATED_ICON));

        UnregisterSignal(user, Dm.list(COMSIG_HUMAN_GET_VISIBLE_NAME, COMSIG_HUMAN_GET_FORCED_NAME));
        current_slot = null;
        user.remove_alt_appearance(Dm.text("sneaking_mission[]", REF(this)));
        REMOVE_TRAIT(user, TRAIT_UNKNOWN_APPEARANCE, REF(this));
    }

    ///Checks if a mob is holding us, and if so we will modify our appearance to properly match w/ the mob.
    // tg: code/datums/components/tactical.dm:116
    [SignalHandler]
    public virtual void tactical_update(obj_item source = null)
    {
        if (!ismob(source.loc))
            return;
        modify(source, (mob)source.loc, current_slot);
    }

    ///We really want to make sure that, if things ever slightly breaks, that the alt appearance will be removed anyway.
    // tg: code/datums/components/tactical.dm:123
    [SignalHandler]
    public virtual void on_moved(obj_item source = null, atom oldloc = null, object direction = null, object forced = null)
    {
        unmodify(source, oldloc as mob);
    }
}
