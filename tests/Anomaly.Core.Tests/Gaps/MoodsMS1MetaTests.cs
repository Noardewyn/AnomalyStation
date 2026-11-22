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
/// Gaps package MS1 on the converted MetaStation: SSmood boots, the mapped posters and cleanables give their areas' beauty through the
/// beauty element (poster.dm:171, cleanable.dm:38) so the area beauty the moods read is tg's, with no runtime from MS1's files.
/// </summary>
public class MoodsMS1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MoodsMS1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "datums/mood", "mood_events/", "subsystem/moods", "elements/beauty" };

    [MetaFact]
    public void Metas_posters_and_dirt_give_their_areas_beauty_and_SSmood_boots()
    {
        using (_meta.World.Enter())
        {
            Assert.NotNull(SSmood);
            var posters = Dm.each<obj_structure_sign_poster>().Where(p => !QDELETED(p) && p.loc != null && !InertSeam.IsInert(p)).ToList();
            var areas = Dm.each<area>().Where(a => Dm.truthy(a.areasize)).ToList();
            var beautiful = areas.Where(a => a.totalbeauty > 0).ToList();
            var ugly = areas.Where(a => a.totalbeauty < 0).ToList();
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"posters {posters.Count}; areas {areas.Count}, beautiful {beautiful.Count}, ugly {ugly.Count}; MS1 runtimes {runtimes.Count}");
            foreach (var a in beautiful.OrderByDescending(a => a.beauty).Take(5))
                _output.WriteLine($"  {Dm.str(a.name)}: total {a.totalbeauty}, size {a.areasize}, beauty {a.beauty}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");

            Assert.NotEmpty(posters);
            Assert.NotEmpty(beautiful);
            // a mapped poster's area counts its 300 (with whatever dirt and materials share the area)
            area posterArea = (area)get_area(posters[0]);
            Assert.NotEqual(0f, posterArea.totalbeauty);
            // the beauty is the total over the area's size, 0 for an area too big to care (areas.dm:609)
            foreach (area a in areas)
                Assert.Equal(a.areasize >= a.beauty_threshold ? 0f : a.totalbeauty / a.areasize, a.beauty, 3);
            Assert.Empty(runtimes);
        }
    }
}
