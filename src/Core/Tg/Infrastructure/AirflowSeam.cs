using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// The airflow streams (D141, DEVIATION DV34). A host seam, not tg code: tg's SSair moves the atoms of every turf in
// high_pressure_delta by that turf's pressure_difference and pressure_direction (process_high_pressure_delta, air.dm:361)
// and then zeroes the difference; tg never shows that flow. The seam reports each such turf to the world's sink before the
// difference is zeroed, and the end of each cycle's pass, so the host can send the flow to the clients. It reads and
// changes nothing of tg's state.

/// <summary>Where a world's air flow goes (the server's: the airflow sender). Write-only, like the presentation sinks.</summary>
public interface IAirflowSink
{
    /// <summary>tg's air moved on <paramref name="turf"/> this cycle: its <c>pressure_difference</c> (kPa) toward <c>pressure_direction</c> (a BYOND dir).</summary>
    void Flow(turf_open turf, float difference, int direction);

    /// <summary>SSair finished this cycle's pass over <c>high_pressure_delta</c>: the turfs reported since the last end are the cycle's flow.</summary>
    void CycleEnd();

    /// <summary>
    /// A floor device (a vent, a scrubber, an injector, a passive vent, the airlock pump) moved gas between its pipe and <paramref name="turf"/>
    /// this cycle: the pressure that gas makes in the turf's air, in kPa, positive out of the device, negative into it.
    /// </summary>
    void Vent(turf_open turf, float kpa) { }
}

/// <summary>The airflow seam: SSair's pressure pass reports here; the world's sink (none by default) receives it.</summary>
public static class AirflowSeam
{
    /// <summary>Installs the world's sink (the host, before the world runs; null removes it).</summary>
    public static void Install(World world, IAirflowSink sink)
    {
        ArgumentNullException.ThrowIfNull(world);
        Holder.Of(world).Sink = sink;
    }

    /// <summary>A turf of <c>high_pressure_delta</c>, before its <c>pressure_difference</c> is zeroed.</summary>
    public static void Note(turf_open turf)
    {
        if (turf == null)
            return;
        IAirflowSink sink = SinkOrNull();
        if (sink == null || !(turf.pressure_difference > 0))
            return;
        int direction = (int)turf.pressure_direction;
        sink.Flow(turf, turf.pressure_difference, direction != 0 ? direction : turf.airflow_vertical); // a vertical share: tg's 0, drawn UP or DOWN
    }

    /// <summary>The end of a cycle's pass over <c>high_pressure_delta</c> (not a pause: the pass resumes next tick).</summary>
    public static void CycleEnd() => SinkOrNull()?.CycleEnd();

    /// <summary>
    /// A floor device on its turf moved <paramref name="moved"/> out of its pipe into <paramref name="environment"/>
    /// (<paramref name="outward"/>) or out of it into the pipe: reported as the pressure that gas makes in the turf's air
    /// (<c>moles * R * T / volume</c>). Reads the mixes, changes nothing; call it before the gas is merged anywhere.
    /// </summary>
    public static void Vent(atom device, datum_gas_mixture moved, datum_gas_mixture environment, bool outward)
    {
        if (moved == null) return;
        VentMoles(device, (outward ? 1f : -1f) * Dm.num(moved.total_moles()), moved.temperature, environment);
    }

    /// <summary>As <see cref="Vent"/>, with the moles signed (positive out of the device) and their temperature in K.</summary>
    public static void VentMoles(atom device, float moles, float temperature, datum_gas_mixture environment)
    {
        IAirflowSink sink = SinkOrNull();
        if (sink == null || device?.loc is not turf_open turf || !(Math.Abs(moles) > 0) || !(temperature > 0)) return;
        float volume = environment != null ? Dm.num(environment.volume) : Defines.CELL_VOLUME;
        if (!(volume > 0)) volume = Defines.CELL_VOLUME;
        sink.Vent(turf, moles * Defines.R_IDEAL_GAS_EQUATION * temperature / volume);
    }

    /// <summary>
    /// As <see cref="Vent"/>, for a device that moves gas on a turf other than its own (the airlock pump fills and siphons its
    /// turf and the turfs atmos-adjacent to it, airlock_pump.dm:212-214, :220-222): reported on <paramref name="tile"/>.
    /// </summary>
    public static void VentOn(turf tile, datum_gas_mixture moved, datum_gas_mixture environment, bool outward)
    {
        if (moved == null) return;
        IAirflowSink sink = SinkOrNull();
        float moles = (outward ? 1f : -1f) * Dm.num(moved.total_moles());
        if (sink == null || tile is not turf_open open || !(Math.Abs(moles) > 0) || !(moved.temperature > 0)) return;
        float volume = environment != null ? Dm.num(environment.volume) : Defines.CELL_VOLUME;
        if (!(volume > 0)) volume = Defines.CELL_VOLUME;
        sink.Vent(open, moles * Defines.R_IDEAL_GAS_EQUATION * moved.temperature / volume);
    }

    private static IAirflowSink SinkOrNull()
    {
        World world = World.CurrentOrNull;
        return world == null ? null : Holder.Of(world).Sink;
    }

    // The per-world sink (World.GetExtension: no static state).
    private sealed class Holder
    {
        internal IAirflowSink Sink;

        internal static Holder Of(World world) => world.GetExtension(static _ => new Holder());
    }
}
