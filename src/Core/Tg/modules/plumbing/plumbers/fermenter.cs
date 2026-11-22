// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/fermenter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111). Gaps P2 (hydroponics): the fermentation of the grown
// produce.

// tg: code/modules/plumbing/plumbers/fermenter.dm:1
public partial class obj_machinery_plumbing_fermenter : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/fermenter.dm:13
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_supply), layer));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/fermenter.dm:16  Initialize's var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_machinery_plumbing_fermenter__Initialize__loc_connections;

    // tg: code/modules/plumbing/plumbers/fermenter.dm:21
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        eat_dir = Dm.num(newdir);
    }

    // tg: code/modules/plumbing/plumbers/fermenter.dm:25
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (!Dm.truthy(anchored))
            return dot;
        if (Dm.eq(border_dir, eat_dir))
            return TRUE;
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/fermenter.dm:32
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        Waiting.NoSleep(ferment(AM));
    }

    /// uses fermentation proc similar to fermentation barrels
    // tg: code/modules/plumbing/plumbers/fermenter.dm:37
    public virtual async DmTask ferment(atom AM = null)
    {
        if (!Dm.truthy(is_operational))
            return;
        if (Dm.truthy(((datum_reagents)reagents).holder_full()))
            return;
        if (!isitem(AM))
            return;
        if (istype(AM, typeof(obj_item_food_grown)))
        {
            var G = (obj_item_food_grown)AM;
            if (Dm.truthy(G.distill_reagent))
            {
                var amount = Dm.num(((obj_item_seeds)G.seed).potency) * 0.25f;
                await ((datum_reagents)reagents).add_reagent(G.distill_reagent, amount);
                await use_energy(Dm.num(active_power_usage) * amount);
                qdel(G);
            }
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/plumbing/plumbers/fermenter.dm:16
    internal DmList obj_machinery_plumbing_fermenter__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_machinery_plumbing_fermenter.on_entered)));
}
