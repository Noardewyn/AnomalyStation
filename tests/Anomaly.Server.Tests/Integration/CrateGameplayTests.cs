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
/// The 3D crate (docs/ai/specs/models.md) through the real server on MetaStation: a headless client stands south of the freezer crate in medbay's cold room at
/// (55,89) and uses it by hand as tg's crate does (crates.dm): a click opens it (its icon state becomes <c>freezeropen</c>), an item clicked on the open crate goes
/// onto its turf, a click shuts it with the item inside. Each time the client reads the crate as the renderer does (<see cref="CrateLook"/>) and the catalog's crate
/// model draws both states. The catalog's "dir" rule made it face away from the wall north of it.
/// </summary>
public class CrateGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int X = 55, Y = 89;

    private readonly ITestOutputHelper _output;

    public CrateGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static List<string> States(ReplicaWorld replica, long id)
        => CrewOutfitTests.Icons(replica, replica.Entities.Single(e => e.Id == id).State.Appearance).Select(i => i.State).ToList();

    private static bool SeenOnTile(ReplicaWorld replica, long id, int z)
        => replica.Entities.Any(e => e.Id == id && e.State.X == X && e.State.Y == Y && e.State.Z == z);

    [MetaServerFact]
    public void A_player_opens_the_freezer_crate_puts_an_item_in_and_shuts_it()
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "metastation", MapsFolder = MetaServer.Folder(), DevCommands = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 4000 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;
        ModelCatalog catalog = ModelCatalog.Parse(File.ReadAllText(Path.Combine(ModelsRoot(), ModelCatalog.FileName)));

        var crate = In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(X, Y, z)).contents).OfType<obj_structure_closet_crate_freezer>().FirstOrDefault());
        Assert.NotNull(crate);
        Assert.Equal(2, In(w, () => (int)Dm.num(crate.dir))); // the wall is north of it: it faces south
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(Builtins.locate(X, Y - 1, z)));
        w.Run(Second);

        ModelEntry Model() => catalog.Find("icons/obj/storage/crates.dmi", States(replica, crate.dm_id)[0]);
        bool Open() => CrateLook.Open(Model(), States(replica, crate.dm_id)[0], States(replica, crate.dm_id).Skip(1));

        // Shut: the catalog's freezer crate draws it.
        _output.WriteLine($"the crate as the client sees it: {string.Join(", ", States(replica, crate.dm_id))}");
        Assert.Equal("crate_freezer_crate", Model()?.Id);
        Assert.False(Open());

        // A click with an empty hand opens it: tg's state [base]open, the same model, read as open.
        Assert.True(p.Session.Click(crate.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(crate.opened)), "the crate opened");
        Assert.Equal("freezeropen", States(replica, crate.dm_id)[0]);
        Assert.Equal("crate_freezer_crate", Model()?.Id);
        Assert.True(Open());

        // An item clicked on the open crate goes onto its turf, where the client sees it (drawn on the crate's floor).
        var wrench = In(w, () =>
        {
            var item = Dm.@new<obj_item_wrench>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(item))), "the wrench is in hand");
            return item;
        });
        w.Run(Second / 2);
        Assert.True(p.Session.Click(crate.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(In(w, () => crate.loc), In(w, () => wrench.loc));
        Assert.True(SeenOnTile(replica, wrench.dm_id, z), "the client sees the wrench on the crate's tile");

        // A click with the empty hand shuts it with the wrench inside; the client reads it shut again.
        Assert.True(p.Session.Click(crate.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(crate.opened)), "the crate shut");
        Assert.Same(crate, In(w, () => wrench.loc));
        Assert.False(SeenOnTile(replica, wrench.dm_id, z), "the wrench is not on the tile while the crate is shut");
        Assert.Equal("freezer", States(replica, crate.dm_id)[0]);
        Assert.False(Open());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
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
