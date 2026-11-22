using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package H3 on the converted MetaStation: its bar sign boots live, mounted on its wall and switched off; its drinks, glasses,
/// bottles and cans boot live, the filled glasses wearing their drink's look; and nothing of the bar's files raises a runtime.
/// </summary>
public class BarH3MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public BarH3MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "barsigns", "drinkingglass", "glassbottle", "drinks.cs", "soda.cs", "mauna_mug", "organ_jar", "takes_reagent_appearance",
        "_glass_datum", "jukebox", "dance_machine",
    };

    [MetaFact]
    public void Metas_bar_sign_and_drinkware_boot_live()
    {
        using (_meta.World.Enter())
        {
            int signs = 0, unmounted = 0, inert = 0, cups = 0, styled = 0;
            var inertKinds = new SortedSet<string>(StringComparer.Ordinal);
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        if (thing is obj_machinery_barsign sign)
                        {
                            signs++;
                            if (sign.GetComponent(typeof(datum_component_atom_mounted)) == null)
                                unmounted++;
                            Assert.Equal("bar sign", sign.name); // the "Off" sign renames nothing
                            Assert.IsType<datum_barsign_hiddensigns_signoff>(sign.chosen_sign);
                            if (InertSeam.IsInert(sign))
                                inertKinds.Add(sign.GetType().Name);
                        }
                        if (thing is obj_item_reagent_containers_cup_glass or obj_item_reagent_containers_cup_soda_cans)
                        {
                            cups++;
                            if (InertSeam.IsInert(thing))
                            {
                                inert++;
                                inertKinds.Add(thing.GetType().Name);
                            }
                            if (thing is obj_item_reagent_containers_cup_glass_drinkingglass glass
                                && glass.GetComponent(typeof(datum_component_takes_reagent_appearance)) != null
                                && glass.fill_icon_thresholds == null)
                                styled++;
                        }
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {signs} bar signs ({unmounted} unmounted), {cups} drinks ({inert} inert: {string.Join(", ", inertKinds)}), {styled} styled glasses; bar runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.Equal(1, signs);
            Assert.Equal(0, unmounted);
            Assert.True(cups >= 30, "too few drinks");
            Assert.Equal(0, inert);
            Assert.Empty(runtimes);
        }
    }
}
