// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/components/avatar_gear.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole, for the lead-acid battery (game/objects/items/maintenance_loot.cs). The component follows the
// topmost human carrying its item and answers their bitrunner's gear signal with the item's callback; the quantum server sends that
// signal when it stocks a new avatar (gaps BR1, server/obj_generation.cs). As in tg, UnregisterFromParent deletes the weak reference to its container tracker, not
// the tracker. The var block is generated (D111).

/// Tracks the highest up human carrying this item, and loads bitrunning gear onto their avatar when they enter a virtual domain
// tg: code/modules/bitrunning/components/avatar_gear.dm:3
public partial class datum_component_loads_avatar_gear : datum_component
{
    // tg: code/modules/bitrunning/components/avatar_gear.dm:11
    [DmParams("load_callback")]
    public override DmTask<object> Initialize(params object[] __extra)
    {
        datum_callback load_callback = Dm.arg(__extra, 0) as datum_callback;
        if (!isitem(parent))
            return DmTask<object>.FromResult(COMPONENT_INCOMPATIBLE);

        this.load_callback = load_callback;
        return DmTask<object>.FromResult(null);
    }

    // tg: code/modules/bitrunning/components/avatar_gear.dm:17
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_ENTERING, nameof(on_entered_loc));

        connect_ref = WEAKREF(await AddComponent(typeof(datum_component_connect_containers), parent, RegisterWithParent__loc_connections) as datum) as datum_weakref;
    }

    // tg: code/modules/bitrunning/components/avatar_gear.dm:20  RegisterWithParent's var/static/list/loc_connections (rules 10.6)
    private static DmList RegisterWithParent__loc_connections => TgState.Current.datum_component_loads_avatar_gear__RegisterWithParent__loc_connections;

    // tg: code/modules/bitrunning/components/avatar_gear.dm:25
    public override DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_ENTERING
        ));

        qdel(connect_ref);
        return DmTask.CompletedTask;
    }

    // tg: code/modules/bitrunning/components/avatar_gear.dm:32
    public override object Destroy(float? force = null)
    {
        load_callback = null;
        tracked_human_ref = null;
        return base.Destroy(force);
    }


    // tg: code/modules/bitrunning/components/avatar_gear.dm:38
    [SignalHandler]
    public virtual void on_entered_loc(datum source = null, atom destination = null, atom old_loc = null, DmList old_locs = null)
    {
        // No need to do checks if we're just moving turfs
        if (isturf(destination) && isturf(old_loc))
            return;

        // Iterate over list in reverse so we get the topmost human first
        // Because it's funnier if you get to use the avatar gear of the people you're carrying
        DmList nested_locs = get_nested_locs(parent as atom_movable) as DmList;
        for (float i = length(nested_locs), i__end = 1; i >= i__end; i += -1)
        {
            atom container = nested_locs[i] as atom;
            if (ishuman(container))
            {
                switch_tracking(container as mob_living_carbon_human);
                return;
            }
        }

        // No humans found, stop tracking
        switch_tracking(null);
    }

    // tg: code/modules/bitrunning/components/avatar_gear.dm:57
    public virtual void switch_tracking(mob_living_carbon_human to_track = null)
    {
        mob_living_carbon_human tracked_human = tracked_human_ref?.resolve() as mob_living_carbon_human;
        if (tracked_human == to_track)
            return;

        if (Dm.truthy(tracked_human))
            UnregisterSignal(tracked_human, COMSIG_BITRUNNER_STOCKING_GEAR);
        tracked_human_ref = WEAKREF(to_track) as datum_weakref;
        RegisterSignal(to_track, COMSIG_BITRUNNER_STOCKING_GEAR, nameof(load_onto_avatar));
    }

    // tg: code/modules/bitrunning/components/avatar_gear.dm:67
    [SignalHandler]
    public virtual object load_onto_avatar(mob_living_carbon_human neo = null, mob_living_carbon_human avatar = null, object domain_flags = null)
    {
        return load_callback == null ? null : Waiting.NoSleep(load_callback.Invoke(neo, avatar, domain_flags));
    }
}

// tg: code/modules/bitrunning/components/avatar_gear.dm:20
public sealed partial class TgState
{
    internal DmList datum_component_loads_avatar_gear__RegisterWithParent__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERING, nameof(datum_component_loads_avatar_gear.on_entered_loc)));
}
