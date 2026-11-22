// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/simple_machines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var blocks are generated (D111).

///We can empty beakers in here and everything
// tg: code/modules/plumbing/plumbers/simple_machines.dm:2
public partial class obj_machinery_plumbing_input : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/simple_machines.dm:9
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_supply), layer));
        return dot;
    }
}

///We can fill beakers in here and everything. we dont inheret from input because it has nothing that we need
// tg: code/modules/plumbing/plumbers/simple_machines.dm:14
public partial class obj_machinery_plumbing_output : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/simple_machines.dm:22
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_demand), layer, Dm.named("distinct_reagent_cap", 5)));
        return dot;
    }
}

///For storing large volume of reagents
// tg: code/modules/plumbing/plumbers/simple_machines.dm:33
public partial class obj_machinery_plumbing_tank : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/simple_machines.dm:39
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_tank), layer));
        return dot;
    }
}

///Layer manifold machine that connects a bunch of layers
// tg: code/modules/plumbing/plumbers/simple_machines.dm:44
public partial class obj_machinery_plumbing_layer_manifold : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/simple_machines.dm:50
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_manifold), FIRST_DUCT_LAYER));
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_manifold), SECOND_DUCT_LAYER));
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_manifold), THIRD_DUCT_LAYER));
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_manifold), FOURTH_DUCT_LAYER));
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_manifold), FIFTH_DUCT_LAYER));
        return dot;
    }
}
