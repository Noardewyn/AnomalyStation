using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M1Seams;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.M2Seams;

/// <summary>
/// <c>/atom/var/name</c> as an engine property (M2 contracts; replication.md section 5.2, object-model.md section 2): an
/// assignment to a registered atom is an appearance hint like <c>dir</c> or <c>icon_state</c> (BYOND's name is an
/// appearance var), so tg's renames (<c>update_visible_name</c>) reach the replicator in the tick they happen instead of
/// through its sweep. The value itself behaves as before: DM var access, <c>initial()</c>, text rendering, the state hash.
/// </summary>
public class NamePropertyTests
{
    [Fact]
    public void ARenameOfARegisteredAtomIsHintedToBothSinks()
    {
        var room = TgToyRoom.Create();
        var hints = new HintRecorder();
        PresentationSinks.Of(room.World).Replication = hints;
        RecordingSinks recording = RecordingSinks.Install(room.World);
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            hints.Lines.Clear();
            recording.Lines.Clear();

            thing.name = "crowbar";

            Assert.Equal(new[] { "appearance " + HintRecorder.Describe(thing) + " Name" }, hints.Lines);
            Assert.Single(recording.Lines, l => l.Contains("changed") && l.EndsWith(" Name", StringComparison.Ordinal));
            Assert.Equal("crowbar", thing.name);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AnUnregisteredAtomRenamesSilently()
    {
        var room = TgToyRoom.Create();
        var hints = new HintRecorder();
        PresentationSinks.Of(room.World).Replication = hints;
        room.In(() =>
        {
            var prototype = new obj(); // a prototype: dm_id 0, never reported (presentation-seam.md rule 4)
            prototype.name = "nothing";
            Assert.Equal("nothing", prototype.name);
        });
        Assert.Empty(hints.Lines);
    }

    [Fact]
    public void TheDefaultIsTheTypeNameAndDmVarAccessAndInitialSeeTheProperty()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            Assert.Equal("obj", thing.name); // BYOND: the last path element, underscores as spaces
            Dm.set_var(thing, "name", "toolbox");
            Assert.Equal("toolbox", Dm.get_var(thing, "name"));
            Assert.Equal("obj", Dm.initial(thing, (atom p) => p.name));
            Assert.Equal("the toolbox", Dm.text("\\the []", thing));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void TheStateHashReadsTheName()
    {
        ulong Hash(string name)
        {
            var room = TgToyRoom.Create(seed: 5);
            room.In(() =>
            {
                obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
                thing.name = name;
            });
            return room.World.ComputeStateHash();
        }

        Assert.Equal(Hash("a"), Hash("a"));
        Assert.NotEqual(Hash("a"), Hash("b"));
    }
}
