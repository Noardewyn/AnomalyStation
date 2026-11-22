using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package MO2 on the converted MetaStation: the twelve MODsuits of the suit storage units carry every module their kind and
/// theme list, live (the seam keeps out only the research director's circuit module: wiremod is not ported); the loose MOD modules
/// (the maintenance loot) are live; no runtime comes from MO2's files.
/// </summary>
public class ModsuitsMO2MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ModsuitsMO2MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "modules/mod/modules/", "datums/components/tether", "datums/components/anomalock_module", "datums/components/dejavu",
        "proximity_monitor/fields/timestop", "xenobiology/crossbreeding/_misc", "datums/components/candela_node",
    };

    private static void Walk(atom holder, Action<atom> visit)
    {
        foreach (object o in Dm.each(holder.contents))
        {
            if (o is not atom thing)
                continue;
            visit(thing);
            Walk(thing, visit);
        }
    }

    [MetaFact]
    public void Metas_modsuits_carry_all_their_modules_live()
    {
        using (_meta.World.Enter())
        {
            int mods = 0, installed = 0, liveLooseModules = 0;
            var missing = new List<string>();
            var leftOut = new List<string>();
            var inertLoose = new List<string>();
            for (int z = 1; z <= world.maxz; z++)
            {
                for (int y = 1; y <= world.maxy; y++)
                {
                    for (int x = 1; x <= world.maxx; x++)
                    {
                        turf t = locate(x, y, z);
                        if (t == null)
                            continue;
                        Walk(t, thing =>
                        {
                            if (thing is obj_machinery_suit_storage_unit ssu && ssu.mod_value is obj_item_mod_control_pre_equipped mod)
                            {
                                mods++;
                                var expected = Dm.each_cast<Type>(mod.applied_modules).Concat(Dm.each_cast<Type>(((datum_mod_theme)mod.theme).inbuilt_modules));
                                foreach (Type moduleType in expected)
                                {
                                    if (InertSeam.IsInertType(moduleType))
                                    {
                                        leftOut.Add(mod.GetType().Name + ": " + moduleType.Name);
                                        continue;
                                    }
                                    if (!Dm.each(mod.modules).Any(m => m.GetType() == moduleType))
                                        missing.Add(mod.GetType().Name + ": " + moduleType.Name);
                                }
                                foreach (object module in Dm.each(mod.modules))
                                {
                                    installed++;
                                    Assert.False(InertSeam.IsInertType(module.GetType()), mod.GetType().Name + " holds the inert " + module.GetType().Name);
                                }
                            }
                            else if (thing is obj_item_mod_module module && thing.loc is turf or obj_structure)
                            {
                                if (InertSeam.IsInert(module))
                                    inertLoose.Add(module.GetType().Name);
                                else
                                    liveLooseModules++;
                            }
                        });
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {mods} MODsuits with {installed} modules; left out by the seam: {string.Join(", ", leftOut)}; "
                + $"missing: {string.Join(", ", missing)}; loose modules {liveLooseModules} live, inert: {string.Join(", ", inertLoose)}; MO2 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.Equal(12, mods); // the heads' and departments' units
            Assert.Empty(missing);
            Assert.All(leftOut, s => Assert.EndsWith("obj_item_mod_module_circuit", s)); // wiremod's, not ported
            Assert.Empty(inertLoose);
            Assert.Empty(runtimes);
        }
    }
}
