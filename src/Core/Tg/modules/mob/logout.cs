// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/logout.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M1 P1, the client layer).

// tg: code/modules/mob/logout.dm:1
public partial class mob : atom_movable
{
    // tg: code/modules/mob/logout.dm:1  (rules 7.5: /mob/Logout is defined by BYOND (Logout__d0), here and in
    // tgui_input/say_modal/typing.dm, the last of which is the slot's root, typing.cs; this one is Logout__d1)
    internal async DmTask<object> Logout__d1()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_LOGOUT]))
            _SendSignal(COMSIG_MOB_LOGOUT, Dm.list(this));
        log_message(Dm.text("[] is no longer owning mob []([])", key_name(this), this, this.type), LOG_OWNERSHIP);
        // tg: :4  SStgui.on_logout(src)
        SStgui.on_logout(this);
        remove_from_player_list();
        update_ambience_area(null); // Unset ambience vars so it plays again on login
        Logout__d0();

        if (Dm.truthy(loc))
            loc.on_log(FALSE);

        await become_uncliented();

        return TRUE;
    }
}
