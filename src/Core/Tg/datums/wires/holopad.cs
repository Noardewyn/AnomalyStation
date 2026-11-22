// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/holopad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the holopad's wires (loop and replay of a recorded message). The var block is generated (D111).

// tg: code/datums/wires/holopad.dm:1
public partial class datum_wires_holopad : datum_wires
{
    // tg: code/datums/wires/holopad.dm:5
    public override bool interactable(mob user)
    {
        obj_machinery_holopad holopad = holder as obj_machinery_holopad;

        return Dm.truthy(holopad.panel_open) ? base.interactable(user) : false;
    }

    // tg: code/datums/wires/holopad.dm:10
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(
            WIRE_LOOP_MODE,
            WIRE_REPLAY_MODE
        );
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/holopad.dm:17
    public override DmList get_status()
    {
        obj_machinery_holopad holopad = holder as obj_machinery_holopad;
        DmList status = Dm.list();
        Dm.append(ref status, Dm.text("The purple light is [].", Dm.truthy(holopad.loop_mode) ? "on" : "off"));
        Dm.append(ref status, Dm.text("The red light is [].", Dm.truthy(holopad.replay_mode) ? "on" : "off"));
        return status;
    }

    // tg: code/datums/wires/holopad.dm:24
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_holopad holopad = holder as obj_machinery_holopad;
        if (Dm.eq(wire, WIRE_LOOP_MODE))
            holopad.loop_mode = Dm.truthy(holopad.loop_mode) ? FALSE : TRUE;
        else if (Dm.eq(wire, WIRE_REPLAY_MODE))
        {
            if (!Dm.truthy(holopad.replay_mode))
            {
                await holopad.replay_start();
                holopad.replay_mode = TRUE;
            }
            else
            {
                holopad.replay_stop();
                holopad.replay_mode = FALSE;
            }
        }
        await base.on_pulse(wire, user);
    }

    // tg: code/datums/wires/holopad.dm:38
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        obj_machinery_holopad holopad = holder as obj_machinery_holopad;
        if (Dm.eq(wire, WIRE_REPLAY_MODE))
        {
            if (Dm.truthy(mend))
            {
                holopad.replay_stop();
                holopad.replay_mode = FALSE;
            }
            else
            {
                await holopad.replay_start();
                holopad.replay_mode = TRUE;
            }
        }
        await base.on_cut(wire, mend, source);
    }
}
