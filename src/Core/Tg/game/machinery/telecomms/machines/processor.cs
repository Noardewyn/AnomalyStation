// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/machines/processor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: telecomms): the processor unit and its presets. The var blocks are generated (D111).

/**
 * The processor is a very simple machine that decompresses subspace signals and
 * transfers them back to the original bus. It is essential in producing audible
 * data.
 *
 * They'll link to servers if bus is not present, with some delay added to it.
 */
// tg: code/game/machinery/telecomms/machines/processor.dm:8
public partial class obj_machinery_telecomms_processor : obj_machinery_telecomms
{
    // tg: code/game/machinery/telecomms/machines/processor.dm:20  #define COMPRESSION_AMOUNT_COMPRESSING 100 (file-local)
    private const float COMPRESSION_AMOUNT_COMPRESSING = 100;
    // tg: code/game/machinery/telecomms/machines/processor.dm:21  #define COMPRESSION_AMOUNT_DECOMPRESSING 0 (file-local)
    private const float COMPRESSION_AMOUNT_DECOMPRESSING = 0;

    // tg: code/game/machinery/telecomms/machines/processor.dm:23
    public override async DmTask receive_information(datum_signal signal_in = null, obj_machinery_telecomms machine_from = null)
    {
        datum_signal_subspace signal = signal_in as datum_signal_subspace;
        if (!is_freq_listening(signal))
            return;

        if (Dm.truthy(compressing))
            signal.data["compression"] = COMPRESSION_AMOUNT_COMPRESSING; // We compress the signal even further.
        // Otherwise we just fully decompress it if it was compressed to begin with.
        else if (Dm.truthy(signal.data["compression"]))
            signal.data["compression"] = COMPRESSION_AMOUNT_DECOMPRESSING;

        if (machine_from is obj_machinery_telecomms_bus)
            await relay_direct_information(signal, machine_from); // send the signal back to the machine
        else // no bus detected - send the signal to servers instead
        {
            signal.data["slow"] = Dm.add(signal.data["slow"], Dm.rand(5, 10)); // slow the signal down
            await relay_information(signal, signal.server_type);
        }
    }
}
