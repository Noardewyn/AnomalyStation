// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/debug.dm
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
    /// Logging for loading and caching assets
    // tg: code/__HELPERS/logging/debug.dm:2
    public static void log_asset(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_DEBUG_ASSET, text, data);
    }

    /// Logging for config errors
    /// Rarely gets called; just here in case the config breaks.
    // tg: code/__HELPERS/logging/debug.dm:7
    public static void log_config(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_CONFIG, text, data);
        Dm.output_to(world.log, text);
    }

    // tg: code/__HELPERS/logging/debug.dm:11
    public static void log_filter_raw(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_FILTER, text, data);
    }

    /// Logging for job slot changes
    // tg: code/__HELPERS/logging/debug.dm:15
    public static void log_job_debug(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_DEBUG_JOB, text, data);
    }

    /// Logging for TTS
    // tg: code/__HELPERS/logging/debug.dm:19
    public static void log_tts(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_DEBUG_TTS, text, data);
    }

    /// Logging for lua scripting
    // tg: code/__HELPERS/logging/debug.dm:23
    public static void log_lua(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_DEBUG_LUA, text, data);
    }

    /// Logging for mapping errors
    // tg: code/__HELPERS/logging/debug.dm:27
    public static void log_mapping(object text = null, object skip_world_log = null)
    {
        // tg: #ifdef UNIT_TESTS ... #endif not translated (inactive in production)
        // tg: #ifdef MAP_TEST message_admins("Mapping: [text]") #else not translated (inactive in production)
        logger.Log(LOG_CATEGORY_DEBUG_MAPPING, text);
        if (Dm.truthy(skip_world_log))
            return;
        Dm.output_to(world.log, text);
    }

    /// Logging for game performance
    // tg: code/__HELPERS/logging/debug.dm:47
    public static object log_perf(DmList perf_info = null)
    {
        object dot = null;
        dot = Dm.text("[]\n", perf_info.Join(","));
        WRITE_LOG_NO_FORMAT(GLOB.perf_log, dot);
        return dot;
    }

    /// Logging for hard deletes
    // tg: code/__HELPERS/logging/debug.dm:52
    public static void log_qdel(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_QDEL, text, data);
    }

    /* Log to the logfile only. */
    // tg: code/__HELPERS/logging/debug.dm:56
    public static void log_runtime(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_RUNTIME, text, data);
    }

    // tg: code/__HELPERS/logging/debug.dm:59
    public static void log_signal(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_SIGNAL, text, data);
    }

    /// Logging for DB errors
    // tg: code/__HELPERS/logging/debug.dm:63
    public static void log_sql(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_DEBUG_SQL, text, data);
    }

    /// Logging for world/Topic
    // tg: code/__HELPERS/logging/debug.dm:67
    public static void log_topic(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_GAME_TOPIC, text, data);
    }

    /// Log to both DD and the logfile.
    // tg: code/__HELPERS/logging/debug.dm:71
    public static void log_world(object text = null, DmList data = null)
    {
        // tg: #ifdef USE_CUSTOM_ERROR_HANDLER: active (tgstation.dme defines DEBUG, _compile_options.dm:7-9)
        logger.Log(LOG_CATEGORY_RUNTIME, text, data);
        Dm.output_to(world.log, text);
    }
}
