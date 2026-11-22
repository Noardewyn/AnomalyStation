// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/blocks_explosives.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5x slice 1 (explosions, D149): the whole file (the windows and doors add their explosion block to their turf). The var
// block is generated (D111).

/// Apply this element to a movable atom when you want it to block explosions
/// It will mirror the blocking down to that movable's turf, keeping explosion work cheap
// tg: code/datums/elements/blocks_explosives.dm:3
public partial class datum_element_blocks_explosives : datum_element
{
    // tg: code/datums/elements/blocks_explosives.dm:6
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = null;
        if (!ismovable(target))
            return dot;
        dot = await base.Attach(target, __extra);
        ADD_TRAIT(target, TRAIT_BLOCKING_EXPLOSIVES, TRAIT_GENERIC);
        atom_movable moving_target = target as atom_movable;
        RegisterSignal(moving_target, COMSIG_MOVABLE_MOVED, nameof(blocker_moved));
        RegisterSignal(moving_target, COMSIG_MOVABLE_EXPLOSION_BLOCK_CHANGED, nameof(blocking_changed));
        moving_target.explosive_resistance = moving_target.explosion_block;

        if (length(moving_target.locs) > 1)
        {
            foreach (atom location in Dm.each_cast<atom>(moving_target.locs))
                block_loc(location, moving_target.explosion_block);
        }
        else if (Dm.truthy(moving_target.loc))
            block_loc(moving_target.loc, moving_target.explosion_block);
        return dot;
    }

    // tg: code/datums/elements/blocks_explosives.dm:22
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Detach(source, __extra);
        atom_movable source_movable = source as atom_movable;
        if (length(source_movable.locs) > 1)
        {
            foreach (atom location in Dm.each_cast<atom>(source_movable.locs))
                unblock_loc(location, source_movable.explosion_block);
        }
        else if (Dm.truthy(source_movable.loc))
            unblock_loc(source_movable.loc, source_movable.explosion_block);
        REMOVE_TRAIT(source_movable, TRAIT_BLOCKING_EXPLOSIVES, TRAIT_GENERIC);
        return dot;
    }

    /// Call this when our blocking well, changes. we'll update our turf(s) with the details
    // tg: code/datums/elements/blocks_explosives.dm:32
    [SignalHandler]
    public virtual void blocking_changed(atom_movable target = null, object old_block = null, object new_block = null)
    {
        if (length(target.locs) > 1)
        {
            foreach (atom location in Dm.each_cast<atom>(target.locs))
            {
                unblock_loc(location, old_block);
                block_loc(location, new_block);
            }
        }
        else if (Dm.truthy(target.loc))
        {
            unblock_loc(target.loc, old_block);
            block_loc(target.loc, new_block);
        }
    }

    /// Applies a block amount to a turf. proc for convenince
    // tg: code/datums/elements/blocks_explosives.dm:42
    public virtual void block_loc(atom location = null, object block_amount = null)
    {
        location.explosive_resistance += Dm.num(block_amount);
    }

    /// Removes a block amount from a turf. proc for convenince
    // tg: code/datums/elements/blocks_explosives.dm:46
    public virtual void unblock_loc(atom location = null, object block_amount = null)
    {
        location.explosive_resistance -= Dm.num(block_amount);
    }

    /// Essentially just blocking_changed except we remove from the old loc, and add to the new one
    // tg: code/datums/elements/blocks_explosives.dm:50
    [SignalHandler]
    public virtual void blocker_moved(atom_movable target = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null)
    {
        if (length(old_locs) > 1)
        {
            foreach (atom location in Dm.each_cast<atom>(old_locs))
                unblock_loc(location, target.explosion_block);
        }
        else if (Dm.truthy(old_loc))
            unblock_loc(old_loc, target.explosion_block);

        if (length(target.locs) > 1)
        {
            foreach (atom location in Dm.each_cast<atom>(target.locs))
                block_loc(location, target.explosion_block);
        }
        else if (Dm.truthy(target.loc))
            block_loc(target.loc, target.explosion_block);
    }
}
