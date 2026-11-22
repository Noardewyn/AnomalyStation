// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/mob_update_icons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M2 P3): the /mob roots of the overlay-updating procs that the carbon and human appearance code
// overrides (carbon_update_icons.cs, human_update_icons.cs). /mob is a builtin type: its [DmPath] is on its engine part.

//Most of these are defined at this level to reduce on checks elsewhere in the code.
//Having them here also makes for a nice reference list of the various overlay-updating procs available

// tg: code/modules/mob/mob_update_icons.dm:5
public partial class mob : atom_movable
{
    ///Redraws the entire mob. For carbons, this is rather expensive, please use the individual update_X procs.
    // tg: code/modules/mob/mob_update_icons.dm:5
    public virtual async DmTask<object> regenerate_icons() //TODO: phase this out completely if possible
    {
        return null;
    }

    ///Updates every item slot passed into it.
    // tg: code/modules/mob/mob_update_icons.dm:9
    public virtual async DmTask update_clothing(object slot_flags = null)
    {
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_BACK)))
            await update_worn_back();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_MASK)))
            await update_worn_mask();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_NECK)))
            await update_worn_neck();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_HANDCUFFED)))
            update_worn_handcuffs();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_LEGCUFFED)))
            update_worn_legcuffs();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_BELT)))
            await update_worn_belt();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_ID)))
            await update_worn_id();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_EARS)))
            await update_worn_ears();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_EYES)))
            await update_worn_glasses();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_GLOVES)))
            await update_worn_gloves();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_HEAD)))
            await update_worn_head();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_FEET)))
            await update_worn_shoes();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_OCLOTHING)))
            await update_worn_oversuit();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_ICLOTHING)))
            await update_worn_undersuit();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_SUITSTORE)))
            await update_suit_storage();
        if (Dm.truthy(Dm.bitand(slot_flags, Dm.bitor(ITEM_SLOT_LPOCKET, ITEM_SLOT_RPOCKET))))
            update_pockets();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_HANDS)))
            await update_held_items();
    }

    /// Recalculates the mob's obscured and covered slots based on currently equipped items
    // tg: code/modules/mob/mob_update_icons.dm:46
    [SignalHandler]
    public virtual void refresh_obscured()
    {
        return;
    }

    // tg: code/modules/mob/mob_update_icons.dm:50
    public virtual async DmTask update_icons()
    {
        return;
    }

    ///Updates the handcuff overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:54
    public virtual void update_worn_handcuffs()
    {
        return;
    }

    ///Updates the legcuff overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:58
    public virtual void update_worn_legcuffs()
    {
        return;
    }

    ///Updates the back overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:62
    public virtual async DmTask update_worn_back()
    {
        return;
    }

    ///Updates the held items overlay(s) & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:66
    [ShouldCallParent]
    public virtual async DmTask update_held_items()
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOB_UPDATE_HELD_ITEMS]))
            this._SendSignal(COMSIG_MOB_UPDATE_HELD_ITEMS, Dm.list(this));
        hud_used?.update_inventory_slot(ITEM_SLOT_HANDS);
    }

    ///Updates the mask overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:72
    public virtual async DmTask update_worn_mask()
    {
        return;
    }

    ///Updates the neck overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:76
    public virtual async DmTask update_worn_neck()
    {
        return;
    }

    ///Updates the oversuit overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:80
    public virtual async DmTask update_worn_oversuit()
    {
        return;
    }

    ///Updates the undersuit/uniform overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:84
    public virtual async DmTask update_worn_undersuit()
    {
        return;
    }

    ///Updates the belt overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:88
    public virtual async DmTask update_worn_belt()
    {
        return;
    }

    ///Updates the on-head overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:92
    public virtual async DmTask update_worn_head()
    {
        return;
    }

    ///Updates every part of a carbon's body. Including parts, mutant parts, lips, underwear, and socks.
    // tg: code/modules/mob/mob_update_icons.dm:96
    public virtual async DmTask update_body(params object[] __extra)
    {
        return;
    }

    ///Updates the glasses overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:100
    public virtual async DmTask update_worn_glasses()
    {
        return;
    }

    ///Updates the id overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:104
    public virtual async DmTask update_worn_id()
    {
        return;
    }

    ///Updates the shoes overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:108
    public virtual async DmTask update_worn_shoes()
    {
        return;
    }

    ///Updates the glasses overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:112
    public virtual async DmTask update_worn_gloves()
    {
        return;
    }

    ///Updates the suit storage overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:116
    public virtual async DmTask update_suit_storage()
    {
        return;
    }

    ///Updates the pocket overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:120
    public virtual void update_pockets()
    {
        return;
    }

    ///Updates the headset overlay & HUD element.
    // tg: code/modules/mob/mob_update_icons.dm:124
    public virtual async DmTask update_worn_ears()
    {
        return;
    }
}
