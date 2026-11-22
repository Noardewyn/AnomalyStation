using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.Tg.M3Doors.P3;

// The builtin range (Builtin/map.builtin.cs, M3 P3): orange with the centre, in OpenDream's order (HandleRange with
// includeCenter, DantomSpiral).
public class RangeTests
{
    [Fact]
    public void RangeOfATurfStartsWithTheTurfItsAreaAndContentsThenTheSpiral()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            turf center = locate(4, 4, 1);
            var item = Dm.@new<obj_item>(center);
            DmList r = range(1, center);
            Assert.Same(center, r[1]);
            Assert.Same(center.loc, r[2]); // its area, the first time a turf of it is added
            Assert.Same(item, r[3]);
            Assert.Same(locate(3, 3, 1), r[4]); // ring 1: the left column bottom to top first
            Assert.Same(locate(3, 4, 1), r[5]);
            int turfs = 0;
            foreach (turf t in Dm.each<turf>(r)) turfs++;
            Assert.Equal(9, turfs);
            // orange leaves the centre and its contents out
            DmList o = orange(1, center);
            Assert.False(Dm.@in(center, o));
            Assert.False(Dm.@in(item, o));
            Assert.Same(locate(3, 3, 1), o[1]);
        });
    }

    [Fact]
    public void RangeOfAMovableStartsWithItsContentsThenItsTurfWithItself()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            turf loc = locate(4, 4, 1);
            var box = Dm.@new<obj_item>(loc);
            var inner = Dm.@new<obj_item>(box);
            DmList r = range(1, box);
            Assert.Same(inner, r[1]);
            Assert.Same(loc, r[2]);
            Assert.Same(loc.loc, r[3]);
            Assert.True(Dm.@in(box, r)); // the loc's contents include the centre
            Assert.False(Dm.@in(box, orange(1, box)));
            // a movable inside another atom: that atom and its contents, no box around
            DmList inside = range(1, inner);
            Assert.Same(box, inside[1]);
            Assert.Same(inner, inside[2]);
            Assert.Equal(2f, inside.len);
        });
    }
}
