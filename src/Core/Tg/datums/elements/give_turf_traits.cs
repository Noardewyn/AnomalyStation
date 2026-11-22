// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/give_turf_traits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 4 (the lattice and the catwalk give their turf these traits): the whole file. TURF-IDENTITY: trait_sources is keyed
// by the turf; ChangeTurf moves the replaced turf's key to the new turf (change_turf.cs, dm_replace_signal_turf), as tg's
// location reference would, before reoccupy_turf runs as a post-change callback.

/// A bespoke element that adds a set of traits to the turf while occupied by at least one attached movabled.
// tg: code/datums/elements/give_turf_traits.dm:2
public partial class datum_element_give_turf_traits : datum_element
{
    // tg: code/datums/elements/give_turf_traits.dm:10
    [DmParams("target", "traits")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        DmList traits = Dm.arg(__extra, 0) as DmList;
        object dot = await base.Attach(target, Dm.forward(__extra, traits));
        if (!istype(target, typeof(atom_movable)))
            return ELEMENT_INCOMPATIBLE;

        this.traits = traits;

        atom_movable movable = (atom_movable)target;
        RegisterSignal(movable, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        if (isturf(movable.loc))
            await add_to_occupied_turfs((turf)movable.loc, movable);
        return dot;
    }

    // tg: code/datums/elements/give_turf_traits.dm:20
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        atom_movable movable = (atom_movable)source;
        UnregisterSignal(movable, COMSIG_MOVABLE_MOVED);
        if (isturf(movable.loc))
            Waiting.NoSleep(remove_from_occupied_turfs((turf)movable.loc, movable));
        return base.Detach(source, __extra);
    }

    /// Removes the trait from the old turf and adds it to the new one.
    // tg: code/datums/elements/give_turf_traits.dm:27
    [SignalHandler]
    public virtual void on_moved(atom_movable source = null, atom old_loc = null)
    {
        if (isturf(old_loc))
            Waiting.NoSleep(remove_from_occupied_turfs((turf)old_loc, source));

        if (isturf(source.loc))
            Waiting.NoSleep(add_to_occupied_turfs((turf)source.loc, source));
    }

    /**
     * Registers the turf signals if it was previously unoccupied and adds it to the list of occupied turfs.
     * Otherwise, it just adds the movable to the assoc value of lists occupying the turf.
     */
    // tg: code/datums/elements/give_turf_traits.dm:40
    public virtual async DmTask add_to_occupied_turfs(turf location = null, atom_movable source = null)
    {
        object trait_source = REF(source);
        if (trait_sources == null || trait_sources[location] == null)
            RegisterSignal(location, COMSIG_TURF_CHANGE, nameof(pre_change_turf));

        LAZYADDASSOCLIST(ref trait_sources, location, trait_source);
        bool update_movespeeds = Dm.@in(TRAIT_TURF_IGNORE_SLOWDOWN, traits) && !HAS_TRAIT(location, TRAIT_TURF_IGNORE_SLOWDOWN);
        foreach (object trait in Dm.each(traits))
            ADD_TRAIT(location, trait, trait_source);
        if (update_movespeeds)
        {
            foreach (mob_living living in Dm.each<mob_living>(location))
                await living.update_turf_movespeed();
        }
    }

    /**
     * Unregisters the turf signals if it's no longer unoccupied and removes it from the list of occupied turfs.
     * Otherwise, it just removes the movable from the assoc value of lists occupying the turf.
     */
    // tg: code/datums/elements/give_turf_traits.dm:56
    public virtual async DmTask remove_from_occupied_turfs(turf location = null, atom_movable source = null)
    {
        object trait_source = REF(source);
        LAZYREMOVEASSOC(ref trait_sources, location, trait_source);
        if (trait_sources == null || trait_sources[location] == null)
            UnregisterSignal(location, COMSIG_TURF_CHANGE);

        foreach (object trait in Dm.each(traits))
            REMOVE_TRAIT(location, trait, trait_source);

        if (Dm.@in(TRAIT_TURF_IGNORE_SLOWDOWN, traits) && !HAS_TRAIT(location, TRAIT_TURF_IGNORE_SLOWDOWN))
        {
            foreach (mob_living living in Dm.each<mob_living>(location))
                await living.update_turf_movespeed();
        }
    }

    /// Signals are carried over when the turf is changed, but traits aren't, so they've to be readded post-change.
    // tg: code/datums/elements/give_turf_traits.dm:71
    [SignalHandler]
    public virtual void pre_change_turf(turf changed = null, object path = null, DmList new_baseturfs = null, object flags = null, DmList post_change_callbacks = null)
    {
        Dm.append(ref post_change_callbacks, Dm.@new<datum_callback>(this, nameof(reoccupy_turf)));
    }

    /// Reapply turf traits to the provided turf
    // tg: code/datums/elements/give_turf_traits.dm:76
    public virtual void reoccupy_turf(turf changed = null)
    {
        foreach (object trait in Dm.each(traits))
        {
            foreach (object source in Dm.each(trait_sources[changed]))
                ADD_TRAIT(changed, trait, source);
        }
    }
}
