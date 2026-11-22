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
/// Gaps package RG1 on the converted MetaStation: every drink and food reagent in Meta's mapped bottles, glasses, cans, condiments
/// and dispensers is live (the ethanol base and every alcohol, soft drink and food reagent are translated), and the bar's booze
/// dispenser and the soda dispensers offer only live reagents, with no runtime from RG1's files.
/// </summary>
public class DrinksRG1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public DrinksRG1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "drinks/alcohol_reagents", "drinks/drink_reagents", "reagents/food_reagents", "debuffs/drunk", "debuffs/drugginess",
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
    public void Metas_drinks_and_dispensers_hold_live_reagents()
    {
        using (_meta.World.Enter())
        {
            int holders = 0, drinks = 0, alcohols = 0, dispensers = 0, offered = 0;
            var inert = new SortedSet<string>(StringComparer.Ordinal);
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
                            if (thing.reagents is datum_reagents holder && Dm.truthy(Dm.length(holder.reagent_list)))
                            {
                                bool any = false;
                                foreach (datum_reagent reagent in Dm.each<datum_reagent>(holder.reagent_list))
                                {
                                    if (reagent is not datum_reagent_consumable)
                                        continue;
                                    any = true;
                                    drinks++;
                                    if (reagent is datum_reagent_consumable_ethanol)
                                        alcohols++;
                                    if (InertSeam.IsInertType(reagent.GetType()))
                                        inert.Add(DmTypes.PathOf(reagent.GetType()));
                                }
                                if (any)
                                    holders++;
                            }
                            if (thing is obj_machinery_chem_dispenser_drinks dispenser)
                            {
                                dispensers++;
                                foreach (object path in Dm.each(dispenser.dispensable_reagents))
                                {
                                    if (path is not Type type || !typeof(datum_reagent_consumable).IsAssignableFrom(type))
                                        continue;
                                    offered++;
                                    if (InertSeam.IsInertType(type))
                                        inert.Add(DmTypes.PathOf(type));
                                }
                            }
                        });
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {holders} holders with {drinks} drink and food reagents ({alcohols} alcohols); {dispensers} drink dispensers offering {offered}; inert: {string.Join(", ", inert)}; RG1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(alcohols >= 20, "too few mapped alcohols: " + alcohols);
            Assert.True(dispensers >= 2, "the booze and soda dispensers are missing: " + dispensers);
            Assert.True(offered >= 20, "the drink dispensers offer too little: " + offered);
            Assert.Empty(inert);
            Assert.Empty(runtimes);
        }
    }
}
