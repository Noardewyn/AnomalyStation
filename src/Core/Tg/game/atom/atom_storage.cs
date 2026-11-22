// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_storage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the var (wave 3, the atoms package) and create_storage and clone_storage (M4a P3,
// docs/ai/specs/m4a-ownership.md).

// tg: code/game/atom/atom_storage.dm:1
public partial class atom : datum
{
    /// the datum handler for our contents - see create_storage() for creation method
    // tg: code/game/atom/atom_storage.dm:3
    public datum_storage atom_storage;
}

// tg: code/game/atom/atom_storage.dm:6
public partial class atom : datum
{
    /// A quick and easy way to create a storage datum for an atom
    // tg: code/game/atom/atom_storage.dm:6
    public virtual async DmTask<object> create_storage(object max_slots = null, object max_specific_storage = null, object max_total_storage = null, DmList canhold = null, DmList canthold = null, Type storage_type = null, object rustle_sound = null, object remove_rustle_sound = null)
    {
        storage_type ??= typeof(datum_storage);
        if (Dm.truthy(atom_storage))
            QDEL_NULL(ref atom_storage);

        atom_storage = await Dm.new_async(storage_type, this, max_slots, max_specific_storage, max_total_storage, rustle_sound, remove_rustle_sound) as datum_storage;

        if (Dm.truthy(canhold) || Dm.truthy(canthold))
            atom_storage.set_holdable(canhold, canthold);

        return atom_storage;
    }

    /**
     * A quick and easy way to /clone/ a storage datum for an atom (does not copy over contents, only the datum details)
     *
     * Imperfect, does not copy over ALL variables, only important ones (max storage size, etc)
     */
    // tg: code/game/atom/atom_storage.dm:33
    public virtual async DmTask<object> clone_storage(datum_storage cloning = null)
    {
        if (Dm.truthy(atom_storage))
            QDEL_NULL(ref atom_storage);

        atom_storage = await Dm.new_async(cloning.type, this, cloning.max_slots, cloning.max_specific_storage, cloning.max_total_storage) as datum_storage;

        if (Dm.truthy(cloning.can_hold) || Dm.truthy(cloning.cant_hold))
        {
            if (!Dm.truthy(atom_storage.can_hold) && !Dm.truthy(atom_storage.cant_hold)) //In the event that the can/can't hold lists are already in place (such as from storage objects added on initialize).
                atom_storage.set_holdable(cloning.can_hold, cloning.cant_hold);
        }

        return atom_storage;
    }
}
