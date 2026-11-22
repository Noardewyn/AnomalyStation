// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plumbing/simple_components.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var blocks are generated (D111). The sinks, showers, toilets and aquariums
// that add these components are not ported yet (their own packages); the plumbed tanks and the plumbing machines are.

///has one pipe input that only takes, example is manual output pipe
// tg: code/datums/components/plumbing/simple_components.dm:3
public partial class datum_component_plumbing_simple_demand : datum_component_plumbing
{
    // tg: code/datums/components/plumbing/simple_components.dm:6
    [DmParams("ducting_layer", "distinct_reagent_cap")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object ducting_layer = Dm.arg(__extra, 0);
        float? distinct_reagent_cap = Dm.arg<float?>(__extra, 1);
        distinct_reagent_cap ??= INFINITY;
        this.distinct_reagent_cap = distinct_reagent_cap.Value;
        return await base.Initialize(Dm.forward(__extra, ducting_layer, distinct_reagent_cap));
    }
}

///Component for adding an extended overlay on wall mounts
// tg: code/datums/components/plumbing/simple_components.dm:11
public partial class datum_component_plumbing_simple_demand_extended : datum_component_plumbing_simple_demand
{
    // tg: code/datums/components/plumbing/simple_components.dm:11
    [SignalHandler]
    public override void create_overlays(atom_movable parent_movable = null, DmList overlays = null)
    {
        base.create_overlays(parent_movable, overlays);

        // This is a little wiggley extension to make wallmounts like sinks and showers visually link to the pipe
        if (Dm.truthy(overlays.len))
        {
            image edge_overlay = image(Dm.resource("icons/obj/pipes_n_cables/hydrochem/connects.dmi"), icon_state: "edge-extension", layer: PLUMBING_PIPE_VISIBILE_LAYER + ducting_layer * 0.0003f);
            edge_overlay.dir = Dm.num(parent_movable.dir);
            edge_overlay.color = demand_color;
            edge_overlay.pixel_w = -parent_movable.pixel_x - parent_movable.pixel_w;
            edge_overlay.pixel_z = -parent_movable.pixel_y - parent_movable.pixel_z;
            Dm.append(ref overlays, edge_overlay);
        }
    }
}

///For disposing reagents
// tg: code/datums/components/plumbing/simple_components.dm:24
public partial class datum_component_plumbing_simple_demand_disposer : datum_component_plumbing_simple_demand
{
    // tg: code/datums/components/plumbing/simple_components.dm:26
    [DmParams("ducting_layer", "distinct_reagent_cap")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!istype(parent, typeof(obj_machinery_plumbing_disposer)))
            return COMPONENT_INCOMPATIBLE;
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/plumbing/simple_components.dm:31
    public override async DmTask send_request(object dir = null)
    {
        obj_machinery_plumbing_disposer target = (obj_machinery_plumbing_disposer)parent;
        if (Dm.truthy(target.on))
            await process_request(target.disposal_rate * SSFLUIDS_DT(), dir: dir);
    }
}

///Lazily demand from any direction.
// tg: code/datums/components/plumbing/simple_components.dm:46
public partial class datum_component_plumbing_aquarium : datum_component_plumbing
{
    // tg: code/datums/components/plumbing/simple_components.dm:49
    [SignalHandler]
    public override void create_overlays(atom_movable parent_movable = null, DmList overlays = null)
    {
        //Overlays won't look good, and the aquarium sprite occupies about the entire 32x32 area anyway.
        return;
    }
}

///Connects different layer of ducts
// tg: code/datums/components/plumbing/simple_components.dm:54
public partial class datum_component_plumbing_manifold : datum_component_plumbing
{
    // tg: code/datums/components/plumbing/simple_components.dm:58
    [SignalHandler]
    public override void change_ducting_layer(obj source = null, obj changer = null, float? new_layer = null)
    {
        return;
    }
}
