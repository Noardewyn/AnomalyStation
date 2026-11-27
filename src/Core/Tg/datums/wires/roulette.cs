// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/roulette.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the roulette table's wires (the owner reset, the prize vend, the shock and the bolts). The var block is generated.

// tg: code/datums/wires/roulette.dm:1
public partial class datum_wires_roulette : datum_wires
{
    // tg: code/datums/wires/roulette.dm:6
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(WIRE_RESETOWNER, WIRE_PRIZEVEND, WIRE_SHOCK, WIRE_BOLTS);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/roulette.dm:15
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        var R = (obj_machinery_roulette)holder;
        if (Dm.truthy(Dm.bitand(R.machine_stat, MAINT)))
            return true;
        return false;
    }

    // tg: code/datums/wires/roulette.dm:23
    public override DmList get_status()
    {
        var R = (obj_machinery_roulette)holder;
        DmList status = Dm.list();
        Dm.append(ref status, I18n.T("The machines bolts []", Dm.truthy(R.anchored) ? I18n.T("have engaged!") : I18n.T("have disengaged.")));
        Dm.append(ref status, I18n.T("The main circuit is [].", Dm.truthy(R.on) ? "on" : "off"));
        Dm.append(ref status, I18n.T("The main system lock appears to be [].", Dm.truthy(R.locked) ? "on" : "off"));
        Dm.append(ref status, I18n.T("The account balance system appears to be [].", R.my_card != null ? I18n.T("connected to []", R.my_card.registered_account.account_holder) : I18n.T("disconnected")));
        return status;
    }

    // tg: code/datums/wires/roulette.dm:32
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        var R = (obj_machinery_roulette)holder;
        switch (wire)
        {
            case WIRE_SHOCK:
                await R.shock__proc(usr as mob_living, 50);
                break;
            case WIRE_BOLTS: // Pulse to toggle bolts (but only raise if power is on).
                if (!Dm.truthy(R.on))
                    return;
                await R.set_anchored(Dm.num(!Dm.truthy(R.anchored)));
                break;
            case WIRE_RESETOWNER:
                R.my_card = null;
                R.audible_message(span_warning(I18n.T("Owner reset!")));
                R.locked = FALSE;
                break;
            case WIRE_PRIZEVEND:
                await R.shock__proc(usr as mob_living, 70);
                if (Dm.truthy(R.locked))
                    return;
                R.audible_message(span_warning(I18n.T("Unauthorized prize vend detected! Locking down machine!")));
                R.prize_theft(0.20f);
                break;
        }
    }

    // tg: code/datums/wires/roulette.dm:52
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        var R = (obj_machinery_roulette)holder;
        switch (wire)
        {
            case WIRE_SHOCK:
                await R.shock__proc(usr as mob_living, 60);
                if (Dm.truthy(mend))
                    R.on = TRUE;
                else
                    R.on = FALSE;
                break;
            case WIRE_BOLTS: // Always drop
                if (!Dm.truthy(R.on))
                    return;
                await R.set_anchored(TRUE);
                break;
            case WIRE_RESETOWNER:
                await R.shock__proc(usr as mob_living, 70);
                break;
            case WIRE_PRIZEVEND:
                await R.shock__proc(usr as mob_living, 75);
                if (Dm.truthy(R.locked))
                    return;
                R.audible_message(span_warning(I18n.T("Unauthorized prize vend detected! Locking down machine!")));
                R.prize_theft(0.10f);
                break;
        }
    }
}
