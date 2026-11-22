using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M2Seams;

/// <summary>
/// A <c>/mob/living/carbon/human</c> spawns in a booted test room with <c>NotTranslatedThrows</c> on (scope 2e, risk 2: the
/// throwing boundaries on the human init and shove paths became deferred leaves in the M2 contracts step). At the contracts
/// step the human's own Initialize chain is not translated yet (M2 P1 and P2 add the overrides: create_dna, set_species,
/// the bodyparts and organs), so the spawn runs the translated <c>/mob</c> and <c>/atom</c> chain on the M2 var blocks; the
/// test stays as the packages translate, and every <c>Dm.NotTranslated</c> they leave on the path then fails it. The
/// deferred leaves of the path count instead of throwing.
/// </summary>
public class HumanSpawnTests
{
    [Fact]
    public void AHumanSpawnsWithNoNotTranslatedLeafOnItsPath()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 23);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            Assert.NotNull(human);
            Assert.False(QDELETED(human));
            Assert.Same(TgToyRoom.T(4, 4), human.loc);
            var consistent = Dm.@new<mob_living_carbon_human_consistent>(TgToyRoom.T(5, 4));
            Assert.Same(TgToyRoom.T(5, 4), consistent.loc);
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        room.World.Step(40); // two SSmobs periods (an inert stub until M2 P1)
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void TheDeferredLeavesOfTheInitAndShovePathsCountInsteadOfThrowing()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 24);
        room.In(() =>
        {
            DmWorldState state = DmWorldState.Current;
            state.NotTranslatedThrows = true;
            var human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            int before = state.DeferredCount;
            Waiting.NoSleep(human.do_attack_animation(TgToyRoom.T(5, 4)));
            Waiting.NoSleep(human.drop_all_held_items());
            // M4a P2: drop_all_held_items is translated (no deferred leaf); do_attack_animation is translated whole and
            // reaches its play_fov_effect leaf (atoms_movable.dm:1568).
            Assert.Equal(before + 1, state.DeferredCount); // M9 slice A: grant_language is translated; visible_message is translated (it needs SSspatial_grid, which this room has not)
            state.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }
}
