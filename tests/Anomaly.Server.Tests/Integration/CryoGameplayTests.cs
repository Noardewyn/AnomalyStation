using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.Render;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M8x slice C (docs/ai/specs/cryo.md, D210) on MetaStation through the real server and the clients' own stacks: a doctor in the Cryo room
/// wrenches the anesthetic canister onto its port (tg connects a portable only when wrenched), turns the loop's freezer on at its
/// minimum in its window, takes a mapped cryoxadone beaker from the table into the cell at 80,111 by a click, opens the cell with an
/// alt-click and drags the stunned patient in. Both clients see the cell go pod-open -> pod-on with its flicks and the occupant visual
/// (tg's cryo_occupant, drawn on the cell with the patient in its vis_contents and its bob chain), hear the cryo loop; the doctor reads
/// the Cryo window; the patient's own screen shows the impaired fullscreen. Healed, the cell warns (cryo_warning.ogg) and ejects him.
/// </summary>
public class CryoGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    internal static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    internal static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    internal static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    internal static AppearanceEntry Look(ReplicaWorld replica, long id)
        => replica.TryGet(id, out ReplicaEntity seen) && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;

    internal static string StateOf(ReplicaWorld replica, long id)
    {
        AppearanceEntry look = Look(replica, id);
        return look == null ? null : replica.ResolveString(look.IconState);
    }

    // Every icon state of an appearance and its overlays, depth first, as the client's compositor walks them.
    internal static List<string> States(ReplicaWorld replica, TableRef reference, int depth = 0)
    {
        var found = new List<string>();
        if (depth > 8 || !replica.TryGetAppearance(reference, out AppearanceEntry entry))
            return found;
        found.Add(replica.ResolveString(entry.IconState));
        foreach (TableRef o in entry.Overlays)
            found.AddRange(States(replica, o, depth + 1));
        return found;
    }

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    // The cell's picture as the client composes it (EntityLayer: VisInsets), with the occupant visual at a pixel_y of its bob, or without it.
    private Raster CellPicture(AppearanceCompositor compositor, ReplicaWorld replica, long cell, int? occupantY, string name)
    {
        var insets = new VisInsets(new Tables(replica));
        Assert.True(replica.TryGet(cell, out ReplicaEntity seen));
        TableRef look = seen.State.Appearance;
        if (occupantY is int y)
            look = insets.With(look, OccupantVisual(replica, cell).State.Appearance, 0, y);
        Assert.True(compositor.TryCompose(insets, look, ByondDir.South, 0, out ComposedRaster picture), string.Join("; ", compositor.Library.Problems));
        string folder = Environment.GetEnvironmentVariable("ANOMALY_CRYO_PICTURES");
        if (folder != null)
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), PngEncoder.Encode(picture.Image, 6, 8, 0, false));
        return picture.Image;
    }

    // The pixels that differ, the pictures' bottoms aligned (the occupant's masked group reaches above the cell, transparent).
    private static int Differing(Raster a, Raster b)
    {
        Assert.Equal(a.Width, b.Width);
        int n = 0;
        for (int row = 1; row <= Math.Min(a.Height, b.Height); row++)
        {
            for (int x = 0; x < a.Width; x++)
            {
                int i = ((a.Height - row) * a.Width + x) * 4, j = ((b.Height - row) * b.Width + x) * 4;
                if (a.Pixels[i] != b.Pixels[j] || a.Pixels[i + 1] != b.Pixels[j + 1] || a.Pixels[i + 2] != b.Pixels[j + 2] || a.Pixels[i + 3] != b.Pixels[j + 3])
                    n++;
            }
        }
        return n;
    }

    internal static ReplicaEntity OccupantVisual(ReplicaWorld replica, long cell)
        => replica.Entities.FirstOrDefault(e => e.IsVisual && e.VisHost == cell && e.TypePath == "/atom/movable/visual/cryo_occupant");

    [MetaServerFact]
    public void A_doctor_puts_a_stunned_patient_in_the_cryo_cell_which_shows_him_bobbing_inside_and_ejects_him_healed()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 97, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        ReplicaWorld seenByAlice = alice.Session.Replica;
        ReplicaWorld seenByBob = bob.Session.Replica;
        var heard = new List<SoundEvent>();
        seenByAlice.SoundsReceived += events => heard.AddRange(events);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        var cell = In(w, () => Dm.locate<obj_machinery_cryo_cell>(locate(80, 111, 1)));
        var freezer = In(w, () => Dm.locate<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(locate(79, 107, 1)));
        var canister = In(w, () => Dm.locate<obj_machinery_portable_atmospherics_canister_anesthetic_mix>(locate(81, 107, 1)));
        var wrench = In(w, () => Dm.each_cast<atom>(locate(80, 107, 1).contents).OfType<obj_item_wrench>().First());
        var beaker = In(w, () => Dm.each_cast<atom>(locate(79, 111, 1).contents).OfType<obj_item_reagent_containers_cup_beaker_cryoxadone>().OrderBy(b => b.dm_id).First());
        Assert.NotNull(cell);
        Assert.NotNull(freezer);
        Assert.NotNull(canister);

        Do(w, () => Waiting.NoSleep(aliceMob.forceMove(locate(80, 108, 1)))); // M10 slice C: a client holds what is around it
        w.Run(Second);

        // the cell at the round start: closed and off, its occupant visual (tg's cryo_occupant) drawn on it as a visual of its own, with no one in it
        Assert.Equal("pod-off", StateOf(seenByAlice, cell.dm_id));
        ReplicaEntity occupantSeen = OccupantVisual(seenByAlice, cell.dm_id);
        Assert.NotNull(occupantSeen);
        Assert.Equal("mask_bg", StateOf(seenByAlice, occupantSeen.Id));
        Assert.DoesNotContain("mask_bg", States(seenByAlice, seenByAlice.Entities.First(e => e.Id == cell.dm_id).State.Appearance)); // not twice in the cell's picture

        // 1. the doctor wrenches the canister at 81,107 onto its port (portable_atmospherics.dm:280)
        Do(w, () => Waiting.NoSleep(aliceMob.forceMove(locate(80, 108, 1))));
        w.Run(Second / 2);
        Assert.True(alice.Session.Click(On(wrench), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(aliceMob, In(w, () => wrench.loc));
        Assert.True(alice.Session.Click(On(canister), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.NotNull(In(w, () => canister.connected_port));

        // 2. the freezer at 79,107 on at its minimum, in its window (an empty hand)
        Do(w, () => Waiting.NoSleep(aliceMob.dropItemToGround(wrench)));
        w.Run(5);
        Assert.True(alice.Session.Click(On(freezer), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        UiWindowState thermo = Assert.Single(seenByAlice.UiWindows.Tgui());
        Assert.Equal("ThermoMachine", thermo.Interface);
        UiNode content = Shown(windows, seenByAlice, thermo.Window);
        ByKey(content, "power").OnClick();
        w.Run(5);
        content = Shown(windows, seenByAlice, thermo.Window);
        ByKey(content, "min").OnClick();
        w.Run(5);
        Assert.Equal(1f, In(w, () => freezer.on));
        Assert.Equal(73.15f, In(w, () => Dm.num(freezer.target_temperature)), 2);
        alice.Session.UiClose(thermo.Window);
        w.Run(5);

        // 3. a cryoxadone beaker from the table at 79,111 into the cell by a click
        Do(w, () => Waiting.NoSleep(aliceMob.forceMove(locate(80, 110, 1))));
        w.Run(Second / 2);
        Assert.True(alice.Session.Click(On(beaker), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(aliceMob, In(w, () => beaker.loc));
        Assert.True(alice.Session.Click(On(cell), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(beaker, In(w, () => cell.beaker));

        // 4. alt-click opens the cell (tg's click_alt): pod-open, its opening flick seen
        Assert.True(alice.Session.Click(On(cell), ClickButton.Left, ClickModifiers.Alt));
        w.Run(5);
        Assert.True(In(w, () => Dm.truthy(cell.state_open)));
        Assert.Equal("pod-open", StateOf(seenByAlice, cell.dm_id));
        Assert.True(seenByAlice.TryGet(cell.dm_id, out ReplicaEntity cellSeen));
        Assert.Contains("pod-open-anim", States(seenByAlice, cellSeen.State.FlickAppearance));

        // 5. Bob, hurt and stunned, beside the doctor and the cell; she drags him onto it (tg's mouse_drop_receive takes an incapacitated carbon)
        Do(w, () => Waiting.NoSleep(bobMob.forceMove(locate(81, 110, 1))));
        alice.Session.DevCommand("damage Bob brute 12 chest");
        alice.Session.DevCommand("damage Bob burn 8 l_arm");
        alice.Session.DevCommand("stun Bob 20");
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.truthy(bobMob.incapacitated)));
        Assert.True(alice.Session.Drop(new DragRelease(On(bobMob), On(cell), ClickButton.Left, ClickModifiers.None, 4)));
        w.Run(5);
        Assert.Same(cell, In(w, () => bobMob.loc));
        Assert.Same(bobMob, In(w, () => cell.occupant));
        Assert.True(In(w, () => Dm.truthy(cell.on)), "an occupant turns the cell on (close_machine)");
        w.Run(Second);

        // both clients: pod-on with its closing flick and the cover over the patient, the occupant visual holding Bob's look and bobbing
        foreach (ReplicaWorld replica in new[] { seenByAlice, seenByBob })
        {
            Assert.Equal("pod-on", StateOf(replica, cell.dm_id));
            Assert.True(replica.TryGet(cell.dm_id, out ReplicaEntity seen));
            Assert.Contains("pod-close-anim", States(replica, seen.State.FlickAppearance));
            Assert.Contains("cover-on", States(replica, seen.State.Appearance));
            ReplicaEntity visual = OccupantVisual(replica, cell.dm_id);
            Assert.NotNull(visual);
            List<string> inside = States(replica, visual.State.Appearance);
            output.WriteLine("the occupant visual: " + string.Join(", ", inside));
            Assert.Contains(inside, s => s != null && s.Contains("chest", StringComparison.Ordinal)); // his body parts, standing
            Assert.NotNull(visual.Animation); // tg's bob: pixel_y 22 <-> 24, 2 s each way, looping
            Assert.Equal(22, Look(replica, visual.Id).PixelY);
            Assert.False(replica.Entities.Any(e => e.Id == bobMob.dm_id), "the patient himself is no entity inside the cell");
        }

        // the cell as Alice's client draws it: the patient behind the glass, under the cover, 2 pixels higher at the top of the bob
        string iconRoot = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (iconRoot != null)
        {
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(iconRoot)));
            Raster empty = CellPicture(compositor, seenByAlice, cell.dm_id, null, "cell-without-occupant");
            Raster low = CellPicture(compositor, seenByAlice, cell.dm_id, 22, "cell-occupant-22");
            Raster high = CellPicture(compositor, seenByAlice, cell.dm_id, 24, "cell-occupant-24");
            output.WriteLine($"the occupant changes {Differing(empty, low)} pixels of the cell's picture; the bob {Differing(low, high)}");
            Assert.True(Differing(empty, low) > 100, "the patient is seen in the cell");
            Assert.True(Differing(low, high) > 20, "the bob moves him");
        }

        // the loop's hum (tg's looping_sound/cryo_cell)
        Assert.Contains(heard, h => (h.File ?? "").StartsWith("sound/machines/cryo/cryo_", StringComparison.Ordinal));

        // Bob's own screen: tg's impaired fullscreen (get_remote_view_fullscreens, through reset_perspective)
        atom_movable_screen_fullscreen impaired = In(w, () => bobMob.screens?["remote_view"] as atom_movable_screen_fullscreen);
        Assert.NotNull(impaired);
        Assert.Equal("impairedoverlay1", In(w, () => impaired.icon_state)); // severity 1
        Assert.Contains(seenByBob.Screen.Values, e => e.Id == impaired.dm_id);

        // 6. the Cryo window: the occupant, the cell's temperature, the beaker
        Assert.True(alice.Session.Click(On(cell), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        UiWindowState cryo = Assert.Single(seenByAlice.UiWindows.Values, v => v.Interface == "Cryo");
        content = Shown(windows, seenByAlice, cryo.Window);
        output.WriteLine(content.AllText());
        Assert.Equal(In(w, () => bobMob.name), ByKey(content, "name").Text);
        Assert.Equal("On", ByKey(content, "power").Text);
        Assert.Contains("Cryoxadone", content.AllText());
        Assert.EndsWith(" K", ByKey(content, "cell-temperature").Text);

        // 7. healed, the cell sounds the warning, turns off and ejects him (autoeject)
        int waited = 0;
        while (In(w, () => bobMob.loc == cell) && waited < 240 * Second)
        {
            w.Run(5 * Second);
            waited += 5 * Second;
            output.WriteLine(In(w, () => $"{waited / Second} s: body {bobMob.bodytemperature:0.0} K, cell gas {Dm.num(((datum_gas_mixture)cell.internal_connector.gas_connector.airs[1]).temperature):0.0} K, health {bobMob.health:0.0}, stat {bobMob.stat}"));
        }
        Assert.True(In(w, () => bobMob.loc is turf), "the healed patient is ejected");
        Assert.Contains(heard, h => h.File == "sound/machines/cryo_warning.ogg");
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(cell.on)));
        Assert.Equal("pod-open", StateOf(seenByAlice, cell.dm_id));
        Assert.DoesNotContain(States(seenByAlice, OccupantVisual(seenByAlice, cell.dm_id).State.Appearance), s => s != null && s.Contains("chest", StringComparison.Ordinal));
        Assert.True(seenByAlice.Entities.Any(e => e.Id == bobMob.dm_id), "out of the cell he is an entity again");
        Assert.Null(In(w, () => bobMob.screens?["remote_view"]));

        // 8. the empty cell: alt-click shuts it (pod-off), ctrl-click turns it on and off (click_ctrl)
        Do(w, () => Waiting.NoSleep(bobMob.forceMove(locate(81, 109, 1))));
        Assert.True(alice.Session.Click(On(cell), ClickButton.Left, ClickModifiers.Alt));
        w.Run(5);
        Assert.False(In(w, () => Dm.truthy(cell.state_open)));
        Assert.Equal("pod-off", StateOf(seenByAlice, cell.dm_id));
        Do(w, () => aliceMob.next_click = -1);
        Assert.True(alice.Session.Click(On(cell), ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(5);
        Assert.True(In(w, () => Dm.truthy(cell.on)));
        Assert.Equal("pod-on", StateOf(seenByAlice, cell.dm_id));
        Do(w, () => aliceMob.next_click = -1);
        Assert.True(alice.Session.Click(On(cell), ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(5);
        Assert.False(In(w, () => Dm.truthy(cell.on)));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
