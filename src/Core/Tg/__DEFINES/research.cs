// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/research.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): the function-like macro of research.dm that the machines linking to the station's techweb use (rules 11.1). The
// constant defines are generated.

public static partial class GlobalProcs
{
    /// <summary>
    /// tg: code/__DEFINES/research.dm:112  #define CONNECT_TO_RND_SERVER_ROUNDSTART(server_var, holder): the first research server on a
    /// valid level, else the station's science techweb. A host-built room without the Master has no SSresearch (rules 10.6): no web.
    /// </summary>
    public static void CONNECT_TO_RND_SERVER_ROUNDSTART<T>(ref T server_var, atom holder) where T : datum_techweb
    {
        DmList found_servers = SSresearch?.get_available_servers(get_turf(holder)) ?? Dm.list();
        obj_machinery_rnd_server selected_server = Dm.truthy(length(found_servers)) ? found_servers[1] as obj_machinery_rnd_server : null;
        if (selected_server != null)
            server_var = selected_server.stored_research as T;
        else
        {
            datum_techweb station_fallback_web = null;
            foreach (object web in Dm.each(SSresearch?.techwebs))
            {
                if (web is datum_techweb_science science)
                {
                    station_fallback_web = science;
                    break;
                }
            }
            server_var = station_fallback_web as T;
        }
    }
}
