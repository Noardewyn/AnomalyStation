// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/research.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): the research log, whole.

public static partial class GlobalProcs
{
    /// Puts the text into the research html file, not log. See [INVESTIGATE_RESEARCH] for [/atom/proc/investigate_log]
    // tg: code/__HELPERS/logging/research.dm:2
    public static void log_research(object text = null)
    {
        if (!Dm.truthy(text))
            return;
        // shortcut: a world that never set a log folder (the test rooms) keeps no log, a server always has one (as investigate_log)
        if (GLOB.log_directory == null)
            return;
        object html_file = Dm.text("[]/[].html", GLOB.log_directory, INVESTIGATE_RESEARCH);
        Dm.text2file(Dm.text("[] []<br>", server_timestamp(format: "YYYY-MM-DD hh:mm:ss"), text), html_file);
    }
}
