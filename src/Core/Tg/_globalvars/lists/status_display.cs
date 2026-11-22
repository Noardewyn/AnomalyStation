// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/status_display.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G5: the file (the communications console's status display page).

// tg: code/_globalvars/lists/status_display.dm:2
public partial class datum_controller_global_vars : datum_controller
{
    // All valid inputs to status display post_status
    // tg: code/_globalvars/lists/status_display.dm:2
    public DmList status_display_approved_pictures;

    // tg: code/_globalvars/lists/status_display.dm:2  (GLOBAL_MANAGED(status_display_approved_pictures, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstatus_display_approved_pictures()
    {
        status_display_approved_pictures = Dm.list(
            "blank",
            "shuttle",
            "default",
            "biohazard",
            "lockdown",
            "greenalert",
            "bluealert",
            "redalert",
            "deltaalert",
            "radiation",
            "currentalert"); //For automatic set of status display on current level
        Dm.append(ref gvars_datum_init_order, "status_display_approved_pictures");
    }

    // Members of status_display_approved_pictures that are actually states and not alert values
    // tg: code/_globalvars/lists/status_display.dm:17
    public DmList status_display_state_pictures;

    // tg: code/_globalvars/lists/status_display.dm:17  (GLOBAL_MANAGED(status_display_state_pictures, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstatus_display_state_pictures()
    {
        status_display_state_pictures = Dm.list(
            "blank",
            "shuttle");
        Dm.append(ref gvars_datum_init_order, "status_display_state_pictures");
    }
}