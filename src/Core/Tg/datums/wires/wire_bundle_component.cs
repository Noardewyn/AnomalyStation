// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/wire_bundle_component.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the wires a circuit wire bundle component gives its shell. The hacking window is left
// out with wire hacking (D162). The var block is generated (D111).

// tg: code/datums/wires/wire_bundle_component.dm:3
public partial class datum_wires_wire_bundle_component : datum_wires
{
    // tg: code/datums/wires/wire_bundle_component.dm:1  (#undef at :27)
    private const float CAPACITY_PER_WIRE = 10;

    // tg: code/datums/wires/wire_bundle_component.dm:8
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        datum_component_shell shell_comp = holder.GetComponent(typeof(datum_component_shell)) as datum_component_shell;
        if (!istype(shell_comp, typeof(datum_component_shell)))
            throw Dm.CRASH("Holder does not have a shell component!", "code/datums/wires/wire_bundle_component.dm", 11);
        float wire_count = Dm.clamp(Dm.round(shell_comp.capacity / CAPACITY_PER_WIRE, 1), 1, MAX_WIRE_COUNT);
        for (float index = 1; index <= wire_count; index++)
            LAZYADD(ref wires, Dm.text("Port []", index));
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/wire_bundle_component.dm:17
    public override bool always_reveal_wire(object color)
    {
        return true; // Let's not make wiring up this stuff confusing - just give them what wires correspond to what ports.
    }

    // tg: code/datums/wires/wire_bundle_component.dm:20
    public override async DmTask<object> ui_data(mob user = null)
    {
        proper_name = Dm.str(holder.name);
        return await base.ui_data(user);
    }

    // tg: code/datums/wires/wire_bundle_component.dm:24
    public override object ui_host(mob user = null)
    {
        return holder.ui_host(user);
    }
}
