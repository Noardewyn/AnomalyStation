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
/// The recreation on the converted MetaStation: the holodeck console and its rec center boot live and linked, with SSmapping's
/// holodeck programs loaded from the converted templates; the reinforced floor stays until a program is loaded, as tg's console starts
/// on the offline program; a program loads onto Meta's holodeck floor and the offline program's plating replaces it; the Orion Trail cabinet boots live and starts a game; SStrading_card_game has its sets; and nothing in
/// the recreation files throws over the ticks.
/// </summary>
public class RecreationMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public RecreationMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "holodeck/computer.cs", "holo_effect.cs", "holodeck/items.cs", "areas/holodeck.cs", "arcade/", "slotmachine.cs",
        "roulette_machine.cs", "tcg/", "tcgsetup.cs",
    };

    private void Step(int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
            _meta.World.Step();
    }

    [MetaFact]
    public void Metas_holodeck_loads_a_program_and_its_arcade_plays()
    {
        Step(40);
        int runtimesBefore = _meta.Log.Runtimes.Count;
        obj_machinery_computer_holodeck console;
        using (_meta.World.Enter())
        {
            console = Assert.Single(Dm.each<obj_machinery_computer_holodeck>().ToList());
            Assert.False(InertSeam.IsInert(console));
            var area = Assert.IsAssignableFrom<area_station_holodeck_rec_center>(console.linked);
            Assert.Same(console, area.linked);
            _output.WriteLine($"Meta's holodeck: {SSmapping.holodeck_templates.len} programs, {Dm.each<turf>(area).Count()} turfs, offline program {console.program}");
            Assert.Equal(29f, SSmapping.holodeck_templates.len);
            Assert.Equal("holodeck_offline", console.program);
            // tg's console starts on the offline program, so its init's load returns at once: Meta's reinforced floor stays until a
            // program is loaded, and the process skips the floor check meanwhile
            Assert.All(Dm.each<turf>(area).ToList(), t => Assert.IsType<turf_open_floor_engine>(t));
            Assert.Equal(0f, console.damaged);

            console.holodeck_cooldown = 0;
            Waiting.NoSleep(console.load_program("holodeck_basketball"));
        }
        Step(20);
        using (_meta.World.Enter())
        {
            Assert.Equal("holodeck_basketball", console.program);
            var hoops = Dm.each(console.spawned).OfType<obj_structure_hoop>().ToList();
            _output.WriteLine($"the basketball court: {length(console.spawned)} spawned, {hoops.Count} hoops");
            Assert.NotEmpty(hoops);
            Assert.All(hoops, h => Assert.True(Dm.@in(get_turf(h), console.linked)));
            Assert.True(Dm.truthy(console.floorcheck()), "the court is projected over the whole floor");
            console.holodeck_cooldown = 0;
            Waiting.NoSleep(console.load_program("holodeck_offline"));
        }
        Step(20);
        using (_meta.World.Enter())
        {
            Assert.Equal("holodeck_offline", console.program);
            Assert.All(Dm.each<turf>(console.linked).ToList(), t => Assert.IsType<turf_open_floor_holofloor_plating>(t));
            Assert.Empty(Dm.each(console.spawned).OfType<obj_structure_hoop>().Where(h => !QDELETED(h)).ToList());

            // the mapped Orion Trail and the cabinets of Meta's arcade spawners (spawners/random/entertainment.dm)
            var cabinets = Dm.each<obj_machinery_computer_arcade>().ToList();
            _output.WriteLine("Meta's arcade cabinets: " + string.Join(", ", cabinets.GroupBy(c => c.GetType().Name).Select(g => g.Key + " x" + g.Count())));
            Assert.All(cabinets, c => Assert.False(InertSeam.IsInert(c)));
            var orion = Dm.each<obj_machinery_computer_arcade_orion_trail>().First(o => o.GetType() == typeof(obj_machinery_computer_arcade_orion_trail));
            var player = Dm.@new<mob_living_carbon_human>(get_step(orion, SOUTH));
            player.real_name = "A Settler";
            orion.newgame(player);
            Assert.Equal(2f, orion.gameStatus);
            Assert.Equal(4f, length(orion.settlers));

            Assert.Equal(1f, SStrading_card_game.loaded);
            Assert.NotNull(SStrading_card_game.cached_cards["coreset2020"]);
            Assert.NotNull(SStrading_card_game.cached_cards["resinfront"]);
        }
        Step(100);
        var runtimes = _meta.Log.Runtimes.Skip(runtimesBefore)
            .Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
        foreach (var e in runtimes)
            _output.WriteLine("runtime: " + (e.Message.Length > 300 ? e.Message[..300] : e.Message));
        Assert.Empty(runtimes);
        // the boot itself: no recreation runtime before the test either
        var boot = _meta.Log.Runtimes.Take(runtimesBefore).Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
        foreach (var e in boot)
            _output.WriteLine("boot runtime: " + (e.Message.Length > 300 ? e.Message[..300] : e.Message));
        Assert.Empty(boot);
    }
}
