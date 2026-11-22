// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/radio.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M9 slice D: the radio): the radio's wires (a pulse turns its listening or broadcasting; a cut TX wire silences it).
// interactable is the wires window's check, which comes with the hacking interface (D162). The var block is generated (D111).

// tg: code/datums/wires/radio.dm:1
public partial class datum_wires_radio : datum_wires
{
    // tg: code/datums/wires/radio.dm:5
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        wires = Dm.list(
            WIRE_SIGNAL,
            WIRE_RX, WIRE_TX
        );
        await base.New(__extra);
    }

    // tg: code/datums/wires/radio.dm:18
    public override async DmTask on_pulse(object index = null, mob_living user = null)
    {
        obj_item_radio R = (obj_item_radio)holder;
        object sw__1 = index;
        if (Dm.eq(sw__1, WIRE_SIGNAL))
        {
            R.set_listening(Dm.num(!Dm.truthy(R.get_listening())));
            R.set_broadcasting(R.get_listening());
        }
        else if (Dm.eq(sw__1, WIRE_RX))
            R.set_listening(Dm.num(!Dm.truthy(R.get_listening())));
        else if (Dm.eq(sw__1, WIRE_TX))
            R.set_broadcasting(Dm.num(!Dm.truthy(R.get_broadcasting())));
    }
}
