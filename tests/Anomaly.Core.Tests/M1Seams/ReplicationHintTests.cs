using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M1Seams;

/// <summary>
/// A replication hint sink for tests: one line per hint, objects written as type and id (it keeps no reference to a world
/// object, as a real sink must not: a reference would keep the object alive and change the state hash).
/// </summary>
internal sealed class HintRecorder : IReplicationHintSink
{
    public List<string> Lines { get; } = new();

    public void Moved(object atom) => Lines.Add("moved " + Describe(atom));

    public void AppearanceChanged(object atom, AppearanceAspect aspect) => Lines.Add("appearance " + Describe(atom) + " " + aspect);

    public void VisibilityChanged(object atom) => Lines.Add("visibility " + Describe(atom));

    public void Deleted(long id) => Lines.Add("deleted " + id);

    public static string Describe(object atom) => atom is IDmObject o ? o.GetType().Name + "#" + o.dm_id : "?";
}

/// <summary>
/// The per-world replication hint sink (M1 contracts step; presentation-seam.md rule 5, object-model.md section 6): the atom
/// engine parts report moves, appearance and visibility changes of registered atoms, the registry reports deletions, and
/// creations are what lies above the registry's id watermark. Hints carry no values.
/// </summary>
public class ReplicationHintTests
{
    [Fact]
    public void AWorldStartsWithoutAReplicationSink()
    {
        Assert.Null(PresentationSinks.Of(new World()).Replication);
    }

    [Fact]
    public void AMoveAnAppearanceChangeAndAVisibilityChangeAreHinted()
    {
        var room = TgToyRoom.Create();
        var hints = new HintRecorder();
        PresentationSinks.Of(room.World).Replication = hints;
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            string name = HintRecorder.Describe(thing);
            Assert.Contains("moved " + name, hints.Lines); // the silent placement of a new atom is a move too
            hints.Lines.Clear();

            thing.loc = TgToyRoom.T(3, 3);
            thing.dir = NORTH;
            thing.glide_size = 8;
            thing.opacity = 1;

            Assert.Equal(new[]
            {
                "moved " + name,
                "appearance " + name + " Dir",
                "appearance " + name + " Glide",
                "visibility " + name,
            }, hints.Lines);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AContentsEditIsHintedWhenTheEngineAppliesIt()
    {
        var room = TgToyRoom.Create();
        var hints = new HintRecorder();
        PresentationSinks.Of(room.World).Replication = hints;
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            turf target = TgToyRoom.T(4, 3);
            hints.Lines.Clear();
            Dm.append(target.contents, thing); // DM: target.contents += thing, applied at the next engine access (D95 item 10)
            Assert.Same(target, thing.loc);
            Assert.Equal(new[] { "moved " + HintRecorder.Describe(thing) }, hints.Lines);
        });
    }

    [Fact]
    public void ADeletionIsHintedThroughTheRegistryAfterTheMoveToNullspace()
    {
        var room = TgToyRoom.Create();
        var hints = new HintRecorder();
        PresentationSinks.Of(room.World).Replication = hints;
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            long id = thing.dm_id;
            hints.Lines.Clear();
            qdel(thing);
            Assert.Contains("moved " + HintRecorder.Describe(thing), hints.Lines);
            Assert.Equal("deleted " + id, hints.Lines[^1]);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ObjectsCreatedSinceAWatermarkLieAboveIt()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            long mark = room.World.Objects.HighestId;
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            Assert.True(thing.dm_id > mark);
            var above = new List<object>();
            for (long cursor = mark; room.World.Objects.TryGetNextLive(cursor, out long id, out object found); cursor = id)
                above.Add(found);
            Assert.Contains(thing, above);
        });
    }

    [Fact]
    public void RemovingTheSinkAlsoRemovesItsRegistrySubscription()
    {
        var room = TgToyRoom.Create();
        var first = new HintRecorder();
        var second = new HintRecorder();
        PresentationSinks.Of(room.World).Replication = first;
        PresentationSinks.Of(room.World).Replication = second; // replaces the first, registry hook included
        room.In(() => qdel(Dm.@new<obj>(TgToyRoom.T(2, 2))));
        Assert.Empty(first.Lines);
        Assert.Contains(second.Lines, line => line.StartsWith("deleted ", StringComparison.Ordinal));

        PresentationSinks.Of(room.World).Replication = null;
        second.Lines.Clear();
        room.In(() => qdel(Dm.@new<obj>(TgToyRoom.T(2, 2))));
        Assert.Empty(second.Lines);
    }

    [Fact]
    public void ASinkHearsOnlyItsOwnWorld()
    {
        var hinted = TgToyRoom.Create();
        var other = TgToyRoom.Create();
        var hints = new HintRecorder();
        PresentationSinks.Of(hinted.World).Replication = hints;
        other.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            thing.loc = TgToyRoom.T(3, 2);
            thing.dir = EAST;
            qdel(thing);
        });
        Assert.Empty(hints.Lines);
    }

    [Fact]
    public void AnObjectWithoutAWorldIdIsNeverHinted()
    {
        var room = TgToyRoom.Create();
        var hints = new HintRecorder();
        PresentationSinks.Of(room.World).Replication = hints;
        room.In(() =>
        {
            var prototype = new obj(); // constructor only: no id, not registered (object-model.md section 3)
            prototype.dir = WEST;
            prototype.glide_size = 4;
            prototype.opacity = 1;
        });
        Assert.Empty(hints.Lines);
    }
}
