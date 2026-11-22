using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package DV2 on the converted MetaStation: its disks (tech, design, ordnance and the other computer disks, the DNA data disk,
/// the nuke disk), the implant pads of the warden's implant boxes, the multitools, gas analyzers, T-ray scanners, health analyzers,
/// penlights and intercoms boot live (on the floor, on the tables, in the lockers and the boxes), and so does any device of the
/// package a maintenance spawner rolled, with no runtime from the files the package translated.
/// </summary>
public class DevicesDV2MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public DevicesDV2MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "floppy_disk", "unique_disks", "maintenance_disks", "implantpad", "implantchair", "implant_noteleport", "implant_exile",
        "implant_beacon", "flashlight", "multitool", "gas_analyzer", "t_scanner", "health_analyzer", "slime_scanner", "intercom",
        "megaphone", "boombox", "music_tapes", "vaporizer", "forcefieldprojector", "pressureplates", "table_clock", "swapper",
        "desynchronizer", "blood_scanner", "quantum_keycard", "pipe_painter", "anomaly_neutralizer", "anomaly_releaser",
        "polycircuit", "RWD", "machine_wand", "wind_turbine", "wet_stacks_clothing", "space_protection",
    };

    // what a turf holds, and what that holds, all the way down (the lockers, the boxes, the bags)
    private static IEnumerable<atom> Walk(IEnumerable<atom> things)
    {
        foreach (atom thing in things)
        {
            yield return thing;
            foreach (atom inner in Walk(Dm.each<atom>(thing.contents)))
                yield return inner;
        }
    }

    [MetaFact]
    public void Metas_disks_implant_pads_and_devices_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            var inertKinds = new List<string>();
            int emptyPads = 0, unstickered = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in Walk(locate(x, y, 1).dm_held))
                    {
                        string kind = thing switch
                        {
                            obj_item_disk => "disk",
                            obj_item_disk_stack => "disk stack",
                            obj_item_implantpad => "implant pad",
                            obj_item_implantcase => "implant case",
                            obj_item_multitool_circuit => "circuit multitool", // wiremod's marker (modules/wiremod/core/marker.dm), not DV2's
                            obj_item_multitool => "multitool",
                            obj_item_analyzer => "gas analyzer",
                            obj_item_t_scanner => "t-ray scanner",
                            obj_item_healthanalyzer => "health analyzer",
                            obj_item_flashlight_pen => "penlight",
                            obj_item_radio_intercom => "intercom",
                            obj_item_megaphone => "megaphone",
                            obj_item_boombox or obj_item_music_tape or obj_item_vaporizer or obj_item_forcefield_projector
                                or obj_item_pressure_plate or obj_item_table_clock or obj_item_swapper or obj_item_desynchronizer
                                or obj_item_blood_scanner or obj_item_quantum_keycard or obj_item_pipe_painter or obj_item_anomaly_neutralizer
                                or obj_item_anomaly_releaser or obj_item_stack_circuit_stack or obj_item_slime_scanner or obj_item_rwd
                                or obj_item_machine_remote or obj_item_portable_wind_turbine or obj_machinery_implantchair => "other DV2 device",
                            _ => null,
                        };
                        if (kind == null)
                            continue;
                        counts[kind] = counts.GetValueOrDefault(kind) + 1;
                        if (kind != "circuit multitool" && InertSeam.IsInert(thing))
                            inertKinds.Add(thing.GetType().Name);
                        switch (thing)
                        {
                            case obj_item_disk disk when disk.sticker_icon_state == null && disk is not obj_item_disk_nuclear: // (tg's nuke disk has none)
                                unstickered++;
                                break;
                            case obj_item_implantpad pad when pad.icon_state != "implantpad-0" && pad.icon_state != "implantpad-1":
                                emptyPads++;
                                break;
                        }
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f + ".dm", StringComparison.Ordinal)
                || (e.ToString() ?? "").Contains(f + ".cs", StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inertKinds.Count} inert ({string.Join(", ", inertKinds.Distinct())}), {unstickered} disks without a sticker state,"
                + $" {emptyPads} pads without their picture; DV2 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("disk") >= 15, "too few disks");
            Assert.True(counts.GetValueOrDefault("implant pad") >= 2, "too few implant pads");
            Assert.True(counts.GetValueOrDefault("multitool") >= 10, "too few multitools");
            Assert.True(counts.GetValueOrDefault("t-ray scanner") >= 3, "too few T-ray scanners");
            Assert.True(counts.GetValueOrDefault("gas analyzer") >= 3, "too few gas analyzers");
            Assert.True(counts.GetValueOrDefault("health analyzer") >= 5, "too few health analyzers");
            Assert.True(counts.GetValueOrDefault("intercom") >= 10, "too few intercoms");
            Assert.Empty(inertKinds);
            Assert.Equal(0, unstickered);
            Assert.Equal(0, emptyPads);
            Assert.Empty(runtimes);
        }
    }
}
