using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.M2Seams;

/// <summary>
/// The BYOND builtins the M2 appearance code needs (M2 contracts; docs/ai/specs/dm-conformance.md "M2 contracts"): the
/// <c>transform</c> var's copy semantics, <c>/mutable_appearance</c> with tg's override of its <c>New</c>, the image's
/// appearance vars, and <see cref="IDmAppearance"/> as the read-only view of an atom and an image.
/// </summary>
public class AppearanceBuiltinTests
{
    [Fact]
    public void ReadingTransformGivesACopyAndAssigningStoresOne()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            // no transformation by default: a read gives the identity matrix (the var's default is matrix(); M2 integration)
            var identity = (matrix)thing.transform;
            Assert.Equal((1f, 0f, 0f, 0f, 1f, 0f), (identity.a, identity.b, identity.c, identity.d, identity.e, identity.f));
            Assert.Null(((IDmAppearance)thing).transform); // the stored value: none

            matrix turned = Dm.matrix().Turn(90);
            thing.transform = turned;
            turned.Scale(2); // a later change of the assigned matrix does not reach the atom
            var first = (matrix)thing.transform;
            Assert.NotSame(first, thing.transform); // every read is a new copy
            Assert.Equal(0f, first.a, 5);
            Assert.Equal(1f, first.b, 5);

            first.Translate(16, 0); // changing a read copy changes nothing until it is assigned back (tg's idiom)
            Assert.Equal(0f, ((matrix)thing.transform).c);
            thing.transform = first;
            Assert.Equal(16f, ((matrix)thing.transform).c);

            thing.transform = Dm.list(2, 0, 0, 0, 2, 0); // "a list with six values, which is equivalent to using a matrix"
            Assert.Equal((2f, 2f), (((matrix)thing.transform).a, ((matrix)thing.transform).e));

            thing.transform = null;
            Assert.Equal(1f, ((matrix)thing.transform).a); // the identity again
            Assert.Null(((IDmAppearance)thing).transform);
            Assert.Throws<DmRuntimeException>(() => thing.transform = "turned");
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AMutableAppearanceWithoutASourceIsOnTheFloatPlaneAsTgSetsIt()
    {
        var ma = Dm.@new<mutable_appearance>();
        Assert.Equal(FLOAT_PLANE, ma.plane); // tg's override, datums/mutable_appearance.dm:8-11
        Assert.Equal(FLOAT_LAYER, ma.layer);
        Assert.Equal(255f, ma.alpha);
        Assert.Empty(ma.overlays.CopyItems());
        Assert.Equal("/mutable_appearance", DmTypes.PathOf(typeof(mutable_appearance)));
        Assert.True(ma is image); // "technically a descendant of /image"
    }

    [Fact]
    public void AMutableAppearanceCopiesItsSourcesAppearance()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            thing.icon_state = "crowbar";
            thing.layer = 3.5f;
            thing.plane = 7;
            thing.alpha = 128;
            thing.color = "#ff0000";
            thing.pixel_x = 4;
            thing.transform = Dm.matrix().Turn(90);
            thing.name = "bar";
            var overlay = Dm.@new<mutable_appearance>();
            overlay.icon_state = "glow";
            Dm.append(thing.overlays, overlay);

            var ma = Dm.@new<mutable_appearance>(thing);
            Assert.Equal("crowbar", ma.icon_state);
            Assert.Equal((3.5f, 7f, 128f, 4f), (ma.layer, ma.plane, ma.alpha, ma.pixel_x)); // a source keeps its plane
            Assert.Equal("#ff0000", ma.color);
            Assert.Equal("bar", ma.name);
            Assert.NotSame(thing.overlays, ma.overlays); // a copied list
            Assert.Equal(1f, ma.overlays.len);

            var again = Dm.@new<mutable_appearance>(ma);
            Assert.Equal((3.5f, 7f), (again.layer, again.plane));
            Assert.Equal(1f, ((matrix)again.transform).b, 5);
            Assert.Throws<DmRuntimeException>(() => Dm.@new<mutable_appearance>("crowbar"));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AnAtomAndAnImageShowTheirAppearanceThroughIDmAppearance()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            thing.icon_state = "box";
            thing.pixel_w = 3;
            thing.transform = Dm.matrix().Scale(2);
            IDmAppearance view = thing;
            Assert.Equal("box", view.icon_state);
            Assert.Equal(3f, view.pixel_w);
            Assert.Same(view.transform, view.transform); // the view reads the stored matrix, it does not copy
            Assert.Same(thing.overlays, view.overlays);
        });
        IDmAppearance image = Dm.image(null, null, "state");
        Assert.Equal("state", image.icon_state);
        Assert.Equal(FLOAT_PLANE, image.plane);
        Assert.NotNull(image.overlays);
    }

    [Fact]
    public void TheAppearanceFlagsAreBYONDs()
    {
        // BYOND ref appearance_flags (stddef constants, src/Core/DmShim/Dm.Constants.cs).
        Assert.Equal((1f, 2f, 4f, 8f, 16f, 32f, 64f, 128f, 256f, 512f, 1024f, 2048f),
            (LONG_GLIDE, RESET_COLOR, RESET_ALPHA, RESET_TRANSFORM, NO_CLIENT_COLOR, KEEP_TOGETHER, KEEP_APART, PLANE_MASTER, TILE_BOUND, PIXEL_SCALE, PASS_MOUSE, TILE_MOVER));
    }
}
