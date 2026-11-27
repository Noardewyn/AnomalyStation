// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/mass_driver.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the mass driver's wires (a pulse on the launch wire fires it, on the safeties wire it triples its power; cutting
// the safeties puts the power back). The var block is generated (D111).

// tg: code/datums/wires/mass_driver.dm:1
public partial class datum_wires_mass_driver : datum_wires
{
    // tg: code/datums/wires/mass_driver.dm:5
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(WIRE_LAUNCH, WIRE_SAFETIES);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/mass_driver.dm:9
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_mass_driver the_mass_driver = (obj_machinery_mass_driver)holder;
        if (Dm.eq(wire, WIRE_LAUNCH))
        {
            await the_mass_driver.drive();
            holder.visible_message(span_notice(I18n.T("The drive mechanism activates.")));
        }
        else if (Dm.eq(wire, WIRE_SAFETIES))
        {
            the_mass_driver.power = 3;
            holder.visible_message(span_notice(I18n.T("You hear a worrying whirring noise emitting from the mass driver.")));
        }
    }

    // tg: code/datums/wires/mass_driver.dm:19
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        await DmTask.CompletedTask;
        obj_machinery_mass_driver the_mass_driver = (obj_machinery_mass_driver)holder;
        if (Dm.eq(wire, WIRE_SAFETIES))
        {
            if (Dm.num(the_mass_driver.power) > 1)
            {
                the_mass_driver.power = 1;
                holder.visible_message(span_notice(I18n.T("The whirring noise emitting from the mass driver stops.")));
            }
        }
    }
}
