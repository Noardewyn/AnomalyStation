// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/manifest.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    /// Logging for player manifest (ckey, name, job, special role, roundstart/latejoin)
    // tg: code/__HELPERS/logging/manifest.dm:2
    public static void log_manifest(object ckey = null, datum_mind mind = null, mob body = null, object latejoin = null)
    {
        latejoin ??= FALSE;
        object roles = english_list(mind.get_special_roles() as DmList, nothing_text: "NONE");
        string message = Dm.text("[] \\\\ [] \\\\ [] \\\\ [] \\\\ []", ckey, body.real_name, mind.assigned_role.title, roles, Dm.truthy(latejoin) ? "LATEJOIN" : "ROUNDSTART");
        logger.Log(LOG_CATEGORY_MANIFEST, message, Dm.list(
            Dm.kv("mind", mind), Dm.kv("body", body), Dm.kv("latejoin", latejoin)));
        // Roundstart happens with SSblackbox.ReportRoundstartManifest
        // if(latejoin) SSblackbox.ReportManifest(...): the database's manifest table (SSdbcore, M10+)
    }
}
