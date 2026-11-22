// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/wet_stacks_clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole, for the hydro-vaporizer (devices/vaporizer.dm), its only user: while the holder carries it, every
// 10 to 30 seconds it drains its callback's cell and wets them (three wet stacks, a droplet's drip), unless they burn. The var block is
// generated (D111).

/// a component for clothing or objects which will apply wet stacks to whoever wears or holds it
// tg: code/datums/components/wet_stacks_clothing.dm:2
public partial class datum_component_wet_stacks_granting : datum_component
{
    // tg: code/datums/components/wet_stacks_clothing.dm:16
    [DmParams("use_cell", "must_be_worn")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        datum_callback use_cell__1 = Dm.arg<datum_callback>(__extra, 0);
        object must_be_worn__1 = Dm.arg(__extra, 1);
        object dot = await base.Initialize(__extra);
        if (!isobj(parent))
            stack_trace(Dm.text("[] component was applied to something other than an object, [] ([].)", this, parent, ((datum)parent).type), "code/datums/components/wet_stacks_clothing.dm", 19);
        this.use_cell = use_cell__1;
        this.must_be_worn = Dm.num(must_be_worn__1);
        return dot;
    }

    // tg: code/datums/components/wet_stacks_clothing.dm:23
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(start_processing));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(stop_processing));
    }

    // tg: code/datums/components/wet_stacks_clothing.dm:28
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));
    }

    /// proc when picked up or worn by someone
    // tg: code/datums/components/wet_stacks_clothing.dm:33
    [SignalHandler]
    public virtual void start_processing(obj_item source = null, mob_living user = null, object slot = null)
    {
        if (Dm.truthy(must_be_worn) && Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
            return;
        START_PROCESSING(SSobj, this);
        wearer = user as mob_living_carbon;
    }

    /// proc when dropped
    // tg: code/datums/components/wet_stacks_clothing.dm:41
    [SignalHandler]
    public virtual void stop_processing(obj_item source = null, mob_living user = null)
    {
        STOP_PROCESSING(SSobj, this);
        wearer = null;
    }

    // tg: code/datums/components/wet_stacks_clothing.dm:46
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!(tick_cooldown <= world.time)) // COOLDOWN_FINISHED(src, tick_cooldown), inline (rules 11.1 (a))
            return null;
        tick_cooldown = world.time + Dm.num(rand(10 * SECONDS, 30 * SECONDS)); // COOLDOWN_START(src, tick_cooldown, rand(10 SECONDS, 30 SECONDS)), inline
        // cancel if on fire and dousing is false
        datum_status_effect_fire_handler_fire_stacks fire_status = wearer.has_status_effect(typeof(datum_status_effect_fire_handler_fire_stacks)) as datum_status_effect_fire_handler_fire_stacks;
        if (!Dm.truthy(dousing) && Dm.truthy(fire_status))
            return null;
        // try to drain a cell if provided
        if (!Dm.truthy(use_cell != null ? Waiting.NoSleep(use_cell.Invoke()) : null))
            return null;
        playsound(wearer, Dm.resource("sound/effects/droplet.ogg"), rand(15, 35), TRUE, falloff_exponent: 5);
        Waiting.NoSleep(wearer.set_wet_stacks(stacks_to_add, remove_fire_stacks: dousing));
        return null;
    }
}
