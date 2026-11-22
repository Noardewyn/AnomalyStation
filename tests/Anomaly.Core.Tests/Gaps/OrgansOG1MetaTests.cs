using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package OG1 on the converted MetaStation: the morgue's dead body placer slides tg's three cadavers (the recovered crew's morgue
/// class, not revivable) into its closed beeping trays, each in a body bag tagged and renamed with the body's name and species, and
/// deletes itself, with no runtime from the lost crew's files.
/// </summary>
public class OrgansOG1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public OrgansOG1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    [MetaFact]
    public void Metas_morgue_trays_hold_the_dead_body_placers_three_cadavers()
    {
        using (_meta.World.Enter())
        {
            var bags = new List<obj_structure_closet_body_bag>();
            foreach (obj_structure_bodycontainer_morgue morgue in Dm.each<obj_structure_bodycontainer_morgue>(Dm.world))
                bags.AddRange(Dm.each<obj_structure_closet_body_bag>(morgue.contents));
            foreach (var bag in bags)
                _output.WriteLine($"{bag.name} at {bag.loc}");
            Assert.Equal(3, bags.Count);
            foreach (var bag in bags)
            {
                var body = Assert.Single(Dm.each<mob_living_carbon_human>(bag.contents));
                Assert.Equal(DEAD, body.stat);
                Assert.True(Dm.truthy(bag.opened) == false, "closed");
                Assert.StartsWith(Dm.str(body.real_name) + " - ", Dm.str(bag.tag_name));
                Assert.Contains(Dm.str(bag.tag_name), Dm.str(bag.name));
                Assert.NotNull(body.station_timestamp_timeofdeath);
                // not revivable: no ghost role, no exploding brain
                Assert.False(HAS_TRAIT(body, TRAIT_GHOSTROLE_ON_REVIVE));
            }
            Assert.Empty(Dm.each<obj_effect_mapping_helpers_dead_body_placer>(Dm.world));
            var runtimes = _meta.Log.Runtimes.Where(e => (e.ToString() ?? "").Contains("lost_crew", StringComparison.Ordinal)
                || (e.ToString() ?? "").Contains("corpse", StringComparison.Ordinal)).ToList();
            Assert.True(runtimes.Count == 0, string.Join("\n", runtimes));
        }
    }
}
