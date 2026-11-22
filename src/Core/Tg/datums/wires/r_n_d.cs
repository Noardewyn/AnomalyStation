// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/r_n_d.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD0, D333: the research machines' base): the R&D machines' wires (hack, disable, shock). The var block
// is generated (D111).

// tg: code/datums/wires/r_n_d.dm:1
public partial class datum_wires_rnd : datum_wires
{
    // tg: code/datums/wires/r_n_d.dm:6
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(
            WIRE_HACK, WIRE_DISABLE,
            WIRE_SHOCK
        );
        add_duds(5);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/r_n_d.dm:14
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        var R = (obj_machinery_rnd)holder;
        return Dm.truthy(R.panel_open);
    }

    // tg: code/datums/wires/r_n_d.dm:21
    public override DmList get_status()
    {
        var R = (obj_machinery_rnd)holder;
        DmList status = Dm.list();
        Dm.append(ref status, I18n.T("The red light is [].", Dm.truthy(R.disabled) ? "off" : "on"));
        Dm.append(ref status, I18n.T("The blue light is [].", Dm.truthy(R.hacked) ? "off" : "on"));
        return status;
    }

    // tg: code/datums/wires/r_n_d.dm:28
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        await DmTask.CompletedTask; // tg's set waitfor = FALSE: the body never sleeps
        var R = (obj_machinery_rnd)holder;
        if (Dm.eq(wire, WIRE_HACK))
            R.hacked = Dm.truthy(R.hacked) ? FALSE : TRUE;
        else if (Dm.eq(wire, WIRE_DISABLE))
            R.disabled = Dm.truthy(R.disabled) ? FALSE : TRUE;
    }

    // tg: code/datums/wires/r_n_d.dm:37
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        await DmTask.CompletedTask;
        var R = (obj_machinery_rnd)holder;
        if (Dm.eq(wire, WIRE_HACK))
            R.hacked = Dm.truthy(mend) ? FALSE : TRUE;
        else if (Dm.eq(wire, WIRE_DISABLE))
            R.disabled = Dm.truthy(mend) ? FALSE : TRUE;
    }
}
