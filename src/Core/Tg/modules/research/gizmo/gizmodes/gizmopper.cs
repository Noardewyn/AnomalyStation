// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmodes/gizmopper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the mopper gizmo mode: its holder fills with fifty units of a reagent (water, acid, salt, radium or one random
// reagent) that regenerates two units a second, wets the tiles in a small, medium or large circle with it, blows it out as smoke or
// swaps it for another. The var blocks are generated (D111).

/// Gizmo mode that regenerates, cycles and expells reagents in different functions
// tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:2
public partial class datum_gizmodes_mopper : datum_gizmodes
{
    // tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:34
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);

        for (float i = 1, i__end = random_reagents_to_add; i <= i__end; i++)
            Dm.append(ref reagents, get_random_reagent_id());
    }

    // tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:40
    public override async DmTask activate(atom_movable holder = null)
    {
        if (holder.reagents == null)
        {
            holder.create_reagents(max_volume, reagent_flags);
            await (holder.reagents as datum_reagents).add_reagent(active_reagent, max_volume);
            reagent_holder = holder.reagents as datum_reagents;
            START_PROCESSING(SSdcs, this);
        }
        await base.activate(holder);
    }

    // tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:48  (set waitfor = FALSE, inherited from /datum/process: the wrapper of
    // waiting-model.md 5.4)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null);
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        await reagent_holder.add_reagent(active_reagent, regeneration_speed * Dm.num(seconds_per_tick));
        return null;
    }
}

/// Wet the surounding tiles
// tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:52
public partial class datum_gizpulse_wet_tiles : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:52
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        datum_gizmodes_mopper mopper = (datum_gizmodes_mopper)master; // tg declares the parameter as /datum/gizmodes/mopper
        DmList tiles = get_tiles(holder) as DmList;
        foreach (turf_open tile in Dm.each<turf_open>(tiles))
        {
            await tile.expose_reagents((holder.reagents as datum_reagents).reagent_list, holder.reagents as datum_reagents);

            await (holder.reagents as datum_reagents).expose(tile, TOUCH, 1, mopper.max_volume / Dm.num(tiles.len));
            await (holder.reagents as datum_reagents).remove_reagent(mopper.active_reagent, mopper.max_volume / Dm.num(tiles.len));
        }
    }

    /// Get the tiles to wet
    // tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:61
    public virtual object get_tiles(atom_movable holder = null)
    {
        return null;
    }
}

/// Dump reagents in a circle
// tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:65
public partial class datum_gizpulse_wet_tiles_fluid_circle : datum_gizpulse_wet_tiles
{
    // tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:69
    public override object get_tiles(atom_movable holder = null)
    {
        return RANGE_TURFS(size, holder);
    }
}

/// Make a smoke cloud of our fluid
// tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:85
public partial class datum_gizpulse_fluid_smoke : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:85  (tg names its second parameter interface: it receives the master)
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await do_chem_smoke(3, holder, get_turf(holder), carry: holder.reagents as datum_reagents);
        await (holder.reagents as datum_reagents).clear_reagents();
    }
}

/// Select different reagents
// tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:90
public partial class datum_gizpulse_swap_reagent : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizmopper.dm:90
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        datum_gizmodes_mopper mopper = (datum_gizmodes_mopper)master; // tg declares the parameter as /datum/gizmodes/mopper
        await (holder.reagents as datum_reagents).clear_reagents();
        mopper.active_reagent = pick(Dm.sub(mopper.reagents, mopper.active_reagent) as DmList) as Type; //maybe also add a cycle one instead of random
    }
}
