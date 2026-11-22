using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The 3D closet (docs/ai/specs/models.md) through the real server on MetaStation: a headless client stands south of the emergency closet at (65,94) and uses it
/// by hand as tg's closet does (closets.dm): a click opens it, an item clicked on the open closet is put on its turf (where the client draws it on a shelf), a
/// click closes it with the item inside, a click opens it again and the item lies on the turf again. Each time the client reads the door from the closet's
/// overlays as the renderer does (<see cref="ClosetLook"/>): shut with its door state, open, and the model in the catalog draws it.
/// </summary>
public class ClosetGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int X = 65, Y = 94;

    private readonly ITestOutputHelper _output;

    public ClosetGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static List<string> States(ReplicaWorld replica, long id)
        => CrewOutfitTests.Icons(replica, replica.Entities.Single(e => e.Id == id).State.Appearance).Select(i => i.State).ToList();

    // The door as the client reads it from the closet's overlays (the root's own state is no door overlay).
    private static bool? Door(ReplicaWorld replica, long id, out string door) => ClosetLook.Open(States(replica, id).Skip(1), out door);

    private static bool SeenOnTile(ReplicaWorld replica, long id, int z)
        => replica.Entities.Any(e => e.Id == id && e.State.X == X && e.State.Y == Y && e.State.Z == z);

    [MetaServerFact]
    public void A_player_opens_the_emergency_closet_puts_an_item_in_and_shuts_it()
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "metastation", MapsFolder = MetaServer.Folder(), DevCommands = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 4000 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;

        var closet = In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(X, Y, z)).contents).OfType<obj_structure_closet_emcloset>().FirstOrDefault());
        Assert.NotNull(closet);
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(Builtins.locate(X, Y - 1, z)));
        w.Run(Second);

        // Shut, with tg's door overlay from the map load on (the emergency closet runs its own Initialize, D112), so the renderer draws that door from the
        // first frame. The catalog's wardrobe locker draws its state.
        Assert.Equal(false, Door(replica, closet.dm_id, out string shutDoor));
        Assert.Equal("emergency", shutDoor);
        string icon = replica.ResolveResource(replica.TryGetAppearance(replica.Entities.Single(e => e.Id == closet.dm_id).State.Appearance, out AppearanceEntry root) ? root.Icon : default);
        _output.WriteLine($"the closet as the client sees it: {icon} {string.Join(", ", States(replica, closet.dm_id))}");
        ModelCatalog catalog = ModelCatalog.Parse(File.ReadAllText(Path.Combine(ModelsRoot(), ModelCatalog.FileName)));
        Assert.Equal("locker_wardrobe", catalog.Find(icon, States(replica, closet.dm_id)[0])?.Id);

        // A click with an empty hand opens it: the open overlay once the door's animation (door_anim_time, 0.2 s) is over.
        Assert.True(p.Session.Click(closet.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(closet.opened)), "the closet opened");
        Assert.Equal(true, Door(replica, closet.dm_id, out _));

        // An item clicked on the open closet goes onto its turf: the client sees it on the closet's tile (where it is drawn on a shelf).
        var wrench = In(w, () =>
        {
            var item = Dm.@new<obj_item_wrench>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(item))), "the wrench is in hand");
            return item;
        });
        w.Run(Second / 2);
        Assert.True(p.Session.Click(closet.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(In(w, () => closet.loc), In(w, () => wrench.loc));
        Assert.True(SeenOnTile(replica, wrench.dm_id, z), "the client sees the wrench on the closet's tile");

        // A click with the empty hand shuts it: the wrench is inside (tg's take_contents), off the tile; the shut door overlay names its state.
        Assert.True(p.Session.Click(closet.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(closet.opened)), "the closet shut");
        Assert.Same(closet, In(w, () => wrench.loc));
        Assert.False(SeenOnTile(replica, wrench.dm_id, z), "the wrench is not on the tile while the closet is shut");
        Assert.Equal(false, Door(replica, closet.dm_id, out string door));
        Assert.Equal("emergency", door);

        // Open again: the wrench lies on the turf again (dump_contents).
        Assert.True(p.Session.Click(closet.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(SeenOnTile(replica, wrench.dm_id, z), "the wrench is back on the closet's tile");
        Assert.Equal(true, Door(replica, closet.dm_id, out _));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // The owner's playtest, 2026-10-24: the electrical supplies locker at (161,147) drew a wrong door until clicked. Its type was inert (D112: tg procs
    // of its own untranslated), so it skipped the closet's Initialize and its update_appearance; every shut closet with tg's door overlay shows it now.
    [MetaServerFact]
    public void Every_map_closet_with_a_door_shows_it_from_the_map_load()
    {
        // every closet of the map, so the whole map is sent (--no-interest; M10 slice C sends only what is near the player)
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "metastation", MapsFolder = MetaServer.Folder(), NoInterest = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 4000 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        ReplicaWorld replica = p.Session.Replica;
        w.Run(Second);

        var missing = new List<string>();
        int checkedClosets = 0;
        foreach (ReplicaEntity e in replica.Entities.Where(e => e.State.Z == 1 && e.TypePath.StartsWith("/obj/structure/closet", StringComparison.Ordinal)).ToList()) // the station's: CentCom's ERT closets are still inert types
        {
            var closet = In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(e.State.X, e.State.Y, e.State.Z)).contents).OfType<obj_structure_closet>()
                .FirstOrDefault(c => c.dm_id == e.Id));
            // a crate draws its lid by its own closet_update_overlays (crates.dm:87), no door overlay
            if (closet == null || closet is obj_structure_closet_crate || !In(w, () => Dm.truthy(closet.enable_door_overlay) && Dm.truthy(closet.has_closed_overlay) && !Dm.truthy(closet.opened)))
                continue;
            checkedClosets++;
            if (Door(replica, e.Id, out _) != false)
                missing.Add($"{e.TypePath} at ({e.State.X},{e.State.Y})");
        }
        _output.WriteLine($"{checkedClosets} shut closets with a door; without one: {string.Join("; ", missing)}");
        Assert.True(checkedClosets > 50, "MetaStation has many closets");
        Assert.Empty(missing);
        long electrical = In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(161, 147, (int)w.MobOf(p).z)).contents)
            .OfType<obj_structure_closet_secure_closet_engineering_electrical>().Single().dm_id);
        Assert.Equal(false, Door(replica, electrical, out string door));
        Assert.Equal("eng_elec", door); // its icon_door, not its icon_state's eng_door
    }

    private static string ModelsRoot()
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "godot", "assets", "models");
            if (File.Exists(Path.Combine(candidate, ModelCatalog.FileName)))
                return candidate;
        }
        throw new InvalidOperationException("godot/assets/models/models.json not found above the test's folder");
    }
}
