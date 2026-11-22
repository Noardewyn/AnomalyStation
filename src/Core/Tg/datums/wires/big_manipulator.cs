// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/big_manipulator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the big manipulator's wires, whole: pulsing the power wire presses its power button, the drop wire drops what it
// holds, cutting the power wire disables the button; the status lights. The hacking window is the shared wire interface (D162). The
// var block is generated (D111).

// tg: code/datums/wires/big_manipulator.dm:1
public partial class datum_wires_big_manipulator : datum_wires
{
    // tg: code/datums/wires/big_manipulator.dm:5
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(
            WIRE_ON,
            WIRE_DROP,
            WIRE_ITEM_TYPE,
            WIRE_CHANGE_MODE,
            WIRE_ONE_PRIORITY_BUTTON,
            WIRE_THROW_RANGE);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/big_manipulator.dm:16
    public override bool interactable(mob user)
    {
        obj_machinery_big_manipulator holder_manipulator = (obj_machinery_big_manipulator)holder;

        return Dm.truthy(holder_manipulator.panel_open) ? base.interactable(user) : false;
    }

    // tg: code/datums/wires/big_manipulator.dm:21
    public override DmList get_status()
    {
        obj_machinery_big_manipulator holder_manipulator = (obj_machinery_big_manipulator)holder;
        DmList status = Dm.list();
        Dm.append(status, Dm.text("The big light bulb [].", Dm.truthy(holder_manipulator.power_access_wire_cut) ? "is off" : Dm.text("is glowing []", Dm.truthy(holder_manipulator.on) ? "green" : "red")));
        Dm.append(status, Dm.text("The small light bulb [].", holder_manipulator.held_object != null ? "is glowing bright green" : "is off"));
        Dm.append(status, Dm.text("The number on the display shows [].", Dm.length(holder_manipulator.tasks)));
        return status;
    }

    // tg: code/datums/wires/big_manipulator.dm:29
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_big_manipulator holder_manipulator = (obj_machinery_big_manipulator)holder;
        if (Dm.eq(wire, WIRE_ON))
            await holder_manipulator.try_press_on(usr);
        else if (Dm.eq(wire, WIRE_DROP))
            await holder_manipulator.drop_held_atom();
    }

    // tg: code/datums/wires/big_manipulator.dm:37
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        obj_machinery_big_manipulator holder_manipulator = (obj_machinery_big_manipulator)holder;
        if (Dm.eq(wire, WIRE_ON))
        {
            if (Dm.truthy(mend))
            {
                holder_manipulator.power_access_wire_cut = FALSE;
                return;
            }
            holder_manipulator.power_access_wire_cut = TRUE;
        }
    }
}
