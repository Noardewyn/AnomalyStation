namespace Anomaly.Protocol;

// The airflow streams (D141, DEVIATION DV34). Version 6 adds to version 5 (MessagesV5.cs) the Airflow message: the air flow
// of the cells around one player, which tg's SSair computes every cycle (LINDA's pressure_difference and pressure_direction
// of the turfs in high_pressure_delta, the same numbers that push atoms) and which tg itself never shows. The server sends
// each player only the cells near them and only the changes since the last message; the client draws streams from them.

/// <summary>
/// The air flow of one cell as one byte: <see cref="Airflow.Clear"/> (no flow), <see cref="Airflow.Unchanged"/> (inside a
/// run only: the cell keeps what it had), or a direction (<see cref="AirflowDir"/>, bits 5-7) and a level (1 to
/// <see cref="Airflow.MaxLevel"/>, bits 0-4: tg's pressure difference on a log scale, <see cref="Airflow.KpaOf"/>).
/// </summary>
public static class Airflow
{
    /// <summary>The byte of a cell with no flow.</summary>
    public const byte Clear = 0;

    /// <summary>The byte of a cell a run skips (it keeps its flow): lets one run cover a few unchanged cells between changes.</summary>
    public const byte Unchanged = 0xFF;

    /// <summary>The highest level.</summary>
    public const int MaxLevel = 31;

    /// <summary>The pressure difference of level 1, in kPa; a smaller one is no flow.</summary>
    public const float MinKpa = 0.5f;

    /// <summary>Levels per doubling of the pressure difference (level 31 is about 190 kPa).</summary>
    public const float StepsPerDoubling = 3.5f;

    /// <summary>The cell byte of a direction and a level.</summary>
    public static byte Pack(AirflowDir dir, int level)
    {
        if (dir is < AirflowDir.North or > AirflowDir.Down)
            throw new ArgumentOutOfRangeException(nameof(dir), dir, "a direction North..Down");
        if (level < 1 || level > MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(level), level, "a level 1.." + MaxLevel);
        return (byte)(((int)dir << 5) | level);
    }

    /// <summary>The direction of a cell byte (<see cref="AirflowDir.None"/> for <see cref="Clear"/>).</summary>
    public static AirflowDir DirOf(byte cell) => (AirflowDir)(cell >> 5);

    /// <summary>The level of a cell byte (0 for <see cref="Clear"/>).</summary>
    public static int LevelOf(byte cell) => cell & 31;

    /// <summary>Whether a byte is a cell's flow (<see cref="Clear"/> or a direction North..Down with a level 1..31).</summary>
    public static bool IsFlow(byte cell)
    {
        if (cell == Clear) return true;
        int dir = cell >> 5, level = cell & 31;
        return dir >= (int)AirflowDir.North && dir <= (int)AirflowDir.Down && level >= 1;
    }

    /// <summary>The level of a pressure difference in kPa: 0 under <see cref="MinKpa"/>, else 1 to <see cref="MaxLevel"/>.</summary>
    public static int LevelOfKpa(float kpa)
    {
        if (!(kpa >= MinKpa)) return 0; // NaN too
        int level = 1 + (int)MathF.Round(MathF.Log2(kpa / MinKpa) * StepsPerDoubling);
        return Math.Clamp(level, 1, MaxLevel);
    }

    /// <summary>The pressure difference a level stands for, in kPa (0 for level 0).</summary>
    public static float KpaOf(int level) => level <= 0 ? 0f : MinKpa * MathF.Pow(2f, (Math.Min(level, MaxLevel) - 1) / StepsPerDoubling);

    /// <summary>The direction of one of BYOND's dirs (tg's <c>pressure_direction</c>: a cardinal, UP or DOWN); None for any other.</summary>
    public static AirflowDir DirOfByond(int dir) => dir switch
    {
        1 => AirflowDir.North,
        2 => AirflowDir.South,
        4 => AirflowDir.East,
        8 => AirflowDir.West,
        16 => AirflowDir.Up,
        32 => AirflowDir.Down,
        _ => AirflowDir.None,
    };
}

/// <summary>Where a cell's air goes: to the neighbour that took it (tg's <c>pressure_direction</c>; north is +y, east is +x).</summary>
public enum AirflowDir : byte
{
    None = 0,
    North = 1,
    South = 2,
    East = 3,
    West = 4,
    Up = 5,
    Down = 6,
}

/// <summary>Version 7: which way a floor device moves the air of its cell.</summary>
public enum AirflowVentKind : byte
{
    None = 0,

    /// <summary>Out of the device into the room (a vent releasing, an injector, a passive vent filling the room).</summary>
    Out = 1,

    /// <summary>From the room into the device (a vent or scrubber siphoning, a scrubber scrubbing, a passive vent draining it).</summary>
    In = 2,
}

/// <summary>
/// Version 7: a floor device's air movement as one byte: <see cref="Airflow.Clear"/> (none), or a kind
/// (<see cref="AirflowVentKind"/>, bits 5-7) and a level (1 to <see cref="Airflow.MaxLevel"/>, bits 0-4, on <see cref="Airflow"/>'s
/// scale: the pressure the moved gas makes in the cell's air in one SSair cycle).
/// </summary>
public static class AirflowVents
{
    /// <summary>The byte of a kind and a level.</summary>
    public static byte Pack(AirflowVentKind kind, int level)
    {
        if (kind is not (AirflowVentKind.Out or AirflowVentKind.In))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Out or In");
        if (level < 1 || level > Airflow.MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(level), level, "a level 1.." + Airflow.MaxLevel);
        return (byte)(((int)kind << 5) | level);
    }

    public static AirflowVentKind KindOf(byte cell) => (AirflowVentKind)(cell >> 5);

    /// <summary>Whether a byte is a device's air movement (<see cref="Airflow.Clear"/> or Out/In with a level 1..31).</summary>
    public static bool IsVent(byte cell)
    {
        if (cell == Airflow.Clear) return true;
        int kind = cell >> 5, level = cell & 31;
        return kind is (int)AirflowVentKind.Out or (int)AirflowVentKind.In && level >= 1;
    }
}

/// <summary>Version 7: a floor device's cell and its air movement byte (<see cref="AirflowVents"/>; <see cref="Airflow.Clear"/>: it stopped).</summary>
public readonly record struct AirflowVent(int X, int Y, int Z, byte Cell);

/// <summary>Consecutive cells of one row: the cell bytes (<see cref="Airflow"/>) of x = <see cref="X0"/> onwards on row <see cref="Y"/> of level <see cref="Z"/>.</summary>
public readonly record struct AirflowRun(int X0, int Y, int Z, byte[] Cells);

/// <summary>
/// Version 6, server to client on the State channel after the tick's Delta (and after the MapGrid of a join): the changes of
/// the air flow of the cells around the player since the last update. The client starts with no flow anywhere (and drops
/// it all when a MapGrid arrives); a cell far from the player is cleared before it leaves the server's interest.
/// </summary>
public sealed class AirflowUpdate
{
    /// <summary>The runs, at most <see cref="Limits.MaxAirflowRuns"/> with at most <see cref="Limits.MaxAirflowCells"/> cells in all.</summary>
    public List<AirflowRun> Runs { get; } = new();

    /// <summary>Version 7: the floor devices whose air movement changed, at most <see cref="Limits.MaxAirflowVents"/>.</summary>
    public List<AirflowVent> Vents { get; } = new();
}
