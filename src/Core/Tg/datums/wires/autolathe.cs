// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/autolathe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD1, D333: the fabricators): the autolathe's wires (hack, disable, shock and zap), a pulse undone after
// six seconds. The var block is generated (D111).

// tg: code/datums/wires/autolathe.dm:1
public partial class datum_wires_autolathe : datum_wires
{
    // tg: code/datums/wires/autolathe.dm:5
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(
            WIRE_HACK, WIRE_DISABLE,
            WIRE_SHOCK, WIRE_ZAP
        );
        add_duds(6);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/autolathe.dm:13
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        var A = (obj_machinery_autolathe)holder;
        if (Dm.truthy(A.panel_open))
            return true;
        return false;
    }

    // tg: code/datums/wires/autolathe.dm:20
    public override DmList get_status()
    {
        var A = (obj_machinery_autolathe)holder;
        DmList status = Dm.list();
        Dm.append(ref status, I18n.T("The red light is [].", Dm.truthy(A.disabled) ? "on" : "off"));
        Dm.append(ref status, I18n.T("The blue light is [].", Dm.truthy(A.hacked) ? "on" : "off"));
        return status;
    }

    // tg: code/datums/wires/autolathe.dm:27
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        var A = (obj_machinery_autolathe)holder;
        object sw__1 = wire;
        if (Dm.eq(sw__1, WIRE_HACK))
        {
            await A.adjust_hacked(Dm.num(!Dm.truthy(A.hacked)));
            _addtimer(Dm.@new<datum_callback>(A, nameof(obj_machinery_autolathe.reset), wire), 6 * SECONDS, file: "code/datums/wires/autolathe.dm", line: 32);
        }
        else if (Dm.eq(sw__1, WIRE_SHOCK))
        {
            A.shocked = Dm.num(!Dm.truthy(A.shocked));
            _addtimer(Dm.@new<datum_callback>(A, nameof(obj_machinery_autolathe.reset), wire), 6 * SECONDS, file: "code/datums/wires/autolathe.dm", line: 35);
        }
        else if (Dm.eq(sw__1, WIRE_DISABLE))
        {
            A.disabled = Dm.num(!Dm.truthy(A.disabled));
            _addtimer(Dm.@new<datum_callback>(A, nameof(obj_machinery_autolathe.reset), wire), 6 * SECONDS, file: "code/datums/wires/autolathe.dm", line: 38);
        }
    }

    // tg: code/datums/wires/autolathe.dm:40
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        var A = (obj_machinery_autolathe)holder;
        object sw__1 = wire;
        if (Dm.eq(sw__1, WIRE_HACK))
            await A.adjust_hacked(Dm.num(!Dm.truthy(mend)));
        else if (Dm.eq(sw__1, WIRE_SHOCK))
            A.shocked = Dm.num(!Dm.truthy(mend));
        else if (Dm.eq(sw__1, WIRE_DISABLE))
            A.disabled = Dm.num(!Dm.truthy(mend));
        else if (Dm.eq(sw__1, WIRE_ZAP))
            await A.shock__proc(usr as mob_living, 50);
    }
}
