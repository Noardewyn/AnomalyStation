// Translated from tgstation (snapshot 1d99bd7f): code/datums/greyscale/_greyscale_config.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with clothing and the GAGS configs (M4a). Holds the [DmPath] of /datum/greyscale_config, which
// SSgreyscale.GetColoredIconByType tests (M2 P3), and the vars that name a configuration's files, which the client's GAGS
// composition reads from tg's sources (D104); the config procs (Refresh, Generate, ...) are not declared.

/// <summary>
/// Datum that holds the json configuration and icon file for a greyscale icon (tg's comment on the type).
/// </summary>
// tg: code/datums/greyscale/_greyscale_config.dm:4
[DmPath("/datum/greyscale_config")]
public partial class datum_greyscale_config : datum
{
    /// User friendly name used in the debug menu
    // tg: code/datums/greyscale/_greyscale_config.dm:6
    public object name;

    /// Reference to the json config file
    // tg: code/datums/greyscale/_greyscale_config.dm:9
    public object json_config;

    /// Reference to the dmi file for this config
    // tg: code/datums/greyscale/_greyscale_config.dm:12
    public object icon_file;
}
