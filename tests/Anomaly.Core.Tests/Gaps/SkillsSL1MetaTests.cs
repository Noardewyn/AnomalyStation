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
/// Gaps package SL1 on the converted MetaStation: SSskills boots with every skill's singleton, the library's Skillsoft station and the
/// chaplain's smoke book boot live with tg's open picture, with no runtime from SL1's files.
/// </summary>
public class SkillsSL1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public SkillsSL1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "datums/skills/", "mind/skills", "subsystem/skills", "skill_learning/", "elements/skill_reward", "admin/skill_panel",
        "programs/skill_tracker", "items/granters/",
    };

    [MetaFact]
    public void Metas_skill_station_and_the_skills_boot_live()
    {
        using (_meta.World.Enter())
        {
            Assert.NotNull(SSskills);
            Assert.Equal(length(GLOB.skill_types), length(SSskills.all_skills));
            var stations = Dm.each<obj_machinery_skill_station>().Where(s => !QDELETED(s) && s.loc != null).ToList();
            var books = Dm.each<obj_item_book_granter_action_spell_smoke>().Where(b => !QDELETED(b) && b.loc != null).ToList();
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"skill stations {stations.Count} ({string.Join(", ", stations.Select(s => s.icon_state))}); smoke books {books.Count}; skills {length(SSskills.all_skills)}; SL1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.Single(stations);
            Assert.False(InertSeam.IsInert(stations[0]));
            Assert.Equal("implantchair_open", stations[0].icon_state);
            Assert.Single(books);
            Assert.False(InertSeam.IsInert(books[0]));
            Assert.Empty(runtimes);
        }
    }
}
