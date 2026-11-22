// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/mouse_entered.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (D125, docs/ai/specs/interaction.md 6.1): the subsystem with tg's flags (SS_NO_INIT | SS_TICKER, wait 1)
// and its fire. The var block (name, wait, flags, priority, runlevels, hovers) is generated.

/// Defers MouseEntered inputs to only apply to the most recently hovered over atom in the tick
// tg: code/controllers/subsystem/mouse_entered.dm:2 SUBSYSTEM_DEF(mouse_entered)
public partial class datum_controller_subsystem_mouse_entered : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(mouse_entered) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSmouse_entered)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/mouse_entered.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSmouse_entered != this)
        {
            if (SSmouse_entered is datum_controller_subsystem_mouse_entered)
            {
                Recover();
                qdel(SSmouse_entered);
            }
            SSmouse_entered = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/mouse_entered.dm:11
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        foreach (object hovering_client in Dm.each(hovers))
        {
            atom hovering_atom = hovers[hovering_client] as atom;
            if (hovering_atom == null)
                continue;

            hovering_atom.on_mouse_enter(hovering_client as client);

            // This intentionally runs `= null` and not `-= hovering_client`, as we want to prevent the list from shrinking,
            // which could cause problems given the heat of MouseEntered.
            // Lummox has teased this for 515: https://www.byond.com/forum/post/2621745
            // ...though you're most likely reading this on BYOND version 600.
            hovers[hovering_client] = null;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/mouse_entered.dm:2  SUBSYSTEM_DEF(mouse_entered): GLOBAL_REAL(SSmouse_entered, /datum/controller/subsystem/mouse_entered)
    internal datum_controller_subsystem_mouse_entered SSmouse_entered;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_mouse_entered SSmouse_entered => ref TgState.Current.SSmouse_entered;
}
