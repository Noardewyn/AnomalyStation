using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Appearance.P3;

/// <summary>
/// The overlay snapshot engine part (BYOND ref <c>/atom/var/overlays</c>: "a static snapshot of the object is taken at the
/// time when the overlay is created. Future changes to the object will not change the appearance of the overlay"; BYOND
/// interns appearances): docs/ai/specs/dm-conformance.md "M2 P3".
/// </summary>
public class OverlaySnapshotTests
{
    private static mutable_appearance Ma(string state, float layer = -1f)
        => (mutable_appearance)mutable_appearance(Dm.resource("icons/mob/effects/dam_mob.dmi"), state, layer);

    [Fact]
    public void AnOverlaysMaptextAndItsBoxAreKeptInTheSnapshot()
    {
        // protocol version 18 (HUD maptext): tg's keybind text on an action button is a mutable appearance with maptext (action_button.dm:177)
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            mutable_appearance ma = Ma("human_chest_10");
            ma.maptext = "<span class='maptext'>Q</span>";
            ma.maptext_width = 64;
            ma.maptext_x = -3;
            thing.add_overlay(ma);
            var view = (IDmAppearance)thing.overlays[1f];
            Assert.Equal("<span class='maptext'>Q</span>", view.maptext);
            Assert.Equal((64f, 32f, -3f, 0f), (view.maptext_width, view.maptext_height, view.maptext_x, view.maptext_y));
            ma.maptext = "changed";
            Assert.Equal("<span class='maptext'>Q</span>", view.maptext);

            thing.maptext = "7";
            thing.maptext_y = 5;
            var copy = (mutable_appearance)Dm.@new<mutable_appearance>(thing);
            Assert.Equal("7", copy.maptext);
            Assert.Equal((32f, 32f, 0f, 5f), (copy.maptext_width, copy.maptext_height, copy.maptext_x, copy.maptext_y));
        });
    }

    [Fact]
    public void AnAddedMutableAppearanceIsStoredAsASnapshotThatALaterChangeDoesNotReach()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            mutable_appearance ma = Ma("human_chest_10", -DAMAGE_LAYER);
            ma.color = "#ff0000";
            thing.add_overlay(ma);

            object item = thing.overlays[1f];
            Assert.IsType<DmAppearance>(item);
            Assert.NotSame(ma, item);
            var view = (IDmAppearance)item;
            Assert.Equal("human_chest_10", view.icon_state);
            Assert.True(Dm.eq(Dm.resource("icons/mob/effects/dam_mob.dmi"), view.icon));
            Assert.Equal(-DAMAGE_LAYER, view.layer);
            Assert.Equal("#ff0000", view.color);
            Assert.Equal(FLOAT_PLANE, view.plane); // mutable_appearance() leaves it on the float plane

            ma.icon_state = "human_chest_30"; // the overlay keeps the old look
            Assert.Equal("human_chest_10", ((IDmAppearance)thing.overlays[1f]).icon_state);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void RemovingFindsTheSnapshotEqualToTheItemsCurrentAppearance()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            mutable_appearance a = Ma("human_head_01");
            mutable_appearance twin = Ma("human_head_01");   // another object with an equal appearance
            thing.add_overlay(a);
            Assert.Same(thing.overlays[1f], DmAppearance.Of(twin)); // interned: one appearance per look
            Assert.True(Dm.@in(twin, thing.overlays));
            Assert.Equal(1f, thing.overlays.Find(twin));

            thing.cut_overlay(twin);
            Assert.Equal(0f, thing.overlays.len);

            thing.add_overlay(a);
            a.icon_state = "human_head_02"; // changed after it was added: it no longer equals the overlay (BYOND alike)
            thing.cut_overlay(a);
            Assert.Equal(1f, thing.overlays.len);
            a.icon_state = "human_head_01";
            Dm.remove(thing.overlays, a);
            Assert.Equal(0f, thing.overlays.len);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void NestedOverlaysAreSnapshotsTooAndTheirListsAreCopies()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            mutable_appearance holder = (mutable_appearance)mutable_appearance(layer: -DAMAGE_LAYER);
            mutable_appearance part = Ma("human_l_arm_20", -DAMAGE_LAYER);
            Dm.append(holder.overlays, part);
            Dm.append(holder.overlays, "glow"); // an icon state stays text
            thing.add_overlay(holder);

            var view = (IDmAppearance)thing.overlays[1f];
            DmList nested = view.overlays;
            Assert.Equal(2f, nested.len);
            Assert.IsType<DmAppearance>(nested[1f]);
            Assert.Equal("human_l_arm_20", ((IDmAppearance)nested[1f]).icon_state);
            Assert.Equal("glow", nested[2f]);
            Dm.append(nested, "more"); // a read gives a new list
            Assert.Equal(2f, view.overlays.len);

            part.icon_state = "human_l_arm_30"; // the nested snapshot does not change either
            Assert.Equal("human_l_arm_20", ((IDmAppearance)((IDmAppearance)thing.overlays[1f]).overlays[1f]).icon_state);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void TextAndIconsStayAsTheyAreAndAnOrdinaryListConvertsNothing()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            DmResource icon = Dm.resource("icons/effects/blood.dmi");
            thing.overlays.Add("state", icon);
            Assert.Equal("state", thing.overlays[1f]);
            Assert.Same(icon, thing.overlays[2f]);
            Dm.append(thing.underlays, "under");
            Assert.Equal("under", thing.underlays[1f]);

            mutable_appearance ma = Ma("x");
            DmList plain = Dm.list(ma);
            Assert.Same(ma, plain[1f]); // only an atom's overlays and underlays snapshot
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AnAtomAndAnAtomTypeAddedAsOverlaysAreSnapshotsOfTheirAppearance()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            obj other = Dm.@new<obj>(TgToyRoom.T(3, 2));
            other.icon = Dm.resource("icons/obj/storage/box.dmi");
            other.icon_state = "box";
            other.layer = 3.2f;
            Dm.append(thing.underlays, other);
            var snapshot = (DmAppearance)thing.underlays[1f];
            Assert.Equal("box", snapshot.icon_state);
            Assert.Equal(3.2f, snapshot.layer);
            Assert.True(snapshot.InheritsDir);
            other.icon_state = "box_open";
            Assert.Equal("box", ((IDmAppearance)thing.underlays[1f]).icon_state);

            Dm.append(thing.overlays, typeof(atom_movable_screen_healths)); // an object type: its prototype's appearance
            var typed = (IDmAppearance)thing.overlays[1f];
            Assert.Equal("health0", typed.icon_state);
            Assert.True(Dm.eq(Dm.resource("icons/hud/screen_gen.dmi"), typed.icon));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ADirectionInheritsUnlessAnImageWasGivenAnotherOne()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            Assert.True(DmAppearance.Of(Ma("a")).InheritsDir);                                   // a mutable appearance always
            Assert.True(DmAppearance.Of(image(Dm.resource("icons/x.dmi"), "a", -1)).InheritsDir);  // an image made without a dir
            DmAppearance east = DmAppearance.Of(image(Dm.resource("icons/x.dmi"), "a", -1, EAST));
            Assert.False(east.InheritsDir);
            Assert.Equal((float)EAST, east.dir);
        });
    }

    [Fact]
    public void ATransformAndAColourMatrixAreCopiedAndReadAsCopies()
    {
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            mutable_appearance ma = Ma("lying");
            ma.transform = Dm.matrix().Turn(90);
            ma.color = Dm.list(1, 0, 0, 0, 1, 0, 0, 0, 1);
            DmAppearance snapshot = DmAppearance.Of(ma);
            ((matrix)ma.transform).Translate(0, -6);
            ((DmList)ma.color)[1f] = 0;
            Assert.Equal(0f, ((matrix)snapshot.transform).f);
            ((matrix)snapshot.transform).Scale(5);
            Assert.Equal(1f, ((matrix)snapshot.transform).b, 4);
            Assert.True(Dm.eq(1, ((DmList)snapshot.color)[1f]));
            Assert.NotSame(DmAppearance.Of(ma), snapshot); // the changed matrix and colour make another appearance
            Assert.Equal(2, DmWorldState.Current.InternedAppearanceCount);
        });
    }

    [Fact]
    public void AnInPlaceEditOfOverlaysOrUnderlaysIsAnAppearanceChange()
    {
        // replication.md 5.3: an overlays += x, Add or Cut raises Changed(Overlays) in the tick it is made, as an assignment does
        var room = TgToyRoom.Create();
        room.In(() =>
        {
            obj thing = Dm.@new<obj>(TgToyRoom.T(2, 2));
            RecordingSinks sinks = RecordingSinks.Install(room.World);
            thing.add_overlay(Ma("a"));
            Assert.Contains(sinks.Lines, l => l.Contains("changed") && l.EndsWith(" Overlays"));
            sinks.Lines.Clear();
            Dm.append(thing.underlays, Ma("b"));
            Assert.Contains(sinks.Lines, l => l.EndsWith(" Underlays"));
            sinks.Lines.Clear();
            thing.overlays.Cut();
            Assert.Contains(sinks.Lines, l => l.EndsWith(" Overlays"));
        });
        Assert.Empty(room.RuntimeText);
    }
}
