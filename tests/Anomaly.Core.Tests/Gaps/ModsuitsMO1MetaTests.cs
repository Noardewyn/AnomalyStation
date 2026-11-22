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
/// Gaps package MO1 on the converted MetaStation: the suit storage units hold live MOD control units (no longer the EVA stopgap
/// suits), each with its theme's parts, a charged core and only live modules; the loose MOD modules (the maintenance loot) of MO1's
/// files boot live; no runtime comes from MO1's files.
/// </summary>
public class ModsuitsMO1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ModsuitsMO1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "modules/mod/", "datums/components/hat_stabilizer", "datums/wires/mod", "preferences/mod_select", "maintenance/modsuit",
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
    public void Metas_suit_storage_units_hold_live_modsuits()
    {
        using (_meta.World.Enter())
        {
            int units = 0, mods = 0, eva = 0, inertModules = 0, partless = 0, uncharged = 0, liveLooseModules = 0, inertLooseModules = 0;
            var themes = new Dictionary<string, int>();
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
                            if (thing is obj_machinery_suit_storage_unit ssu)
                            {
                                units++;
                                if (ssu.mod_type != null)
                                {
                                    var mod = ssu.mod_value as obj_item_mod_control;
                                    Assert.True(mod != null, "an SSU with a mod_type holds no control unit");
                                    mods++;
                                    if (ssu.suit_value is obj_item_clothing_suit_space_eva)
                                        eva++;
                                    string theme = (mod.theme as datum_mod_theme)?.name ?? "?";
                                    themes[theme] = themes.GetValueOrDefault(theme) + 1;
                                    if (Dm.each(mod.get_parts()).Count() != 4)
                                        partless++;
                                    if (!Dm.truthy(mod.get_charge()))
                                        uncharged++;
                                    foreach (object module in Dm.each(mod.modules))
                                        if (InertSeam.IsInertType(module.GetType()))
                                            inertModules++;
                                }
                            }
                            else if (thing is obj_item_mod_module module && thing.loc is turf or obj_structure)
                            {
                                if (InertSeam.IsInert(module))
                                    inertLooseModules++;
                                else
                                    liveLooseModules++;
                            }
                        });
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {units} suit storage units, {mods} with a MODsuit ({string.Join(", ", themes.OrderBy(k => k.Key).Select(k => k.Value + " " + k.Key))}); "
                + $"{eva} EVA stopgaps, {partless} without four parts, {uncharged} uncharged, {inertModules} inert installed modules; "
                + $"loose modules {liveLooseModules} live, {inertLooseModules} inert; MO1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.Equal(12, mods); // the heads' and departments' units
            Assert.Equal(0, eva);
            Assert.Equal(0, partless);
            Assert.Equal(0, uncharged);
            Assert.Equal(0, inertModules);
            Assert.Empty(runtimes);
        }
    }
}
