// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/fax.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298): the fax machine's wires. The var block is generated.

// tg: code/datums/wires/fax.dm:1
public partial class datum_wires_fax : datum_wires
{
    // tg: code/datums/wires/fax.dm:5
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(WIRE_SHOCK, WIRE_SIGNAL, WIRE_THROW, WIRE_LOADCHECK);
        add_duds(1);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/fax.dm:10
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        obj_machinery_fax machine = (obj_machinery_fax)holder;
        if (!HAS_SILICON_ACCESS(user) && machine.seconds_electrified != 0 && Dm.truthy(Waiting.NoSleep(machine.shock__proc(user as mob_living, 100))))
            return false;
        if (Dm.truthy(machine.panel_open))
            return true;
        return false; // tg falls off the end: null
    }

    // tg: code/datums/wires/fax.dm:20
    public override DmList get_status()
    {
        obj_machinery_fax machine = (obj_machinery_fax)holder;
        DmList status = Dm.list();
        Dm.append(ref status, I18n.T("A red light is [].", machine.seconds_electrified != 0 ? "blinking" : "off"));
        Dm.append(ref status, I18n.T("The network light is [].", Dm.truthy(machine.visible_to_network) ? "on" : "off"));
        Dm.append(ref status, I18n.T("The output servo is [].", Dm.truthy(machine.hurl_contents) ? "spinning rapidly" : "on"));
        Dm.append(ref status, I18n.T("The input servo is [].", Dm.truthy(machine.allow_exotic_faxes) ? "spinning rapidly" : "on"));
        return status;
    }

    // tg: code/datums/wires/fax.dm:29
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_fax machine = (obj_machinery_fax)holder;
        switch (wire)
        {
            case WIRE_SHOCK:
                machine.seconds_electrified = MACHINE_DEFAULT_ELECTRIFY_TIME;
                break;
            case WIRE_SIGNAL:
                machine.visible_to_network = Dm.truthy(machine.visible_to_network) ? FALSE : TRUE;
                break;
            case WIRE_THROW:
                machine.hurl_contents = Dm.truthy(machine.hurl_contents) ? FALSE : TRUE;
                break;
            case WIRE_LOADCHECK:
                machine.allow_exotic_faxes = Dm.truthy(machine.allow_exotic_faxes) ? FALSE : TRUE;
                break;
        }
    }

    // tg: code/datums/wires/fax.dm:41
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        obj_machinery_fax machine = (obj_machinery_fax)holder;
        switch (wire)
        {
            case WIRE_SHOCK:
                machine.seconds_electrified = Dm.truthy(mend) ? MACHINE_NOT_ELECTRIFIED : MACHINE_ELECTRIFIED_PERMANENT;
                break;
            case WIRE_SIGNAL:
                machine.visible_to_network = Dm.num(mend);
                break;
            case WIRE_THROW:
                machine.hurl_contents = Dm.truthy(mend) ? FALSE : TRUE;
                break;
            case WIRE_LOADCHECK:
                machine.allow_exotic_faxes = Dm.truthy(mend) ? FALSE : TRUE;
                break;
        }
    }
}
