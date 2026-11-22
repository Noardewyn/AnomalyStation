using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>The desk bell of MetaStation's Cargo Desk (the owner's report, 2026-11-05): a hand from the lobby side reaches it past the open firelock and shutters.</summary>
public class DeskBellTests
{
    [Fact]
    public void AClickFromTheLobbyRingsTheBellOnADeskWithAWindoorBehindIt()
    {
        var r = new AudioRoom(10, 10, seed: 8311);
        var bell = r.In(() =>
        {
            turf desk = locate(5, 5, 1);
            Dm.@new<obj_structure_table_reinforced>(desk);
            Dm.@new<obj_machinery_door_firedoor>(desk);
            Dm.@new<obj_machinery_door_window_right_directional_south>(desk);
            Dm.@new<obj_machinery_door_poddoor_shutters_preopen>(desk);
            return Dm.@new<obj_structure_desk_bell>(desk);
        });
        mob_living_carbon_human bob = r.Join(5, 6, "Bob");
        r.Step(20);

        WallTests.Click(r, bob, bell);

        Assert.Equal(1, r.In(() => bell.times_rang));
        r.AssertNoRuntimes();
    }
}
