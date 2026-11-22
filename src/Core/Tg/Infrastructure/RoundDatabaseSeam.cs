using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// M10 slice I (D275). A host seam, not tg code: tg's SSdbcore gives the round its id from the database's round table
// (InitializeRound, SetRoundStart, SetRoundEnd; controllers/subsystem/dbcore.dm:325-358) through rust-g's SQL, which is not
// translated (rules 13). The server's database answers through this seam; a world without one (the Core tests) has no round id,
// as tg without a database.

/// <summary>The round table of a host's database.</summary>
public interface IRoundDatabase
{
    /// <summary>tg's InitializeRound: a new round row; its id as text (tg's <c>GLOB.round_id</c>), or null when there is none.</summary>
    string InitializeRound();

    /// <summary>tg's SetRoundStart: the round's start time.</summary>
    void SetRoundStart(string roundId);

    /// <summary>tg's SetRoundEnd: the round's end time, the mode's result and the station's name.</summary>
    void SetRoundEnd(string roundId, string modeResult, string stationName);
}

/// <summary>The world's round database (none by default).</summary>
public static class RoundDatabaseSeam
{
    /// <summary>Installs the world's round database (the host, before the boot; null removes it).</summary>
    public static void Install(World world, IRoundDatabase database)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.GetExtension(static _ => new Holder()).Database = database;
    }

    /// <summary>The current world's round database, or null.</summary>
    public static IRoundDatabase Current => World.CurrentOrNull?.GetExtension(static _ => new Holder()).Database;

    private sealed class Holder
    {
        internal IRoundDatabase Database;
    }
}
