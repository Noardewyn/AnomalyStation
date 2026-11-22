using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M5 slice 10 through the real server on MetaStation, with a headless client (<see cref="PlaySession"/>): the firelock beside
/// Fitness (138,182), under the Fitness glass airlock, closed (set-up, as a fire alarm closes it), welded with the welder's right click, its bolt lock opened with a screwdriver and its
/// floor bolts undone with a wrench (firedoor.dm:502-527): the firelock becomes tg's firelock frame with its panel open
/// ("frame2", firedoor.dm:701); the client sees the frame entity with that picture and hears deconstruct.ogg; the wrench on the
/// frame (the airlock opened first: a closed airlock takes the clicks on its tile) finishes a firelock again (firedoor.dm:910).
/// </summary>
public class FirelockGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int FirelockX = 138, FirelockY = 182;

    private readonly ITestOutputHelper _output;

    public FirelockGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static T Give<T>(RealHeadlessWorld w, mob_living_carbon_human me, string path) where T : obj_item
    {
        using (w.World.Enter())
        {
            foreach (object held in Dm.each(me.held_items.Copy()))
            {
                if (held is obj_item old)
                    GlobalProcs.qdel(old);
            }
            DevCommands.Give(me, path);
            return me.get_active_held_item() as T;
        }
    }

    private static List<T> On<T>(RealHeadlessWorld w, int x, int y)
        => In(w, () => Dm.each_cast<atom>(Builtins.locate(x, y, 1).contents).OfType<T>().ToList());

    private static bool Click(RealHeadlessWorld w, RealHeadlessWorld.Player p, datum target, float seconds, ClickButton button = ClickButton.Left)
    {
        bool sent = p.Session.Click(target.dm_id, button, ClickModifiers.None);
        w.Run((int)(seconds * Second));
        return sent;
    }

    [MetaServerFact]
    public void A_welded_firelock_is_unbolted_into_its_frame_and_the_frame_is_bolted_back_into_a_firelock()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 60, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(Builtins.locate(FirelockX, FirelockY + 1, 1)));
        w.Run(Second);
        var door = Assert.Single(On<obj_machinery_door_firedoor>(w, FirelockX, FirelockY));
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // set-up: the firelock closed as a fire alarm closes it (it stands under the Fitness glass airlock)
        if (!In(w, () => Dm.truthy(door.density)))
        {
            using (w.World.Enter())
                Waiting.Detach(door.close());
            w.Run(3 * Second);
        }
        Assert.True(In(w, () => Dm.truthy(door.density)), "the firelock is closed");
        var welder = Give<obj_item_weldingtool>(w, me, "/obj/item/weldingtool");
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(Click(w, p, door, 3, ClickButton.Right));
        Assert.True(In(w, () => Dm.truthy(door.welded)), "the firelock is welded");

        // the screwdriver opens the bolt lock; the wrench undoes the bolts (2 s): the frame with its panel open
        Give<obj_item_screwdriver>(w, me, "/obj/item/screwdriver");
        Assert.True(Click(w, p, door, 1));
        Assert.False(In(w, () => Dm.truthy(door.boltslocked)));
        Give<obj_item_wrench>(w, me, "/obj/item/wrench");
        int before = heard.Count(h => h.File == "sound/items/deconstruct.ogg");
        Assert.True(Click(w, p, door, 3));
        Assert.True(In(w, () => GlobalProcs.QDELETED(door)));
        var frame = Assert.Single(On<obj_structure_firelock_frame>(w, FirelockX, FirelockY));
        Assert.Equal(2f, In(w, () => frame.constructionStep));
        Assert.True(heard.Count(h => h.File == "sound/items/deconstruct.ogg") > before, "deconstruct.ogg heard");
        w.Run(Second);
        Assert.DoesNotContain(replica.Entities, e => e.Id == door.dm_id);
        ReplicaEntity seenFrame = Assert.Single(replica.Entities, e => e.Id == frame.dm_id);
        Assert.True(replica.TryGetAppearance(seenFrame.State.Appearance, out AppearanceEntry look));
        _output.WriteLine($"the frame's picture: {replica.ResolveResource(look.Icon)} {replica.ResolveString(look.IconState)}");
        Assert.Equal("frame2", replica.ResolveString(look.IconState));

        // the wrench on the frame (2 s) finishes a firelock (firedoor.dm:910)
        // the closed glass airlock on the same tile (PREVENT_CLICK_UNDER_1) takes a click on the frame below it, as in tg: it is
        // opened first (set-up; its own access and power are the M4a/M7 checks')
        var airlock = Assert.Single(On<obj_machinery_door_airlock>(w, FirelockX, FirelockY));
        using (w.World.Enter())
            Waiting.Detach(airlock.open());
        w.Run(2 * Second);
        Assert.True(Click(w, p, frame, 3));
        Assert.True(In(w, () => GlobalProcs.QDELETED(frame)));
        var rebuilt = Assert.Single(On<obj_machinery_door_firedoor>(w, FirelockX, FirelockY));
        w.Run(Second);
        Assert.Single(replica.Entities, e => e.Id == rebuilt.dm_id);
        Assert.DoesNotContain(replica.Entities, e => e.Id == frame.dm_id);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }
}
