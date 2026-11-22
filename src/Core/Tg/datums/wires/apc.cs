// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/apc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only New (M7 slice 3: the APC's wire set, which its lock and its reset read), on_pulse (an EMP pulses the wires,
// wires/_wires.dm:230) and on_cut. The panel availability and status indicators now use the shared wire window.
// The var block is generated (D111).

// tg: code/datums/wires/apc.dm:1
public partial class datum_wires_apc : datum_wires
{
    // tg: code/datums/wires/apc.dm:5
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(
            WIRE_EQUIPMENT,
            WIRE_LIGHT,
            WIRE_ENVIRONMENT,
            WIRE_POWER1,
            WIRE_POWER2,
            WIRE_INTERFACE,
            WIRE_AI
        );
        add_duds(6);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/apc.dm:36
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_power_apc A = (obj_machinery_power_apc)holder;
        object sw__1 = wire;
        if (Dm.eq(sw__1, WIRE_EQUIPMENT))
        {
            A.equipment = Dm.num(A.equipment) > APC_CHANNEL_OFF ? APC_CHANNEL_OFF : APC_CHANNEL_AUTO_ON;
            A.update_appearance();
            A.update();
        }
        else if (Dm.eq(sw__1, WIRE_LIGHT))
        {
            A.lighting = A.lighting > APC_CHANNEL_OFF ? APC_CHANNEL_OFF : APC_CHANNEL_AUTO_ON;
            A.update_appearance();
            A.update();
        }
        else if (Dm.eq(sw__1, WIRE_ENVIRONMENT))
        {
            A.environ = A.environ > APC_CHANNEL_OFF ? APC_CHANNEL_OFF : APC_CHANNEL_AUTO_ON;
            A.update_appearance();
            A.update();
        }
        else if (Dm.eq(sw__1, WIRE_POWER1) || Dm.eq(sw__1, WIRE_POWER2)) // Short for a long while.
        {
            if (!Dm.truthy(A.shorted))
            {
                A.shorted = TRUE;
                _addtimer(Dm.@new<datum_callback>(A, nameof(obj_machinery_power_apc.reset), wire), 2 * MINUTES, file: "code/datums/wires/apc.dm", line: 54);
            }
        }
        else if (Dm.eq(sw__1, WIRE_INTERFACE)) // Unlock for a little while.
        {
            A.locked = FALSE;
            _addtimer(Dm.@new<datum_callback>(A, nameof(obj_machinery_power_apc.reset), wire), 30 * SECONDS, file: "code/datums/wires/apc.dm", line: 57);
        }
        else if (Dm.eq(sw__1, WIRE_AI)) // Disable AI control for a very short time.
        {
            if (!Dm.truthy(A.aidisabled))
            {
                A.aidisabled = TRUE;
                _addtimer(Dm.@new<datum_callback>(A, nameof(obj_machinery_power_apc.reset), wire), 1 * SECONDS, file: "code/datums/wires/apc.dm", line: 61);
            }
        }
    }

    // tg: code/datums/wires/apc.dm:63
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        obj_machinery_power_apc A = (obj_machinery_power_apc)holder;
        object sw__1 = wire;
        if (Dm.eq(sw__1, WIRE_EQUIPMENT))
        {
            A.equipment = Dm.truthy(mend) ? APC_CHANNEL_AUTO_ON : APC_CHANNEL_OFF;
            A.update_appearance();
            A.update();
        }
        else if (Dm.eq(sw__1, WIRE_LIGHT))
        {
            A.lighting = Dm.truthy(mend) ? APC_CHANNEL_AUTO_ON : APC_CHANNEL_OFF;
            A.update_appearance();
            A.update();
        }
        else if (Dm.eq(sw__1, WIRE_ENVIRONMENT))
        {
            A.environ = Dm.truthy(mend) ? APC_CHANNEL_AUTO_ON : APC_CHANNEL_OFF;
            A.update_appearance();
            A.update();
        }
        else if (Dm.eq(sw__1, WIRE_POWER1) || Dm.eq(sw__1, WIRE_POWER2)) // Short out.
        {
            if (Dm.truthy(mend) && !Dm.truthy(is_cut(WIRE_POWER1)) && !Dm.truthy(is_cut(WIRE_POWER2)))
                A.shorted = FALSE;
            else
                A.shorted = TRUE;
            await A.shock__proc(usr as mob_living, 50);
        }
        else if (Dm.eq(sw__1, WIRE_INTERFACE))
            A.locked = Dm.not(mend);
        else if (Dm.eq(sw__1, WIRE_AI)) // Disable AI control.
            A.aidisabled = Dm.not(mend);
    }
}

public partial class datum_wires_apc
{
    public override DmList get_status()
    {
        var apc = (obj_machinery_power_apc)holder;
        return Dm.list(Dm.truthy(apc.locked) ? "The interface light is red." : "The interface light is green.",
            Dm.truthy(apc.shorted) ? "The short indicator is lit." : "The short indicator is off.",
            Dm.truthy(apc.equipment) ? "The channel one light is on." : "The channel one light is off.",
            Dm.truthy(apc.lighting) ? "The channel two light is on." : "The channel two light is off.",
            Dm.truthy(apc.environ) ? "The channel three light is on." : "The channel three light is off.",
            Dm.truthy(apc.aidisabled) ? "The AI connection light is off." : "The AI connection light is on.");
    }

    public override bool interactable(mob user) => base.interactable(user)
        && holder is obj_machinery_power_apc apc && Dm.truthy(apc.panel_open) && !Dm.truthy(apc.opened);
}
