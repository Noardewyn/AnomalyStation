// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/logging.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only WRITE_LOG and WRITE_LOG_NO_FORMAT translated (the logging package, wave 3). The constants are generated
// (Defines); DIRECT_OUTPUT, SEND_TEXT, SEND_SOUND, SEND_IMAGE and WRITE_FILE are Dm.output_to (rules 12); DIRECT_INPUT and
// READ_FILE come with their first caller.

public static partial class GlobalProcs
{
    //This is an external call, "true" and "false" are how rust parses out booleans
    // tg: code/__DEFINES/logging.dm:196  #define WRITE_LOG(log, text) rustg_log_write(log, text, "true")
    public static string WRITE_LOG(object log, object text) => rustg_log_write(log, text, "true");

    // tg: code/__DEFINES/logging.dm:197  #define WRITE_LOG_NO_FORMAT(log, text) rustg_log_write(log, text, "false")
    public static string WRITE_LOG_NO_FORMAT(object log, object text) => rustg_log_write(log, text, "false");
}
