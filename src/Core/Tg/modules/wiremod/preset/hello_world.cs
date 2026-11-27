// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/preset/hello_world.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # Hello World preset
 *
 * Says "Hello World" when triggered. Needs to be wired up and connected first.
 */
// tg: code/modules/wiremod/preset/hello_world.dm:6
public partial class obj_item_integrated_circuit_loaded_hello_world : obj_item_integrated_circuit_loaded
{
    // tg: code/modules/wiremod/preset/hello_world.dm:8
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_item_circuit_component_speech speech = Dm.@new<obj_item_circuit_component_speech>();
        Waiting.NoSleep(add_component(speech));

        speech.message.set_input("Hello World");
        return dot;
    }
}
