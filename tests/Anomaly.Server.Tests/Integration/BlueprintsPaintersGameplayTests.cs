using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Package E14 (docs/ai/specs/engineering.md) on MetaStation through the real server and a client's own stack. An engineer takes the chief
/// engineer's blueprints out of his locker, stands in the cargo bay's sealed security post, and through tg's Blueprints window makes a
/// new area of the room: the room's tiles move into it, its APC, air alarm and lights follow (the APC powers the new area, the alarm
/// and the APC take its name, the lights stay lit on the APC's grid), the old area with no turf left is deleted; renaming the area
/// renames the machines; the structural view shows the roundstart pipes and cables around him to his client alone. With one of Meta's
/// airlock painters he repaints an airlock from tg's list (the client sees the new icon), and the decal painter's window paints a red
/// warning stripe the client draws on the floor tile.
/// </summary>
public class BlueprintsPaintersGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20; // ticks per second
    private const int PostApcX = 83, PostApcY = 137; // the APC of "Security Post - Cargo Bay"

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private sealed class Setup : IDisposable
    {
        public RealHeadlessWorld W { get; }
        public RealHeadlessWorld.Player P { get; }
        public mob_living_carbon_human Me { get; }
        public ReplicaWorld Replica { get; }
        public TguiWindows Windows { get; }
        public int Z { get; }

        public Setup(ulong seed, ITestOutputHelper output)
        {
            W = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 24000 },
                MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
            P = W.Join("Engineer");
            Me = (mob_living_carbon_human)W.MobOf(P);
            Replica = P.Session.Replica;
            Windows = new TguiWindows
            {
                SendAct = (window, action, parameters) => P.Session.UiAct(window, action, parameters),
                SendClose = window => P.Session.UiClose(window),
            };
            W.World.RuntimeErrorSink = e => output.WriteLine("RUNTIME: " + e.Message.Split('\n')[0]);
            Z = In(W, () => (int)Me.z);
        }

        public void Dispose() => W.Dispose();

        public void MoveTo(turf t)
        {
            In(W, () => Waiting.NoSleep(Me.forceMove(t)));
            W.Run(Second);
        }

        public void Click(atom target)
        {
            Assert.True(P.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            W.Run(Second);
        }

        public void ClickTurf(int x, int y)
        {
            Assert.True(P.Session.Click(new ClickTarget(TargetKind.Turf, 0, x, y, Z), ClickButton.Left, ClickModifiers.None));
            W.Run(Second);
        }

        public void Hold(obj_item item)
        {
            In(W, () =>
            {
                foreach (object held in Dm.each(Me.held_items.Copy()))
                    if (held is obj_item old)
                        Waiting.NoSleep(Me.dropItemToGround(old));
                Assert.True(Dm.truthy(Waiting.NoSleep(Me.put_in_hands(item))));
            });
            W.Run(2);
        }

        public UiWindowState Window(string @interface) => Assert.Single(Replica.UiWindows.Tgui(), v => v.Interface == @interface);

        public UiNode Content(UiWindowState window)
        {
            Windows.Sync(Replica.UiWindows, 1280, 720, 1);
            return Windows.Get(window.Window)?.Content;
        }

        // the answer to the open tgui input window of that interface (tg's ListInputWindow, TextInputModal)
        public void Answer(string @interface, string entry)
        {
            W.Run(Second / 2);
            UiWindowState input = Window(@interface);
            Assert.True(P.Session.UiAct(input.Window, "submit", UiValue.ObjectOf(("entry", UiValue.Of(entry)))));
            W.Run(Second);
        }
    }

    // a free open tile of the list: no density on it
    private static turf FreeTile(IEnumerable<turf> tiles) => tiles.First(t => t is turf_open_floor && !Dm.truthy(t.density)
        && !Dm.each<atom_movable>(t).Any(a => Dm.truthy(a.density)));

    [MetaServerFact]
    public void An_engineer_makes_a_new_area_of_a_sealed_room_with_the_blueprints_and_its_machines_follow()
    {
        using var s = new Setup(1401, output);
        RealHeadlessWorld w = s.W;

        // Meta keeps the station blueprints in the chief engineer's locker
        obj_item_blueprints prints = In(w, () => Dm.each<obj_item_blueprints>(Dm.world).Single());
        Assert.IsType<obj_structure_closet_secure_closet_engineering_chief>(In(w, () => prints.loc));

        // the cargo bay's security post: a room of one area with its APC, an air alarm and lights
        var (room, apc, alarm, lights, post) = In(w, () =>
        {
            var apcHere = Dm.locate<obj_machinery_power_apc>(locate(PostApcX, PostApcY, s.Z));
            DmList detected = Waiting.NoSleep(detect_room((turf)get_turf(apcHere), Dm.list(), 600)) as DmList;
            List<turf> tiles = Dm.each(detected).OfType<turf>().ToList();
            var alarmHere = tiles.SelectMany(t => Dm.each<obj_machinery_airalarm>(t)).Single();
            var lightsHere = tiles.SelectMany(t => Dm.each<obj_machinery_light>(t)).ToList();
            return (tiles, apcHere, alarmHere, lightsHere, apcHere.area);
        });
        string oldName = In(w, () => post.name);
        output.WriteLine($"the room: {room.Count} turfs of {oldName}, {lights.Count} lights");
        Assert.Equal("Security Post - Cargo Bay", oldName);
        Assert.True(lights.Count >= 2);
        Assert.True(In(w, () => lights.All(l => Dm.truthy(l.on))), "the lights are lit before");

        s.MoveTo(In(w, () => FreeTile(room)));
        s.Hold(prints);

        // tg's Blueprints window, used in hand
        s.P.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState window = s.Window("Blueprints");
        UiNode content = s.Content(window);
        Assert.Equal("You are now in Security Post - Cargo Bay", ByKey(content, "notice").Text);

        // create an area: tg's list (a new one or the room's area) then the name
        ByKey(content, "create").OnClick();
        s.Answer("ListInputWindow", "New Area");
        s.Answer("TextInputModal", "Cargo Den");

        area den = In(w, () => (area)get_area(s.Me));
        Assert.Equal("Cargo Den", In(w, () => den.name));
        In(w, () =>
        {
            Assert.All(room, t => Assert.Same(den, get_area(t)));
            // the APC moved with its room and powers the new area; the alarm and the APC take its name
            Assert.Same(den, apc.area);
            Assert.Same(apc, den.apc);
            Assert.True(Dm.truthy(den.power_light) && Dm.truthy(den.power_equip) && Dm.truthy(den.power_environ));
            Assert.Same(den, alarm.my_area);
            Assert.Equal("Cargo Den Air Alarm", alarm.name);
            Assert.Contains("Cargo Den", apc.name, StringComparison.Ordinal);
            Assert.All(lights, l => Assert.True(Dm.truthy(l.on), "a light stays lit on the APC's grid"));
            // the post's area held only this room: deleted (areas.dm:203)
            Assert.True(QDELETED(post));
        });
        content = s.Content(window);
        Assert.Equal("You are now in Cargo Den", ByKey(content, "notice").Text);

        // rename it: the area and the machines named after it
        ByKey(content, "rename").OnClick();
        s.Answer("TextInputModal", "Cargo Lounge");
        In(w, () =>
        {
            Assert.Equal("Cargo Lounge", den.name);
            Assert.Equal("Cargo Lounge Air Alarm", alarm.name);
            Assert.Contains("Cargo Lounge", apc.name, StringComparison.Ordinal);
        });

        // the wire legend: the round's wire sets, then the air alarm's colours
        ByKey(s.Content(window), "legend").OnClick();
        w.Run(Second);
        ByKey(s.Content(window), "wireset:/obj/machinery/airalarm").OnClick();
        w.Run(Second);
        content = s.Content(window);
        Assert.Equal("Air Alarm wires:", ByKey(content, "wires_name").Text);
        Assert.Contains(content.Walk(), n => n.Key?.StartsWith("wire:", StringComparison.Ordinal) == true);
        ByKey(content, "back").OnClick();
        w.Run(Second);
        ByKey(s.Content(window), "back").OnClick();
        w.Run(Second);
        Assert.Equal("off", s.Replica.UiWindows[window.Window].Merged.Get("legend").Text);

        // the structural view: the roundstart pipes and cables in view, as images on their tiles for this client alone
        int before = s.Replica.Images.Values.Count(i => i.TurfX > 0);
        ByKey(s.Content(window), "structure").OnClick();
        w.Run(Second);
        int shown = s.Replica.Images.Values.Count(i => i.TurfX > 0) - before;
        output.WriteLine($"structural view: {shown} images");
        Assert.True(shown > 10, "the pipes and cables around: " + shown);
        content = s.Content(window);
        Assert.Equal(("Hide structural data", "red"), (ByKey(content, "structure").Text, ByKey(content, "structure").Color));
        ByKey(content, "structure").OnClick();
        w.Run(Second);
        Assert.Equal(before, s.Replica.Images.Values.Count(i => i.TurfX > 0));
    }

    [MetaServerFact]
    public void An_engineer_repaints_an_airlock_and_paints_a_warning_stripe_the_client_draws()
    {
        using var s = new Setup(1402, output);
        RealHeadlessWorld w = s.W;

        // one of Meta's airlock painters (the tool storages' and engineering's, and those of the maintenance loot) and the nearest airlock
        // with a free tile beside it
        var (painter, airlock, stand) = In(w, () =>
        {
            obj_item_airlock_painter p = Dm.each<obj_item_airlock_painter>(Dm.world).First(x => x.GetType() == typeof(obj_item_airlock_painter));
            turf at = (turf)get_turf(p);
            foreach (obj_machinery_door_airlock door in Dm.each<obj_machinery_door_airlock>(Dm.world)
                .Where(d => d.z == at.z && Dm.num(get_dist(d, at)) <= 10)
                .OrderBy(d => Dm.num(get_dist(d, at))))
            {
                turf beside = Dm.each<turf>(orange(1, door)).FirstOrDefault(t => (t.x == door.x || t.y == door.y) && t is turf_open_floor
                    && !Dm.truthy(t.density) && !Dm.each<atom_movable>(t).Any(a => Dm.truthy(a.density)));
                if (beside != null && !Dm.truthy(door.locked))
                    return (p, door, beside);
            }
            throw new InvalidOperationException("no airlock near the painter");
        });
        string before = In(w, () => Dm.str(airlock.icon));
        output.WriteLine($"airlock {In(w, () => airlock.name)} ({before}) at {In(w, () => airlock.x)},{In(w, () => airlock.y)}");
        s.MoveTo(stand);
        s.Hold(painter);
        s.Click(airlock);
        s.Answer("ListInputWindow", "Atmospherics");
        Assert.Equal("icons/obj/doors/airlocks/station/atmos.dmi", In(w, () => Dm.str(airlock.icon)));
        Assert.Equal(4f, In(w, () => painter.ink.charges));
        ReplicaEntity seen = s.Replica.Entities.First(e => e.Id == airlock.dm_id);
        Assert.True(s.Replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        Assert.Equal("icons/obj/doors/airlocks/station/atmos.dmi", s.Replica.ResolveResource(look.Icon));

        // the decal painter's window: the warning stripes, red, facing east
        var decals = In(w, () => Dm.@new<obj_item_airlock_painter_decal>(s.Me.loc));
        s.Hold(decals);
        s.P.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState window = s.Window("DecalPainter");
        UiNode content = s.Content(window);
        Assert.Contains(content.Walk(), n => n.Kind == UiKind.ImageButton && n.DmIconState == "tile_fourcorners_2_d4d4d432");
        ByKey(content, "category:Warning Stripes").OnClick();
        w.Run(Second);
        ByKey(s.Content(window), "color:Red").OnClick();
        w.Run(Second);
        ByKey(s.Content(window), "decal:warningline_4_red").OnClick();
        w.Run(Second);
        content = s.Content(window);
        Assert.True(ByKey(content, "decal:warningline_4_red").Selected);

        // the floor tile under the engineer takes the stripe; the client's turf picture carries it
        (int x, int y) = In(w, () => ((int)s.Me.x, (int)s.Me.y));
        s.ClickTurf(x, y);
        w.Run(Second);
        Assert.True(s.Replica.TryGetAppearance(s.Replica.TurfAppearance(x, y, s.Z), out AppearanceEntry floor));
        var overlays = new List<(string Icon, string State, byte Dir)>();
        foreach (TableRef o in floor.Overlays)
            if (s.Replica.TryGetAppearance(o, out AppearanceEntry overlay))
                overlays.Add((s.Replica.ResolveResource(overlay.Icon), s.Replica.ResolveString(overlay.IconState), overlay.Dir));
        output.WriteLine("the floor's overlays: " + string.Join(", ", overlays));
        Assert.Contains(("icons/turf/decals.dmi", "warningline_red", (byte)4), overlays);
        Assert.Equal(24f, In(w, () => decals.ink.charges));
    }
}
