// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/tgui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only GLOB.emote_panel (M9 slice B) and GLOB.changelog_tgui (M10x slice H); the hotkeys help window comes with its module.

// tg: code/_globalvars/tgui.dm:4  GLOBAL_DATUM(emote_panel, /datum/emote_panel)
public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/tgui.dm:2  GLOBAL_DATUM(changelog_tgui, /datum/changelog)
    public datum_changelog changelog_tgui;

    // tg: code/_globalvars/tgui.dm:2  (GLOBAL_UNMANAGED(changelog_tgui), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalchangelog_tgui()
    {
        return;
    }

    // tg: code/_globalvars/tgui.dm:4  GLOBAL_DATUM(emote_panel, /datum/emote_panel)
    public datum_emote_panel emote_panel;

    // tg: code/_globalvars/tgui.dm:4  (GLOBAL_UNMANAGED(emote_panel), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalemote_panel()
    {
        return;
    }
}
