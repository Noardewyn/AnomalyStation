using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The aquariums through the real server: a joined player puts a fish into an aquarium by a click; the client sees the fish's visual
/// as a visual of the aquarium with its swimming chain (pixel_w and pixel_z) and draws it into the aquarium's picture between the water
/// and the borders; the player feeds the fish with a can of fish feed, opens the control panel with an alt-click and reads the Aquarium
/// window, switches it to manual and sets the fluid in it.
/// </summary>
public class AquariumGameplayTests(ITestOutputHelper output)
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

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static List<string> States(ReplicaWorld replica, TableRef reference, int depth = 0)
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

    /// <summary>An open tile next to the mob with nothing dense on it.</summary>
    private static turf Beside(mob me) => Dm.each(range(1, me)).OfType<turf>()
        .Where(t => t is turf_open && !Dm.truthy(t.density) && t != me.loc && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)))
        .First();

    private static void HoldNew<T>(RealHeadlessWorld w, mob_living_carbon_human me) where T : obj_item
    {
        Do(w, () =>
        {
            var thing = Dm.@new<T>(get_turf(me));
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(thing))));
        });
        w.Run(5);
    }

    [Fact]
    public void A_player_puts_a_fish_in_an_aquarium_feeds_it_and_sets_its_fluid()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14851, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player p = w.Join("Aquarist");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(10);
        var aquarium = In(w, () => Dm.@new<obj_structure_aquarium>(Beside(me)));
        w.Run(Second);

        // a caught fish into the aquarium
        HoldNew<obj_item_fish_goldfish>(w, me);
        var goldfish = In(w, () => (obj_item_fish_goldfish)me.get_active_held_item());
        Assert.True(p.Session.Click(On(aquarium), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(aquarium, In(w, () => goldfish.loc));

        // the client: the aquarium's glass and borders, and the fish's visual on it with its swimming chain
        Assert.True(replica.TryGet(aquarium.dm_id, out ReplicaEntity seenAquarium));
        List<string> aquariumStates = States(replica, seenAquarium.State.Appearance);
        output.WriteLine("the aquarium: " + string.Join(", ", aquariumStates));
        Assert.Contains("aquarium_base", aquariumStates);
        Assert.Contains("aquarium_glass_water", aquariumStates);
        Assert.Contains("aquarium_borders", aquariumStates);
        ReplicaEntity fishVisual = replica.Entities.FirstOrDefault(e => e.IsVisual && e.VisHost == aquarium.dm_id && e.TypePath == "/obj/effect/aquarium");
        Assert.True(fishVisual != null, "the fish's visual: " + string.Join(", ", replica.Entities.Where(e => e.IsVisual).Select(e => e.TypePath)));
        Assert.Contains("fish_greyscale", States(replica, fishVisual.State.Appearance));
        Assert.NotNull(fishVisual.Animation);
        Assert.Contains(fishVisual.Animation.Tracks, t => t.Steps.Any(s => (s.Vars.Mask & (AnimatedVar.PixelW | AnimatedVar.PixelZ)) != 0));
        Assert.Equal(0, fishVisual.Animation.Tracks[0].Loop); // swims for ever

        // the client draws the fish into the aquarium's picture (EntityLayer: VisInsets)
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root != null)
        {
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
            var insets = new VisInsets(new Tables(replica));
            Assert.True(compositor.TryCompose(new Tables(replica), seenAquarium.State.Appearance, ByondDir.South, 0, out ComposedRaster bare), string.Join("; ", compositor.Library.Problems));
            Assert.True(replica.TryGetAppearance(fishVisual.State.Appearance, out AppearanceEntry fishLook));
            TableRef withFish = insets.With(seenAquarium.State.Appearance, fishVisual.State.Appearance, fishLook.PixelX, fishLook.PixelY, 255, null, fishLook.PixelW, fishLook.PixelZ);
            Assert.True(compositor.TryCompose(insets, withFish, ByondDir.South, 0, out ComposedRaster drawn), string.Join("; ", compositor.Library.Problems));
            int differing = 0;
            for (int y = 0; y < Math.Min(bare.Image.Height, drawn.Image.Height); y++)
                for (int x = 0; x < Math.Min(bare.Image.Width, drawn.Image.Width); x++)
                    if (bare.Image.Get(x, y) != drawn.Image.Get(x, y))
                        differing++;
            output.WriteLine($"the goldfish changes {differing} pixels of the aquarium's picture");
            Assert.True(differing > 0, "the fish shows in the aquarium");
        }

        // feeding it with a can of fish feed
        Do(w, () => goldfish.last_feeding = 0f);
        HoldNew<obj_item_reagent_containers_cup_fish_feed>(w, me);
        Assert.True(p.Session.Click(On(aquarium), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.num(goldfish.last_feeding)) > 0, "the goldfish was fed");

        // the control panel: alt-click, then the window with an empty hand
        Do(w, () => Waiting.NoSleep(me.dropItemToGround(me.get_active_held_item() as obj_item)));
        w.Run(5);
        Assert.True(p.Session.Click(On(aquarium), ClickButton.Left, ClickModifiers.Alt));
        w.Run(Second);
        Assert.True(In(w, () => HAS_TRAIT(aquarium, TRAIT_AQUARIUM_PANEL_OPEN)), "the panel opened");
        Assert.True(p.Session.Click(On(aquarium), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = replica.UiWindows.Tgui().SingleOrDefault(win => win.Interface == "Aquarium");
        Assert.True(window != null, "the Aquarium window: " + string.Join(", ", replica.UiWindows.Tgui().Select(win => win.Interface)));
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        UiNode shown = windows.Get(window.Window)?.Content;
        Assert.NotNull(shown);
        Assert.Contains(shown.Walk(), n => n.Key == "name" && n.Text == "GOLDFISH");
        shown.Walk().First(n => n.Key == "modes").OnSelected("Manual");
        w.Run(Second);
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        shown = windows.Get(window.Window)?.Content;
        shown.Walk().First(n => n.Key == "fluid-Saltwater").Walk().First(n => n.Key == "pick").OnClick();
        w.Run(Second);
        Assert.Equal(AQUARIUM_FLUID_SALTWATER, In(w, () => ((datum_component_aquarium)aquarium.GetComponent(typeof(datum_component_aquarium))).fluid_type));
        Assert.False(In(w, () => Dm.truthy(Dm.bitand(goldfish.fish_flags, FISH_FLAG_SAFE_FLUID))), "a goldfish does not live in salt water");
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
