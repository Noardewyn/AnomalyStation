// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/fish_safe_storage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish kept in stasis, fed and healed inside a fish case, a nugget box, a toilet, a moisture trap or a deep
// fryer. The var block is generated (D111).

///An element that puts in stasis any fish that enters the atom.
// tg: code/datums/elements/fish_safe_storage.dm:2
public partial class datum_element_fish_safe_storage : datum_element
{
    // tg: code/datums/elements/fish_safe_storage.dm:6
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        START_PROCESSING(SSprocessing, this);
    }

    // tg: code/datums/elements/fish_safe_storage.dm:10
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ATOM_ENTERED, nameof(on_enter));
        RegisterSignal(target, COMSIG_ATOM_EXITED, nameof(on_exit));
        RegisterSignal(target, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(on_init_on));
        ADD_TRAIT(target, TRAIT_STOP_FISH_FLOPPING, REF(this));
        foreach (obj_item_fish fish in Dm.each<obj_item_fish>(target))
        {
            Dm.combine(tracked_fish, fish);
            ADD_TRAIT(fish, TRAIT_FISH_STASIS, REF(this));
        }
        return dot;
    }

    // tg: code/datums/elements/fish_safe_storage.dm:23
    public override object Detach(datum source = null, params object[] __extra)
    {
        foreach (obj_item_fish fish in Dm.each<obj_item_fish>(source))
        {
            Dm.remove(tracked_fish, fish);
            REMOVE_TRAIT(fish, TRAIT_FISH_STASIS, REF(this));
        }
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_EXITED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));
        REMOVE_TRAIT(source, TRAIT_STOP_FISH_FLOPPING, REF(this));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/fish_safe_storage.dm:31
    [SignalHandler]
    public virtual void on_enter(datum source = null, obj_item_fish arrived = null)
    {
        if (isfish(arrived))
        {
            Dm.combine(tracked_fish, arrived);
            ADD_TRAIT(arrived, TRAIT_FISH_STASIS, REF(this));
        }
    }

    // tg: code/datums/elements/fish_safe_storage.dm:37
    [SignalHandler]
    public virtual void on_init_on(datum source = null, obj_item_fish created = null)
    {
        if (isfish(created) && !QDELETED(created))
        {
            Dm.combine(tracked_fish, created);
            ADD_TRAIT(created, TRAIT_FISH_STASIS, REF(this));
        }
    }

    // tg: code/datums/elements/fish_safe_storage.dm:43
    [SignalHandler]
    public virtual void on_exit(datum source = null, obj_item_fish gone = null)
    {
        if (isfish(gone))
        {
            Dm.remove(tracked_fish, gone);
            REMOVE_TRAIT(gone, TRAIT_FISH_STASIS, REF(this));
        }
    }

    // tg: code/datums/elements/fish_safe_storage.dm:49
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        foreach (obj_item_fish fish in Dm.each_cast<obj_item_fish>(tracked_fish))
        {
            ///Keep delaying hunger and breeding while in stasis, and also heal them.
            fish.last_feeding = Dm.num(fish.last_feeding) + Dm.num(seconds_per_tick) * SECONDS;
            fish.breeding_wait = Dm.num(fish.breeding_wait) + Dm.num(seconds_per_tick) * SECONDS;
            if (Dm.num(fish.get_health_percentage()) < 0.65f)
                Waiting.NoSleep(fish.repair_damage(0.75f * Dm.num(seconds_per_tick)));
        }
        return null;
    }
}
