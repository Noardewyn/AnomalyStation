// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/dbcore.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: an inert subsystem (lead decision W3 G2) that keeps tg's [DmPath], name, init_stage and dependencies, so that
// Master/New creates it and the dependency graph of the subsystems that name it resolves (controllers/master.dm:361-363), but
// neither initializes nor fires; SQL is not translated (rules 13). M10 slice I: the round row (InitializeRound, SetRoundStart,
// SetRoundEnd) through the host's database (RoundDatabaseSeam). Connect, IsConnected and Disconnect are tg's own, down to the
// sql_enabled check: with the database off (the port's case) they report no connection, so the callers take tg's no-database
// paths (the library's consoles); the connection pool behind an enabled config is not translated.

// tg: code/controllers/subsystem/dbcore.dm:2 SUBSYSTEM_DEF(dbcore)
[DmPath("/datum/controller/subsystem/dbcore")]
public partial class datum_controller_subsystem_dbcore : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/dbcore.dm:3
    public datum_controller_subsystem_dbcore()
    {
        name = "Database";
        init_stage = INITSTAGE_FIRST;
        // STUB(M10): SS_NO_INIT | SS_NO_FIRE instead of tg's flags (SS_TICKER, wait 10, runlevels LOBBY|DEFAULT) until the stub is translated.
        ss_flags = Dm.bitor(SS_NO_INIT, SS_NO_FIRE);
    }

    // The New() that SUBSYSTEM_DEF(dbcore) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSdbcore) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/dbcore.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSdbcore != this)
        {
            if (SSdbcore is datum_controller_subsystem_dbcore)
            {
                Recover();
                qdel(SSdbcore);
            }
            SSdbcore = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/dbcore.dm:250
    // The checks before the connection are tg's; the connection itself (start_db_daemon and rust-g's SQL pool, :266-301) is not
    // translated (rules 13): the port has no tg database (the host's own data is D76's), so with SQL_ENABLED off, as the port's
    // config ships, the callers take tg's own no-database paths (the library's archive, the random books).
    public virtual object Connect()
    {
        if (Dm.truthy(IsConnected()))
            return TRUE;

        if (Dm.truthy(connection))
        {
            Disconnect(); //clear the current connection handle so isconnected() calls stop invoking rustg
            connection = null; //make sure its cleared even if runtimes happened
        }

        if (failed_connection_timeout <= world.time) //it's been long enough since we failed to connect, reset the counter
        {
            failed_connections = 0;
            failed_connection_timeout = 0;
        }

        if (failed_connection_timeout > 0)
            return FALSE;

        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_sql_enabled)))) // CONFIG_GET(flag/sql_enabled)
            return FALSE;

        Dm.NotTranslated("code/controllers/subsystem/dbcore.dm:266", "Connect: the SQL connection pool (start_db_daemon, rustg_sql_connect_pool; rules 13)");
        return FALSE;
    }

    // tg: code/controllers/subsystem/dbcore.dm:360
    public virtual void Disconnect()
    {
        failed_connections = 0;
        if (Dm.truthy(connection))
            Dm.NotTranslated("code/controllers/subsystem/dbcore.dm:363", "Disconnect: rustg_sql_disconnect_pool (rules 13)");
        connection = null;
    }

    // tg: code/controllers/subsystem/dbcore.dm:366
    public virtual object IsConnected()
    {
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_sql_enabled)))) // CONFIG_GET(flag/sql_enabled)
            return FALSE;
        if (!Dm.truthy(connection))
            return FALSE;
        Dm.NotTranslated("code/controllers/subsystem/dbcore.dm:371", "IsConnected: rustg_sql_connected (rules 13)");
        return FALSE;
    }

    // tg: code/controllers/subsystem/dbcore.dm:325  (world/ConfigLoaded calls it, game/world.dm:157)
    // M10 slice I: the round row through the host's database (RoundDatabaseSeam); tg's CheckSchemaVersion and the SQL query
    // are the host's (rules 13). Without a database tg returns early, as here.
    public virtual async DmTask InitializeRound()
    {
        IRoundDatabase database = RoundDatabaseSeam.Current;
        if (database == null)
            return;
        GLOB.round_id = database.InitializeRound();
        if (GLOB.round_id == null)
            return;

        log_world(Dm.text("Round ID: []", GLOB.round_id));
    }

    // tg: code/controllers/subsystem/dbcore.dm:340
    public virtual async DmTask SetRoundStart()
    {
        if (RoundDatabaseSeam.Current is not IRoundDatabase database || GLOB.round_id == null)
            return;
        database.SetRoundStart(Dm.text("[]", GLOB.round_id));
    }

    // tg: code/controllers/subsystem/dbcore.dm:350
    public virtual async DmTask SetRoundEnd()
    {
        if (RoundDatabaseSeam.Current is not IRoundDatabase database || GLOB.round_id == null)
            return;
        database.SetRoundEnd(Dm.text("[]", GLOB.round_id), Dm.text("[]", SSticker.mode_result), Dm.text("[]", station_name()));
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/dbcore.dm:2  SUBSYSTEM_DEF(dbcore): GLOBAL_REAL(SSdbcore, /datum/controller/subsystem/dbcore)
    internal datum_controller_subsystem_dbcore SSdbcore;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_dbcore SSdbcore => ref TgState.Current.SSdbcore;
}
