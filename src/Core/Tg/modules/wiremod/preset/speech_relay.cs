// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/preset/speech_relay.dm
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
 * # Speech Relay preset
 *
 * Acts like poly. Says whatever it hears.
 */
// tg: code/modules/wiremod/preset/speech_relay.dm:6
public partial class obj_item_integrated_circuit_loaded_speech_relay : obj_item_integrated_circuit_loaded
{
    // tg: code/modules/wiremod/preset/speech_relay.dm:8
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_item_circuit_component_hear hear = Dm.@new<obj_item_circuit_component_hear>();
        Waiting.NoSleep(add_component(hear));
        hear.rel_x = 100;
        hear.rel_y = 200;

        obj_item_circuit_component_speech speech = Dm.@new<obj_item_circuit_component_speech>();
        Waiting.NoSleep(add_component(speech));
        speech.rel_x = 400;
        speech.rel_y = 200;

        speech.message.connect(hear.message_port);
        return dot;
    }
}
