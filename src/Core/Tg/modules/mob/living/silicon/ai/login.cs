// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/login.dm, logout.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole files (silicons SI4, D326).

// tg: code/modules/mob/living/silicon/ai/login.dm:1
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/ai/login.dm:1
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;
        if (stat != DEAD)
        {
            if (lacks_power() && apc_override != null) //Placing this in Login() in case the AI doesn't have this link for whatever reason.
                to_chat(usr, Dm.text("[] <A href='byond://?src=[];emergencyAPC=[]'>", span_warning(I18n.T("Main power is unavailable, backup power in use. Diagnostics scan complete.")), REF(this), TRUE) + I18n.T("Local APC ready for connection.") + "</A>");
        }
        set_eyeobj_visible(TRUE);
        if (Dm.truthy(multicam_on))
            end_multicam();
        await view_core();
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/logout.dm:1
    public override async DmTask<object> Logout()
    {
        await base.Logout();
        set_eyeobj_visible(FALSE);
        await view_core();
        return null;
    }
}
