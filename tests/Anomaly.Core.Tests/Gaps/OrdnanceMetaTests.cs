using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The ordnance lab on the converted MetaStation: the tank compressor, the doppler array, the four mass drivers and their launch
/// controls boot live; each control found its driver; the doppler array records a blast in the bomb range it faces; the ordnance
/// driver's control fires what lies on the driver; the compressor takes a tank; and nothing of them throws over the ticks.
/// </summary>
public class OrdnanceMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public OrdnanceMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private void Step(int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
            _meta.World.Step();
    }

    [MetaFact]
    public void Metas_ordnance_machines_and_mass_drivers_work()
    {
        Step(40);
        int runtimesBefore = _meta.Log.Runtimes.Count;
        obj_machinery_doppler_array doppler;
        obj_item toy;
        turf driverTurf;
        using (_meta.World.Enter())
        {
            var compressors = Dm.each<obj_machinery_atmospherics_components_binary_tank_compressor>().ToList();
            var dopplers = Dm.each<obj_machinery_doppler_array>().ToList();
            var drivers = Dm.each<obj_machinery_mass_driver>().ToList();
            var controls = Dm.each<obj_machinery_computer_pod>().ToList();
            _output.WriteLine($"Meta's ordnance: {compressors.Count} compressors, {dopplers.Count} doppler arrays, {drivers.Count} mass drivers, {controls.Count} launch controls");
            Assert.Single(compressors);
            Assert.Single(dopplers);
            Assert.Equal(4, drivers.Count);
            Assert.Equal(4, controls.Count);
            foreach (var machine in compressors.Cast<atom>().Concat(dopplers).Concat(drivers).Concat(controls))
                Assert.False(InertSeam.IsInert(machine), machine.GetType().Name + " is inert");
            foreach (var control in controls)
            {
                Assert.True(control.connected != null, $"the {Dm.str(control.id)} control found no driver");
                Assert.True(Dm.eq(control.connected.id, control.id));
            }

            // the doppler array faces the bomb range: a blast there is recorded
            doppler = dopplers[0];
            var array = get_turf(doppler);
            // a scientist turns it to face the range, as tg's players do (it is mapped facing its default way)
            var bombTurfs = Dm.each<turf>(Builtins.block(locate(1, 1, array.z), locate(world.maxx, world.maxy, array.z)))
                .Where(t => get_area(t) is area_station_science_ordnance_bomb && t is turf_open && get_dist(array, t) <= doppler.max_dist).ToList();
            _output.WriteLine($"{bombTurfs.Count} bomb range tiles in reach, from {bombTurfs.Min(t => t.x)},{bombTurfs.Min(t => t.y)} to {bombTurfs.Max(t => t.x)},{bombTurfs.Max(t => t.y)}");
            var centre = bombTurfs.OrderBy(t => t.x).ThenBy(t => t.y).ElementAt(bombTurfs.Count / 2);
            float towards = get_dir(array, centre);
            float cardinal = Math.Abs(centre.x - array.x) >= Math.Abs(centre.y - array.y) ? Dm.bitand(towards, Dm.bitor(EAST, WEST)) : Dm.bitand(towards, Dm.bitor(NORTH, SOUTH));
            Waiting.NoSleep(doppler.setDir(cardinal));
            var rangeTurfs = bombTurfs.Where(t => Dm.truthy(Dm.bitand(get_dir(array, t), doppler.dir)) && get_dist(array, t) >= 5).ToList();
            _output.WriteLine($"the array at {array.x},{array.y} faces {Dm.str(dir2text(doppler.dir))}; {rangeTurfs.Count} range tiles in front of it");
            Assert.NotEmpty(rangeTurfs);
            Assert.False(Dm.truthy(Dm.bitand(doppler.machine_stat, NOPOWER)), "the array has power");
            var blast = rangeTurfs[rangeTurfs.Count / 2];
            Waiting.Detach(explosion(blast, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 3));

            // the ordnance driver's control fires what lies on its driver
            var ordnance = controls.Single(c => Dm.eq(c.id, MASSDRIVER_ORDNANCE));
            driverTurf = get_turf(ordnance.connected);
            toy = Dm.@new<obj_item_toy_crayon_red>(driverTurf);
            Waiting.Detach(ordnance.alarm());

            // the compressor takes a tank
            var compressor = compressors[0];
            var scientist = Dm.@new<mob_living_carbon_human>(get_step(compressor, SOUTH));
            var tank = Dm.@new<obj_item_tank_internals_plasma>(scientist.loc);
            Waiting.NoSleep(scientist.put_in_active_hand(tank));
            Waiting.NoSleep(compressor.item_interaction(scientist, tank));
            Assert.Same(tank, compressor.inserted_tank);
            compressor.active = TRUE;
        }
        Step(4 * 20);
        using (_meta.World.Enter())
        {
            Assert.True(Dm.length(doppler.records) > 0, "the doppler array recorded the blast in the range");
            _output.WriteLine("the record: " + (doppler.records[1] as datum_data_tachyon_record).coordinates);
            Assert.False(get_turf(toy) == driverTurf, "the ordnance driver threw the toy");
        }
        Step(100);
        var runtimes = _meta.Log.Runtimes.Skip(runtimesBefore)
            .Where(e => (e.ToString() ?? "").Contains("doppler", StringComparison.OrdinalIgnoreCase) || (e.ToString() ?? "").Contains("tank_compressor", StringComparison.Ordinal)
                || (e.ToString() ?? "").Contains("mass_driver", StringComparison.Ordinal) || (e.ToString() ?? "").Contains("computer_pod", StringComparison.Ordinal))
            .ToList();
        foreach (var e in runtimes)
            _output.WriteLine("runtime: " + (e.Message.Length > 300 ? e.Message[..300] : e.Message));
        Assert.Empty(runtimes);
    }
}
