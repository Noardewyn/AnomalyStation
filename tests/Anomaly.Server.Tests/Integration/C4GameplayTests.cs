using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M5x slice 4 through the real server on MetaStation with a headless Godot client: an engineer in the Aux base construction zone
/// holds a C-4 charge, presses Z (tg's activate_inhand: the charge's attack_self opens the "C4 Timer" number window), sets 12
/// seconds in the client's translated NumberInputModal and submits, clicks the zone's west wall (42,155): after the 3 s do_after
/// the charge has left the hand, the wall's appearance on the client carries the charge's <c>plastic-explosive2</c> overlay. The
/// engineer walks out of the zone; 12 s after the plant the wall is gone (dismantled by the charge's heavy ex_act, the
/// (0, 0, 3) blast after it), the overlay with it, and the zone vents into space.
/// </summary>
public class C4GameplayTests
{
    private const int Second = 20;
    private const int WallX = 42, WallY = 155;

    private readonly ITestOutputHelper _output;

    public C4GameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    private static bool ShowsSticker(ReplicaWorld replica, int x, int y)
    {
        if (!replica.TryGetAppearance(replica.TurfAppearance(x, y, 1), out AppearanceEntry wall))
            return false;
        return wall.Overlays.Any(o => replica.TryGetAppearance(o, out AppearanceEntry e) && replica.ResolveString(e.IconState) == "plastic-explosive2");
    }

    private static byte StickerDir(ReplicaWorld replica, int x, int y)
    {
        replica.TryGetAppearance(replica.TurfAppearance(x, y, 1), out AppearanceEntry wall);
        foreach (TableRef o in wall.Overlays)
            if (replica.TryGetAppearance(o, out AppearanceEntry e) && replica.ResolveString(e.IconState) == "plastic-explosive2")
                return e.Dir;
        return 0;
    }

    [MetaServerFact]
    public void An_engineer_sets_the_timer_plants_C4_on_the_Aux_base_wall_and_it_breaches_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 37, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        obj_item_grenade_c4 c4 = null;
        using (w.World.Enter())
        {
            Waiting.NoSleep(me.forceMove(At(WallX + 1, WallY)));
            c4 = Dm.@new<obj_item_grenade_c4>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(c4))));
        }
        w.Run(Second);
        Assert.Equal("/turf/closed/wall", In(w, () => DmTypes.PathOf(At(WallX, WallY).GetType())));

        // Z: the charge's attack_self asks for the timer in tg's NumberInputModal
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("NumberInputModal", window.Interface);
        Assert.Equal("C4 Timer", window.Merged.Str("title")); // the modal draws its title from its data
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        UiNode content = windows.Get(window.Window).Content;
        content.Walk().Single(n => n.Key == "entry").OnChange(12);
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        windows.Get(window.Window).Content.Walk().Single(n => n.Key == "submit").OnClick();
        w.Run(Second);
        Assert.Equal(12f, In(w, () => c4.det_time));
        Assert.Empty(replica.UiWindows.Tgui());

        // A click on the wall: the 3 s plant, the charge out of the hand, the sticker on the wall the client draws.
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, WallX, WallY, 1), ClickButton.Left, ClickModifiers.None));
        w.Run(4 * Second);
        Assert.Null(In(w, () => c4.loc));
        Assert.Same(In(w, () => At(WallX, WallY)), In(w, () => c4.target));
        Assert.True(ShowsSticker(replica, WallX, WallY), "the wall's appearance carries plastic-explosive2");
        // DV63: the sticker faces the planter, who stood east of the wall (the 3D client draws it on that face only)
        Assert.Equal(ByondDir.East, StickerDir(replica, WallX, WallY));

        // Out of the zone through its airlock, then the blast.
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(At(48, 148)));
        w.Run(8 * Second);
        Assert.Equal("/turf/closed/wall", In(w, () => DmTypes.PathOf(At(WallX, WallY).GetType()))); // 12 s are not up yet
        w.Run(5 * Second);
        string after = In(w, () => DmTypes.PathOf(At(WallX, WallY).GetType()));
        _output.WriteLine("the wall after the charge: " + after);
        Assert.True(In(w, () => QDELETED(c4)));
        Assert.False(after.StartsWith("/turf/closed", StringComparison.Ordinal), "breached: " + after);
        Assert.False(ShowsSticker(replica, WallX, WallY), "the sticker went with the wall");
        // tg's explosion sound (explosions.dm: SFX_EXPLOSION near the epicentre) reached the engineer
        Assert.Contains(heard, e => e.File != null && e.File.StartsWith("sound/effects/explosion/", StringComparison.Ordinal));
        Assert.NotEqual("/turf/closed/wall", replica.TurfTypeAt(WallX, WallY, 1));
    }

    /// <summary>
    /// The owner's report (2026-10-18): a charge planted on a window, a firelock or a locker was not drawn. tg plants C-4 on whatever its click
    /// reaches (plastic.dm:106, interact_with_atom; the target's item_interaction first): an engineer south of each thing clicks it with a charge
    /// (a long timer, no blast); the charge sticks to it and the thing's appearance on the client carries <c>plastic-explosive2</c> facing south,
    /// the planter's side (DV63), which the 3D client draws its model on (at the attach point facing that way). A type whose own item_interaction is not
    /// translated yet (a vending machine) refuses the click with "not ported yet" (D132), so it is not among them.
    /// </summary>
    [MetaServerFact]
    public void A_charge_sticks_to_every_kind_of_thing_and_faces_its_planter()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 43, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var targets = new (Type Type, int X, int Y)[]
        {
            (typeof(obj_machinery_door_firedoor), 44, 158), (typeof(obj_structure_window_fulltile), 46, 158), (typeof(obj_structure_grille), 48, 158),
            (typeof(obj_structure_closet), 50, 158), (typeof(obj_structure_closet_crate), 44, 154), (typeof(obj_structure_window_reinforced_fulltile), 46, 154),
            (typeof(obj_machinery_door_airlock), 48, 154), (typeof(obj_structure_table), 50, 154),
        };
        var made = new List<atom>();
        using (w.World.Enter())
            foreach ((Type type, int x, int y) in targets)
                made.Add((atom)Dm.@new(type, At(x, y)));
        w.Run(Second);
        for (int i = 0; i < targets.Length; i++)
        {
            (Type type, int x, int y) = targets[i];
            obj_item_grenade_c4 c4;
            using (w.World.Enter())
            {
                Waiting.NoSleep(me.forceMove(At(x, y - 1)));
                c4 = Dm.@new<obj_item_grenade_c4>(me.loc);
                c4.det_time = 600;
                Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(c4))));
            }
            w.Run(Second / 2);
            Assert.True(p.Session.Click(made[i].dm_id, ClickButton.Left, ClickModifiers.None));
            w.Run(4 * Second);
            string name = DmTypes.PathOf(type);
            Assert.True(ReferenceEquals(made[i], In(w, () => c4.target)), name + ": the charge is stuck to it");
            Assert.True(replica.TryGet(made[i].dm_id, out ReplicaEntity seen), name + " on the client");
            Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
            byte dir = 0;
            foreach (TableRef o in look.Overlays)
                if (replica.TryGetAppearance(o, out AppearanceEntry e) && replica.ResolveString(e.IconState) == "plastic-explosive2")
                    dir = e.Dir;
            _output.WriteLine($"{name}: sticker dir {dir}");
            Assert.Equal(ByondDir.South, dir);
        }
    }

    /// <summary>
    /// The grenade's multitool act asks for the fuse in tg's ListInputWindow (tgui_input_list): the client's translated window
    /// lists tg's possible_fuse_time, a click selects "3" and Submit sends it; the grenade's det_time is 3 s.
    /// </summary>
    [MetaServerFact]
    public void A_multitool_on_a_grenade_picks_its_fuse_from_tgs_list_window()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 41, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        obj_item_grenade grenade = null;
        using (w.World.Enter())
        {
            Waiting.NoSleep(me.forceMove(At(WallX + 2, WallY)));
            grenade = Dm.@new<obj_item_grenade>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(Dm.@new<obj_item_multitool>(me.loc)))));
        }
        w.Run(Second);
        Assert.Equal(50f, In(w, () => grenade.det_time));

        Assert.True(p.Session.Click(grenade.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("ListInputWindow", window.Interface);
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        List<UiNode> entries = windows.Get(window.Window).Content.Walk().Where(n => n.Kind == UiKind.Button && n.OnDoubleClick != null).ToList();
        Assert.Equal(new[] { "Instant", "3", "4", "5" }, entries.Select(n => n.Text).ToArray());
        entries[1].OnClick(); // select "3"
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        Assert.True(windows.Get(window.Window).Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "3").Selected);
        windows.Get(window.Window).Content.Walk().Single(n => n.Key == "submit").OnClick();
        w.Run(Second);
        Assert.Equal(30f, In(w, () => grenade.det_time));
        Assert.Empty(replica.UiWindows.Tgui());
    }
}

