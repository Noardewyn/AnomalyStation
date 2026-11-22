using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package SE1 on the converted MetaStation: the brig's secway with its riding element and its key, the pepper spray refillers
/// mounted on their walls and filled with condensed capsaicin, and virology's virus food dispenser boot live, with no runtime from
/// SE1's files.
/// </summary>
public class SecurityGearSE1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public SecurityGearSE1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "secway.cs", "net_snare.cs", "riding_vehicle", "reagent_dispenser", "energy_gun.cs", "atom_mounted" };

    [MetaFact]
    public void Metas_security_gear_boots_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, unmounted = 0, dry = 0, unridable = 0;
            var inertKinds = new SortedSet<string>(StringComparer.Ordinal);
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        string kind = thing switch
                        {
                            obj_vehicle_ridden_secway => "secway",
                            obj_item_key_security => "secway key",
                            obj_structure_reagent_dispensers_wall_peppertank => "pepper spray refiller",
                            obj_structure_reagent_dispensers_wall_virusfood => "virus food dispenser",
                            _ => null,
                        };
                        if (kind == null)
                            continue;
                        counts[kind] = counts.GetValueOrDefault(kind) + 1;
                        if (InertSeam.IsInert(thing))
                        {
                            inert++;
                            inertKinds.Add(thing.GetType().Name);
                        }
                        if (thing is obj_structure_reagent_dispensers_wall wall)
                        {
                            if (wall.GetComponent(typeof(datum_component_atom_mounted)) == null)
                                unmounted++;
                            if (wall.reagents is not datum_reagents holder || Dm.num(holder.total_volume) <= 0)
                                dry++;
                            else if (wall is obj_structure_reagent_dispensers_wall_peppertank && !Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_condensedcapsaicin))))
                                dry++;
                        }
                        if (thing is obj_vehicle_ridden_secway way && !Dm.truthy(way._listen_lookup?[Defines.COMSIG_MOVABLE_PREBUCKLE])) // the ridable element: the riding component comes at the buckle
                            unridable++;
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inert} inert ({string.Join(", ", inertKinds)}), {unmounted} unmounted, {dry} dry, {unridable} unridable; SE1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("secway") >= 1, "no secway");
            Assert.True(counts.GetValueOrDefault("pepper spray refiller") >= 5, "too few pepper spray refillers");
            Assert.True(counts.GetValueOrDefault("virus food dispenser") >= 1, "no virus food dispenser");
            Assert.Equal(0, inert);
            Assert.Equal(0, unmounted);
            Assert.Equal(0, dry);
            Assert.Equal(0, unridable);
            Assert.Empty(runtimes);
        }
    }
}
